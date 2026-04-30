namespace OpenClawClient.Models;

public class ReceivedFile
{
    public string FileName { get; set; } = "";
    public string LocalPath { get; set; } = "";
    public long Size { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public string MimeType { get; set; } = "";
}
