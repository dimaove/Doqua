using System.Globalization;
using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Date field in the classic style: a read-only field showing <see cref="Value"/> with <see cref="Format"/>, and a
/// button that opens a calendar. The calendar picks a day; its title switches to month and year grids.
/// Dates outside [<see cref="Min"/>, <see cref="Max"/>] cannot be chosen.
/// <list type="bullet">
/// <item>Closed: Up / Down change the day, PageUp / PageDown the month, the wheel the day;
/// F4, Alt+Down, Space or Enter open the calendar.</item>
/// <item>Open: arrows move by day / week, PageUp / PageDown by month (with Ctrl by year), Home / End to the
/// first / last day of the month, Enter selects, Escape or a click outside cancels.</item>
/// </list>
/// </summary>
public class DateInput : Control
{
    private const int EdgeSize = 2;
    private const int ButtonWidth = 18;
    private const int TextPadding = 3;

    private DateTime _value = DateTime.Today;
    private DateTime? _min;
    private DateTime? _max;
    private string _format = "d";
    private CultureInfo? _culture;
    private DateInputCalendar? _calendar;
    private Font? _font;
    private Color _color = Color.Black;
    private Color _disabledColor = ClassicStyle.Shadow;
    private Color _background = Color.White;
    private Color _disabledBackground = ClassicStyle.Face;
    private Color _selectionBackground = new(0, 0, 128);
    private Color _selectionColor = Color.White;

    /// <summary>Creates a date input showing today, 140 pixels wide.</summary>
    public DateInput()
    {
        Focusable = true;
        Width = 140;
        Height = Math.Max(22, Font.LineHeight + 6);
    }

    /// <summary>Raised after <see cref="Value"/> changes: picked, stepped, or corrected by a new Min / Max.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>The date (its time of day is dropped); setting it clamps it into [Min, Max].</summary>
    public DateTime Value
    {
        get => _value;
        set
        {
            var date = Clamp(value.Date);
            if (date == _value)
                return;
            _value = date;
            Invalidate();
            OnValueChanged(EventArgs.Empty);
        }
    }

    /// <summary>Earliest date that can be chosen, or null for no limit. Moving it past Max moves Max too.</summary>
    public DateTime? Min
    {
        get => _min;
        set
        {
            _min = value?.Date;
            if (_min > _max)
                _max = _min;
            Value = _value; // Clamp.
            Invalidate();
        }
    }

    /// <summary>Latest date that can be chosen, or null for no limit. Moving it before Min moves Min too.</summary>
    public DateTime? Max
    {
        get => _max;
        set
        {
            _max = value?.Date;
            if (_max < _min)
                _min = _max;
            Value = _value;
            Invalidate();
        }
    }

    /// <summary>
    /// .NET date format of the field, e.g. "d" (short date, the default), "D", "yyyy-MM-dd", "dd MMM yyyy".
    /// </summary>
    public string Format
    {
        get => _format;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = DateTime.Today.ToString(value, Culture); // Throws FormatException now rather than while drawing.
            _format = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Culture for the field text, month and weekday names and the first day of the week;
    /// <see cref="Localization.Culture"/> (English by default) unless set.
    /// </summary>
    public CultureInfo Culture
    {
        get => _culture ?? Localization.Culture;
        set
        {
            _culture = value ?? throw new ArgumentNullException(nameof(value));
            Invalidate();
        }
    }

    /// <summary><see cref="Value"/> as shown in the field.</summary>
    public string Text => _value.ToString(_format, Culture);

    /// <summary>True while the calendar is open.</summary>
    public bool IsDroppedDown => _calendar != null;

    /// <summary>Font of the field and of the calendar; <see cref="GUI.Font.Default"/> unless set.</summary>
    public Font Font
    {
        get => _font ??= Font.Default;
        set
        {
            _font = value ?? throw new ArgumentNullException(nameof(value));
            Invalidate();
        }
    }

    /// <summary>Text color.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(ref _color, value);
    }

    /// <summary>Text color while disabled.</summary>
    public Color DisabledColor
    {
        get => _disabledColor;
        set => SetColor(ref _disabledColor, value);
    }

