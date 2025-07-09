using MediaApp.Models;

namespace MediaApp.Services;

public class MediaFileLoadResult
{
    public List<MediaFile> LoadedFiles { get; set; } = new List<MediaFile>();
    public List<string> UnsupportedFiles { get; set; } = new List<string>();
}