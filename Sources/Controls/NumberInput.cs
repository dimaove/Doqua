using System.Globalization;
using Doqua.GUI;
using Timer = Doqua.GUI.Timer;

namespace Doqua.Controls;

/// <summary>
/// Integer input with classic up / down buttons (a "spin box"). <see cref="Value"/> always lies in
/// [<see cref="Min"/>, <see cref="Max"/>]. Typing accepts only digits (and a leading '-' when Min is
/// negative) and refuses numbers that can no longer fit the range; a number below Min is allowed while
/// typing and is corrected when the field loses focus or on Enter.
/// The buttons (repeating while held), Up / Down (by <see cref="Step"/>), PageUp / PageDown (10 steps)
/// and the mouse wheel change the value.
/// </summary>
public class NumberInput : Control
{
    private const int FrameSize = 2;
    private const int ButtonWidth = 16;
    private const int RepeatDelayMilliseconds = 400;
    private const int RepeatIntervalMilliseconds = 50;

    private readonly Input _input;
    private int _value;
    private int _min;
    private int _max = 100;
    private int _step = 1;
    private int _pressedDirection;  // +1 up button held, -1 down button held, 0 none.
    private int _repeatTimer;
    private bool _updatingText;

    public NumberInput()
    {
        _input = new Input { ShowFrame = false, TextFilter = IsAcceptable };
        _input.TextChanged += (sender, e) => OnTextEdited();
        _input.LostFocus += (sender, e) => Commit();
        Children = [_input];
        _input.Parent = this;
        Width = 80;
        Height = Math.Max(22, _input.Font.LineHeight + 8);
        UpdateText();
    }

    /// <summary>Raised after <see cref="Value"/> changes: from the buttons, keys, wheel, typing or code.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>The number; setting it clamps it into [<see cref="Min"/>, <see cref="Max"/>].</summary>
    public int Value
    {
        get => _value;
        set
        {
            SetValue(value);
            UpdateText();
        }
    }

    /// <summary>Smallest allowed value (default 0). Raising it above <see cref="Max"/> raises Max too.</summary>
    public int Min
    {
        get => _min;
        set
        {
            _min = value;
            if (_max < value)
                _max = value;
            Value = _value; // Clamp and refresh.
        }
    }

    /// <summary>Largest allowed value (default 100). Lowering it below <see cref="Min"/> lowers Min too.</summary>
    public int Max
    {
        get => _max;
        set
        {
            _max = value;
            if (_min > value)
                _min = value;
            Value = _value;
        }
    }

