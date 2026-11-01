using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.ComponentModel;

namespace Mh2TexEditor;

internal enum PreviewBackground
{
    Checkerboard,
    Dark,
    Light,
}

internal sealed class TextureViewport : Control
{
    private Bitmap? image;
    private float zoom = 1f;
    private PointF origin;
    private bool fitToWindow = true;
    private bool dragging;
    private Point dragStart;
    private PointF originAtDragStart;

    public TextureViewport()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(34, 38, 46);
        AllowDrop = true;
        TabStop = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        DragEnter += HandleDragEnter;
        DragDrop += HandleDragDrop;
    }

    public event EventHandler<string>? PngDropped;

    [DefaultValue(PreviewBackground.Checkerboard)]
    public PreviewBackground BackgroundMode { get; set; } = PreviewBackground.Checkerboard;

    public float Zoom => fitToWindow ? CalculateFitScale() : zoom;

    public void SetImage(Bitmap? value)
    {
        image?.Dispose();
        image = value;
        fitToWindow = true;
        Invalidate();
    }

    public void Fit()
    {
        fitToWindow = true;
        Invalidate();
    }

    public void ActualSize()
    {
        if (image is null)
            return;
        fitToWindow = false;
        zoom = 1f;
        origin = new PointF((ClientSize.Width - image.Width) / 2f, (ClientSize.Height - image.Height) / 2f);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            image?.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawBackground(e.Graphics);
        if (image is null)
        {
            using var font = new Font(Font.FontFamily, 11f);
            TextRenderer.DrawText(
                e.Graphics,
                "未打开 TEX",
                font,
                ClientRectangle,
                Color.FromArgb(150, 160, 176),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var scale = fitToWindow ? CalculateFitScale() : zoom;
        var drawOrigin = fitToWindow
            ? new PointF((ClientSize.Width - image.Width * scale) / 2f, (ClientSize.Height - image.Height * scale) / 2f)
            : origin;
        var destination = new RectangleF(drawOrigin.X, drawOrigin.Y, image.Width * scale, image.Height * scale);

        e.Graphics.InterpolationMode = scale >= 1f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = scale >= 1f ? PixelOffsetMode.Half : PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(image, destination, new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);

        using var border = new Pen(Color.FromArgb(110, 122, 142));
        e.Graphics.DrawRectangle(border, destination.X, destination.Y, destination.Width, destination.Height);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (image is null)
            return;

        var oldScale = fitToWindow ? CalculateFitScale() : zoom;
        var oldOrigin = fitToWindow
            ? new PointF((ClientSize.Width - image.Width * oldScale) / 2f, (ClientSize.Height - image.Height * oldScale) / 2f)
            : origin;
        var factor = e.Delta > 0 ? 1.2f : 1f / 1.2f;
        var newScale = Math.Clamp(oldScale * factor, 0.03f, 32f);
        var imageX = (e.X - oldOrigin.X) / oldScale;
        var imageY = (e.Y - oldOrigin.Y) / oldScale;

        fitToWindow = false;
        zoom = newScale;
        origin = new PointF(e.X - imageX * newScale, e.Y - imageY * newScale);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (image is null || (e.Button != MouseButtons.Left && e.Button != MouseButtons.Middle))
            return;
        if (fitToWindow)
        {
            var scale = CalculateFitScale();
            origin = new PointF((ClientSize.Width - image.Width * scale) / 2f, (ClientSize.Height - image.Height * scale) / 2f);
            zoom = scale;
            fitToWindow = false;
        }
        dragging = true;
        dragStart = e.Location;
        originAtDragStart = origin;
        Cursor = Cursors.SizeAll;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging)
            return;
        origin = new PointF(
            originAtDragStart.X + e.X - dragStart.X,
            originAtDragStart.Y + e.Y - dragStart.Y);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragging = false;
        Cursor = Cursors.Default;
        Capture = false;
    }

    private float CalculateFitScale()
    {
        if (image is null || image.Width == 0 || image.Height == 0)
            return 1f;
        var width = Math.Max(1, ClientSize.Width - 32);
        var height = Math.Max(1, ClientSize.Height - 32);
        return Math.Max(0.03f, Math.Min(width / (float)image.Width, height / (float)image.Height));
    }

    private void DrawBackground(Graphics graphics)
    {
        if (BackgroundMode == PreviewBackground.Dark)
        {
            graphics.Clear(Color.FromArgb(28, 31, 38));
            return;
        }
        if (BackgroundMode == PreviewBackground.Light)
        {
            graphics.Clear(Color.FromArgb(232, 235, 240));
            return;
        }

        graphics.Clear(Color.FromArgb(56, 61, 70));
        const int tile = 16;
        using var brush = new SolidBrush(Color.FromArgb(75, 81, 92));
        for (var y = 0; y < ClientSize.Height; y += tile)
        {
            for (var x = 0; x < ClientSize.Width; x += tile)
            {
                if (((x / tile) + (y / tile)) % 2 == 0)
                    graphics.FillRectangle(brush, x, y, tile, tile);
            }
        }
    }

    private void HandleDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files &&
            files.Length == 1 &&
            string.Equals(Path.GetExtension(files[0]), ".png", StringComparison.OrdinalIgnoreCase))
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void HandleDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1)
            PngDropped?.Invoke(this, files[0]);
    }
}
