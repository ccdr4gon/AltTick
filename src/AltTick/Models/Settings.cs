namespace AltTick.Models;

internal class AppSettings
{
    public int ThumbnailWidth { get; set; } = 240;
    public int ThumbnailHeight { get; set; } = 160;
    public bool RunAtStartup { get; set; } = false;
    public bool ShowWindowTitles { get; set; } = true;
    public List<string> ExcludedApps { get; set; } = [];
}
