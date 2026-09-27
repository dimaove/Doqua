using Doqua.GUI;
using Timer = Doqua.GUI.Timer;

namespace Doqua.Controls;

/// <summary>
/// Classic vertical scroll bar drawn and driven by its owner control (not a control of its own):
/// arrow buttons scroll by <see cref="SmallChange"/>, the track by <see cref="LargeChange"/> (both repeat
/// while the button is held), and the thumb can be dragged. The owner forwards its mouse events and
/// keeps <see cref="Bounds"/> and the range properties up to date; values are in the owner's own units.
/// </summary>
internal sealed class ClassicScrollBar
{
    public const int Thickness = 16;
    private const int MinThumbSize = 8;
    private const int RepeatDelayMilliseconds = 400;
    private const int RepeatIntervalMilliseconds = 50;

    private readonly Control _owner;
    private readonly Func<int> _getValue;
    private readonly Action<int> _setValue; // The owner clamps the value and redraws.
    private Part _pressedPart;
    private int _thumbGrabOffset;
    private int _repeatTimer;

    private enum Part
    {
        None,
        UpButton,
        DownButton,
        TrackAbove,
        TrackBelow,
        Thumb,
    }

    public ClassicScrollBar(Control owner, Func<int> getValue, Action<int> setValue)
    {
        _owner = owner;
        _getValue = getValue;
        _setValue = setValue;
    }

    /// <summary>Where the bar is, in the owner's coordinates.</summary>
    public Rect Bounds { get; set; }

    /// <summary>Largest value (content size minus view size).</summary>
    public int Maximum { get; set; }

    /// <summary>Visible part of the content; with <see cref="ContentSize"/> it sets the thumb size.</summary>
    public int ViewSize { get; set; }

    public int ContentSize { get; set; }

    public int SmallChange { get; set; } = 1;

    public int LargeChange { get; set; } = 10;

    /// <summary>True while a mouse button pressed on the bar is held.</summary>
    public bool IsPressed => _pressedPart != Part.None;

    public void Press(int x, int y)
    {
        var (up, down, track, thumb) = Layout();
        _pressedPart = up.Contains(x, y) ? Part.UpButton
            : down.Contains(x, y) ? Part.DownButton
            : thumb.Contains(x, y) ? Part.Thumb
            : track.Contains(x, y) ? (y < thumb.Y ? Part.TrackAbove : Part.TrackBelow)
            : Part.None;
        if (_pressedPart == Part.Thumb)
        {
            _thumbGrabOffset = y - thumb.Y;
            _owner.Invalidate();
            return;
        }
        if (_pressedPart == Part.None)
            return;

        Step();
        StopRepeat();
        _repeatTimer = Timer.SetTimeout(() =>
        {
            if (_pressedPart is Part.None or Part.Thumb)
                return;
            _repeatTimer = Timer.SetInterval(Step, RepeatIntervalMilliseconds);
        }, RepeatDelayMilliseconds);
    }

    /// <summary>Mouse moved while pressed: drags the thumb.</summary>
    public void Drag(int y)
    {
        if (_pressedPart != Part.Thumb)
            return;
        var (_, _, track, thumb) = Layout();
        var room = track.Height - thumb.Height;
        if (room <= 0)
            return;
        var thumbTop = Math.Clamp(y - _thumbGrabOffset, track.Y, track.Y + room);
        _setValue((int)((long)(thumbTop - track.Y) * Maximum / room));
    }

    public void Release()
    {
        if (_pressedPart == Part.None)
            return;
        _pressedPart = Part.None;
        StopRepeat();
        _owner.Invalidate();
    }

    public void Draw(DrawingContext dc, bool enabled)
    {
        var (up, down, track, thumb) = Layout();

        // Classic dotted track: face color with every other pixel in the highlight color.
        dc.FillRectangle(track, ClassicStyle.Face);
        for (var y = track.Y; y < track.Bottom; y++)
        {
            for (var x = track.X + (y & 1); x < track.Right; x += 2)
                dc.FillRectangle(x, y, 1, 1, ClassicStyle.Highlight);
        }
        if (_pressedPart is Part.TrackAbove or Part.TrackBelow && enabled)
        {
            var pressed = _pressedPart == Part.TrackAbove ? Rect.FromEdges(track.X, track.Y, track.Right, thumb.Y)
                : Rect.FromEdges(track.X, thumb.Bottom, track.Right, track.Bottom);
            dc.FillRectangle(pressed, ClassicStyle.DarkShadow);
        }

        DrawArrowButton(dc, up, pointsUp: true, _pressedPart == Part.UpButton, enabled);
        DrawArrowButton(dc, down, pointsUp: false, _pressedPart == Part.DownButton, enabled);
        dc.FillRectangle(thumb, ClassicStyle.Face);
        ClassicStyle.DrawRaisedEdge(dc, thumb, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);
    }

    /// <summary>Raised button with a 7 x 4 arrow; flat shadow frame and arrow moved 1 px while pressed.</summary>
    public static void DrawArrowButton(DrawingContext dc, Rect r, bool pointsUp, bool pressed, bool enabled)
    {
        dc.FillRectangle(r, ClassicStyle.Face);
        if (pressed)
            dc.DrawRectangle(r, ClassicStyle.Shadow);
        else
            ClassicStyle.DrawRaisedEdge(dc, r, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        var shift = pressed ? 1 : 0;
        var centerX = r.X + r.Width / 2 + shift;
        var top = r.Y + (r.Height - 4) / 2 + shift;
        var color = enabled ? Color.Black : ClassicStyle.Shadow;
        for (var row = 0; row < 4; row++)
        {
            var half = pointsUp ? row : 3 - row;
            dc.FillRectangle(centerX - half, top + row, 2 * half + 1, 1, color);
        }
    }

    private void Step()
    {
        var value = _getValue();
        switch (_pressedPart)
        {
            case Part.UpButton: _setValue(value - SmallChange); break;
            case Part.DownButton: _setValue(value + SmallChange); break;
            case Part.TrackAbove: _setValue(value - LargeChange); break;
            case Part.TrackBelow: _setValue(value + LargeChange); break;
        }
        _owner.Invalidate();
    }

    private void StopRepeat()
    {
        Timer.ClearTimeout(_repeatTimer);
        _repeatTimer = 0;
    }

    private (Rect Up, Rect Down, Rect Track, Rect Thumb) Layout()
    {
        var bar = Bounds;
        var up = bar with { Height = Thickness };
        var down = bar with { Y = bar.Bottom - Thickness, Height = Thickness };
        var track = Rect.FromEdges(bar.X, up.Bottom, bar.Right, down.Y);
        var thumbSize = Math.Clamp((int)((long)track.Height * ViewSize / Math.Max(1, ContentSize)), MinThumbSize, Math.Max(MinThumbSize, track.Height));
        var value = Math.Clamp(_getValue(), 0, Math.Max(0, Maximum));
        var thumbTop = track.Y + (Maximum <= 0 ? 0 : (int)((long)(track.Height - thumbSize) * value / Maximum));
        return (up, down, track, new Rect(track.X, thumbTop, track.Width, Math.Min(thumbSize, Math.Max(0, track.Height))));
    }
}
