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
using System.Collections.Generic;

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
        
        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        private async void SelectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Відкрити",
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
                    "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);

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
                            "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);

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
            if (e.OriginalSource == sender || (e.OriginalSource is FrameworkElement fe && fe.DataContext == DataContext))
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
        
        private void SetDragOverlayVisibility(Visibility visibility)
        {
            if (FileListBox.Template.FindName("DragOverlay", FileListBox) is Border dragOverlay)
            {
                dragOverlay.Visibility = visibility;
            }
        }

        private void FileListBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                SetDragOverlayVisibility(Visibility.Visible);
            }
        }

        private void FileListBox_DragLeave(object sender, DragEventArgs e)
        {
            SetDragOverlayVisibility(Visibility.Collapsed);
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
        
        private string[] GetFilesFromPaths(string[] paths)
        {
            var fileList = new List<string>();
            
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    fileList.Add(path);
                }
                else if (Directory.Exists(path))
                {
                    try
                    {
                        // Рекурсивно дістаємо всі файли з папки та її підпапок
                        fileList.AddRange(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories));
                    }
                    catch (UnauthorizedAccessException) 
                    { 
                        // Ігноруємо системні папки, до яких немає доступу
                    }
                }
            }
            return fileList.ToArray();
        }

        private async void FileListBox_Drop(object sender, DragEventArgs e)
        {
            // ОДРАЗУ ховаємо зелену зону, щоб не залипала
            SetDragOverlayVisibility(Visibility.Collapsed); 

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                var allFiles = GetFilesFromPaths(paths); // Використовуємо метод, що ми додали минулого разу
                
                if (DataContext is ViewModels.MainWindowViewModel viewModel && allFiles.Length > 0)
                {
                    await viewModel.LoadMediaFilesAsync(allFiles);
                }
            }
        }
        
        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Перевіряємо чи натиснуто саме CTRL + V
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var pathsToProcess = new List<string>();

                // Варіант 1: Файли скопійовано класично (через Провідник Windows)
                if (Clipboard.ContainsFileDropList())
                {
                    var fileDropList = Clipboard.GetFileDropList();
                    foreach (string path in fileDropList)
                    {
                        pathsToProcess.Add(path);
                    }
                }
                // Варіант 2: Файли скопійовано як текст (просто шляхи "C:\folder\file.jpg")
                else if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    // Розбиваємо текст на рядки (на випадок, якщо скопійовано декілька шляхів)
                    var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var cleanPath = line.Trim('\"', ' ', '\t'); // Прибираємо зайві лапки та пробіли
                        if (File.Exists(cleanPath) || Directory.Exists(cleanPath))
                        {
                            pathsToProcess.Add(cleanPath);
                        }
                    }
                }

                if (pathsToProcess.Count > 0)
                {
                    // Зупиняємо подальшу передачу події (щоб інші елементи вікна не реагували на це натискання)
                    e.Handled = true; 

                    var allFiles = GetFilesFromPaths(pathsToProcess.ToArray());
                    
                    if (DataContext is ViewModels.MainWindowViewModel viewModel && allFiles.Length > 0)
                    {
                        await viewModel.LoadMediaFilesAsync(allFiles);
                    }
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