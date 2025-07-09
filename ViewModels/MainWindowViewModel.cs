using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MediaApp.Models;
using MediaApp.Services;

namespace MediaApp.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IFileService _fileService;
    private readonly IDuplicateDetector _duplicateDetector;
    private ObservableCollection<MediaFile> _mediaFiles;
    private bool _canCheckDuplicates;
    private bool _canClearFiles;
    private bool _canDeleteDuplicates;
    private Visibility _progressVisibility;
    private int _progressValue;
    private string _progressPercentageText;
    private bool _isProcessing;
    private string _progressText;
    private string _resultMessage;
    private Visibility _resultVisibility;
    private System.Windows.Media.Brush _resultTextColor;
    private string _fileCountText;

    public ICommand DeleteDuplicatesCommand { get; private set; }

    public MainWindowViewModel(IFileService fileService, IDuplicateDetector duplicateDetector)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _duplicateDetector = duplicateDetector ?? throw new ArgumentNullException(nameof(duplicateDetector));
        FileCountText = "Файли не додано";
        MediaFiles = new ObservableCollection<MediaFile>();
        CanCheckDuplicates = false;
        CanClearFiles = false;
        CanDeleteDuplicates = false;
        ProgressVisibility = Visibility.Collapsed;
        ProgressValue = 0;
        ProgressPercentageText = "0%";
        ProgressText = "Готово";
        ResultVisibility = Visibility.Collapsed;
        ResultTextColor = System.Windows.Media.Brushes.Green;
        _fileService.ProgressChanged += OnFileServiceProgressChanged;
        _duplicateDetector.ProgressChanged += OnDuplicateDetectorProgressChanged;
        DeleteDuplicatesCommand = new RelayCommand(async () => await ExecuteDeleteDuplicatesAsync(), () => CanDeleteDuplicates);
    }

    public string FileCountText
    {
        get => _fileCountText;
        set
        {
            _fileCountText = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<MediaFile> MediaFiles
    {
        get => _mediaFiles;
        set
        {
            _mediaFiles = value;
            OnPropertyChanged();
            UpdateFileCountText();
            UpdateButtonStates();
        }
    }

    private void UpdateFileCountText()
    {
        if (MediaFiles?.Count > 0)
        {
            FileCountText = $"Додано файлів: {MediaFiles.Count}";
        }
        else
        {
            FileCountText = "Файли не додано";
        }
    }

    private void UpdateButtonStates()
    {
        var hasFiles = MediaFiles?.Any() == true;
        CanCheckDuplicates = hasFiles && !IsProcessing;
        CanClearFiles = hasFiles && !IsProcessing;
        CanDeleteDuplicates =
            hasFiles && !IsProcessing &&
            MediaFiles.Any(f => f.IsDuplicate);
        (DeleteDuplicatesCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public bool CanDeleteDuplicates
    {
        get => _canDeleteDuplicates;
        set
        {
            _canDeleteDuplicates = value;
            OnPropertyChanged();
        }
    }

    public bool CanCheckDuplicates
    {
        get => _canCheckDuplicates;
        set
        {
            _canCheckDuplicates = value;
            OnPropertyChanged();
        }
    }

    public bool CanClearFiles
    {
        get => _canClearFiles;
        set
        {
            _canClearFiles = value;
            OnPropertyChanged();
        }
    }

    public Visibility ProgressVisibility
    {
        get => _progressVisibility;
        set
        {
            _progressVisibility = value;
            OnPropertyChanged();
        }
    }

    public int ProgressValue
    {
        get => _progressValue;
        set
        {
            _progressValue = value;
            OnPropertyChanged();
        }
    }

    public string ProgressPercentageText
    {
        get => _progressPercentageText;
        set
        {
            _progressPercentageText = value;
            OnPropertyChanged();
        }
    }

    public string ProgressText
    {
        get => _progressText;
        set
        {
            _progressText = value;
            OnPropertyChanged();
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            _isProcessing = value;
            OnPropertyChanged();
            UpdateButtonStates();
        }
    }

    public string ResultMessage
    {
        get => _resultMessage;
        set
        {
            _resultMessage = value;
            OnPropertyChanged();
        }
    }

    public Visibility ResultVisibility
    {
        get => _resultVisibility;
        set
        {
            _resultVisibility = value;
            OnPropertyChanged();
        }
    }

    public System.Windows.Media.Brush ResultTextColor
    {
        get => _resultTextColor;
        set
        {
            _resultTextColor = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadMediaFilesAsync(string[] filePaths)
    {
        if (filePaths == null || filePaths.Length == 0)
            return;
        try
        {
            IsProcessing = true;
            ProgressVisibility = Visibility.Visible;
            ProgressValue = 0;
            ProgressPercentageText = "0%";
            ProgressText = "Завантаження файлів...";
            ResultVisibility = Visibility.Collapsed;
            var duplicateCheckResult = await CheckForDuplicatesBeforeAdding(filePaths);

            if (duplicateCheckResult.HasIdenticalFiles)
            {
                MessageBox.Show(
                    $"Файли вже додано до списку:\n{string.Join("\n", duplicateCheckResult.IdenticalFiles)}",
                    "Файли вже додано", MessageBoxButton.OK, MessageBoxImage.Information);
                if (duplicateCheckResult.FilesToProcess.Count == 0)
                {
                    ProgressVisibility = Visibility.Collapsed;
                    IsProcessing = false;
                    return;
                }
            }

            if (duplicateCheckResult.HasConflictingFiles)
            {
                var conflictMessage = "Виявлено файли з однаковими іменами, але різним вмістом:\n" +
                                      string.Join("\n", duplicateCheckResult.ConflictingFiles) +
                                      "\n\nБажаєте замінити існуючі файли новими?";
                var result = MessageBox.Show(conflictMessage, "Конфлікт файлів",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var conflictFile in duplicateCheckResult.ConflictingFiles)
                    {
                        var existingFile = MediaFiles.FirstOrDefault(f => f.FileName == conflictFile);
                        if (existingFile != null)
                        {
                            MediaFiles.Remove(existingFile);
                        }
                    }
                }
                else
                {
                    duplicateCheckResult.FilesToProcess = duplicateCheckResult.FilesToProcess
                        .Where(path => !duplicateCheckResult.ConflictingFiles.Contains(Path.GetFileName(path)))
                        .ToList();
                }
            }

            if (duplicateCheckResult.FilesToProcess.Count > 0)
            {
                var loadResult = await _fileService.LoadMediaFilesAsync(duplicateCheckResult.FilesToProcess.ToArray());

                if (loadResult.UnsupportedFiles.Any())
                {
                    var message = "Наступні файли не підтримуються і не були додані:\n\n" +
                                  string.Join("\n", loadResult.UnsupportedFiles);
                    MessageBox.Show(message, "Непідтримуваний тип файлу", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                foreach (var file in loadResult.LoadedFiles)
                {
                    MediaFiles.Add(file);
                }

                UpdateFileCountText();
                UpdateButtonStates();
            }

            await Task.Delay(1000);
            ProgressVisibility = Visibility.Collapsed;
            ProgressText = "Файли завантажено";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка при завантаженні файлів: {ex.Message}",
                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task<DuplicateCheckResult> CheckForDuplicatesBeforeAdding(string[] filePaths)
    {
        var result = new DuplicateCheckResult();

        foreach (var filePath in filePaths)
        {
            var fileName = Path.GetFileName(filePath);
            var existingFile = MediaFiles.FirstOrDefault(f => f.FileName == fileName);

            if (existingFile != null)
            {
                if (existingFile.FilePath == filePath)
                {
                    result.IdenticalFiles.Add(fileName);
                }
                else
                {
                    var existingHash = existingFile.Hash;
                    if (string.IsNullOrEmpty(existingHash))
                    {
                        existingHash = await _fileService.CalculateFileHashAsync(existingFile.FilePath);
                        existingFile.Hash = existingHash;
                    }

                    var newFileHash = await _fileService.CalculateFileHashAsync(filePath);

                    if (existingHash != newFileHash)
                    {
                        result.ConflictingFiles.Add(fileName);
                    }
                    else
                    {
                        result.IdenticalFiles.Add(fileName);
                    }
                }
            }
            else
            {
                result.FilesToProcess.Add(filePath);
            }
        }

        return result;
    }

    public async Task CheckDuplicatesAsync()
    {
        if (!MediaFiles.Any())
            return;

        try
        {
            IsProcessing = true;
            ProgressVisibility = Visibility.Visible;
            ProgressValue = 0;
            ProgressPercentageText = "0%";
            ProgressText = "Перевірка дублікатів...";
            ResultVisibility = Visibility.Collapsed;

            foreach (var file in MediaFiles)
            {
                file.IsDuplicate = false;
            }

            var updatedFiles = await _duplicateDetector.DetectDuplicatesAsync(MediaFiles.ToList());
            var duplicateCount = updatedFiles.Count(f => f.IsDuplicate);

            ProgressText = "Перевірка завершена";
            await Task.Delay(1000);
            ProgressVisibility = Visibility.Collapsed;

            if (duplicateCount > 0)
            {
                var duplicateGroups = _duplicateDetector.GetDuplicateGroups(updatedFiles);
                ResultMessage =
                    $"Виявлено {duplicateCount} дублікатів у {duplicateGroups.Count} групах. Дублікати виділено червоним кольором.";
                ResultTextColor = System.Windows.Media.Brushes.Red;
                ResultVisibility = Visibility.Visible;
            }
            else
            {
                ResultMessage = "Дублікати не виявлено.";
                ResultTextColor = System.Windows.Media.Brushes.Green;
                ResultVisibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка при перевірці дублікатів: {ex.Message}",
                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task ExecuteDeleteDuplicatesAsync()
    {
        if (!MediaFiles.Any(f => f.IsDuplicate))
        {
            MessageBox.Show("Дублікати не виявлено.", "Немає дублікатів", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirmResult = MessageBox.Show(
            "Ви впевнені, що хочете видалити всі виявлені дублікати файлів, залишивши лише один найкращої якості у кожній групі?",
            "Підтвердження видалення", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsProcessing = true;
            ProgressVisibility = Visibility.Visible;
            ProgressValue = 0;
            ProgressPercentageText = "0%";
            ProgressText = "Видалення дублікатів...";
            ResultVisibility = Visibility.Collapsed;

            var duplicateGroups = _duplicateDetector.GetDuplicateGroups(MediaFiles.ToList());
            var totalDuplicatesToDelete = duplicateGroups.Sum(g => g.Count - 1);
            var deletedCount = 0;

            foreach (var group in duplicateGroups)
            {
                var bestFile = group.OrderByDescending(f => f.FileSize).FirstOrDefault();

                if (bestFile == null) continue;

                foreach (var fileToDelete in group.Where(f => f != bestFile))
                {
                    try
                    {
                        if (File.Exists(fileToDelete.FilePath))
                        {
                            _fileService.DeleteFile(fileToDelete.FilePath);
                            Application.Current.Dispatcher.Invoke(() =>
                                MediaFiles.Remove(fileToDelete));
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка при видаленні файлу {fileToDelete.FileName}: {ex.Message}",
                            "Помилка видалення", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    ProgressValue = (deletedCount * 100) / totalDuplicatesToDelete;
                    ProgressPercentageText = $"{ProgressValue}%";
                    ProgressText = $"Видалення дублікатів... ({deletedCount}/{totalDuplicatesToDelete})";
                    await Task.Delay(10);
                }
            }

            UpdateFileCountText();
            UpdateButtonStates();

            ProgressText = "Видалення завершено";
            await Task.Delay(1000);
            ProgressVisibility = Visibility.Collapsed;

            if (deletedCount > 0)
            {
                await CheckDuplicatesAsync();
            }
            else
            {
                ResultMessage = "Дублікати не виявлено.";
                ResultTextColor = System.Windows.Media.Brushes.Green;
                ResultVisibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка при видаленні дублікатів: {ex.Message}",
                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    public void ClearFiles()
    {
        if (MediaFiles?.Any() == true)
        {
            MediaFiles.Clear();
            UpdateFileCountText();
            UpdateButtonStates();
            ResultVisibility = Visibility.Collapsed;
        }
    }

    private void OnFileServiceProgressChanged(object sender, ProgressEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ProgressValue = e.ProgressPercentage;
            ProgressPercentageText = $"{e.ProgressPercentage}%";
            ProgressText = $"Завантаження файлів... ({e.CurrentFileIndex}/{e.TotalFiles})";
        });
    }

    private void OnDuplicateDetectorProgressChanged(object sender, ProgressEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ProgressValue = e.ProgressPercentage;
            ProgressPercentageText = $"{e.ProgressPercentage}%";
            ProgressText = $"Перевірка дублікатів... ({e.CurrentFileIndex}/{e.TotalFiles})";
        });
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}