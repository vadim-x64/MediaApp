using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MediaApp.Models;
using MediaApp.Services;

namespace MediaApp.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private bool _isGridView = false;

    // Властивість для режиму Плитки
    public bool IsGridView
    {
        get => _isGridView;
        set
        {
            _isGridView = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsListView)); // Оновлюємо залежну властивість
        }
    }

    // Властивість для режиму Списку (протилежність Плитки)
    public bool IsListView
    {
        get => !_isGridView;
        set { IsGridView = !value; }
    }

    private Visibility _cancelPanelVisibility = Visibility.Collapsed;
    private double _cancelProgress;
    private string _cancelCountdownText;
    private string _cancelMessage;
    private CancellationTokenSource _deleteCts;
    private List<MediaFile> _filesPendingDeletion;

    public ICommand RefreshListCommand { get; private set; }
    public ICommand CancelDeleteCommand { get; private set; }
    public ICommand DeleteDuplicatesCommand { get; private set; }
    public ICommand DeleteAllFilesCommand { get; private set; }

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
        DeleteAllFilesCommand = new RelayCommand(async () => await DeleteAllFilesAsync(), () => CanClearFiles);
        CancelDeleteCommand = new RelayCommand(CancelDelete);
        // Додай цей рядок поруч з ініціалізацією інших команд:
        RefreshListCommand = new RelayCommand(RefreshList, () => !IsProcessing && MediaFiles?.Any() == true);
    }

    public Visibility CancelPanelVisibility
    {
        get => _cancelPanelVisibility;
        set { _cancelPanelVisibility = value; OnPropertyChanged(); }
    }

    public double CancelProgress
    {
        get => _cancelProgress;
        set { _cancelProgress = value; OnPropertyChanged(); }
    }

    public string CancelCountdownText
    {
        get => _cancelCountdownText;
        set { _cancelCountdownText = value; OnPropertyChanged(); }
    }

    public string CancelMessage
    {
        get => _cancelMessage;
        set { _cancelMessage = value; OnPropertyChanged(); }
    }

    public string FileCountText
    {
        get => _fileCountText;
        set { _fileCountText = value; OnPropertyChanged(); }
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
            FileCountText = $"Елементів: {MediaFiles.Count}";
        }
        else
        {
            FileCountText = "Файли не додано";
        }
    }

    private void UpdateButtonStates()
    {
        var hasFiles = MediaFiles?.Any() == true;
        var imageCount = MediaFiles?.Count(f => f.FileType == MediaFileType.Image) ?? 0;
        var videoCount = MediaFiles?.Count(f => f.FileType == MediaFileType.Video) ?? 0;
        bool canCompare = imageCount >= 2 || videoCount >= 2;

        CanCheckDuplicates = canCompare && !IsProcessing;
        CanClearFiles = hasFiles && !IsProcessing;
        CanDeleteDuplicates = hasFiles && !IsProcessing && MediaFiles.Any(f => f.IsDuplicate);

        (DeleteDuplicatesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteAllFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // Додай в кінець методу UpdateButtonStates():
        (RefreshListCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public bool CanDeleteDuplicates
    {
        get => _canDeleteDuplicates;
        set { _canDeleteDuplicates = value; OnPropertyChanged(); }
    }

    public bool CanCheckDuplicates
    {
        get => _canCheckDuplicates;
        set { _canCheckDuplicates = value; OnPropertyChanged(); }
    }

    public bool CanClearFiles
    {
        get => _canClearFiles;
        set { _canClearFiles = value; OnPropertyChanged(); }
    }

    public Visibility ProgressVisibility
    {
        get => _progressVisibility;
        set { _progressVisibility = value; OnPropertyChanged(); }
    }

    public int ProgressValue
    {
        get => _progressValue;
        set { _progressValue = value; OnPropertyChanged(); }
    }

    public string ProgressPercentageText
    {
        get => _progressPercentageText;
        set { _progressPercentageText = value; OnPropertyChanged(); }
    }

    public string ProgressText
    {
        get => _progressText;
        set { _progressText = value; OnPropertyChanged(); }
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
        set { _resultMessage = value; OnPropertyChanged(); }
    }

    public Visibility ResultVisibility
    {
        get => _resultVisibility;
        set { _resultVisibility = value; OnPropertyChanged(); }
    }

    public System.Windows.Media.Brush ResultTextColor
    {
        get => _resultTextColor;
        set { _resultTextColor = value; OnPropertyChanged(); }
    }

    public async Task LoadMediaFilesAsync(string[] filePaths)
    {
        if (IsProcessing)
        {
            MessageBox.Show("Будь ласка, дочекайтеся завершення поточного процесу перед додаванням нових файлів.",
                "Процес триває", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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
                    $"Деякі файли ({duplicateCheckResult.IdenticalFiles.Count} шт.) вже присутні у списку і не будуть додані повторно.",
                    "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);

                if (duplicateCheckResult.FilesToProcess.Count == 0)
                {
                    ProgressVisibility = Visibility.Collapsed;
                    IsProcessing = false;
                    return;
                }
            }

            if (duplicateCheckResult.FilesToProcess.Count > 0)
            {
                var loadResult = await _fileService.LoadMediaFilesAsync(duplicateCheckResult.FilesToProcess.ToArray());

                if (loadResult.UnsupportedFiles.Any())
                {
                    var message = "Наступні файли не підтримуються і не були додані:\n\n" +
                                  string.Join("\n", loadResult.UnsupportedFiles);

                    MessageBox.Show(message, "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    private Task<DuplicateCheckResult> CheckForDuplicatesBeforeAdding(string[] filePaths)
    {
        var result = new DuplicateCheckResult();

        foreach (var filePath in filePaths)
        {
            var existingFile = MediaFiles.FirstOrDefault(f => f.FilePath == filePath);

            if (existingFile != null)
            {
                result.IdenticalFiles.Add(Path.GetFileName(filePath));
            }
            else
            {
                result.FilesToProcess.Add(filePath);
            }
        }

        return Task.FromResult(result);
    }

    public async Task CheckDuplicatesAsync()
    {
        if (IsProcessing)
        {
            MessageBox.Show("Будь ласка, дочекайтеся завершення поточного процесу.",
                "Процес триває", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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

            ProgressText = "Перевірку завершена";
            await Task.Delay(1000);
            ProgressVisibility = Visibility.Collapsed;

            if (duplicateCount > 0)
            {
                var duplicateGroups = _duplicateDetector.GetDuplicateGroups(updatedFiles);
                ResultMessage =
                    $"Виявлено {duplicateCount} дублікатів у {duplicateGroups.Count} групах";
                ResultTextColor = System.Windows.Media.Brushes.Red;
                ResultVisibility = Visibility.Visible;
            }
            else
            {
                ResultMessage = "Дублікати не виявлено";
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
            MessageBox.Show("Дублікати не виявлено", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmResult = MessageBox.Show(
            "Ви впевнені, що хочете видалити всі виявлені дублікати файлів?",
            "Попередження", MessageBoxButton.YesNo, MessageBoxImage.Warning);

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
            var manuallyDeletedCount = 0;

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
                            deletedCount++;
                        }
                        else
                        {
                            manuallyDeletedCount++;
                        }

                        Application.Current.Dispatcher.Invoke(() => MediaFiles.Remove(fileToDelete));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка при видаленні файлу {fileToDelete.FileName}: {ex.Message}",
                            "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    int processed = deletedCount + manuallyDeletedCount;
                    ProgressValue = totalDuplicatesToDelete > 0 ? (processed * 100) / totalDuplicatesToDelete : 100;
                    ProgressPercentageText = $"{ProgressValue}%";
                    ProgressText = $"Опрацювання дублікатів... ({processed}/{totalDuplicatesToDelete})";
                    await Task.Delay(10);
                }
            }

            UpdateFileCountText();
            UpdateButtonStates();

            ProgressText = "Видалення завершено";
            await Task.Delay(1000);
            ProgressVisibility = Visibility.Collapsed;

            foreach (var file in MediaFiles)
            {
                file.IsDuplicate = false;
            }

            if (deletedCount > 0 || manuallyDeletedCount > 0)
            {
                string msg = deletedCount > 0
                    ? $"Видалено {deletedCount} дублікат(ів)."
                    : "Дублікати вже були видалені з диска вручну.";

                ResultMessage = $"{msg} Дублікатів більше немає.";
                ResultTextColor = System.Windows.Media.Brushes.Green;
                ResultVisibility = Visibility.Visible;
            }
            else
            {
                ResultMessage = "Дублікати не виявлено";
                ResultTextColor = System.Windows.Media.Brushes.Green;
                ResultVisibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка при опрацюванні дублікатів: {ex.Message}",
                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    public async Task RequestDeleteAsync(List<MediaFile> filesToDelete)
    {
        if (filesToDelete == null || !filesToDelete.Any() || CancelPanelVisibility == Visibility.Visible || IsProcessing)
            return;

        // Додаємо вікно підтвердження перед запуском таймера
        string confirmMessage = filesToDelete.Count == 1
            ? $"Ви впевнені, що хочете назавжди видалити цей файл з диска?\n\n{filesToDelete.First().FilePath}"
            : $"Ви впевнені, що хочете назавжди видалити обрані файли ({filesToDelete.Count} шт.) з диска?";

        var confirmResult = MessageBox.Show(
            confirmMessage,
            "Підтвердження видалення",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmResult != MessageBoxResult.Yes)
        {
            return;
        }

        _filesPendingDeletion = filesToDelete.ToList();
        _deleteCts = new CancellationTokenSource();

        CancelMessage = _filesPendingDeletion.Count == 1
            ? "Видалення 1 файлу..."
            : $"Видалення файлів ({_filesPendingDeletion.Count})...";

        CancelPanelVisibility = Visibility.Visible;

        try
        {
            int totalDurationMs = 5000;
            int stepMs = 50;

            for (int i = totalDurationMs; i >= 0; i -= stepMs)
            {
                CancelProgress = (double)i / totalDurationMs;
                CancelCountdownText = Math.Ceiling((double)i / 1000).ToString();
                await Task.Delay(stepMs, _deleteCts.Token);
            }

            CancelPanelVisibility = Visibility.Collapsed;
            await ExecuteDeletionAsync(_filesPendingDeletion);
        }
        catch (TaskCanceledException)
        {
            CancelPanelVisibility = Visibility.Collapsed;
            _filesPendingDeletion = null;
        }
        finally
        {
            _deleteCts?.Dispose();
            _deleteCts = null;
        }
    }

    private void CancelDelete()
    {
        if (_deleteCts != null && !_deleteCts.IsCancellationRequested)
        {
            _deleteCts.Cancel();
        }
    }

    private async Task ExecuteDeletionAsync(List<MediaFile> filesToDelete)
    {
        if (filesToDelete == null || !filesToDelete.Any()) return;

        IsProcessing = true;
        int deletedCount = 0;

        foreach (var file in filesToDelete)
        {
            try
            {
                if (File.Exists(file.FilePath))
                {
                    _fileService.DeleteFile(file.FilePath);
                    deletedCount++;
                }
                Application.Current.Dispatcher.Invoke(() => MediaFiles.Remove(file));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка при видаленні файлу {file.FileName}: {ex.Message}");
            }
        }

        UpdateFileCountText();
        UpdateButtonStates();
        IsProcessing = false;

        if (deletedCount > 0)
        {
            ResultMessage = $"Успішно видалено файлів: {deletedCount}.";
            ResultTextColor = System.Windows.Media.Brushes.Green;
            ResultVisibility = Visibility.Visible;
        }
    }

    public void ClearFiles()
    {
        if (IsProcessing)
        {
            MessageBox.Show("Будь ласка, дочекайтеся завершення поточного процесу.",
                "Процес триває", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MediaFiles?.Any() == true)
        {
            MediaFiles.Clear();
            UpdateFileCountText();
            UpdateButtonStates();
            ResultVisibility = Visibility.Collapsed;
        }
    }

    private async Task DeleteAllFilesAsync()
    {
        if (!MediaFiles.Any() || IsProcessing) return;

        var result = MessageBox.Show(
            "Ви впевнені, що хочете НАЗАВЖДИ видалити ВСІ файли зі списку з диска?\nЦю дію неможливо відмінити!",
            "УВАГА: Видалення всіх файлів", MessageBoxButton.YesNo, MessageBoxImage.Error);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsProcessing = true;
                ProgressVisibility = Visibility.Visible;
                ProgressValue = 0;
                ProgressPercentageText = "0%";
                ProgressText = "Видалення файлів...";
                ResultVisibility = Visibility.Collapsed;

                var filesToDelete = MediaFiles.ToList();
                int total = filesToDelete.Count;
                int deleted = 0;
                int manuallyDeleted = 0;

                foreach (var file in filesToDelete)
                {
                    try
                    {
                        if (File.Exists(file.FilePath))
                        {
                            _fileService.DeleteFile(file.FilePath);
                            deleted++;
                        }
                        else
                        {
                            manuallyDeleted++;
                        }

                        Application.Current.Dispatcher.Invoke(() => MediaFiles.Remove(file));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Помилка при видаленні файлу {file.FileName}: {ex.Message}");
                    }

                    int processed = deleted + manuallyDeleted;
                    ProgressValue = total > 0 ? (processed * 100) / total : 100;
                    ProgressPercentageText = $"{ProgressValue}%";
                    ProgressText = $"Видалення... ({processed}/{total})";
                    await Task.Delay(10);
                }

                UpdateFileCountText();
                UpdateButtonStates();

                ProgressText = "Видалення завершено";
                await Task.Delay(1000);
                ProgressVisibility = Visibility.Collapsed;

                ResultMessage = $"Успішно видалено файлів: {deleted}.";
                ResultTextColor = System.Windows.Media.Brushes.Green;
                ResultVisibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при видаленні файлів: {ex.Message}",
                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
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

    private void RefreshList()
    {
        if (IsProcessing || MediaFiles == null || !MediaFiles.Any()) return;

        // Шукаємо файли, які фізично відсутні на диску
        var missingFiles = MediaFiles.Where(f => !File.Exists(f.FilePath)).ToList();

        if (missingFiles.Any())
        {
            foreach (var file in missingFiles)
            {
                MediaFiles.Remove(file);
            }

            UpdateFileCountText();
            UpdateButtonStates();

            ResultMessage = $"Список оновлено. Вилучено відсутніх файлів: {missingFiles.Count}.";
            ResultTextColor = System.Windows.Media.Brushes.Green;
            ResultVisibility = Visibility.Visible;
        }
        else
        {
            ResultMessage = "Список актуальний. Всі файли на місці.";
            ResultTextColor = System.Windows.Media.Brushes.Green;
            ResultVisibility = Visibility.Visible;
        }
    }
}