namespace TorDl;

public class Options
{
    /// <summary>
    /// Timeout, in seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(120);
    
    /// <summary>
    /// URLs to download.
    /// </summary>
    public List<Uri> Urls { get; } = [];
}