using StructureSnap.Services;
using StructureSnap.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Navigation;

namespace StructureSnap.Views
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        private readonly MainViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            
            _viewModel = InitializeViewModel();

            if (_viewModel != null)
            {
              
                DataContext = _viewModel;
            }

            
            Closing += MainWindow_Closing;
        }

        /// <summary>
        /// Создаёт и настраивает зависимости для ViewModel.
        /// </summary>
        private MainViewModel? InitializeViewModel()
        {
            try
            {
                
                var projectParser = new ProjectParser();
                var previewGenerator = new PreviewGenerator();
                var exportService = new ExportService();

               
                return new MainViewModel(projectParser, previewGenerator, exportService);
            }
            catch (Exception ex)
            {
               
                MessageBox.Show(
                    $"Ошибка инициализации приложения:\n{ex.Message}\n\nПроверьте, что установлена Visual Studio 2022.",
                    "StructureSnap",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return null;
            }
        }

        /// <summary>
        /// Обработчик закрытия окна.
        /// </summary>
        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
           
            Closing -= MainWindow_Closing;

            try
            {
                
                if (_viewModel is IDisposable disposable)
                {
                   
                    await Task.Run(() => disposable.Dispose());
                }
            }
            catch (Exception ex)
            {
                
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Ошибка при очистке: {ex.Message}");
            }
        }

        
        private void Window_DragMove(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement source &&
                (source is Button || source is TextBox || source is ComboBox || source is Thumb))
            {
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

       
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

       
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true // Важно для .NET Core / .NET 8
                });
                e.Handled = true;
            }
            catch (Win32Exception)
            {
                
                MessageBox.Show(
                    "Не удалось открыть ссылку. Попробуйте скопировать её вручную:\n" + e.Uri.AbsoluteUri,
                    "StructureSnap",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}