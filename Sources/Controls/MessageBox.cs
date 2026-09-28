using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>The buttons of a <see cref="MessageBox"/>.</summary>
public enum MessageBoxButtons
{
    /// <summary>OK.</summary>
    Ok,

    /// <summary>OK and Cancel.</summary>
    OkCancel,

    /// <summary>Yes and No.</summary>
    YesNo,

    /// <summary>Yes, No and Cancel.</summary>
    YesNoCancel,
}

/// <summary>The icon of a <see cref="MessageBox"/>.</summary>
public enum MessageBoxIcon
{
    /// <summary>No icon.</summary>
    None,

    /// <summary>A blue "i".</summary>
    Information,

    /// <summary>A blue "?".</summary>
    Question,

    /// <summary>A yellow triangle with "!".</summary>
    Warning,

    /// <summary>A red circle with "×".</summary>
    Error,
}

/// <summary>The button that closed a <see cref="MessageBox"/>.</summary>
public enum MessageBoxResult
{
    /// <summary>OK was chosen.</summary>
    Ok,

    /// <summary>Cancel was chosen, or the message was closed without a choice (Escape, or the window closed).</summary>
    Cancel,

    /// <summary>Yes was chosen.</summary>
    Yes,

    /// <summary>No was chosen (also Escape with <see cref="MessageBoxButtons.YesNo"/>).</summary>
    No,
}

/// <summary>
/// A modal message window: an icon, the text (wrapped to fit) and buttons, in a small dialog window centred over its
/// owner (see <see cref="Window.ShowModal"/>). While it is open the owner gets no input; <c>Show</c> returns at once
/// and the result arrives in its callback.
/// <list type="bullet">
/// <item>The first button is the default. Left / Right and Tab / Shift+Tab move between the buttons; Enter or
/// Space presses the focused one.</item>
/// <item>Escape and the title bar's close button answer Cancel (No with <see cref="MessageBoxButtons.YesNo"/>, OK with
/// <see cref="MessageBoxButtons.Ok"/>).</item>
/// </list>
/// <example>
/// <code>
/// MessageBox.Show(window, "Save the changes?", "Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
///     result => { if (result == MessageBoxResult.Yes) Save(); });
/// </code>
/// </example>
/// </summary>
public static class MessageBox
{
    /// <summary>
    /// Shows <paramref name="text"/> in a dialog window over <paramref name="owner"/> (lines are broken at '\n' and
    /// wherever needed). <paramref name="title"/> defaults to the owner's title, and the owner's icons are used.
    /// <paramref name="closed"/> runs once, with the chosen button, after the message window has closed.
    /// </summary>
    public static void Show(Window owner, string text, string? title = null, MessageBoxButtons buttons = MessageBoxButtons.Ok,
        MessageBoxIcon icon = MessageBoxIcon.None, Action<MessageBoxResult>? closed = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(text);
        var window = new Window { Title = title ?? owner.Title, Icons = owner.Icons, Resizable = false, Background = ClassicStyle.Face };
        var view = new MessageBoxView(window, text, buttons, icon);
        (window.Width, window.Height) = (view.Width, view.Height);
        window.Content = view;
        window.FocusedControl = view;
        window.Closed += (sender, e) => closed?.Invoke(view.Result);
        window.ShowModal(owner);
    }

    /// <summary>
    /// Shows the message in the window that contains <paramref name="owner"/> (a control shown in a window).
    /// See <see cref="Show(Window, string, string?, MessageBoxButtons, MessageBoxIcon, Action{MessageBoxResult}?)"/>.
    /// </summary>
    public static void Show(Control owner, string text, string? title = null, MessageBoxButtons buttons = MessageBoxButtons.Ok,
        MessageBoxIcon icon = MessageBoxIcon.None, Action<MessageBoxResult>? closed = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var window = owner.GetWindow() ?? throw new InvalidOperationException(Localization.Get("Doqua.Error.NotInWindow"));
        Show(window, text, title, buttons, icon, closed);
    }
}

/// <summary>The content of a message window: icon, text and buttons, drawn and driven by itself.</summary>
internal sealed class MessageBoxView : Control
{
    private const int Padding = 16;
    private const int IconSize = 32;
    private const int ButtonHeight = 24;
    private const int MinButtonWidth = 80;
    private const int ButtonGap = 8;
    private const int MaxTextWidth = 460;

    private readonly Window _window;
    private readonly MessageBoxIcon _icon;
    private readonly (MessageBoxResult Result, string Text)[] _buttons;
    private readonly List<string> _lines;
    private readonly int _buttonWidth;
    private int _focused;
    private int _pressed = -1;

