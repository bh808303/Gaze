using System.Drawing.Drawing2D;

namespace Gaze.App;

internal sealed class RoundedField : Panel
{
    private readonly Control input;
    internal RoundedField(Control input)
    {
        this.input = input;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = input is ListBox ? input.Height + 16 : 38;
        Padding = new Padding(9, 6, 9, 6); TabStop = false;
        if (input is TextBox text) text.BorderStyle = BorderStyle.None;
        if (input is NumericUpDown number) number.BorderStyle = BorderStyle.None;
        if (input is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
        if (input is ListBox list) list.BorderStyle = BorderStyle.None;
        input.BackColor = Color.White; input.Margin = Padding.Empty; input.Dock = DockStyle.None;
        Controls.Add(input);
        input.Enter += (_, _) => Invalidate(); input.Leave += (_, _) => Invalidate();
        input.EnabledChanged += (_, _) => Invalidate();
        MouseDown += (_, _) => input.Focus();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (input == null) return;
        int height = input is ListBox ? ClientSize.Height - Padding.Vertical : input.PreferredSize.Height;
        input.SetBounds(Padding.Left, Math.Max(Padding.Top, (ClientSize.Height - height) / 2), Math.Max(1, ClientSize.Width - Padding.Horizontal), Math.Max(1, height));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float d = 16 * DeviceDpi / 96f;
        var rect = new RectangleF(1, 1, Width - 3, Height - 3);
        d = Math.Min(d, Math.Min(rect.Height, rect.Width));
        using var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90); path.CloseFigure();
        using var fill = new SolidBrush(input.Enabled ? Color.White : Color.FromArgb(238, 240, 243)); e.Graphics.FillPath(fill, path);
        using var border = new Pen(input.ContainsFocus ? Color.FromArgb(0, 103, 184) : Color.FromArgb(217, 223, 230), input.ContainsFocus ? 1.5f : 1f); e.Graphics.DrawPath(border, path);
    }
}
