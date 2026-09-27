using Doqua.GUI;
using Timer = Doqua.GUI.Timer;

namespace Doqua.Controls;

/// <summary>
/// Multi-line text editor in the classic style (sunken edge, vertical scroll bar when needed).
/// <list type="bullet">
/// <item>Enter starts a new line; Tab still moves the focus to the next control.</item>
/// <item>Left/Right (Ctrl: by word), Up/Down, Home/End (Ctrl: whole text), PageUp/PageDown; with Shift they select.</item>
/// <item>Click places the caret, Shift+click and dragging select, double click selects a word, triple click a line.</item>
/// <item>Backspace/Delete (Ctrl: by word); Ctrl+A/C/X/V, Ctrl+Insert, Shift+Insert, Shift+Delete; context menu.</item>
/// <item>Mouse wheel and the scroll bar scroll; the view follows the caret (also horizontally).</item>
/// </list>
/// Lines are separated by '\n'; "\r\n" and "\r" in assigned or pasted text become '\n'. No word wrap.
/// </summary>
public class TextArea : Control
{
    private const int EdgeSize = 2;          // Sunken 3D edge.
    private const int Padding = 3;           // Between the edge and the text.
    private const int ScrollBarWidth = 16;
    private const int MinThumbSize = 8;
    private const int WheelLines = 3;
    private const int CaretBlinkMilliseconds = 530;
    private const int RepeatDelayMilliseconds = 400;
    private const int RepeatIntervalMilliseconds = 50;

    private string _text = "";
    private int[] _lineStarts = [0];
    private int _caretIndex;
    private int _anchorIndex;
    private int _desiredX = -1;             // Caret x kept while moving up and down; -1 = take it from the caret.
    private int _scrollX;
    private int _scrollY;

    private SelectionUnit _selectionUnit;    // How the current mouse drag extends the selection.
    private bool _isMouseSelecting;
    private (int Start, int End) _initialUnit;

    private ScrollPart _pressedPart;         // Scroll bar part held with the mouse.
    private int _thumbGrabOffset;
    private int _repeatTimer;

    private int _blinkTimer;
    private bool _caretVisible = true;

    private Font? _font;
    private Color _color = Color.Black;
    private Color _background = Color.White;
    private Color _disabledColor = ClassicStyle.Shadow;
    private Color _disabledBackground = ClassicStyle.Face;
    private Color _selectionBackground = new(0, 120, 215);
    private Color _selectionColor = Color.White;
    private Color _inactiveSelectionBackground = new(204, 204, 204);

    private enum SelectionUnit
    {
        Character,
        Word,
        Line,
    }

    private enum ScrollPart
    {
        None,
        UpButton,
        DownButton,
        TrackAbove,
        TrackBelow,
        Thumb,
    }

    public TextArea()
    {
        Focusable = true;
        Width = 300;
        Height = 150;
    }

    /// <summary>Raised after <see cref="Text"/> changes, by typing or from code.</summary>
    public event EventHandler? TextChanged;

