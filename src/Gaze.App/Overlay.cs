namespace Gaze.App;

internal sealed class Overlay : Form
{
    private readonly Label label = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true, ForeColor = Color.White, Font = new Font("Segoe UI", 11), Padding = new Padding(8) };
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; }
    }
    internal Overlay()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        BackColor = Color.FromArgb(30, 34, 42); Size = new Size(260, 42); StartPosition = FormStartPosition.Manual;
        Location = new Point(Screen.PrimaryScreen!.WorkingArea.Right - Width - 24, 24);
        Controls.Add(label);
        Point drag = Point.Empty;
        label.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) drag = e.Location; };
        label.MouseMove += (_, e) => { if (e.Button == MouseButtons.Left) Location = new Point(Left + e.X - drag.X, Top + e.Y - drag.Y); };
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!Native.SetWindowDisplayAffinity(Handle, 0x11))
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "Cannot exclude Gaze overlay from capture.");
    }
    internal void Display(string text)
    {
        var area = Screen.FromPoint(Location).WorkingArea;
        var multiline = text.Contains('\n');
        Size = new Size(Math.Min(multiline ? 620 : 420, area.Width), multiline ? 90 : 42);
        Location = new Point(Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        label.Text = text; if (!Visible) Show();
    }
}
