using MediaApp.Models;

namespace MediaApp.Services;

public class DuplicateDetector : IDuplicateDetector
{
    public event EventHandler<ProgressEventArgs> ProgressChanged;
    private readonly IFileService _fileService;

    public DuplicateDetector(IFileService fileService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    }

    public async Task<List<MediaFile>> DetectDuplicatesAsync(List<MediaFile> mediaFiles)
    {
        if (mediaFiles == null || mediaFiles.Count == 0)
            return new List<MediaFile>();

        var totalFiles = mediaFiles.Count;
        var processedFiles = 0;
        
        foreach (var file in mediaFiles)
        {
            if (string.IsNullOrEmpty(file.Hash))
            {
                try
                {
                    file.Hash = await _fileService.CalculateFileHashAsync(file.FilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Помилка при обчисленні хешу для файлу {file.FilePath}: {ex.Message}");
                    file.Hash = string.Empty;
                }
            }

            processedFiles++;
            
            OnProgressChanged(new ProgressEventArgs
            {
                ProgressPercentage = (processedFiles * 100) / totalFiles,
                CurrentFileIndex = processedFiles,
                TotalFiles = totalFiles,
                CurrentFileName = file.FileName
            });
            
            await Task.Delay(10);
        }
        
        var duplicateGroups = mediaFiles
            .Where(f => !string.IsNullOrEmpty(f.Hash))
            .GroupBy(f => new { f.FileType, f.Hash })
            .Where(g => g.Count() > 1)
            .ToList();
        
        foreach (var group in duplicateGroups)
        {
            foreach (var file in group)
            {
                file.IsDuplicate = true;
            }
        }

        return mediaFiles;
    }

    public bool AreDuplicates(MediaFile file1, MediaFile file2)
    {
        if (file1 == null || file2 == null)
            return false;
        
        return file1.FileType == file2.FileType &&
               !string.IsNullOrEmpty(file1.Hash) &&
               !string.IsNullOrEmpty(file2.Hash) &&
               file1.Hash.Equals(file2.Hash, StringComparison.OrdinalIgnoreCase);
    }

    public List<List<MediaFile>> GetDuplicateGroups(List<MediaFile> mediaFiles)
    {
        if (mediaFiles == null || mediaFiles.Count == 0)
            return new List<List<MediaFile>>();

        return mediaFiles
            .Where(f => f.IsDuplicate && !string.IsNullOrEmpty(f.Hash))
            .GroupBy(f => new { f.FileType, f.Hash })
            .Where(g => g.Count() > 1)
            .Select(g => g.ToList())
            .ToList();
    }

    protected virtual void OnProgressChanged(ProgressEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);
    }
}