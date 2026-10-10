namespace Sonarr.Api.V5.ManualImport;

public class ManualImportDeleteResource
{
    public string? Folder { get; set; }
    public string? DownloadId { get; set; }
    public List<string> Paths { get; set; } = [];
    public bool DeleteFolders { get; set; }
}