    public MessageBoxView(Window window, string text, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        _window = window;
        _icon = icon;
        _buttons = buttons switch
        {
            MessageBoxButtons.OkCancel => [(MessageBoxResult.Ok, "Doqua.MessageBox.Ok"), (MessageBoxResult.Cancel, "Doqua.MessageBox.Cancel")],
            MessageBoxButtons.YesNo => [(MessageBoxResult.Yes, "Doqua.MessageBox.Yes"), (MessageBoxResult.No, "Doqua.MessageBox.No")],
            MessageBoxButtons.YesNoCancel => [(MessageBoxResult.Yes, "Doqua.MessageBox.Yes"), (MessageBoxResult.No, "Doqua.MessageBox.No"),
                (MessageBoxResult.Cancel, "Doqua.MessageBox.Cancel")],
            _ => [(MessageBoxResult.Ok, "Doqua.MessageBox.Ok")],
        };
        for (var i = 0; i < _buttons.Length; i++)
            _buttons[i].Text = Localization.Get(_buttons[i].Text);
        Result = buttons switch
        {
            MessageBoxButtons.Ok => MessageBoxResult.Ok,
            MessageBoxButtons.YesNo => MessageBoxResult.No,
            _ => MessageBoxResult.Cancel,
        };
        Cursor = Cursor.Arrow;
        Focusable = true; // Takes the window's keys.

        // Size (of the window): the wrapped text with the icon, or the buttons, whichever is wider.
        var font = Font;
        var iconSpace = icon == MessageBoxIcon.None ? 0 : IconSize + Padding;
        _lines = Wrap(text, font, MaxTextWidth);
        var textWidth = _lines.Count == 0 ? 0 : _lines.Max(line => font.MeasureText(line).Width);
        _buttonWidth = Math.Max(MinButtonWidth, _buttons.Max(b => font.MeasureText(b.Text).Width + 24));
        var buttonsWidth = _buttons.Length * _buttonWidth + (_buttons.Length - 1) * ButtonGap;
        var contentWidth = Math.Max(iconSpace + textWidth, buttonsWidth);
        var textHeight = _lines.Count * font.LineHeight;
        Width = Math.Max(240, contentWidth + 2 * Padding);
        Height = Padding + Math.Max(textHeight, iconSpace > 0 ? IconSize : 0) + Padding + ButtonHeight + Padding;
    }

    /// <summary>The chosen button; the Escape result until a button is chosen.</summary>
    public MessageBoxResult Result { get; private set; }

    private static Font Font => Font.Default;

    private Rect ButtonRect(int index)
    {
        var total = _buttons.Length * _buttonWidth + (_buttons.Length - 1) * ButtonGap;
        var x = (Width - total) / 2 + index * (_buttonWidth + ButtonGap);
        return new Rect(x, Height - Padding - ButtonHeight, _buttonWidth, ButtonHeight);
    }