    /// <summary>How much the buttons, Up / Down and the wheel change the value (default 1).</summary>
    public int Step
    {
        get => _step;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _step = value;
        }
    }

    /// <summary>Font of the number.</summary>
    public Font Font
    {
        get => _input.Font;
        set => _input.Font = value;
    }

    public Color Color
    {
        get => _input.Color;
        set => _input.Color = value;
    }

    public Color Background
    {
        get => _input.Background;
        set => _input.Background = value;
    }

    private IReadOnlyList<Control> Children { get; }

    protected override IReadOnlyList<Control> VisualChildren => Children;

    protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);

    protected override void OnSizeChanged(EventArgs e)
    {
        // The text field fills the frame, left of the buttons.
        _input.Bounds = Rect.FromEdges(FrameSize, FrameSize, Math.Max(FrameSize, Width - FrameSize - ButtonWidth), Math.Max(FrameSize, Height - FrameSize));
        base.OnSizeChanged(e);
    }

    /// <summary>Up / Down and PageUp / PageDown arrive here from the focused text field (they bubble up).</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Modifiers != KeyModifiers.None)
            return;
        switch (e.Key)
        {
            case Key.Up:
                StepBy(1);
                break;
            case Key.Down:
                StepBy(-1);
                break;
            case Key.PageUp:
                StepBy(10);
                break;
            case Key.PageDown:
                StepBy(-10);
                break;
            case Key.Enter:
                Commit();
                return; // Not handled: Enter may still mean something to the window.
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.Handled)
            return;
        e.Handled = true;
        StepBy(e.Delta);
    }

    /// <summary>Only the buttons get here: the text field covers the rest.</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButton.Left || !IsOverButtons(e.X))
            return;
        _input.Focus();
        _pressedDirection = e.Y < Height / 2 ? 1 : -1;
        StepBy(_pressedDirection);
        Timer.ClearTimeout(_repeatTimer);
        _repeatTimer = Timer.SetTimeout(() =>
        {
            if (_pressedDirection != 0)
                _repeatTimer = Timer.SetInterval(() => StepBy(_pressedDirection), RepeatIntervalMilliseconds);
        }, RepeatDelayMilliseconds);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButton.Left || _pressedDirection == 0)
            return;
        _pressedDirection = 0;
        Timer.ClearTimeout(_repeatTimer);
        _repeatTimer = 0;
        Invalidate();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var enabled = IsEffectivelyEnabled;
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, enabled ? _input.Background : _input.DisabledBackground);
        ClassicStyle.DrawSunkenEdge(dc, bounds, ClassicStyle.Highlight, ClassicStyle.Face, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        var left = Width - FrameSize - ButtonWidth;
        var top = FrameSize;
        var half = (Height - 2 * FrameSize) / 2;
        var up = new Rect(left, top, ButtonWidth, half);
        var down = Rect.FromEdges(left, top + half, left + ButtonWidth, Height - FrameSize);
        DrawSpinButton(dc, up, pointsUp: true, pressed: _pressedDirection > 0, enabled && _value < _max);
        DrawSpinButton(dc, down, pointsUp: false, pressed: _pressedDirection < 0, enabled && _value > _min);
    }

    /// <summary>Half-height classic button with a small 5 x 3 arrow; greyed when the value is at that end of the range.</summary>
    private static void DrawSpinButton(DrawingContext dc, Rect r, bool pointsUp, bool pressed, bool enabled)
    {
        dc.FillRectangle(r, ClassicStyle.Face);
        if (pressed)
            dc.DrawRectangle(r, ClassicStyle.Shadow);
        else
            ClassicStyle.DrawRaisedEdge(dc, r, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        var shift = pressed ? 1 : 0;
        var centerX = r.X + r.Width / 2 - 1 + shift;
        var top = r.Y + (r.Height - 3) / 2 + shift;
        var color = enabled ? Color.Black : ClassicStyle.Shadow;
        for (var row = 0; row < 3; row++)
        {
            var width = pointsUp ? row : 2 - row;
            dc.FillRectangle(centerX - width, top + row, 2 * width + 1, 1, color);
        }
    }

    private bool IsOverButtons(int x) => x >= Width - FrameSize - ButtonWidth;

    private void StepBy(int steps)
    {
        Commit(); // Start from what is typed, corrected into the range.
        Value = (int)Math.Clamp((long)_value + (long)steps * _step, _min, _max);
    }

    private void SetValue(int value)
    {
        value = Math.Clamp(value, _min, _max);
        if (value == _value)
            return;
        _value = value;
        Invalidate(); // The buttons grey out at the ends of the range.
        OnValueChanged(EventArgs.Empty);
    }

    /// <summary>Shows <see cref="Value"/> as text (keeping the caret at the end).</summary>
    private void UpdateText()
    {
        var text = _value.ToString(CultureInfo.InvariantCulture);
        if (_input.Text == text)
            return;
        _updatingText = true;
        try
        {
            _input.Text = text;
            _input.CaretIndex = text.Length;
        }
        finally
        {
            _updatingText = false;
        }
    }

    /// <summary>While typing, a complete in-range number becomes the value right away.</summary>
    private void OnTextEdited()
    {
        if (_updatingText)
            return;
        if (TryParse(_input.Text, out var number) && number >= _min && number <= _max)
            SetValue((int)number);
    }

    /// <summary>Corrects the typed text into the range (empty or "-" becomes Min) and shows the value.</summary>
    private void Commit()
    {
        var number = TryParse(_input.Text, out var parsed) ? parsed : _min;
        SetValue((int)Math.Clamp(number, _min, _max));
        UpdateText();
    }

    /// <summary>
    /// Whether an edit may produce <paramref name="text"/>: empty, a lone '-' (if negatives are allowed), or an
    /// integer that could still become valid. More digits only move a number away from zero, so a positive
    /// number above Max or a negative one below Min can never be fixed by typing and is refused.
    /// </summary>
    private bool IsAcceptable(string text)
    {
        if (text.Length == 0)
            return true;
        if (text == "-")
            return _min < 0;
        if (!TryParse(text, out var number))
            return false;
        if (text[0] == '-' && _min >= 0)
            return false;
        return number >= 0 ? number <= Math.Max(_max, 0) : number >= Math.Min(_min, 0);
    }

    private static bool TryParse(string text, out long number)
    {
        number = 0;
        var digits = text.StartsWith('-') ? text.AsSpan(1) : text.AsSpan();
        if (digits.Length == 0 || digits.Length > 12)
            return false;
        foreach (var ch in digits)
        {
            if (ch is < '0' or > '9')
                return false;
        }
        number = long.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        return true;
    }
}
