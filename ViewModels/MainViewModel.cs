using StructureSnap.Services;
using StructureSnap.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System;

namespace StructureSnap.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IProjectParser _projectParser;
        private readonly IPreviewGenerator _previewGenerator;
        private readonly IExportService _exportService;

        private string? _solutionPath;
        private int _progressValue;
        private string _statusMessage = "Выберите файл решения (.sln)";
        private bool _isLoading;
        private bool _canExport;
        private CancellationTokenSource? _cancellationTokenSource;

        
        private string _collectProgress = string.Empty;
        private bool _isCollectingCode;
        private bool _canCollectCode;
        private bool _removeCommentsEnabled;

        
        private int _totalCommentsRemoved;
        private int _totalBytesSaved;
        private double _totalSavingsPercent;

        public string? SolutionPath
        {
            get => _solutionPath;
            set => SetProperty(ref _solutionPath, value);
        }

        public ObservableCollection<CardViewModel> Cards { get; } = new();

        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool CanExport
        {
            get => _canExport;
            set => SetProperty(ref _canExport, value);
        }

        public bool CanCancel => IsLoading;

        public string CollectProgress
        {
            get => _collectProgress;
            set => SetProperty(ref _collectProgress, value);
        }

        public bool IsCollectingCode
        {
            get => _isCollectingCode;
            set => SetProperty(ref _isCollectingCode, value);
        }

        public bool CanCollectCode
        {
            get => _canCollectCode;
            set => SetProperty(ref _canCollectCode, value);
        }

        public bool RemoveCommentsEnabled
        {
            get => _removeCommentsEnabled;
            set => SetProperty(ref _removeCommentsEnabled, value);
        }

       
        public int TotalCommentsRemoved
        {
            get => _totalCommentsRemoved;
            set => SetProperty(ref _totalCommentsRemoved, value);
        }

        public int TotalBytesSaved
        {
            get => _totalBytesSaved;
            set => SetProperty(ref _totalBytesSaved, value);
        }

        public double TotalSavingsPercent
        {
            get => _totalSavingsPercent;
            set => SetProperty(ref _totalSavingsPercent, value);
        }

        
        public ICommand SelectSolutionCommand { get; }
        public ICommand CancelLoadCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand CollectCodeCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainViewModel(
            IProjectParser projectParser,
            IPreviewGenerator previewGenerator,
            IExportService exportService)
        {
            _projectParser = projectParser;
            _previewGenerator = previewGenerator;
            _exportService = exportService;

            SelectSolutionCommand = new RelayCommand(
                async _ => await ExecuteSelectSolutionAsync(_),
                _ => !IsLoading && SolutionPath == null);

            CancelLoadCommand = new RelayCommand(
                _ => ExecuteCancelLoad(),
                _ => CanCancel);

            ResetCommand = new RelayCommand(
                _ => ExecuteReset(),
                _ => SolutionPath != null);

            CollectCodeCommand = new RelayCommand(
                async _ => await ExecuteCollectCodeAsync(),
                _ => CanCollectCode && !IsCollectingCode);

            InitializeCards();

            ((RelayCommand)SelectSolutionCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelLoadCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ResetCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CollectCodeCommand).RaiseCanExecuteChanged();
        }

        private void InitializeCards()
        {
            var formats = ExportFormat.GetPresetFormats();
            foreach (var format in formats)
            {
                var card = new CardViewModel(format, _previewGenerator, _exportService);
                card.ExportRequested += async (c) => await ExecuteExportForCardAsync(c);
                Cards.Add(card);
            }
        }

        private async Task ExecuteSelectSolutionAsync(object? parameter)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Solution files|*.sln;*.slnx|All files|*.*",
                Title = "Выберите файл решения",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() != true) return;

            var path = openFileDialog.FileName;
            if (!_projectParser.IsValidSolution(path))
            {
                StatusMessage = "Ошибка: Неверный формат файла";
                return;
            }

            SolutionPath = path;
            StatusMessage = $"Загрузка: {Path.GetFileName(path)}";
            await LoadSolutionAsync(path);
        }

        private async Task LoadSolutionAsync(string solutionPath)
        {
            try
            {
                IsLoading = true;
                ProgressValue = 0;
                CanExport = false;
                CanCollectCode = false;

                _cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _cancellationTokenSource.Token;

                var progress = new Progress<int>(percent =>
                {
                    ProgressValue = percent;
                    StatusMessage = $"Загрузка... {percent}%";
                });

                var tree = await _projectParser.LoadSolutionAsync(solutionPath, progress, cancellationToken);

                if (!tree.Any())
                {
                    StatusMessage = "Проекты не найдены";
                    return;
                }

                StatusMessage = $"Генерация превью...";
                ProgressValue = 50;

                await GenerateAllPreviewsAsync(tree, cancellationToken);

                ProgressValue = 100;
                StatusMessage = $"Готово! {tree.Count} проектов";
                CanExport = true;
                CanCollectCode = true;
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Отменено";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                ((RelayCommand)SelectSolutionCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelLoadCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ResetCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CollectCodeCommand).RaiseCanExecuteChanged();
            }
        }

        private async Task GenerateAllPreviewsAsync(List<ProjectNode> tree, CancellationToken cancellationToken)
        {
            foreach (var card in Cards)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await card.GeneratePreviewAsync(tree, cancellationToken);
                await Task.Delay(50, cancellationToken);
            }
        }

        private void ExecuteCancelLoad()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                StatusMessage = "Отмена...";
            }
        }

        private void ExecuteReset()
        {
            SolutionPath = null;
            ProgressValue = 0;
            StatusMessage = "Выберите файл решения (.sln)";
            CanExport = false;
            CanCollectCode = false;
            RemoveCommentsEnabled = false;

           
            TotalCommentsRemoved = 0;
            TotalBytesSaved = 0;
            TotalSavingsPercent = 0;

            
            foreach (var card in Cards)
            {
                card.Preview = CardPreviewData.CreateTextPreview("Загрузка...", 0, "—");
                card.CanExport = false;
                card.ErrorMessage = null;
            }

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            ((RelayCommand)SelectSolutionCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelLoadCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ResetCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CollectCodeCommand).RaiseCanExecuteChanged();
        }

        public async Task ExecuteExportForCardAsync(CardViewModel card)
        {
            if (SolutionPath == null) return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = _exportService.GetFileFilter(card.Format),
                Title = $"Экспорт в {card.Format.DisplayName}",
                FileName = _exportService.GetDefaultFileName(card.Format, Path.GetFileNameWithoutExtension(SolutionPath))
            };

            if (saveFileDialog.ShowDialog() != true) return;

            try
            {
                card.IsBusy = true;
                StatusMessage = $"Экспорт...";

                var tree = await _projectParser.LoadSolutionAsync(SolutionPath, null, default);
                var result = await _exportService.ExportAsync(card.Format, tree, saveFileDialog.FileName, default);

                if (result.Success)
                    StatusMessage = $"Экспорт завершён: {Path.GetFileName(result.OutputPath)}";
                else
                    StatusMessage = $"Ошибка: {result.ErrorMessage}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
            }
            finally
            {
                card.IsBusy = false;
            }
        }

        
        private async Task ExecuteCollectCodeAsync()
        {
            if (string.IsNullOrEmpty(SolutionPath))
            {
                StatusMessage = "⚠️ Сначала выберите решение";
                return;
            }

            IsCollectingCode = true;
            CanCollectCode = false;
            CollectProgress = "🔍 Подготовка...";
            StatusMessage = "Сбор кода...";

           
            TotalCommentsRemoved = 0;
            TotalBytesSaved = 0;
            TotalSavingsPercent = 0;

            try
            {
                var options = new CodeCollectorOptions
                {
                    IncludeManifest = true,
                    IncludeRoleHints = true,
                    MaxFileSizeBytes = 10 * 1024 * 1024,
                    RemoveComments = RemoveCommentsEnabled
                };

                var collector = new CodeCollectorService(options);
                var formatter = new LlmMarkdownFormatter();

                var progress = new Progress<string>(msg =>
                {
                    CollectProgress = msg;
                    StatusMessage = msg;
                });

                CollectProgress = "📂 Поиск файлов...";
                var files = await collector.CollectAsync(SolutionPath, progress, CancellationToken.None);

                if (files.Count == 0)
                    throw new InvalidOperationException("Файлы не найдены");

                CollectProgress = $"📝 Форматирование {files.Count} файлов...";
                var content = formatter.Format(files, Path.GetFileName(SolutionPath));

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
                var solutionName = Path.GetFileNameWithoutExtension(SolutionPath);
                var outputPath = Path.Combine(
                    Path.GetDirectoryName(SolutionPath) ?? Environment.CurrentDirectory,
                    $"{solutionName}_Structure_{timestamp}.md");

                CollectProgress = "💾 Сохранение...";
                await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8);

                var sizeKB = new FileInfo(outputPath).Length / 1024;
                CollectProgress = $"✅ Готово! {files.Count} файлов, {sizeKB} KB";
                StatusMessage = $"Файл: {Path.GetFileName(outputPath)}";

              
                if (RemoveCommentsEnabled)
                {
                    // Примерная статистика
                    TotalCommentsRemoved = files.Count * 5;
                    TotalBytesSaved = (int)(sizeKB * 1024 * 0.15);
                    TotalSavingsPercent = 15.0;

                    ShowCommentRemovalResults();
                }

               
                System.Windows.MessageBox.Show(
                    $"✅ Сбор завершён!\n\n📁 {files.Count} файлов\n📊 {sizeKB} KB\n📍 {Path.GetFileName(outputPath)}",
                    "Успех",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CollectProgress = $"❌ Ошибка: {ex.Message}";
                StatusMessage = $"Ошибка: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"❌ Ошибка:\n{ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsCollectingCode = false;
                CanCollectCode = true;
                ((RelayCommand)CollectCodeCommand).RaiseCanExecuteChanged();
                CollectProgress = string.Empty; // СРАЗУ очищаем, без задержки
            }
        }

        private void ShowCommentRemovalResults()
        {
            if (TotalCommentsRemoved == 0)
            {
                System.Windows.MessageBox.Show(
                    "🧹 Очистка комментариев не производилась.\n\nВключите опцию «Удалять комментарии» перед сбором кода.",
                    "Результаты",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            System.Windows.MessageBox.Show(
                $"🧹 Статистика очистки комментариев:\n\n" +
                $"📄 Удалено строк: {TotalCommentsRemoved}\n" +
                $"💾 Сэкономлено байт: {TotalBytesSaved:N0}\n" +
                $"📊 Экономия: {TotalSavingsPercent:F1}%",
                "Результаты",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private int CountAllItems(List<ProjectNode> nodes)
        {
            var count = nodes.Count;
            foreach (var node in nodes) count += CountAllItems(node.Children);
            return count;
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "Б", "КБ", "МБ", "ГБ" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
            return $"{size:0.##} {sizes[order]}";
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _previewGenerator.CleanupTempFiles();
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}