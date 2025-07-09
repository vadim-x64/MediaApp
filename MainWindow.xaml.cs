using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaApp.Models;
using MediaApp.Services;
using MediaApp.ViewModels;
using System.Globalization;
using System.Windows.Data;

namespace MediaApp
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private Border _dragOverlay;

        public MainWindow()
        {
            InitializeComponent();
            IFileService fileService = new FileService();
            IDuplicateDetector duplicateDetector = new DuplicateDetector(fileService);
            _viewModel = new MainWindowViewModel(fileService, duplicateDetector);
            DataContext = _viewModel;
            this.Closing += Window_Closing;
        }

        private async void SelectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Виберіть медіафайли",
                Multiselect = true,
                Filter =
                    "Медіафайли|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.tiff;*.webp;*.mp4;*.avi;*.mov;*.mkv;*.wmv;*.flv;*.webm;*.m4v|" +
                    "Зображення|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.tiff;*.webp|" +
                    "Відео|*.mp4;*.avi;*.mov;*.mkv;*.wmv;*.flv;*.webm;*.m4v|" +
                    "Всі файли|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await _viewModel.LoadMediaFilesAsync(openFileDialog.FileNames);
            }
        }

        private async void CheckDuplicatesButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.CheckDuplicatesAsync();
        }

        private void ClearMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.MediaFiles?.Any() == true)
            {
                var result = MessageBox.Show("Ви впевнені, що хочете очистити список файлів?",
                    "Підтвердження очистки", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _viewModel.ClearFiles();
                }
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        
        private void FileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FileListBox.SelectedItem is MediaFile selectedFile)
            {
                try
                {
                    if (File.Exists(selectedFile.FilePath))
                    {
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = "rundll32.exe",
                            Arguments = $"shell32.dll,OpenAs_RunDLL {selectedFile.FilePath}",
                            UseShellExecute = true
                        };
                        
                        Process.Start(processInfo);
                    }
                    else
                    {
                        MessageBox.Show($"Файл не знайдено: {selectedFile.FilePath}", 
                            "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при відкритті файлу: {ex.Message}", 
                        "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var result = MessageBox.Show("Ви впевнені, що хочете закрити програму?",
                "Підтвердження закриття", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
            
            if (_viewModel.IsProcessing)
            {
                var processingResult = MessageBox.Show(
                    "Зараз виконується обробка файлів. Ви впевнені, що хочете перервати процес і закрити програму?",
                    "Попередження", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (processingResult == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }
        
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == sender || 
                (e.OriginalSource is FrameworkElement fe && fe.DataContext == DataContext))
            {
                FileListBox.SelectedItem = null;
                Keyboard.ClearFocus();
            }
        }

        private async void CopyPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string filePath)
            {
                try
                {
                    Clipboard.SetText(filePath);
            
                    var parentGrid = button.Parent as Grid;
                    if (parentGrid != null)
                    {
                        var indicator = parentGrid.Children.OfType<TextBlock>()
                            .FirstOrDefault(tb => tb.Name == "CopySuccessIndicator");
                        var copyButton = parentGrid.Children.OfType<Button>().FirstOrDefault();
                        
                        if (indicator != null && copyButton != null)
                        {
                            copyButton.Visibility = Visibility.Collapsed;
                            indicator.Visibility = Visibility.Visible;
                            
                            await Task.Delay(2000);
                            
                            indicator.Visibility = Visibility.Collapsed;
                            copyButton.Visibility = Visibility.Visible;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при копіюванні: {ex.Message}", 
                        "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #region Drag and Drop Support

        private void FileListBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                ShowDragOverlay();
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void FileListBox_DragLeave(object sender, DragEventArgs e)
        {
            HideDragOverlay();
        }

        private void FileListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private async void FileListBox_Drop(object sender, DragEventArgs e)
        {
            HideDragOverlay();
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    await _viewModel.LoadMediaFilesAsync(files);
                }
            }
        }

        private void ShowDragOverlay()
        {
            if (_dragOverlay == null)
            {
                _dragOverlay = FileListBox.Template.FindName("DragOverlay", FileListBox) as Border;
            }
            
            if (_dragOverlay != null)
            {
                _dragOverlay.Visibility = Visibility.Visible;
            }
        }

        private void HideDragOverlay()
        {
            if (_dragOverlay != null)
            {
                _dragOverlay.Visibility = Visibility.Collapsed;
            }
        }

        #endregion
    }
    
    public class GreaterThanZeroConverter : IValueConverter
    {
        public static readonly GreaterThanZeroConverter Instance = new GreaterThanZeroConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}