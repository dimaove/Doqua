using Doqua.GUI;
using Timer = Doqua.GUI.Timer;

namespace Doqua.Controls;

/// <summary>
/// Single-line text editor.
/// <list type="bullet">
/// <item>Left/Right (Ctrl: by word), Home/End move the caret; with Shift they extend the selection.</item>
/// <item>Click places the caret, Shift+click extends the selection, dragging selects.</item>
/// <item>Double click selects a word (dragging then extends by words), triple click selects all.</item>
/// <item>Backspace/Delete (Ctrl: by word) delete the selection or a character.</item>
/// <item>Ctrl+A select all; Ctrl+C / Ctrl+Insert copy; Ctrl+X / Shift+Delete cut; Ctrl+V / Shift+Insert paste.</item>
/// </list>
/// Text wider than the control scrolls to keep the caret visible.
/// </summary>
public class Input : Control
{
    private const int Padding = 6;
    private const int CaretBlinkMilliseconds = 530;

    private string _text = "";
    private int _caretIndex;
    private int _anchorIndex; // Other end of the selection; equals _caretIndex when nothing is selected.
    private bool _isMouseSelecting;
    private bool _isWordSelecting; // Dragging after a double click extends by whole words.
    private (int Start, int End) _initialWord; // Word selected by the double click.
    private int _blinkTimer; // Timer id while focused, otherwise 0.
    private bool _caretVisible = true;
    private int _scrollX; // Pixels of text hidden on the left; updated when rendering.
    private Font? _font;
    private Color _color = Color.Black;
    private Color _background = Color.White;
    private Color _borderColor = new(122, 122, 122);
    private Color _focusedBorderColor = new(0, 120, 215);
    private Color _disabledColor = new(160, 160, 160);
    private Color _disabledBackground = new(240, 240, 240);
    private Color _disabledBorderColor = new(204, 204, 204);
    private Color _selectionBackground = new(0, 120, 215);
    private Color _selectionColor = Color.White;
    private Color _inactiveSelectionBackground = new(204, 204, 204);

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

    /// <summary>
    /// Caret position as an index into <see cref="Text"/> (0 = before the first character).
    /// Setting it clears the selection.
    /// </summary>
    public int CaretIndex
    {
        get => _caretIndex;
        set => MoveCaret(value, extendSelection: false);
    }

    /// <summary>Index of the first selected character.</summary>
    public int SelectionStart => Math.Min(_anchorIndex, _caretIndex);

    /// <summary>Number of selected UTF-16 characters; 0 if nothing is selected.</summary>
    public int SelectionLength => Math.Abs(_caretIndex - _anchorIndex);

    public string SelectedText => _text.Substring(SelectionStart, SelectionLength);

    private int SelectionEnd => Math.Max(_anchorIndex, _caretIndex);

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

    /// <summary>Background of selected text while the input is focused.</summary>
    public Color SelectionBackground
    {
        get => _selectionBackground;
        set
        {
            _selectionBackground = value;
            Invalidate();
        }
    }

    /// <summary>Color of selected text while the input is focused.</summary>
    public Color SelectionColor
    {
        get => _selectionColor;
        set
        {
            _selectionColor = value;
            Invalidate();
        }
    }

