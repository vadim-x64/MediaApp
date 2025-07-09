using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace MediaApp.Models;

public class MediaFile : INotifyPropertyChanged
{
    private bool _isDuplicate;
    private string _filePath;
    private MediaFileType _fileType;
    private long _fileSize;
    private DateTime _creationTime;
    private string _hash;

    public string FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(FileTypeIcon));
        }
    }

    public MediaFileType FileType
    {
        get => _fileType;
        set
        {
            _fileType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FileTypeIcon));
        }
    }

    public long FileSize
    {
        get => _fileSize;
        set
        {
            _fileSize = value;
            OnPropertyChanged();
        }
    }

    public DateTime CreationTime
    {
        get => _creationTime;
        set
        {
            _creationTime = value;
            OnPropertyChanged();
        }
    }

    public string Hash
    {
        get => _hash;
        set
        {
            _hash = value;
            OnPropertyChanged();
        }
    }

    public bool IsDuplicate
    {
        get => _isDuplicate;
        set
        {
            _isDuplicate = value;
            OnPropertyChanged();
        }
    }

    public string FileName => Path.GetFileName(FilePath);

    public string FileTypeIcon
    {
        get
        {
            return FileType switch
            {
                MediaFileType.Image => "🖼️",
                MediaFileType.Video => "🎬",
                _ => "📄"
            };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}