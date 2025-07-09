namespace MediaApp.Models;

public class ProgressEventArgs : EventArgs
{
    public int ProgressPercentage { get; set; }
    public int CurrentFileIndex { get; set; }
    public int TotalFiles { get; set; }
    public string CurrentFileName { get; set; }
}