    /// <summary>Raised after the caret moves or the selection changes.</summary>
    public event EventHandler? SelectionChanged;

    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetText(NormalizeLineBreaks(value), Math.Min(_caretIndex, value.Length));
        }
    }

    public int LineCount => _lineStarts.Length;

    /// <summary>Caret position as an index into <see cref="Text"/>. Setting it clears the selection.</summary>
    public int CaretIndex
    {
        get => _caretIndex;
        set => MoveCaret(value, extendSelection: false);
    }

    /// <summary>Zero-based line of the caret.</summary>
    public int CaretLine => LineOf(_caretIndex);

    /// <summary>Zero-based column (UTF-16 characters from the start of the line) of the caret.</summary>
    public int CaretColumn => _caretIndex - _lineStarts[CaretLine];

    public int SelectionStart => Math.Min(_anchorIndex, _caretIndex);

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
            EnsureCaretVisible();
            Invalidate();
        }
    }

    public Color Color
    {
        get => _color;
        set => SetColor(ref _color, value);
    }

    public Color Background
    {
        get => _background;
        set => SetColor(ref _background, value);
    }

    public Color DisabledColor
    {
        get => _disabledColor;
        set => SetColor(ref _disabledColor, value);
    }

    public Color DisabledBackground
    {
        get => _disabledBackground;
        set => SetColor(ref _disabledBackground, value);
    }

    public Color SelectionBackground
    {
        get => _selectionBackground;
        set => SetColor(ref _selectionBackground, value);
    }

    public Color SelectionColor
    {
        get => _selectionColor;
        set => SetColor(ref _selectionColor, value);
    }

    public Color InactiveSelectionBackground
    {
        get => _inactiveSelectionBackground;
        set => SetColor(ref _inactiveSelectionBackground, value);
    }

    /// <summary>
    /// Selects from <paramref name="start"/> to <paramref name="start"/> + <paramref name="length"/>, where the
    /// caret goes; a negative length selects backwards.
    /// </summary>
    public void Select(int start, int length)
    {
        MoveCaret(start, extendSelection: false);
        MoveCaret(start + length, extendSelection: true);
    }

    public void SelectAll() => Select(0, _text.Length);

    public void Copy()
    {
        if (SelectionLength > 0)
            Clipboard.SetText(SelectedText);
    }

    public void Cut()
    {
        if (SelectionLength > 0 && Clipboard.SetText(SelectedText))
            ReplaceSelection("");
    }

    public void Paste()
    {
        if (Clipboard.GetText() is { Length: > 0 } text)
            ReplaceSelection(text);
    }

    /// <summary>Scrolls so that the caret is visible.</summary>
    public void ScrollToCaret() => EnsureCaretVisible();

    protected virtual void OnTextChanged(EventArgs e) => TextChanged?.Invoke(this, e);

    protected virtual void OnSelectionChanged(EventArgs e) => SelectionChanged?.Invoke(this, e);

    protected override PopupMenu? GetContextMenu() =>
        TextContextMenu.Create(this, ContextMenu, SelectionLength > 0, SelectionLength < _text.Length, Cut, Copy, Paste, SelectAll);

    protected override void OnSizeChanged(EventArgs e)
    {
        ClampScroll();
        base.OnSizeChanged(e);
    }

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

    // ---------------------------------------------------------------- Mouse

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButton.Left)
            return;

        if (HitScrollBar(e.X, e.Y) is var part and not ScrollPart.None)
        {
            PressScrollBar(part, e.Y);
            return;
        }

        var index = IndexAtPoint(e.X, e.Y);
        _desiredX = -1;
        switch (e.ClickCount)
        {
            case 1:
                _selectionUnit = SelectionUnit.Character;
                MoveCaret(index, extendSelection: e.Modifiers.HasFlag(KeyModifiers.Shift));
                break;
            case 2:
                _selectionUnit = SelectionUnit.Word;
                _initialUnit = WordAt(CharIndexAtPoint(e.X, e.Y));
                Select(_initialUnit.Start, _initialUnit.End - _initialUnit.Start);
                break;
            default:
                _selectionUnit = SelectionUnit.Line;
                _initialUnit = LineAt(index);
                Select(_initialUnit.Start, _initialUnit.End - _initialUnit.Start);
                break;
        }
        _isMouseSelecting = true;
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        if (_pressedPart == ScrollPart.Thumb)
        {
            DragThumb(e.Y);
            return;
        }
        if (!_isMouseSelecting)
            return;

        // Moves keep coming while the button is held, also outside the control: the view follows the caret.
        if (_selectionUnit == SelectionUnit.Character)
        {
            MoveCaret(IndexAtPoint(e.X, e.Y), extendSelection: true);
            return;
        }
        var unit = _selectionUnit == SelectionUnit.Word ? WordAt(CharIndexAtPoint(e.X, e.Y)) : LineAt(IndexAtPoint(e.X, e.Y));
        if (unit.Start < _initialUnit.Start)
            Select(_initialUnit.End, unit.Start - _initialUnit.End);
        else
            Select(_initialUnit.Start, Math.Max(unit.End, _initialUnit.End) - _initialUnit.Start);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButton.Left)
            return;
        _isMouseSelecting = false;
        if (_pressedPart != ScrollPart.None)
        {
            _pressedPart = ScrollPart.None;
            StopRepeat();
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.Handled || MaxScrollY == 0)
            return;
        e.Handled = true;
        ScrollTo(_scrollY - e.Delta * WheelLines * LineHeight);
    }

    // ---------------------------------------------------------------- Keyboard

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled)
            return;
        e.Handled = true;
        ReplaceSelection(e.Text);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        var control = e.Modifiers.HasFlag(KeyModifiers.Control);
        var shift = e.Modifiers.HasFlag(KeyModifiers.Shift);
        var hasSelection = SelectionLength > 0;
        var keepDesiredX = false;
        switch (e.Key)
        {
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
            case Key.Up when !control:
            case Key.Down when !control:
                MoveVertically(e.Key == Key.Up ? -1 : 1, shift);
                keepDesiredX = true;
                break;
            case Key.PageUp when !control:
            case Key.PageDown when !control:
                var page = Math.Max(1, ViewHeight / LineHeight - 1);
                ScrollTo(_scrollY + (e.Key == Key.PageUp ? -page : page) * LineHeight);
                MoveVertically(e.Key == Key.PageUp ? -page : page, shift);
                keepDesiredX = true;
                break;
            case Key.Home:
                MoveCaret(control ? 0 : _lineStarts[CaretLine], shift);
                break;
            case Key.End:
                MoveCaret(control ? _text.Length : LineEnd(CaretLine), shift);
                break;

            case Key.Enter when !control:
                ReplaceSelection("\n");
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
                return; // Not ours (e.g. Tab): let parents and the window see it.
        }
        if (!keepDesiredX)
            _desiredX = -1;
        e.Handled = true;
    }

    // ---------------------------------------------------------------- Rendering

    protected override void OnRender(DrawingContext dc)
    {
        var enabled = IsEffectivelyEnabled;
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, enabled ? _background : _disabledBackground);
        ClassicStyle.DrawSunkenEdge(dc, bounds, ClassicStyle.Highlight, ClassicStyle.Face, ClassicStyle.Shadow, ClassicStyle.DarkShadow);
        ClampScroll();

        var font = Font;
        var lineHeight = LineHeight;
        var textRect = TextRect;
        var textOffset = (lineHeight - (font.Ascent + font.Descent)) / 2;
        var active = Focused && enabled && GetWindow()?.IsActive == true;
        var textColor = enabled ? _color : _disabledColor;
        int selectionStart = SelectionStart, selectionEnd = SelectionEnd;

        // Clip to the area inside the edge and left of the scroll bar; lines may scroll into the padding.
        using (dc.PushClip(Rect.FromEdges(EdgeSize, EdgeSize, textRect.Right + Padding, Height - EdgeSize)))
        {
            var first = Math.Max(0, _scrollY / lineHeight);
            var last = Math.Min(LineCount - 1, (_scrollY + textRect.Height) / lineHeight);
            for (var line = first; line <= last; line++)
            {
                int start = _lineStarts[line], end = LineEnd(line);
                var y = textRect.Y + line * lineHeight - _scrollY;
                var x = textRect.X - _scrollX;

                // Selection on this line; a selected line break is shown as a small extra block.
                Rect selection = default;
                if (selectionStart < selectionEnd && selectionStart <= end && selectionEnd > start)
                {
                    var from = XInLine(Math.Max(selectionStart, start));
                    var to = XInLine(Math.Min(selectionEnd, end)) + (selectionEnd > end ? font.MeasureText(" ").Width : 0);
                    selection = Rect.FromEdges(x + from, y, x + to, y + lineHeight);
                    dc.FillRectangle(selection, active ? _selectionBackground : _inactiveSelectionBackground);
                }

                var lineText = _text.Substring(start, end - start);
                dc.DrawText(lineText, font, textColor, x, y + textOffset);
                if (active && !selection.IsEmpty)
                {
                    using (dc.PushClip(selection))
                        dc.DrawText(lineText, font, _selectionColor, x, y + textOffset);
                }
            }

            if (active && _caretVisible)
            {
                var caretLine = CaretLine;
                dc.FillRectangle(textRect.X - _scrollX + XInLine(_caretIndex), textRect.Y + caretLine * lineHeight - _scrollY,
                    1, lineHeight, _color);
            }
        }

        if (IsScrollBarVisible)
            DrawScrollBar(dc, enabled);
    }

    private void DrawScrollBar(DrawingContext dc, bool enabled)
    {
        var (up, down, track, thumb) = ScrollBarLayout();

        // Classic dotted track: face color with every other pixel in the highlight color.
        dc.FillRectangle(track, ClassicStyle.Face);
        for (var y = track.Y; y < track.Bottom; y++)
        {
            for (var x = track.X + (y & 1); x < track.Right; x += 2)
                dc.FillRectangle(x, y, 1, 1, ClassicStyle.Highlight);
        }
        if (_pressedPart is ScrollPart.TrackAbove or ScrollPart.TrackBelow && enabled)
        {
            var pressed = _pressedPart == ScrollPart.TrackAbove ? Rect.FromEdges(track.X, track.Y, track.Right, thumb.Y)
                : Rect.FromEdges(track.X, thumb.Bottom, track.Right, track.Bottom);
            dc.FillRectangle(pressed, ClassicStyle.DarkShadow);
        }

        DrawScrollButton(dc, up, pointsUp: true, _pressedPart == ScrollPart.UpButton, enabled);
        DrawScrollButton(dc, down, pointsUp: false, _pressedPart == ScrollPart.DownButton, enabled);
        dc.FillRectangle(thumb, ClassicStyle.Face);
        ClassicStyle.DrawRaisedEdge(dc, thumb, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);
    }

    private static void DrawScrollButton(DrawingContext dc, Rect r, bool pointsUp, bool pressed, bool enabled)
    {
        dc.FillRectangle(r, ClassicStyle.Face);
        if (pressed)
        {
            dc.DrawRectangle(r, ClassicStyle.Shadow); // Classic pressed scroll button: a flat shadow frame.
        }
        else
        {
            ClassicStyle.DrawRaisedEdge(dc, r, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);
        }

        // Arrow: a 7 px wide triangle, 4 rows high, moved 1 px down-right while pressed.
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

    // ---------------------------------------------------------------- Scroll bar

    private int LineHeight => Math.Max(1, Font.LineHeight);

    /// <summary>Area of the text inside the edge, the padding and the scroll bar.</summary>
    private Rect TextRect
    {
        get
        {
            var right = Width - EdgeSize - Padding - (IsScrollBarVisible ? ScrollBarWidth : 0);
            return Rect.FromEdges(EdgeSize + Padding, EdgeSize + Padding, Math.Max(EdgeSize + Padding, right), Math.Max(EdgeSize + Padding, Height - EdgeSize - Padding));
        }
    }

    private int ViewHeight => Math.Max(0, Height - 2 * (EdgeSize + Padding));

    private int ContentHeight => LineCount * LineHeight;

    private int MaxScrollY => Math.Max(0, ContentHeight - ViewHeight);

    private bool IsScrollBarVisible => ContentHeight > ViewHeight && Height >= 2 * EdgeSize + 2 * ScrollBarWidth;

    private (Rect Up, Rect Down, Rect Track, Rect Thumb) ScrollBarLayout()
    {
        var bar = Rect.FromEdges(Width - EdgeSize - ScrollBarWidth, EdgeSize, Width - EdgeSize, Height - EdgeSize);
        var up = bar with { Height = ScrollBarWidth };
        var down = bar with { Y = bar.Bottom - ScrollBarWidth, Height = ScrollBarWidth };
        var track = Rect.FromEdges(bar.X, up.Bottom, bar.Right, down.Y);
        var thumbSize = Math.Clamp((int)((long)track.Height * ViewHeight / Math.Max(1, ContentHeight)), MinThumbSize, Math.Max(MinThumbSize, track.Height));
        var thumbTop = track.Y + (MaxScrollY == 0 ? 0 : (int)((long)(track.Height - thumbSize) * _scrollY / MaxScrollY));
        return (up, down, track, new Rect(track.X, thumbTop, track.Width, Math.Min(thumbSize, track.Height)));
    }

    private ScrollPart HitScrollBar(int x, int y)
    {
        if (!IsScrollBarVisible)
            return ScrollPart.None;
        var (up, down, track, thumb) = ScrollBarLayout();
        return up.Contains(x, y) ? ScrollPart.UpButton
            : down.Contains(x, y) ? ScrollPart.DownButton
            : thumb.Contains(x, y) ? ScrollPart.Thumb
            : track.Contains(x, y) ? (y < thumb.Y ? ScrollPart.TrackAbove : ScrollPart.TrackBelow)
            : ScrollPart.None;
    }

    /// <summary>Arrows scroll a line and the track a page, repeating while the button is held (like classic scroll bars).</summary>
    private void PressScrollBar(ScrollPart part, int y)
    {
        _pressedPart = part;
        if (part == ScrollPart.Thumb)
        {
            _thumbGrabOffset = y - ScrollBarLayout().Thumb.Y;
            Invalidate();
            return;
        }

        void Step()
        {
            var page = Math.Max(LineHeight, ViewHeight - LineHeight);
            switch (_pressedPart)
            {
                case ScrollPart.UpButton: ScrollTo(_scrollY - LineHeight); break;
                case ScrollPart.DownButton: ScrollTo(_scrollY + LineHeight); break;
                case ScrollPart.TrackAbove: ScrollTo(_scrollY - page); break;
                case ScrollPart.TrackBelow: ScrollTo(_scrollY + page); break;
            }
            Invalidate();
        }

        Step();
        StopRepeat();
        _repeatTimer = Timer.SetTimeout(() =>
        {
            if (_pressedPart is ScrollPart.None or ScrollPart.Thumb)
                return;
            _repeatTimer = Timer.SetInterval(Step, RepeatIntervalMilliseconds);
        }, RepeatDelayMilliseconds);
    }

    private void DragThumb(int y)
    {
        var (_, _, track, thumb) = ScrollBarLayout();
        var room = track.Height - thumb.Height;
        if (room <= 0)
            return;
        var thumbTop = Math.Clamp(y - _thumbGrabOffset, track.Y, track.Y + room);
        ScrollTo((int)((long)(thumbTop - track.Y) * MaxScrollY / room));
    }

    private void StopRepeat()
    {
        Timer.ClearTimeout(_repeatTimer);
        _repeatTimer = 0;
    }

    private void ScrollTo(int y)
    {
        var clamped = Math.Clamp(y, 0, MaxScrollY);
        if (clamped == _scrollY)
            return;
        _scrollY = clamped;
        Invalidate();
    }

    private void ClampScroll()
    {
        _scrollY = Math.Clamp(_scrollY, 0, MaxScrollY);
        _scrollX = Math.Max(0, _scrollX);
    }

    /// <summary>Scrolls vertically and horizontally so that the caret is inside the text area.</summary>
    private void EnsureCaretVisible()
    {
        var lineHeight = LineHeight;
        var textRect = TextRect;
        var top = CaretLine * lineHeight;
        if (top < _scrollY)
            _scrollY = top;
        else if (top + lineHeight > _scrollY + textRect.Height)
            _scrollY = top + lineHeight - textRect.Height;

        var x = XInLine(_caretIndex);
        if (x < _scrollX)
            _scrollX = x;
        else if (x > _scrollX + textRect.Width - 1)
            _scrollX = x - textRect.Width + 1;
        ClampScroll();
    }

    // ---------------------------------------------------------------- Text model

    private void SetText(string text, int caretIndex)
    {
        var changed = text != _text;
        _text = text;
        RebuildLines();
        _anchorIndex = _caretIndex = -1; // Force MoveCaret to apply.
        MoveCaret(caretIndex, extendSelection: false);
        if (changed)
            OnTextChanged(EventArgs.Empty);
    }

    private void ReplaceSelection(string text)
    {
        text = NormalizeLineBreaks(text);
        var start = SelectionStart;
        _desiredX = -1;
        SetText(_text.Remove(start, SelectionLength).Insert(start, text), start + text.Length);
    }

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
        EnsureCaretVisible();
        Invalidate();
        if (_blinkTimer != 0)
            RestartCaretBlink();
        OnSelectionChanged(EventArgs.Empty);
    }

    /// <summary>Moves the caret <paramref name="lines"/> lines, keeping its horizontal position.</summary>
    private void MoveVertically(int lines, bool extendSelection)
    {
        if (_desiredX < 0)
            _desiredX = XInLine(_caretIndex);
        var line = Math.Clamp(CaretLine + lines, 0, LineCount - 1);
        MoveCaret(IndexAtX(line, _desiredX), extendSelection);
    }

    private void RebuildLines()
    {
        var starts = new List<int> { 0 };
        for (var i = _text.IndexOf('\n'); i >= 0; i = _text.IndexOf('\n', i + 1))
            starts.Add(i + 1);
        _lineStarts = [.. starts];
    }

    private int LineOf(int index)
    {
        var line = Array.BinarySearch(_lineStarts, index);
        return line >= 0 ? line : ~line - 1;
    }

    /// <summary>Index just past the last character of <paramref name="line"/>, before its '\n'.</summary>
    private int LineEnd(int line) => line + 1 < _lineStarts.Length ? _lineStarts[line + 1] - 1 : _text.Length;

    /// <summary>Pixel offset of <paramref name="index"/> from the start of its line.</summary>
    private int XInLine(int index)
    {
        var face = Font.Face;
        float x = 0;
        foreach (var rune in _text.AsSpan(_lineStarts[LineOf(index)], index - _lineStarts[LineOf(index)]).EnumerateRunes())
            x += face.GetGlyph(rune.Value).Advance;
        return (int)MathF.Round(x);
    }

    /// <summary>Caret position on <paramref name="line"/> closest to the pixel offset <paramref name="x"/>.</summary>
    private int IndexAtX(int line, int x)
    {
        var face = Font.Face;
        int start = _lineStarts[line], end = LineEnd(line);
        float pen = 0;
        var index = start;
        foreach (var rune in _text.AsSpan(start, end - start).EnumerateRunes())
        {
            var advance = face.GetGlyph(rune.Value).Advance;
            if (x < pen + advance / 2)
                return index;
            pen += advance;
            index += rune.Utf16SequenceLength;
        }
        return end;
    }

    /// <summary>Line under the y coordinate; above the first line gives 0, below the last gives the last.</summary>
    private int LineAtY(int y)
    {
        var offset = y - TextRect.Y + _scrollY;
        return offset < 0 ? 0 : Math.Min(offset / LineHeight, LineCount - 1);
    }

    private int IndexAtPoint(int x, int y) => IndexAtX(LineAtY(y), x - TextRect.X + _scrollX);

    /// <summary>Index of the character under the point (the last one of the line if past its end).</summary>
    private int CharIndexAtPoint(int x, int y)
    {
        var line = LineAtY(y);
        var face = Font.Face;
        int start = _lineStarts[line], end = LineEnd(line);
        var target = x - TextRect.X + _scrollX;
        float pen = 0;
        var index = start;
        foreach (var rune in _text.AsSpan(start, end - start).EnumerateRunes())
        {
            pen += face.GetGlyph(rune.Value).Advance;
            if (target < pen)
                return index;
            index += rune.Utf16SequenceLength;
        }
        return Math.Max(start, PreviousIndex(end));
    }

    /// <summary>Run of word characters, spaces or punctuation around <paramref name="index"/>, within its line.</summary>
    private (int Start, int End) WordAt(int index)
    {
        var line = LineOf(index);
        int lineStart = _lineStarts[line], lineEnd = LineEnd(line);
        if (lineStart == lineEnd)
            return (lineStart, lineStart);
        index = Math.Clamp(index, lineStart, lineEnd - 1);
        var kind = KindOf(_text[index]);
        var start = index;
        while (start > lineStart && KindOf(_text[start - 1]) == kind)
            start--;
        var end = index + 1;
        while (end < lineEnd && KindOf(_text[end]) == kind)
            end++;
        return (start, end);
    }

    /// <summary>The whole line containing <paramref name="index"/>, including its '\n'.</summary>
    private (int Start, int End) LineAt(int index)
    {
        var line = LineOf(index);
        return (_lineStarts[line], line + 1 < LineCount ? _lineStarts[line + 1] : _text.Length);
    }

    private static int KindOf(char ch) =>
        char.IsLetterOrDigit(ch) || ch == '_' || char.IsSurrogate(ch) ? 0 : char.IsWhiteSpace(ch) ? 1 : 2;

    private int PreviousIndex(int index) =>
        index <= 0 ? 0 : IsInsideSurrogatePair(index - 1) ? index - 2 : index - 1;

    private int NextIndex(int index) =>
        index >= _text.Length ? _text.Length : IsInsideSurrogatePair(index + 1) ? index + 2 : index + 1;

    /// <summary>Start of the word before <paramref name="index"/>; a line break counts as a word boundary.</summary>
    private int PreviousWord(int index)
    {
        if (index > 0 && _text[index - 1] == '\n')
            return index - 1;
        while (index > 0 && _text[index - 1] is ' ' or '\t')
            index--;
        while (index > 0 && !char.IsWhiteSpace(_text[index - 1]))
            index--;
        return index;
    }

    private int NextWord(int index)
    {
        if (index < _text.Length && _text[index] == '\n')
            return index + 1;
        while (index < _text.Length && !char.IsWhiteSpace(_text[index]))
            index++;
        while (index < _text.Length && _text[index] is ' ' or '\t')
            index++;
        return index;
    }

    private bool IsInsideSurrogatePair(int index) =>
        index > 0 && index < _text.Length && char.IsLowSurrogate(_text[index]) && char.IsHighSurrogate(_text[index - 1]);

    private static string NormalizeLineBreaks(string text) =>
        text.Contains('\r') ? text.Replace("\r\n", "\n").Replace('\r', '\n') : text;

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

    private void SetColor(ref Color field, Color value)
    {
        field = value;
        Invalidate();
    }
}
