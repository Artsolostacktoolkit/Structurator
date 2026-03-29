using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StructureSnap.Models;

namespace StructureSnap.Views.Controls
{
    public partial class FormatCard : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty FormatProperty =
            DependencyProperty.Register(nameof(Format), typeof(ExportFormat), typeof(FormatCard), new PropertyMetadata(null));

        public ExportFormat Format
        {
            get => (ExportFormat)GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }

        public static readonly DependencyProperty PreviewProperty =
            DependencyProperty.Register(nameof(Preview), typeof(CardPreviewData), typeof(FormatCard), new PropertyMetadata(null, OnPreviewChanged));

        public CardPreviewData Preview
        {
            get => (CardPreviewData)GetValue(PreviewProperty);
            set => SetValue(PreviewProperty, value);
        }

        public static readonly DependencyProperty ExportCommandProperty =
            DependencyProperty.Register(nameof(ExportCommand), typeof(ICommand), typeof(FormatCard), new PropertyMetadata(null));

        public ICommand ExportCommand
        {
            get => (ICommand)GetValue(ExportCommandProperty);
            set => SetValue(ExportCommandProperty, value);
        }

        public static readonly DependencyProperty ErrorMessageProperty =
            DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(FormatCard), new PropertyMetadata(null));

        public string? ErrorMessage
        {
            get => (string?)GetValue(ErrorMessageProperty);
            set => SetValue(ErrorMessageProperty, value);
        }

        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register(nameof(IsBusy), typeof(bool), typeof(FormatCard), new PropertyMetadata(false, OnIsBusyChanged));

        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);  // ← ИСПРАВЛЕНО
        }

        public static readonly DependencyProperty CanExportProperty =
            DependencyProperty.Register(nameof(CanExport), typeof(bool), typeof(FormatCard), new PropertyMetadata(false));

        public bool CanExport
        {
            get => (bool)GetValue(CanExportProperty);
            set => SetValue(CanExportProperty, value);
        }

        #endregion

        public FormatCard()
        {
            InitializeComponent();
        }

        private static void OnPreviewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FormatCard card && e.NewValue is CardPreviewData preview)
            {
                if (!string.IsNullOrEmpty(preview.ImagePreviewPath))
                {
                    card.ImagePreviewBlock.Visibility = System.Windows.Visibility.Visible;
                    card.TextPreviewBlock.Visibility = System.Windows.Visibility.Collapsed;

                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(preview.ImagePreviewPath, UriKind.Absolute);
                    bitmap.EndInit();
                    card.ImagePreviewBlock.Source = bitmap;
                }
                else
                {
                    card.ImagePreviewBlock.Visibility = System.Windows.Visibility.Collapsed;
                    card.TextPreviewBlock.Visibility = System.Windows.Visibility.Visible;
                }
                if (card.ExportButton != null)
                {
                    card.ExportButton.Visibility = System.Windows.Visibility.Visible;
                }
            }
        }

        private static void OnIsBusyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FormatCard card)
            {
                card.LoadingIndicator.Visibility = card.IsBusy
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }
        }
    }
}