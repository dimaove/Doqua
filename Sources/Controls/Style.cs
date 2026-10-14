using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>States of a part drawn by a <see cref="Style"/>; combined as flags.</summary>
[Flags]
public enum PartState
{
    /// <summary>Enabled, not under the pointer, not pressed.</summary>
    Normal = 0,

    /// <summary>Under the mouse pointer.</summary>
    Hot = 1,

    /// <summary>Being pressed (mouse button held), or open (a menu title, a drop-down).</summary>
    Pressed = 2,

    /// <summary>Has the keyboard focus (and the window is active).</summary>
    Focused = 4,

    /// <summary>Disabled (the control or one of its parents).</summary>
    Disabled = 8,

    /// <summary>Checked (a check box or radio button).</summary>
    Checked = 16,

    /// <summary>Selected (a tab).</summary>
    Selected = 32,
}

/// <summary>Direction an arrow points (scroll bars, drop-down buttons, spin buttons, calendar navigation).</summary>
public enum ArrowDirection
{
    /// <summary>Up.</summary>
    Up,

    /// <summary>Down.</summary>
    Down,

    /// <summary>Left.</summary>
    Left,

    /// <summary>Right.</summary>
    Right,
}

/// <summary>
/// The look of all controls: a palette of colors and a method for every part the controls draw (button, input frame,
/// check box, tab, scroll bar, menu, ...). The controls draw through <see cref="Current"/>; assigning another style
/// redraws every window at once, so the look can change while the program runs.
/// <para>
/// Doqua has <see cref="ClassicStyle"/> (the default: Windows 9x/2000 3D look) and <see cref="FlatStyle"/> (modern flat
/// design). To make your own, derive from either and override what should look different, or derive from
/// <see cref="Style"/> and implement every part. The colors are init-only properties, so a style can also be adjusted
/// when it is created: <c>Style.Current = new FlatStyle { Accent = Color.FromRgb(0x2E8B57) };</c>
/// </para>
/// <para>
/// Sizes are the same in every style (the controls lay out their parts; a style draws inside the rectangles it is
/// given), so changing the style never moves anything. Colors set on a control (<c>button.Color = ...</c>) win over the
/// style's; colors that were not set follow the current style.
/// </para>
/// </summary>
public abstract class Style
{
    private static Style s_current = new ClassicStyle();