    private int ButtonAt(int x, int y)
    {
        for (var i = 0; i < _buttons.Length; i++)
        {
            if (ButtonRect(i).Contains(x, y))
                return i;
        }
        return -1;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButton.Left || ButtonAt(e.X, e.Y) is not (var index and >= 0))
            return;
        _pressed = _focused = index;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButton.Left || _pressed < 0)
            return;
        var pressed = _pressed;
        _pressed = -1;
        Invalidate();
        if (ButtonAt(e.X, e.Y) == pressed)
            Choose(_buttons[pressed].Result);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        e.Handled = true; // The message takes all keys.
        var shift = e.Modifiers == KeyModifiers.Shift;
        switch (e.Key)
        {
            case Key.Escape:
                _window.Close(); // Result is still the Escape result.
                break;
            case Key.Enter or Key.Space:
                Choose(_buttons[_focused].Result);
                break;
            case Key.Left:
            case Key.Tab when shift:
                MoveFocus(-1);
                break;
            case Key.Right:
            case Key.Tab:
                MoveFocus(1);
                break;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.FillRectangle(new Rect(0, 0, Width, Height), ClassicStyle.Face);

        // Icon and text; one line of text is centred on the icon.
        var font = Font;
        var top = Padding;
        var textX = Padding;
        var textHeight = _lines.Count * font.LineHeight;
        var textTop = top;
        if (_icon != MessageBoxIcon.None)
        {
            DrawIcon(dc, _icon, textX, top);
            textX += IconSize + Padding;
            if (textHeight < IconSize)
                textTop = top + (IconSize - textHeight) / 2;
        }
        using (dc.PushClip(Rect.FromEdges(0, top, Width, ButtonRect(0).Y - 4)))
        {
            for (var i = 0; i < _lines.Count; i++)
                dc.DrawText(_lines[i], font, Color.Black, textX, textTop + i * font.LineHeight);
        }

        for (var i = 0; i < _buttons.Length; i++)
            DrawButton(dc, ButtonRect(i), _buttons[i].Text, focused: i == _focused, pressed: i == _pressed);
    }

    private void MoveFocus(int step)
    {
        _focused = (_focused + step + _buttons.Length) % _buttons.Length;
        Invalidate();
    }

    private void Choose(MessageBoxResult result)
    {
        Result = result;
        _window.Close();
    }

    /// <summary>Classic push button; the focused (default) one has a black frame and a dotted focus rectangle.</summary>
    private static void DrawButton(DrawingContext dc, Rect r, string text, bool focused, bool pressed)
    {
        dc.FillRectangle(r, ClassicStyle.Face);
        var face = r;
        if (focused)
        {
            dc.DrawRectangle(r, Color.Black);
            face = Rect.FromEdges(r.X + 1, r.Y + 1, r.Right - 1, r.Bottom - 1);
        }
        if (pressed)
            dc.DrawRectangle(face, ClassicStyle.Shadow);
        else
            ClassicStyle.DrawRaisedEdge(dc, face, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);
        var font = Font;
        var shift = pressed ? 1 : 0;
        var width = font.MeasureText(text).Width;
        dc.DrawText(text, font, Color.Black, r.X + (r.Width - width) / 2 + shift, r.Y + (r.Height - (font.Ascent + font.Descent)) / 2 + shift);
        if (focused)
            ClassicStyle.DrawFocusRectangle(dc, Rect.FromEdges(face.X + 3, face.Y + 3, face.Right - 3, face.Bottom - 3), Color.Black);
    }

    private static void DrawIcon(DrawingContext dc, MessageBoxIcon icon, int x, int y)
    {
        const float half = IconSize / 2f;
        float cx = x + half, cy = y + half;
        var glyphFont = Font.Default with { Size = 22, Style = FontStyle.Bold };
        switch (icon)
        {
            case MessageBoxIcon.Warning:
                // Yellow triangle, filled row by row, with a dark outline and a black "!".
                var yellow = new Color(255, 204, 0);
                for (var row = 1; row < IconSize - 1; row++)
                {
                    var halfWidth = (row - 1) * (half - 1) / (IconSize - 3);
                    dc.FillRectangle((int)MathF.Round(cx - halfWidth), y + row, Math.Max(1, (int)MathF.Round(2 * halfWidth)), 1, yellow);
                }
                var outline = new Color(96, 72, 0);
                dc.DrawLine(cx, y + 1, x + 1, y + IconSize - 2, outline);
                dc.DrawLine(cx, y + 1, x + IconSize - 1, y + IconSize - 2, outline);
                dc.DrawLine(x + 1, y + IconSize - 2, x + IconSize - 1, y + IconSize - 2, outline);
                dc.FillRectangle((int)cx - 2, y + 10, 4, 11, Color.Black);
                dc.FillRectangle((int)cx - 2, y + 24, 4, 4, Color.Black);
                break;
            case MessageBoxIcon.Error:
                dc.FillEllipse(cx, cy, half - 1, half - 1, new Color(208, 0, 0));
                dc.DrawEllipse(cx, cy, half - 1, half - 1, new Color(128, 0, 0));
                dc.DrawLine(cx - 7, cy - 7, cx + 7, cy + 7, Color.White, 4);
                dc.DrawLine(cx - 7, cy + 7, cx + 7, cy - 7, Color.White, 4);
                break;
            default:
                // Information and Question: a blue circle with a white glyph.
                dc.FillEllipse(cx, cy, half - 1, half - 1, new Color(0, 90, 200));
                dc.DrawEllipse(cx, cy, half - 1, half - 1, new Color(0, 50, 128));
                var glyph = icon == MessageBoxIcon.Question ? "?" : "i";
                var size = glyphFont.MeasureText(glyph);
                dc.DrawText(glyph, glyphFont, Color.White, (int)(cx - size.Width / 2f), (int)(cy - (glyphFont.Ascent + glyphFont.Descent) / 2f));
                break;
        }
    }

    /// <summary>Breaks <paramref name="text"/> at '\n', then between words (or inside a word longer than a line).</summary>
    internal static List<string> Wrap(string text, Font font, int width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (font.MeasureText(candidate).Width <= width)
                {
                    line = candidate;
                    continue;
                }
                if (line.Length > 0)
                    lines.Add(line);
                line = word;
                // A word wider than the line: split it character by character.
                while (line.Length > 1 && font.MeasureText(line).Width > width)
                {
                    var fit = 1;
                    while (fit < line.Length && font.MeasureText(line[..(fit + 1)]).Width <= width)
                        fit++;
                    lines.Add(line[..fit]);
                    line = line[fit..];
                }
            }
            lines.Add(line);
        }
        return lines;
    }
}
