using System.Drawing.Drawing2D;

namespace Gaze.App;

internal enum ButtonIcon { None, Settings, Close, Back }

internal sealed class SoftButton : Button
{
    private bool hovered;
    private ButtonIcon icon;
    [System.ComponentModel.DefaultValue(ButtonIcon.None)]
    internal ButtonIcon Icon { get => icon; set { icon = value; Invalidate(); } }
    internal SoftButton()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        BackColor = Color.White; ForeColor = Color.FromArgb(35, 45, 58);
        Cursor = Cursors.Hand; SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float radius = 8 * DeviceDpi / 96f;
        var rect = new RectangleF(1, 1, Width - 3, Height - 3);
        using var path = new GraphicsPath();
        float d = Math.Min(radius * 2, rect.Height);
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90); path.CloseFigure();
        var fill = !Enabled ? Color.FromArgb(234, 237, 241) : hovered ? ControlPaint.Light(BackColor, .12f) : BackColor;
        if (hovered && BackColor == Color.White && Enabled) fill = Color.FromArgb(233, 240, 247);
        using var brush = new SolidBrush(fill); e.Graphics.FillPath(brush, path);
        using var border = new Pen(BackColor == Color.White ? Color.FromArgb(217, 223, 230) : fill); e.Graphics.DrawPath(border, path);
        if (Icon == ButtonIcon.None)
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Color.FromArgb(145, 151, 161), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        else
        {
            e.Graphics.TranslateTransform(Width / 2f, Height / 2f); e.Graphics.ScaleTransform(DeviceDpi / 96f, DeviceDpi / 96f);
            using var ink = new Pen(Enabled ? ForeColor : SystemColors.GrayText, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (Icon == ButtonIcon.Close) { e.Graphics.DrawLine(ink, -5, -5, 5, 5); e.Graphics.DrawLine(ink, -5, 5, 5, -5); }
            else if (Icon == ButtonIcon.Back) { e.Graphics.DrawLine(ink, 7, 0, -7, 0); e.Graphics.DrawLines(ink, [new PointF(-1, -6), new PointF(-7, 0), new PointF(-1, 6)]); }
            else
            {
                for (int row = 0; row < 3; row++)
                {
                    float y = (row - 1) * 6; float x = row == 1 ? 4 : -3;
                    e.Graphics.DrawLine(ink, -8, y, 8, y);
                    using var knob = new SolidBrush(fill); e.Graphics.FillEllipse(knob, x - 2.5f, y - 2.5f, 5, 5); e.Graphics.DrawEllipse(ink, x - 2.5f, y - 2.5f, 5, 5);
                }
            }
            e.Graphics.ResetTransform();
        }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
    }
}

