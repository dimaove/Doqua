using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Single-line text editor. Keys: Left/Right (Ctrl: by word), Home/End, Backspace/Delete (Ctrl: by word).
/// Clicking places the caret. Text wider than the control scrolls to keep the caret visible.
/// </summary>
public class Input : Control
{
    private const int Padding = 6;

    private string _text = "";
    private int _caretIndex;
    private int _scrollX; // Pixels of text hidden on the left; updated when rendering.
    private Font? _font;
    private Color _color = Color.Black;
    private Color _background = Color.White;
    private Color _borderColor = new(122, 122, 122);
    private Color _focusedBorderColor = new(0, 120, 215);
    private Color _disabledColor = new(160, 160, 160);
    private Color _disabledBackground = new(240, 240, 240);
    private Color _disabledBorderColor = new(204, 204, 204);

    public Input()
    {
        Focusable = true;
        Width = 200;
        Height = 30;
    }

    /// <summary>Raised after <see cref="Text"/> changes, by typing or from code.</summary>
    public event EventHandler? TextChanged;

    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetText(RemoveLineBreaks(value), Math.Min(_caretIndex, value.Length));
        }
    }

    /// <summary>Caret position as an index into <see cref="Text"/> (0 = before the first character).</summary>
    public int CaretIndex
    {
        get => _caretIndex;
        set
        {
            var index = Math.Clamp(value, 0, _text.Length);
            if (IsInsideSurrogatePair(index))
                index++;
            if (_caretIndex == index)
                return;
            _caretIndex = index;
            Invalidate();
        }
    }

    public Font Font
    {
        get => _font ??= Font.Default;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _font = value;
            Invalidate();
        }
    }

    /// <summary>Text and caret color.</summary>
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            Invalidate();
        }
    }

    public Color Background
    {
        get => _background;
        set
        {
            _background = value;
            Invalidate();
        }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    /// <summary>Border color while the input has keyboard focus.</summary>
    public Color FocusedBorderColor
    {
        get => _focusedBorderColor;
        set
        {
            _focusedBorderColor = value;
            Invalidate();
        }
    }

    public Color DisabledColor
    {
        get => _disabledColor;
        set
        {
            _disabledColor = value;
            Invalidate();
        }
    }

    public Color DisabledBackground
    {
        get => _disabledBackground;
        set
        {
            _disabledBackground = value;
            Invalidate();
        }
    }

    public Color DisabledBorderColor
    {
        get => _disabledBorderColor;
        set
        {
            _disabledBorderColor = value;
            Invalidate();
        }
    }

    protected virtual void OnTextChanged(EventArgs e) => TextChanged?.Invoke(this, e);

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButton.Left)
            CaretIndex = IndexFromX(e.X - Padding + _scrollX);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled)
            return;
        e.Handled = true;
        var text = RemoveLineBreaks(e.Text);
        if (text.Length > 0)
            SetText(_text.Insert(_caretIndex, text), _caretIndex + text.Length);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        var byWord = e.Modifiers.HasFlag(KeyModifiers.Control);
        switch (e.Key)
        {
            case Key.Left:
                CaretIndex = byWord ? PreviousWord(_caretIndex) : PreviousIndex(_caretIndex);
                break;
            case Key.Right:
                CaretIndex = byWord ? NextWord(_caretIndex) : NextIndex(_caretIndex);
                break;
            case Key.Home:
                CaretIndex = 0;
                break;
            case Key.End:
                CaretIndex = _text.Length;
                break;
            case Key.Backspace:
                var start = byWord ? PreviousWord(_caretIndex) : PreviousIndex(_caretIndex);
                if (start < _caretIndex)
                    SetText(_text.Remove(start, _caretIndex - start), start);
                break;
            case Key.Delete:
                var end = byWord ? NextWord(_caretIndex) : NextIndex(_caretIndex);
                if (end > _caretIndex)
                    SetText(_text.Remove(_caretIndex, end - _caretIndex), _caretIndex);
                break;
            default:
                return; // Not ours: let parents and the window see it (e.g. Tab, Enter).
        }
        e.Handled = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var enabled = IsEffectivelyEnabled;
        var focused = Focused;
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, enabled ? _background : _disabledBackground);
        if (!enabled)
            dc.DrawRectangle(bounds, _disabledBorderColor);
        else if (focused)
            dc.DrawRectangle(bounds, _focusedBorderColor, 2);
        else
            dc.DrawRectangle(bounds, _borderColor);

        var font = Font;
        var visibleWidth = Math.Max(0, Width - 2 * Padding);
        UpdateScroll(visibleWidth);

        var textHeight = font.Ascent + font.Descent;
        var textY = (Height - textHeight) / 2;

        // Clip to the text area; one extra pixel so the caret stays visible at the right edge.
        using (dc.PushClip(new Rect(Padding, 0, visibleWidth + 1, Height)))
        {
            dc.DrawText(_text, font, enabled ? _color : _disabledColor, Padding - _scrollX, textY);
            if (focused && enabled && GetWindow()?.IsActive == true)
                dc.FillRectangle(Padding + XFromIndex(_caretIndex) - _scrollX, textY, 1, textHeight, _color);
        }
    }

    private void SetText(string text, int caretIndex)
    {
        var changed = text != _text;
        _text = text;
        _caretIndex = -1; // Force CaretIndex to apply and invalidate.
        CaretIndex = caretIndex;
        if (changed)
            OnTextChanged(EventArgs.Empty);
    }

    /// <summary>Scrolls so that the caret is visible and no empty space is left after the text.</summary>
    private void UpdateScroll(int visibleWidth)
    {
        var caretX = XFromIndex(_caretIndex);
        if (caretX - _scrollX > visibleWidth)
            _scrollX = caretX - visibleWidth;
        if (caretX < _scrollX)
            _scrollX = caretX;
        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, XFromIndex(_text.Length) - visibleWidth));
    }

    /// <summary>Pixel offset of the caret position <paramref name="index"/> from the start of the text.</summary>
    private int XFromIndex(int index)
    {
        var face = Font.Face;
        float x = 0;
        foreach (var rune in _text.AsSpan(0, index).EnumerateRunes())
            x += face.GetGlyph(rune.Value).Advance;
        return (int)MathF.Round(x);
    }

    /// <summary>Caret position closest to the pixel offset <paramref name="x"/> from the start of the text.</summary>
    private int IndexFromX(int x)
    {
        var face = Font.Face;
        float pen = 0;
        var index = 0;
        foreach (var rune in _text.EnumerateRunes())
        {
            var advance = face.GetGlyph(rune.Value).Advance;
            if (x < pen + advance / 2)
                return index;
            pen += advance;
            index += rune.Utf16SequenceLength;
        }
        return _text.Length;
    }

    private int PreviousIndex(int index) =>
        index <= 0 ? 0 : IsInsideSurrogatePair(index - 1) ? index - 2 : index - 1;

    private int NextIndex(int index) =>
        index >= _text.Length ? _text.Length : IsInsideSurrogatePair(index + 1) ? index + 2 : index + 1;

    /// <summary>Start of the word before <paramref name="index"/> (skipping spaces first).</summary>
    private int PreviousWord(int index)
    {
        while (index > 0 && char.IsWhiteSpace(_text[index - 1]))
            index--;
        while (index > 0 && !char.IsWhiteSpace(_text[index - 1]))
            index--;
        return index;
    }

    /// <summary>Start of the next word after <paramref name="index"/>.</summary>
    private int NextWord(int index)
    {
        while (index < _text.Length && !char.IsWhiteSpace(_text[index]))
            index++;
        while (index < _text.Length && char.IsWhiteSpace(_text[index]))
            index++;
        return index;
    }

    private bool IsInsideSurrogatePair(int index) =>
        index > 0 && index < _text.Length && char.IsLowSurrogate(_text[index]) && char.IsHighSurrogate(_text[index - 1]);

    private static string RemoveLineBreaks(string text) =>
        text.Contains('\n') || text.Contains('\r') ? text.Replace("\r", "").Replace("\n", " ") : text;
}
