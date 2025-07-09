using MediaApp.Models;

namespace MediaApp.Services;

public interface IDuplicateDetector
{
    event EventHandler<ProgressEventArgs> ProgressChanged;
    Task<List<MediaFile>> DetectDuplicatesAsync(List<MediaFile> mediaFiles);
    bool AreDuplicates(MediaFile file1, MediaFile file2);
    List<List<MediaFile>> GetDuplicateGroups(List<MediaFile> mediaFiles);
}