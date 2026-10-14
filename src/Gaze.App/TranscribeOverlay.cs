namespace Gaze.App;

// Four thin, excluded, nonactivating edges leave the selected pixels unobscured.
internal sealed class TranscribeOverlay : IDisposable
{
    private sealed class Edge : Form
    {
        internal Edge()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            TopMost = true; BackColor = Color.FromArgb(181, 15, 33);
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x08000020; return value; }
        }
    }
    private readonly Edge[] edges = [new(), new(), new(), new()];
    internal void Show(Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0) { Hide(); return; }
        int thickness = Math.Min(3, Math.Min(area.Width, area.Height));
        Rectangle[] bounds = [new(area.Left, area.Top, area.Width, thickness), new(area.Left, area.Bottom - thickness, area.Width, thickness),
            new(area.Left, area.Top, thickness, area.Height), new(area.Right - thickness, area.Top, thickness, area.Height)];
        for (int i = 0; i < edges.Length; i++)
        {
            edges[i].Bounds = bounds[i];
            Native.SetWindowDisplayAffinity(edges[i].Handle, 0x11);
            edges[i].Show();
        }
    }
    internal void Hide() { foreach (var edge in edges) edge.Hide(); }
    public void Dispose() { foreach (var edge in edges) edge.Dispose(); }
}
