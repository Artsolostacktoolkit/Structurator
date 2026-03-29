using StructureSnap.Models;
using StructureSnap.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace StructureSnap.ViewModels
{
    public class CardViewModel : INotifyPropertyChanged
    {
        private readonly IPreviewGenerator _previewGenerator;
        private readonly IExportService _exportService;

        private ExportFormat _format = null!;
        private CardPreviewData _preview = null!;
        private bool _isBusy;
        private bool _canExport;
        private string? _errorMessage;

        
        public event Action<CardViewModel>? ExportRequested;

        public ExportFormat Format
        {
            get => _format;
            private set => SetProperty(ref _format, value);
        }

        public CardPreviewData Preview
        {
            get => _preview;
            set => SetProperty(ref _preview, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool CanExport
        {
            get => _canExport;
            set => SetProperty(ref _canExport, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand ExportCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public CardViewModel(
            ExportFormat format,
            IPreviewGenerator previewGenerator,
            IExportService exportService)
        {
            Format = format;
            _previewGenerator = previewGenerator;
            _exportService = exportService;

            ExportCommand = new RelayCommand(
                async _ => await ExecuteExportAsync(_),
                _ => CanExecuteExport()
            );

            Preview = CardPreviewData.CreateTextPreview("Загрузка...", 0, "—");
            CanExport = false;
        }

        public async Task GeneratePreviewAsync(List<ProjectNode> tree, CancellationToken cancellationToken = default)
        {
            try
            {
                IsBusy = true;
                ErrorMessage = null;

                Preview = await _previewGenerator.GeneratePreviewAsync(Format, tree, cancellationToken);
                CanExport = true;
            }
            catch (OperationCanceledException)
            {
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка превью: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
            }
        }

        private async Task ExecuteExportAsync(object? parameter)
        {
            
            ExportRequested?.Invoke(this);
        }

        private bool CanExecuteExport()
        {
            return !IsBusy && Preview != null && string.IsNullOrEmpty(ErrorMessage);
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}