    /// <summary>Background of selected text while the input (or its window) is not focused.</summary>
    public Color InactiveSelectionBackground
    {
        get => _inactiveSelectionBackground;
        set
        {
            _inactiveSelectionBackground = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Selects from <paramref name="start"/> to <paramref name="start"/> + <paramref name="length"/>, where the
    /// caret goes. A negative length selects backwards, leaving the caret at the left end.
    /// </summary>
    public void Select(int start, int length)
    {
        MoveCaret(start, extendSelection: false);
        MoveCaret(start + length, extendSelection: true);
    }

    public void SelectAll() => Select(0, _text.Length);

    /// <summary>Copies the selection to the clipboard. Does nothing if nothing is selected.</summary>
    public void Copy()
    {
        if (SelectionLength > 0)
            Clipboard.SetText(SelectedText);
    }

    /// <summary>Copies the selection to the clipboard and deletes it.</summary>
    public void Cut()
    {
        if (SelectionLength > 0 && Clipboard.SetText(SelectedText))
            ReplaceSelection("");
    }

    /// <summary>Replaces the selection with the clipboard text (line breaks become spaces).</summary>
    public void Paste()
    {
        var text = Clipboard.GetText();
        if (!string.IsNullOrEmpty(text))
            ReplaceSelection(RemoveLineBreaks(text));
    }

    /// <summary>
    /// Context menu with Cut, Copy, Paste and Select all (enabled as they apply), followed by the items
    /// of <see cref="Control.ContextMenu"/> if one is set. Choosing one of those items raises its Click and
    /// the user menu's Closed event as if that menu had been opened; its font and colors are used too.
    /// </summary>
    protected override PopupMenu? GetContextMenu()
    {
        var user = ContextMenu;
        var menu = new PopupMenu();
        if (user != null)
        {
            (menu.Font, menu.Color, menu.DisabledColor, menu.Background, menu.SelectionBackground, menu.SelectionColor) =
                (user.Font, user.Color, user.DisabledColor, user.Background, user.SelectionBackground, user.SelectionColor);
        }

        var hasSelection = SelectionLength > 0;
        menu.Items.Add(Command("Cut", "Ctrl+X", hasSelection, Cut));
        menu.Items.Add(Command("Copy", "Ctrl+C", hasSelection, Copy));
        menu.Items.Add(Command("Paste", "Ctrl+V", Clipboard.GetText() is { Length: > 0 }, Paste));
        menu.Items.Add(MenuItem.Separator());
        menu.Items.Add(Command("Select all", "Ctrl+A", SelectionLength < _text.Length, SelectAll));

        if (user is { Items.Count: > 0 })
        {
            menu.Items.Add(MenuItem.Separator());
            menu.Items.AddRange(user.Items);
            menu.Closed += (sender, e) => user.RaiseClosed(this, e.SelectedItem);
        }
        return menu;
    }

    private static MenuItem Command(string text, string shortcut, bool enabled, Action action)
    {
        var item = new MenuItem(text, shortcut) { Enabled = enabled };
        item.Click += (sender, e) => action();
        return item;
    }

    protected virtual void OnTextChanged(EventArgs e) => TextChanged?.Invoke(this, e);

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        RestartCaretBlink();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Timer.ClearInterval(_blinkTimer);
        _blinkTimer = 0;
        _caretVisible = true;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButton.Left)
            return;

        var x = e.X - Padding + _scrollX;
        _isWordSelecting = false;
        switch (e.ClickCount)
        {
            case 1:
                MoveCaret(IndexFromX(x), extendSelection: e.Modifiers.HasFlag(KeyModifiers.Shift));
                _isMouseSelecting = true;
                break;
            case 2:
                _initialWord = WordAt(CharIndexFromX(x));
                Select(_initialWord.Start, _initialWord.End - _initialWord.Start);
                _isMouseSelecting = _isWordSelecting = true;
                break;
            default: // Triple click and more.
                SelectAll();
                _isMouseSelecting = false;
                break;
        }
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        // Moves keep coming while the button is held, also outside the control; the text
        // scrolls as the caret follows the pointer past the edges.
        if (!_isMouseSelecting)
            return;
        var x = e.X - Padding + _scrollX;
        if (!_isWordSelecting)
        {
            MoveCaret(IndexFromX(x), extendSelection: true);
            return;
        }

        // Keep the double-clicked word selected and grow the selection to whole words toward the pointer.
        var word = WordAt(CharIndexFromX(x));
        if (word.Start < _initialWord.Start)
            Select(_initialWord.End, word.Start - _initialWord.End);
        else
            Select(_initialWord.Start, Math.Max(word.End, _initialWord.End) - _initialWord.Start);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButton.Left)
            _isMouseSelecting = false;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled)
            return;
        e.Handled = true;
        var text = RemoveLineBreaks(e.Text);
        if (text.Length > 0)
            ReplaceSelection(text);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        var control = e.Modifiers.HasFlag(KeyModifiers.Control);
        var shift = e.Modifiers.HasFlag(KeyModifiers.Shift);
        var hasSelection = SelectionLength > 0;
        switch (e.Key)
        {
            // Without Shift, Left/Right first collapse an existing selection to its edge.
            case Key.Left when hasSelection && !shift && !control:
                MoveCaret(SelectionStart, extendSelection: false);
                break;
            case Key.Left:
                MoveCaret(control ? PreviousWord(_caretIndex) : PreviousIndex(_caretIndex), shift);
                break;
            case Key.Right when hasSelection && !shift && !control:
                MoveCaret(SelectionEnd, extendSelection: false);
                break;
            case Key.Right:
                MoveCaret(control ? NextWord(_caretIndex) : NextIndex(_caretIndex), shift);
                break;
            case Key.Home:
                MoveCaret(0, shift);
                break;
            case Key.End:
                MoveCaret(_text.Length, shift);
                break;

            case Key.Backspace when hasSelection:
            case Key.Delete when hasSelection && !shift:
                ReplaceSelection("");
                break;
            case Key.Backspace:
                MoveCaret(control ? PreviousWord(_caretIndex) : PreviousIndex(_caretIndex), extendSelection: true);
                ReplaceSelection("");
                break;
            case Key.Delete when shift && !control:
                Cut();
                break;
            case Key.Delete:
                MoveCaret(control ? NextWord(_caretIndex) : NextIndex(_caretIndex), extendSelection: true);
                ReplaceSelection("");
                break;

            case Key.A when control && !shift:
                SelectAll();
                break;
            case Key.C when control && !shift:
            case Key.Insert when control && !shift:
                Copy();
                break;
            case Key.X when control && !shift:
                Cut();
                break;
            case Key.V when control && !shift:
            case Key.Insert when shift && !control:
                Paste();
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

        var active = focused && enabled && GetWindow()?.IsActive == true;
        var textX = Padding - _scrollX;
        var textColor = enabled ? _color : _disabledColor;

        // Clip to the text area; one extra pixel so the caret stays visible at the right edge.
        using (dc.PushClip(new Rect(Padding, 0, visibleWidth + 1, Height)))
        {
            var selection = Rect.FromEdges(textX + XFromIndex(SelectionStart), textY, textX + XFromIndex(SelectionEnd), textY + textHeight);
            if (!selection.IsEmpty)
                dc.FillRectangle(selection, active ? _selectionBackground : _inactiveSelectionBackground);

            dc.DrawText(_text, font, textColor, textX, textY);

            // Selected text is drawn again over itself in the selection color, clipped to the selection.
            if (active && !selection.IsEmpty)
            {
                using (dc.PushClip(selection with { Y = 0, Height = Height }))
                    dc.DrawText(_text, font, _selectionColor, textX, textY);
            }

            if (active && _caretVisible)
                dc.FillRectangle(textX + XFromIndex(_caretIndex), textY, 1, textHeight, _color);
        }
    }

    private void SetText(string text, int caretIndex)
    {
        var changed = text != _text;
        _text = text;
        _anchorIndex = _caretIndex = -1; // Force MoveCaret to apply and invalidate.
        MoveCaret(caretIndex, extendSelection: false);
        if (changed)
            OnTextChanged(EventArgs.Empty);
    }

    /// <summary>Replaces the selection (or inserts at the caret) and puts the caret after the new text.</summary>
    private void ReplaceSelection(string text)
    {
        var start = SelectionStart;
        SetText(_text.Remove(start, SelectionLength).Insert(start, text), start + text.Length);
    }

    /// <summary>Moves the caret; without <paramref name="extendSelection"/> the selection is cleared.</summary>
    private void MoveCaret(int index, bool extendSelection)
    {
        index = Math.Clamp(index, 0, _text.Length);
        if (IsInsideSurrogatePair(index))
            index++;
        var anchor = extendSelection ? _anchorIndex : index;
        if (_caretIndex == index && _anchorIndex == anchor)
            return;
        _caretIndex = index;
        _anchorIndex = anchor;
        Invalidate();
        if (_blinkTimer != 0)
            RestartCaretBlink(); // Keep the caret solid while it is being moved or typed at.
    }

    /// <summary>Shows the caret and starts toggling it; runs while the input is focused.</summary>
    private void RestartCaretBlink()
    {
        Timer.ClearInterval(_blinkTimer);
        _caretVisible = true;
        _blinkTimer = Timer.SetInterval(() =>
        {
            _caretVisible = !_caretVisible;
            Invalidate();
        }, CaretBlinkMilliseconds);
        Invalidate();
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

    /// <summary>Index of the character under the pixel offset <paramref name="x"/> (the last one if past the end).</summary>
    private int CharIndexFromX(int x)
    {
        var face = Font.Face;
        float pen = 0;
        var index = 0;
        foreach (var rune in _text.EnumerateRunes())
        {
            pen += face.GetGlyph(rune.Value).Advance;
            if (x < pen)
                return index;
            index += rune.Utf16SequenceLength;
        }
        return Math.Max(0, PreviousIndex(_text.Length));
    }

    /// <summary>
    /// Run of characters of the same kind around <paramref name="index"/>: a word (letters, digits,
    /// '_'), a run of spaces, or a run of punctuation.
    /// </summary>
    private (int Start, int End) WordAt(int index)
    {
        if (_text.Length == 0)
            return (0, 0);
        index = Math.Clamp(index, 0, _text.Length - 1);
        var kind = KindOf(_text[index]);
        var start = index;
        while (start > 0 && KindOf(_text[start - 1]) == kind)
            start--;
        var end = index + 1;
        while (end < _text.Length && KindOf(_text[end]) == kind)
            end++;
        return (start, end);
    }

    // Surrogates count as word characters so that pairs are never split.
    private static int KindOf(char ch) =>
        char.IsLetterOrDigit(ch) || ch == '_' || char.IsSurrogate(ch) ? 0 : char.IsWhiteSpace(ch) ? 1 : 2;

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
