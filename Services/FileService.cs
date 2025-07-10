using System.IO;
using System.Security.Cryptography;
using MediaApp.Models;

namespace MediaApp.Services;

public class FileService : IFileService
{
    public event EventHandler<ProgressEventArgs> ProgressChanged;
    private readonly string[] _supportedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
    private readonly string[] _supportedVideoExtensions = { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v" };
    
    public async Task<MediaFileLoadResult> LoadMediaFilesAsync(string[] filePaths)
    {
        var result = new MediaFileLoadResult();
        var totalFiles = filePaths.Length;

        for (int i = 0; i < totalFiles; i++)
        {
            var filePath = filePaths[i];

            OnProgressChanged(new ProgressEventArgs
            {
                ProgressPercentage = (i * 100) / totalFiles,
                CurrentFileIndex = i + 1,
                TotalFiles = totalFiles,
                CurrentFileName = Path.GetFileName(filePath)
            });

            try
            {
                if (IsMediaFile(filePath))
                {
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        
                        var mediaFile = new MediaFile
                        {
                            FilePath = filePath,
                            FileType = GetMediaFileType(filePath),
                            FileSize = fileInfo.Length,
                            CreationTime = fileInfo.CreationTime
                        };
                        
                        result.LoadedFiles.Add(mediaFile);
                    }
                }
                else
                {
                    result.UnsupportedFiles.Add(Path.GetFileName(filePath));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка при обробці файлу {filePath}: {ex.Message}");
            }

            await Task.Delay(50);
        }

        OnProgressChanged(new ProgressEventArgs
        {
            ProgressPercentage = 100,
            CurrentFileIndex = totalFiles,
            TotalFiles = totalFiles,
            CurrentFileName = "Завантаження завершено"
        });
        
        return result;
    }

    public bool IsMediaFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;
        
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return _supportedImageExtensions.Contains(extension) ||
               _supportedVideoExtensions.Contains(extension);
    }

    public MediaFileType GetMediaFileType(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return MediaFileType.Unknown;
        
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (_supportedImageExtensions.Contains(extension))
            return MediaFileType.Image;
        
        if (_supportedVideoExtensions.Contains(extension))
            return MediaFileType.Video;

        return MediaFileType.Unknown;
    }

    public async Task<string> CalculateFileHashAsync(string filePath)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                var hash = await Task.Run(() => md5.ComputeHash(stream));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public void RenameFile(string oldPath, string newPath)
    {
        if (File.Exists(oldPath))
        {
            File.Move(oldPath, newPath);
        }
    }

    public void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    protected virtual void OnProgressChanged(ProgressEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);
    }
}