    /// <summary>Background of the field and of the calendar.</summary>
    public Color Background
    {
        get => _background;
        set => SetColor(ref _background, value);
    }

    /// <summary>Background of the field while disabled.</summary>
    public Color DisabledBackground
    {
        get => _disabledBackground;
        set => SetColor(ref _disabledBackground, value);
    }

    /// <summary>Background of the selected day, and of the field text while focused.</summary>
    public Color SelectionBackground
    {
        get => _selectionBackground;
        set => SetColor(ref _selectionBackground, value);
    }

    /// <summary>Text color of the selected day, and of the field text while focused.</summary>
    public Color SelectionColor
    {
        get => _selectionColor;
        set => SetColor(ref _selectionColor, value);
    }

    /// <summary>Opens the calendar below the field (above it if there is no room).</summary>
    public void ShowCalendar()
    {
        if (_calendar != null || GetWindow() is not { } window || !IsEffectivelyEnabled)
            return;
        var calendar = new DateInputCalendar(this);
        var (x, below) = PointToWindow(0, Height);
        var above = below - Height - calendar.Height;
        calendar.X = Math.Clamp(x, 0, Math.Max(0, window.Width - calendar.Width));
        calendar.Y = below + calendar.Height <= window.Height || above < 0 ? below : above;
        _calendar = calendar;
        window.OpenPopup(calendar, () =>
        {
            _calendar = null;
            Invalidate();
        });
        Invalidate();
    }

    /// <summary>Closes the calendar without changing the value.</summary>
    public void CloseCalendar() => GetWindow()?.ClosePopup();

    protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButton.Left)
            ShowCalendar();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.Handled)
            return;
        e.Handled = true;
        Value = AddDaysSafe(_value, -e.Delta);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        var alt = e.Modifiers == KeyModifiers.Alt;
        var none = e.Modifiers == KeyModifiers.None;
        if (none && e.Key is Key.F4 or Key.Space or Key.Enter || alt && e.Key is Key.Down or Key.Up)
        {
            ShowCalendar();
        }
        else if (none && e.Key is Key.Up or Key.Down)
        {
            Value = AddDaysSafe(_value, e.Key == Key.Up ? 1 : -1);
        }
        else if (none && e.Key is Key.PageUp or Key.PageDown)
        {
            Value = AddMonthsSafe(_value, e.Key == Key.PageUp ? 1 : -1);
        }
        else
        {
            return;
        }
        e.Handled = true;
    }

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

    protected override void OnRender(DrawingContext dc)
    {
        var enabled = IsEffectivelyEnabled;
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, enabled ? _background : _disabledBackground);
        ClassicStyle.DrawSunkenEdge(dc, bounds, ClassicStyle.Highlight, ClassicStyle.Face, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        var inner = Rect.FromEdges(EdgeSize, EdgeSize, Width - EdgeSize, Height - EdgeSize);
        var button = Rect.FromEdges(Math.Max(inner.X, inner.Right - ButtonWidth), inner.Y, inner.Right, inner.Bottom);
        DrawCalendarButton(dc, button, IsDroppedDown, enabled);

        var textArea = Rect.FromEdges(inner.X, inner.Y, button.X, inner.Bottom);
        var font = Font;
        var textY = textArea.Y + (textArea.Height - (font.Ascent + font.Descent)) / 2;
        var focused = enabled && Focused && !IsDroppedDown && GetWindow()?.IsActive == true;
        using (dc.PushClip(textArea))
        {
            if (focused)
            {
                var highlight = Rect.FromEdges(textArea.X + 1, textArea.Y + 1, textArea.Right - 1, textArea.Bottom - 1);
                dc.FillRectangle(highlight, _selectionBackground);
                ClassicStyle.DrawFocusRectangle(dc, highlight, _selectionColor);
            }
            if (enabled)
                dc.DrawText(Text, font, focused ? _selectionColor : _color, textArea.X + TextPadding, textY);
            else
                ClassicStyle.DrawEmbossedText(dc, Text, font, _disabledColor, ClassicStyle.Highlight, textArea.X + TextPadding, textY);
        }
    }

    /// <summary>Raised button with a small calendar page: a coloured top band with rings, and a grid of days.</summary>
    private static void DrawCalendarButton(DrawingContext dc, Rect r, bool pressed, bool enabled)
    {
        dc.FillRectangle(r, ClassicStyle.Face);
        if (pressed)
            dc.DrawRectangle(r, ClassicStyle.Shadow);
        else
            ClassicStyle.DrawRaisedEdge(dc, r, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        var shift = pressed ? 1 : 0;
        var x = r.X + (r.Width - 11) / 2 + shift;
        var y = r.Y + (r.Height - 11) / 2 + shift;
        var ink = enabled ? Color.Black : ClassicStyle.Shadow;
        var band = enabled ? new Color(0, 0, 128) : ClassicStyle.Shadow;
        dc.FillRectangle(x, y + 1, 11, 10, Color.White);
        dc.DrawRectangle(new Rect(x, y + 1, 11, 10), ink);
        dc.FillRectangle(x, y + 1, 11, 3, band);           // Top band.
        dc.FillRectangle(x + 3, y, 1, 2, ink);              // Rings.
        dc.FillRectangle(x + 7, y, 1, 2, ink);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 4; column++)
                dc.FillRectangle(x + 2 + column * 2, y + 5 + row * 2, 1, 1, ink);
        }
    }

    internal DateTime MinDate => _min ?? DateTime.MinValue;

    internal DateTime MaxDate => _max ?? DateTime.MaxValue.Date;

    internal bool IsInRange(DateTime date) => date >= MinDate && date <= MaxDate;

    internal DateTime Clamp(DateTime date) => date < MinDate ? MinDate : date > MaxDate ? MaxDate : date;

    /// <summary>The calendar chose a day: select it and close.</summary>
    internal void Commit(DateTime date)
    {
        CloseCalendar();
        Value = date;
    }

    /// <summary>Adds days, staying inside the DateTime range (and so never throwing).</summary>
    internal static DateTime AddDaysSafe(DateTime date, int days)
    {
        var limit = days < 0 ? (date - DateTime.MinValue).Days : (DateTime.MaxValue.Date - date).Days;
        return date.AddDays(Math.Clamp(days, -limit, limit));
    }

    internal static DateTime AddMonthsSafe(DateTime date, int months)
    {
        var index = (long)date.Year * 12 + date.Month - 1 + months;
        index = Math.Clamp(index, 12 + 0, 9999L * 12 + 11); // Year 1 January .. year 9999 December.
        int year = (int)(index / 12), month = (int)(index % 12) + 1;
        return new DateTime(year, month, Math.Min(date.Day, DateTime.DaysInMonth(year, month)));
    }

    private void SetColor(ref Color field, Color value)
    {
        field = value;
        Invalidate();
    }
}

/// <summary>The open calendar of a <see cref="DateInput"/>, hosted by the window above its content.</summary>
internal sealed class DateInputCalendar : Control
{
    private const int Border = 1;
    private const int CellWidth = 30;
    private const int CellHeight = 22;
    private const int HeaderHeight = 28;
    private const int WeekdayHeight = 20;
    private const int FooterHeight = 24;
    private const int NavButtonSize = 20;
    private static readonly Color OutOfRange = new(200, 200, 200);

    private readonly DateInput _owner;
    private View _view = View.Days;
    private DateTime _month;   // First day of the month shown in the day view (the year in the other views).
    private DateTime _cursor;  // Day moved with the keyboard; selected with Enter.

    private enum View
    {
        Days,
        Months,
        Years,
    }

    public DateInputCalendar(DateInput owner)
    {
        _owner = owner;
        _cursor = owner.Value;
        _month = new DateTime(_cursor.Year, _cursor.Month, 1);
        Cursor = Cursor.Arrow;
        Width = 2 * Border + 7 * CellWidth;
        Height = 2 * Border + HeaderHeight + WeekdayHeight + 6 * CellHeight + FooterHeight;
    }

    private CultureInfo Culture => _owner.Culture;

    private Rect GridArea => new(Border, Border + HeaderHeight, 7 * CellWidth, WeekdayHeight + 6 * CellHeight);

    private Rect PreviousButton => new(Border + 2 + (_view == View.Days ? NavButtonSize + 2 : 0), Border + (HeaderHeight - NavButtonSize) / 2, NavButtonSize, NavButtonSize);

