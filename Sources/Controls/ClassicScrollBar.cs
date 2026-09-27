using Doqua.GUI;
using Timer = Doqua.GUI.Timer;

namespace Doqua.Controls;

/// <summary>Direction an arrow button points.</summary>
internal enum ArrowDirection
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// Classic scroll bar (vertical, or horizontal with <see cref="IsHorizontal"/>) drawn and driven by its owner control (not a control of its own):
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

    /// <summary>True for a horizontal bar (left / right arrows); the value then grows to the right.</summary>
    public bool IsHorizontal { get; init; }

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
        var along = IsHorizontal ? x : y;
        _pressedPart = up.Contains(x, y) ? Part.UpButton
            : down.Contains(x, y) ? Part.DownButton
            : thumb.Contains(x, y) ? Part.Thumb
            : track.Contains(x, y) ? (along < Start(thumb) ? Part.TrackAbove : Part.TrackBelow)
            : Part.None;
        if (_pressedPart == Part.Thumb)
        {
            _thumbGrabOffset = along - Start(thumb);
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
    public void Drag(int x, int y)
    {
        if (_pressedPart != Part.Thumb)
            return;
        var (_, _, track, thumb) = Layout();
        var room = Length(track) - Length(thumb);
        if (room <= 0)
            return;
        var thumbStart = Math.Clamp((IsHorizontal ? x : y) - _thumbGrabOffset, Start(track), Start(track) + room);
        _setValue((int)((long)(thumbStart - Start(track)) * Maximum / room));
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
            var (from, to) = _pressedPart == Part.TrackAbove ? (Start(track), Start(thumb)) : (End(thumb), End(track));
            dc.FillRectangle(Along(track, from, to - from), ClassicStyle.DarkShadow);
        }

        DrawArrowButton(dc, up, IsHorizontal ? ArrowDirection.Left : ArrowDirection.Up, _pressedPart == Part.UpButton, enabled);
        DrawArrowButton(dc, down, IsHorizontal ? ArrowDirection.Right : ArrowDirection.Down, _pressedPart == Part.DownButton, enabled);
        dc.FillRectangle(thumb, ClassicStyle.Face);
        ClassicStyle.DrawRaisedEdge(dc, thumb, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);
    }

    /// <summary>
    /// Raised button with a 7 x 4 arrow (<paramref name="arrows"/> arrows side by side, e.g. 2 for "«");
    /// flat shadow frame and arrows moved 1 px while pressed.
    /// </summary>
    public static void DrawArrowButton(DrawingContext dc, Rect r, ArrowDirection direction, bool pressed, bool enabled, int arrows = 1)
    {
        dc.FillRectangle(r, ClassicStyle.Face);
        if (pressed)
            dc.DrawRectangle(r, ClassicStyle.Shadow);
        else
            ClassicStyle.DrawRaisedEdge(dc, r, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        var shift = pressed ? 1 : 0;
        var color = enabled ? Color.Black : ClassicStyle.Shadow;
        var vertical = direction is ArrowDirection.Up or ArrowDirection.Down;
        for (var arrow = 0; arrow < arrows; arrow++)
        {
            // Arrows are 4 px deep with 1 px between them, centred together along the pointing direction.
            var along = (arrow - (arrows - 1) / 2.0) * 5;
            var centerX = r.X + r.Width / 2 + shift + (vertical ? 0 : (int)along);
            var centerY = r.Y + r.Height / 2 + shift + (vertical ? (int)along : 0);
            for (var i = 0; i < 4; i++)
            {
                // Row (or column) i of the triangle, from its tip: 1, 3, 5, 7 pixels.
                var half = direction is ArrowDirection.Up or ArrowDirection.Left ? i : 3 - i;
                if (vertical)
                    dc.FillRectangle(centerX - half, centerY - 2 + i, 2 * half + 1, 1, color);
                else
                    dc.FillRectangle(centerX - 2 + i, centerY - half, 1, 2 * half + 1, color);
            }
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

    /// <summary>"Up" and "down" are the start and end buttons: left and right for a horizontal bar.</summary>
    private (Rect Up, Rect Down, Rect Track, Rect Thumb) Layout()
    {
        var bar = Bounds;
        var length = Length(bar);
        var up = Along(bar, Start(bar), Thickness);
        var down = Along(bar, End(bar) - Thickness, Thickness);
        var track = Along(bar, Start(bar) + Thickness, Math.Max(0, length - 2 * Thickness));
        var trackLength = Length(track);
        var thumbSize = Math.Clamp((int)((long)trackLength * ViewSize / Math.Max(1, ContentSize)), MinThumbSize, Math.Max(MinThumbSize, trackLength));
        var value = Math.Clamp(_getValue(), 0, Math.Max(0, Maximum));
        var thumbStart = Start(track) + (Maximum <= 0 ? 0 : (int)((long)(trackLength - thumbSize) * value / Maximum));
        return (up, down, track, Along(track, thumbStart, Math.Min(thumbSize, trackLength)));
    }

    private int Start(Rect r) => IsHorizontal ? r.X : r.Y;

    private int End(Rect r) => IsHorizontal ? r.Right : r.Bottom;

    private int Length(Rect r) => IsHorizontal ? r.Width : r.Height;

    /// <summary>Part of <paramref name="bar"/> from <paramref name="start"/> along the bar's axis, across its full thickness.</summary>
    private Rect Along(Rect bar, int start, int length) =>
        IsHorizontal ? new Rect(start, bar.Y, length, bar.Height) : new Rect(bar.X, start, bar.Width, length);
}
