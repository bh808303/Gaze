using System.Drawing.Drawing2D;
namespace Gaze.App;
internal sealed class ToggleSwitch : CheckBox
{
    internal ToggleSwitch() { AutoSize = false; Size = new Size(46, 26); MinimumSize = Size; MaximumSize = Size; Text = ""; Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f, h = 22 * scale, w = 42 * scale;
        float x = (Width - w) / 2, y = (Height - h) / 2;
        using var path = new GraphicsPath(); path.AddArc(x, y, h, h, 90, 180); path.AddArc(x + w - h, y, h, h, 270, 180); path.CloseFigure();
        using var fill = new SolidBrush(Enabled && Checked ? Color.FromArgb(0, 103, 184) : Color.FromArgb(180, 187, 197)); e.Graphics.FillPath(fill, path);
        using var thumb = new SolidBrush(Color.White); float d = h - 6 * scale;
        e.Graphics.FillEllipse(thumb, Checked ? x + w - d - 3 * scale : x + 3 * scale, y + 3 * scale, d, d);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}