    private Rect NextButton => new(Width - Border - 2 - NavButtonSize - (_view == View.Days ? NavButtonSize + 2 : 0), PreviousButton.Y, NavButtonSize, NavButtonSize);

    // Year buttons ("«" and "»"), in the day view only.
    private Rect PreviousYearButton => new(Border + 2, PreviousButton.Y, NavButtonSize, NavButtonSize);

    private Rect NextYearButton => new(Width - Border - 2 - NavButtonSize, PreviousButton.Y, NavButtonSize, NavButtonSize);

    private Rect TitleArea => Rect.FromEdges(PreviousButton.Right + 2, Border, NextButton.X - 2, Border + HeaderHeight);

    private Rect FooterArea => new(Border, Height - Border - FooterHeight, 7 * CellWidth, FooterHeight);

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButton.Left)
            return;
        var (x, y) = (e.X, e.Y);

        if (_view == View.Days && PreviousYearButton.Contains(x, y))
            Navigate(-12);
        else if (_view == View.Days && NextYearButton.Contains(x, y))
            Navigate(12);
        else if (PreviousButton.Contains(x, y))
            Navigate(_view switch { View.Days => -1, View.Months => -12, _ => -120 });
        else if (NextButton.Contains(x, y))
            Navigate(_view switch { View.Days => 1, View.Months => 12, _ => 120 });
        else if (TitleArea.Contains(x, y))
            _view = _view switch { View.Days => View.Months, _ => View.Years };
        else if (FooterArea.Contains(x, y))
        {
            if (_owner.IsInRange(DateTime.Today))
                _owner.Commit(DateTime.Today);
            return;
        }
        else if (GridArea.Contains(x, y))
            ClickCell(x, y);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        e.Handled = true; // The open calendar takes all keys.
        if (e.Key == Key.Escape)
        {
            _owner.CloseCalendar();
            return;
        }
        if (_view != View.Days)
            return;
        var control = e.Modifiers == KeyModifiers.Control;
        var next = e.Key switch
        {
            Key.Left => DateInput.AddDaysSafe(_cursor, -1),
            Key.Right => DateInput.AddDaysSafe(_cursor, 1),
            Key.Up => DateInput.AddDaysSafe(_cursor, -7),
            Key.Down => DateInput.AddDaysSafe(_cursor, 7),
            Key.PageUp => DateInput.AddMonthsSafe(_cursor, control ? -12 : -1),
            Key.PageDown => DateInput.AddMonthsSafe(_cursor, control ? 12 : 1),
            Key.Home => new DateTime(_cursor.Year, _cursor.Month, 1),
            Key.End => new DateTime(_cursor.Year, _cursor.Month, DateTime.DaysInMonth(_cursor.Year, _cursor.Month)),
            _ => (DateTime?)null,
        };
        if (e.Key == Key.Enter)
        {
            _owner.Commit(_cursor);
            return;
        }
        if (next is not { } date)
            return;
        _cursor = _owner.Clamp(date);
        _month = new DateTime(_cursor.Year, _cursor.Month, 1);
        Invalidate();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, _owner.Background);
        dc.DrawRectangle(bounds, Color.Black);

        // Header: navigation buttons and the title (month and year, year, or decade).
        dc.FillRectangle(Border, Border, Width - 2 * Border, HeaderHeight, ClassicStyle.Face);
        if (_view == View.Days)
        {
            DrawNavButton(dc, PreviousYearButton, ArrowDirection.Left, twice: true, CanNavigate(-12));
            DrawNavButton(dc, NextYearButton, ArrowDirection.Right, twice: true, CanNavigate(12));
        }
        var step = _view switch { View.Days => 1, View.Months => 12, _ => 120 };
        DrawNavButton(dc, PreviousButton, ArrowDirection.Left, twice: false, CanNavigate(-step));
        DrawNavButton(dc, NextButton, ArrowDirection.Right, twice: false, CanNavigate(step));

        var titleFont = _owner.Font with { Style = FontStyle.Bold };
        var decade = _month.Year / 10 * 10;
        var title = _view switch
        {
            View.Days => $"{Capitalize(Culture.DateTimeFormat.GetMonthName(_month.Month))} {_month.Year}",
            View.Months => _month.Year.ToString(Culture),
            _ => $"{decade}–{decade + 9}",
        };
        DrawCentered(dc, title, titleFont, _owner.Color, TitleArea);

        switch (_view)
        {
            case View.Days:
                RenderDays(dc);
                break;
            case View.Months:
                RenderMonths(dc);
                break;
            default:
                RenderYears(dc);
                break;
        }

        // Footer: today.
        var footer = FooterArea;
        dc.FillRectangle(footer.X, footer.Y, footer.Width, 1, ClassicStyle.Shadow);
        // Always the short date: the field's format (e.g. "D") may be wider than the calendar.
        var todayText = Localization.Format("Doqua.Calendar.Today", DateTime.Today.ToString("d", Culture));
        DrawCentered(dc, todayText, _owner.Font, _owner.IsInRange(DateTime.Today) ? _owner.Color : OutOfRange, footer);
    }

    private void RenderDays(DrawingContext dc)
    {
        var font = _owner.Font;
        var area = GridArea;
        var firstDay = (int)Culture.DateTimeFormat.FirstDayOfWeek;

        // Weekday names, then a line.
        for (var column = 0; column < 7; column++)
        {
            var name = Capitalize(Culture.DateTimeFormat.ShortestDayNames[(firstDay + column) % 7]);
            DrawCentered(dc, name, font, ClassicStyle.Shadow, new Rect(area.X + column * CellWidth, area.Y, CellWidth, WeekdayHeight));
        }
        dc.FillRectangle(area.X + 4, area.Y + WeekdayHeight - 1, area.Width - 8, 1, ClassicStyle.Face);

        for (var index = 0; index < 42; index++)
        {
            if (DayAt(index) is not { } day)
                continue;
            var cell = new Rect(area.X + index % 7 * CellWidth, area.Y + WeekdayHeight + index / 7 * CellHeight, CellWidth, CellHeight);
            var inRange = _owner.IsInRange(day);
            var selected = day == _owner.Value;
            if (selected)
                dc.FillRectangle(Inset(cell, 2), _owner.SelectionBackground);
            if (day == DateTime.Today)
                dc.DrawRectangle(Inset(cell, 1), new Color(128, 0, 0)); // Classic "today" frame.
            if (day == _cursor && !selected)
                ClassicStyle.DrawFocusRectangle(dc, Inset(cell, 2), _owner.Color);

            var color = !inRange ? OutOfRange
                : selected ? _owner.SelectionColor
                : day.Month != _month.Month ? ClassicStyle.Shadow
                : _owner.Color;
            DrawCentered(dc, day.Day.ToString(Culture), font, color, cell);
        }
    }

    private void RenderMonths(DrawingContext dc)
    {
        var names = Culture.DateTimeFormat.AbbreviatedMonthNames;
        for (var month = 1; month <= 12; month++)
        {
            var cell = GridCell(month - 1);
            var first = new DateTime(_month.Year, month, 1);
            var inRange = OverlapsRange(first, first.AddDays(DateTime.DaysInMonth(first.Year, month) - 1));
            var current = _owner.Value.Year == first.Year && _owner.Value.Month == month;
            if (current)
                dc.FillRectangle(Inset(cell, 3), _owner.SelectionBackground);
            DrawCentered(dc, Capitalize(names[month - 1].TrimEnd('.')), _owner.Font,
                !inRange ? OutOfRange : current ? _owner.SelectionColor : _owner.Color, cell);
        }
    }

    private void RenderYears(DrawingContext dc)
    {
        var decade = _month.Year / 10 * 10;
        for (var i = 0; i < 12; i++)
        {
            var year = decade - 1 + i; // One year before and after the decade, in grey.
            if (year is < 1 or > 9999)
                continue;
            var cell = GridCell(i);
            var inRange = OverlapsRange(new DateTime(year, 1, 1), new DateTime(year, 12, 31));
            var current = _owner.Value.Year == year;
            if (current)
                dc.FillRectangle(Inset(cell, 3), _owner.SelectionBackground);
            var color = !inRange ? OutOfRange
                : current ? _owner.SelectionColor
                : i is 0 or 11 ? ClassicStyle.Shadow
                : _owner.Color;
            DrawCentered(dc, year.ToString(Culture), _owner.Font, color, cell);
        }
    }

    private void ClickCell(int x, int y)
    {
        var area = GridArea;
        if (_view == View.Days)
        {
            if (y < area.Y + WeekdayHeight)
                return;
            var index = (y - area.Y - WeekdayHeight) / CellHeight * 7 + (x - area.X) / CellWidth;
            if (DayAt(index) is { } day && _owner.IsInRange(day))
                _owner.Commit(day);
            return;
        }

        var cellIndex = (y - area.Y) / (area.Height / 4) * 3 + (x - area.X) / (area.Width / 3);
        if (cellIndex is < 0 or > 11)
            return;
        if (_view == View.Months)
        {
            var first = new DateTime(_month.Year, cellIndex + 1, 1);
            if (!OverlapsRange(first, first.AddDays(DateTime.DaysInMonth(first.Year, first.Month) - 1)))
                return;
            _month = first;
            _view = View.Days;
        }
        else
        {
            var year = _month.Year / 10 * 10 - 1 + cellIndex;
            if (year is < 1 or > 9999 || !OverlapsRange(new DateTime(year, 1, 1), new DateTime(year, 12, 31)))
                return;
            _month = new DateTime(year, Math.Min(_month.Month, 12), 1);
            _view = View.Months;
        }
    }

    /// <summary>Day shown in grid cell <paramref name="index"/> (0..41), starting on the culture's first day of the week.</summary>
    private DateTime? DayAt(int index)
    {
        var firstDay = (int)Culture.DateTimeFormat.FirstDayOfWeek;
        var offset = ((int)_month.DayOfWeek - firstDay + 7) % 7;
        var days = index - offset;
        var limit = days < 0 ? (_month - DateTime.MinValue).Days : (DateTime.MaxValue.Date - _month).Days;
        return Math.Abs(days) > limit ? null : _month.AddDays(days);
    }

    /// <summary>Moves the shown period by <paramref name="months"/> if some of the new period is in range.</summary>
    private void Navigate(int months)
    {
        if (!CanNavigate(months))
            return;
        _month = DateInput.AddMonthsSafe(_month, months);
    }

    private bool CanNavigate(int months)
    {
        var target = DateInput.AddMonthsSafe(_month, months);
        if (target == _month)
            return false; // At the end of the DateTime range.
        var (first, last) = _view switch
        {
            View.Days => (target, target.AddDays(DateTime.DaysInMonth(target.Year, target.Month) - 1)),
            View.Months => (new DateTime(target.Year, 1, 1), new DateTime(target.Year, 12, 31)),
            _ => (new DateTime(Math.Max(1, target.Year / 10 * 10), 1, 1), new DateTime(Math.Min(9999, target.Year / 10 * 10 + 9), 12, 31)),
        };
        return OverlapsRange(first, last);
    }

    private bool OverlapsRange(DateTime first, DateTime last) => last >= _owner.MinDate && first <= _owner.MaxDate;

    private Rect GridCell(int index)
    {
        var area = GridArea;
        int width = area.Width / 3, height = area.Height / 4;
        return new Rect(area.X + index % 3 * width, area.Y + index / 3 * height, width, height);
    }

    private static void DrawNavButton(DrawingContext dc, Rect r, ArrowDirection direction, bool twice, bool enabled) =>
        ClassicScrollBar.DrawArrowButton(dc, r, direction, pressed: false, enabled, arrows: twice ? 2 : 1);

    private static void DrawCentered(DrawingContext dc, string text, Font font, Color color, Rect area)
    {
        var width = font.MeasureText(text).Width;
        dc.DrawText(text, font, color, area.X + (area.Width - width) / 2, area.Y + (area.Height - (font.Ascent + font.Descent)) / 2);
    }

    private string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], Culture) + text[1..];

    private static Rect Inset(Rect r, int amount) =>
        new(r.X + amount, r.Y + amount, Math.Max(0, r.Width - 2 * amount), Math.Max(0, r.Height - 2 * amount));
}
