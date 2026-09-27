using Doqua.Controls;
using Doqua.GUI;

namespace BitmapExample;

/// <summary>
/// A Rectangle that also shows a bitmap: the rectangle's Color is the background behind the
/// image, which shows through its transparent parts.
/// </summary>
class BitmapRectangle : Rectangle
{
    private Bitmap? _bitmap;
    private Rect? _source;
    private bool _stretch;

    public Bitmap? Bitmap
    {
        get => _bitmap;
        set
        {
            _bitmap = value;
            Invalidate();
        }
    }

    /// <summary>Part of the bitmap to show; the whole bitmap if null.</summary>
    public Rect? Source
    {
        get => _source;
        set
        {
            _source = value;
            Invalidate();
        }
    }

    /// <summary>When true, the image is scaled to fill the rectangle; otherwise it is centered at its natural size.</summary>
    public bool Stretch
    {
        get => _stretch;
        set
        {
            _stretch = value;
            Invalidate();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); // Fills the rectangle with Color.
        if (_bitmap == null)
            return;

        if (_stretch)
        {
            _bitmap.Draw(dc, new Rect(0, 0, Width, Height), _source);
        }
        else
        {
            var size = _source ?? new Rect(0, 0, _bitmap.Width, _bitmap.Height);
            _bitmap.Draw(dc, (Width - size.Width) / 2, (Height - size.Height) / 2, _source);
        }
    }
}

class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Doqua Bitmap";
        Icons = ExampleIcon.Load();
        Width = 800;
        Height = 620;

        var bitmap = Bitmap.Load(Path.Combine(AppContext.BaseDirectory, "doqua.png"));
        var captionFont = Font.Default with { Size = 16, Style = FontStyle.Bold };

        // Draw a new bitmap in code, save it as PNG and load it back.
        var drawn = CreateDrawnBitmap(bitmap);
        var savedPath = Path.Combine(Path.GetTempPath(), "doqua-drawn.png");
        drawn.Save(savedPath);
        var reloaded = Bitmap.Load(savedPath);

        Content = new Panel
        {
            Children =
            {
                new Label { X = 20, Y = 16, Text = $"Whole image ({bitmap.Width} x {bitmap.Height})", Font = captionFont },
                new BitmapRectangle { X = 20, Y = 44, Width = 360, Height = 220, Color = Color.LightGray, Bitmap = bitmap },

                new Label { X = 420, Y = 16, Text = "Part: Source = (40, 20, 120, 80)", Font = captionFont },
                new BitmapRectangle
                {
                    X = 420, Y = 44, Width = 360, Height = 220, Color = new Color(40, 40, 40),
                    Bitmap = bitmap, Source = new Rect(40, 20, 120, 80),
                },

                new Label { X = 20, Y = 290, Text = "Stretched to 360 x 262", Font = captionFont },
                new BitmapRectangle
                {
                    X = 20, Y = 318, Width = 360, Height = 262, Color = Color.White,
                    Bitmap = bitmap, Stretch = true,
                },

                new Label { X = 420, Y = 290, Text = "Drawn in code, saved, loaded back", Font = captionFont },
                new BitmapRectangle
                {
                    X = 420, Y = 318, Width = 360, Height = 262, Color = new Color(40, 40, 40),
                    Bitmap = reloaded,
                },
                new Label
                {
                    X = 420, Y = 586, Color = Color.Gray,
                    Text = $"{savedPath} — identical after reload: {AreEqual(drawn, reloaded)}",
                },
            },
        };
    }

    /// <summary>Draws a 240 x 160 picture with a DrawingContext created on a new bitmap.</summary>
    private static Bitmap CreateDrawnBitmap(Bitmap logo)
    {
        var bitmap = new Bitmap(240, 160); // Starts fully transparent.
        var dc = bitmap.CreateDrawingContext();

        // Vertical gradient, one line at a time.
        for (var y = 0; y < bitmap.Height; y++)
            dc.FillRectangle(0, y, bitmap.Width, 1, Color.Lerp(new Color(255, 210, 90), new Color(240, 80, 40), y / (bitmap.Height - 1f)));
        dc.DrawRectangle(new Rect(0, 0, bitmap.Width, bitmap.Height), Color.Black, 2);

        // The loaded image, scaled down, and a translucent white band with text on it.
        logo.Draw(dc, new Rect(120, 12, 108, 72));
        dc.FillRectangle(2, 104, bitmap.Width - 4, 40, new Color(255, 255, 255, 140));
        dc.DrawText("Drawn in code", Font.Default with { Size = 20, Style = FontStyle.Bold }, Color.Black, 14, 110);

        // Clear inside a clip cuts a transparent hole: the dark background shows through.
        using (dc.PushClip(new Rect(16, 16, 60, 60)))
            dc.Clear(Color.Transparent);
        dc.DrawRectangle(new Rect(16, 16, 60, 60), new Color(0, 0, 0, 100));

        return bitmap;
    }

    private static bool AreEqual(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
            return false;
        for (var y = 0; y < a.Height; y++)
        {
            for (var x = 0; x < a.Width; x++)
            {
                if (a.GetPixel(x, y) != b.GetPixel(x, y))
                    return false;
            }
        }
        return true;
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
