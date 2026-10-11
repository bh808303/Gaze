using Gaze.Core;
namespace Gaze.Fluent;
public sealed class PanelOptions
{
    public CaptureMode Mode { get; set; }
    public WindowCanvasMode Canvas { get; set; }
    public int FrameRate { get; set; } = 5;
    public string Folder { get; set; } = "";
    public string ShortcutText { get; set; } = "";
    public uint Modifiers { get; set; }
    public int Key { get; set; }
    public bool AutoCopy { get; set; }
    public bool ClickHighlight { get; set; }
    public bool DoubleEscape { get; set; }
}
