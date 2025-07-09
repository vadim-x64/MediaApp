namespace MediaApp.Models;

public class DuplicateCheckResult
{
    public List<string> IdenticalFiles { get; set; } = new List<string>();
    public List<string> ConflictingFiles { get; set; } = new List<string>();
    public List<string> FilesToProcess { get; set; } = new List<string>();
    public bool HasIdenticalFiles => IdenticalFiles.Count > 0;
    public bool HasConflictingFiles => ConflictingFiles.Count > 0;
}