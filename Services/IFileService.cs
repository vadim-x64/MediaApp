using MediaApp.Models;

namespace MediaApp.Services;

public interface IFileService
{
    event EventHandler<ProgressEventArgs> ProgressChanged;
    Task<MediaFileLoadResult> LoadMediaFilesAsync(string[] filePaths);
    bool IsMediaFile(string filePath);
    MediaFileType GetMediaFileType(string filePath);
    Task<string> CalculateFileHashAsync(string filePath);
    void RenameFile(string oldPath, string newPath);
    void DeleteFile(string filePath);
}