    /// <summary>
    /// The style all controls draw with; <see cref="ClassicStyle"/> by default. Setting it redraws every open window and
    /// raises <see cref="Changed"/>. Use it from the GUI thread.
    /// </summary>
    public static Style Current
    {
        get => s_current;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Application.VerifyAccess();
            if (ReferenceEquals(value, s_current))
                return;
            s_current = value;
            Window.InvalidateAll();
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Raised after <see cref="Current"/> changed.</summary>
    public static event EventHandler? Changed;

    /// <summary>Name of the style, e.g. "Classic".</summary>
    public abstract string Name { get; }

    // ------------------------------------------------------------------ palette

    /// <summary>Surface of buttons, tabs, menus, bars and dialogs.</summary>
    public Color Face { get; init; }

    /// <summary>Face of buttons.</summary>
    public Color ButtonFace { get; init; }

    /// <summary>Background of open menus.</summary>
    public Color MenuBackground { get; init; }

    /// <summary>Background of windows whose <see cref="Window.Background"/> was not set.</summary>
    public Color WindowBackground { get; init; }

    /// <summary>Text of controls, labels and menus.</summary>
    public Color Text { get; init; }

    /// <summary>Text of disabled controls.</summary>
    public Color DisabledText { get; init; }

    /// <summary>Background of input fields, lists, tables and check boxes.</summary>
    public Color FieldBackground { get; init; }

    /// <summary>Background of disabled input fields.</summary>
    public Color DisabledFieldBackground { get; init; }

    /// <summary>Background of the selected item in lists, tables, trees, combo boxes and calendars.</summary>
    public Color SelectionBackground { get; init; }

    /// <summary>Text on <see cref="SelectionBackground"/>.</summary>
    public Color SelectionText { get; init; }

    /// <summary>Background of the selected item while its control does not have the focus.</summary>
    public Color InactiveSelectionBackground { get; init; }

    /// <summary>Background of selected text in <see cref="Input"/> and <see cref="TextArea"/>.</summary>
    public Color TextSelectionBackground { get; init; }

    /// <summary>Text on <see cref="TextSelectionBackground"/>.</summary>
    public Color TextSelectionText { get; init; }

    /// <summary>Background of the highlighted item of a menu.</summary>
    public Color MenuSelectionBackground { get; init; }

    /// <summary>Text of the highlighted item of a menu.</summary>
    public Color MenuSelectionText { get; init; }

    /// <summary>Placeholder text (shown in an empty <see cref="ComboBox{T}"/>).</summary>
    public Color PlaceholderText { get; init; }

    /// <summary>Grid lines of a <see cref="Table"/>.</summary>
    public Color GridLine { get; init; }

    /// <summary>Lines and expand boxes of a <see cref="TreeView"/>.</summary>
    public Color TreeLine { get; init; }

    /// <summary>Whether a <see cref="TreeView"/> draws the dotted lines between nodes.</summary>
    public bool TreeLines { get; init; } = true;

    /// <summary>
    /// Whether a focused drop-down field (<see cref="ComboBox{T}"/>, <see cref="DateInput"/>) shows its value highlighted in the
    /// selection colors, as in the classic style (true by default); when false, only the field's focus look shows it.
    /// </summary>
    public bool HighlightFocusedValue { get; init; } = true;

    /// <summary>How far text and glyphs move down and right while their button is pressed (1 in the classic style).</summary>
    public int PressedOffset { get; init; }

    // ------------------------------------------------------------------ parts

    /// <summary>
    /// A push button with its caption, centred in <paramref name="bounds"/>: face, frame, text and focus mark.
    /// <paramref name="face"/> and <paramref name="textColor"/> are the button's colors (the style's unless set).
    /// </summary>
    public abstract void DrawButton(DrawingContext dc, Rect bounds, string text, Font font, Color face, Color textColor, PartState state);

    /// <summary>
    /// A small square button without text: the arrow of a combo box, the spin buttons, scroll bar arrows, "...".
    /// The caller draws the glyph on top (see <see cref="GlyphColor"/> and <see cref="PressedOffset"/>).
    /// </summary>
    public abstract void DrawToolButton(DrawingContext dc, Rect bounds, PartState state);

    /// <summary>An arrow (or <paramref name="count"/> arrows side by side, e.g. 2 for "«") centred in <paramref name="bounds"/>.</summary>
    public abstract void DrawArrow(DrawingContext dc, Rect bounds, ArrowDirection direction, Color color, int count = 1);

    /// <summary>Color of glyphs (arrows, dots, marks) on tool buttons and in boxes, for the given state.</summary>
    public virtual Color GlyphColor(PartState state) => state.HasFlag(PartState.Disabled) ? DisabledText : Text;

    /// <summary>
    /// The frame and background of an input field, list, table or tree. The frame takes the outer 2 pixels; the
    /// control draws its content inside.
    /// </summary>
    public abstract void DrawField(DrawingContext dc, Rect bounds, Color background, PartState state);

    /// <summary>A check box (13 x 13), checked when <paramref name="state"/> has <see cref="PartState.Checked"/>.</summary>
    public abstract void DrawCheckBox(DrawingContext dc, Rect box, Color background, Color mark, PartState state);

    /// <summary>A radio button (12 x 12), selected when <paramref name="state"/> has <see cref="PartState.Checked"/>.</summary>
    public abstract void DrawRadioButton(DrawingContext dc, Rect box, Color background, Color mark, PartState state);

    /// <summary>Text of a control in its state: enabled text in <paramref name="color"/>, disabled text in the style's disabled look.</summary>
    public abstract void DrawText(DrawingContext dc, string text, Font font, Color color, int x, int y, PartState state);

    /// <summary>The keyboard focus mark around <paramref name="bounds"/> (e.g. around the text of a check box).</summary>
    public abstract void DrawFocusRectangle(DrawingContext dc, Rect bounds, Color color);

    /// <summary>The body of a tab control, below the tabs; <paramref name="selectedTab"/> is where the selected tab joins it.</summary>
    public abstract void DrawTabPanel(DrawingContext dc, Rect bounds, Rect selectedTab, Color face);

    /// <summary>One tab (without its title, which the control draws with <see cref="DrawText"/>); selected with <see cref="PartState.Selected"/>.</summary>
    public abstract void DrawTab(DrawingContext dc, Rect bounds, Color face, PartState state);

    /// <summary>The frame of a group box; the top line leaves out [<paramref name="gapLeft"/>, <paramref name="gapRight"/>) for the title (gapLeft &lt; 0: no gap).</summary>
    public abstract void DrawGroupBox(DrawingContext dc, Rect frame, int gapLeft, int gapRight);

    /// <summary>
    /// A scroll bar's track and thumb (the arrow buttons are drawn with <see cref="DrawScrollBarButton"/>).
    /// <paramref name="pressedTrack"/> is the part of the track being held, if any.
    /// </summary>
    public abstract void DrawScrollBar(DrawingContext dc, Rect track, Rect thumb, bool horizontal, Rect? pressedTrack, PartState thumbState);

    /// <summary>An arrow button of a scroll bar (also used for small navigation buttons).</summary>
    public virtual void DrawScrollBarButton(DrawingContext dc, Rect bounds, ArrowDirection direction, PartState state, int arrows = 1)
    {
        DrawToolButton(dc, bounds, state);
        var shift = state.HasFlag(PartState.Pressed) ? PressedOffset : 0;
        DrawArrow(dc, new Rect(bounds.X + shift, bounds.Y + shift, bounds.Width, bounds.Height), direction, GlyphColor(state), arrows);
    }

    /// <summary>The square where a horizontal and a vertical scroll bar meet.</summary>
    public virtual void DrawScrollBarCorner(DrawingContext dc, Rect bounds) => dc.FillRectangle(bounds, Face);

    /// <summary>A column header cell of a table (without its text).</summary>
    public abstract void DrawHeaderCell(DrawingContext dc, Rect bounds, PartState state);

    /// <summary>The bar of a <see cref="SplitContainer"/>.</summary>
    public abstract void DrawSplitter(DrawingContext dc, Rect bounds, bool vertical, PartState state);

    /// <summary>The expand box of a tree node (+ when collapsed, - when expanded), centred in <paramref name="box"/>.</summary>
    public abstract void DrawTreeExpander(DrawingContext dc, Rect box, bool expanded, Color background, PartState state);

    /// <summary>The background and frame of an open menu.</summary>
    public abstract void DrawMenu(DrawingContext dc, Rect bounds, Color background);

    /// <summary>The background of the highlighted menu item.</summary>
    public virtual void DrawMenuHighlight(DrawingContext dc, Rect bounds, Color background) => dc.FillRectangle(bounds, background);

    /// <summary>A separator line between groups of menu items, centred vertically in <paramref name="bounds"/>.</summary>
    public abstract void DrawMenuSeparator(DrawingContext dc, Rect bounds);

    /// <summary>A title of a menu bar (without its text): <see cref="PartState.Hot"/> under the pointer, <see cref="PartState.Pressed"/> while its menu is open.</summary>
    public abstract void DrawMenuBarItem(DrawingContext dc, Rect bounds, PartState state);

    /// <summary>The background and frame of a drop-down: a combo box list, a calendar, the file browser.</summary>
    public abstract void DrawPopup(DrawingContext dc, Rect bounds, Color background);
}
