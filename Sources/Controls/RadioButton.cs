using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Option button of a <see cref="RadioGroup"/>, drawn in the classic style: a sunken circle with a dot.
/// <see cref="Checked"/> is read from the group. Clicking or Space selects the button; the arrow keys
/// select the next or previous button of the group; the whole group is a single Tab stop.
/// Create <see cref="RadioButton{T}"/> instances.
/// </summary>
public abstract class RadioButton : ToggleControl
{
    private RadioGroup? _group;

    protected RadioButton(RadioGroup group, string text)
    {
        ArgumentNullException.ThrowIfNull(group);
        _group = group;
        group.Add(this);
        Text = text;
    }

    /// <summary>The group this button belongs to; null after <see cref="RadioGroup.Remove"/>.</summary>
    public RadioGroup? Group => _group;

    public bool Checked => _group?.SelectedButton == this;

    /// <summary>Raised when this button becomes checked or unchecked, before the group's ValueChanged.</summary>
    public event EventHandler? CheckedChanged;

    protected override int BoxSize => 12;

    /// <summary>
    /// Only one button per group is a Tab stop: the checked one, or the first focusable one if none is
    /// checked (or the checked one cannot take focus).
    /// </summary>
    protected override bool IsTabStop
    {
        get
        {
            if (_group == null)
                return true;
            if (_group.SelectedButton is { CanFocus: true } selected)
                return selected == this;
            return _group.Buttons.FirstOrDefault(button => button.CanFocus) == this;
        }
    }

    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);

    protected override void OnActivated() => _group?.Select(this);

    /// <summary>Arrow keys: focus and select the previous (Up, Left) or next (Down, Right) button.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _group == null || e.Modifiers != KeyModifiers.None)
            return;
        var step = e.Key switch
        {
            Key.Up or Key.Left => -1,
            Key.Down or Key.Right => 1,
            _ => 0,
        };
        if (step == 0)
            return;
        e.Handled = true;
        if (_group.Neighbour(this, step) is { } next)
        {
            next.Focus();
            _group.Select(next);
        }
    }

    /// <summary>
    /// Classic sunken circle, built from offset discs: each darker top-left crescent is a disc shifted
    /// up and left over a lighter one, the way the classic two-tone ring looks.
    /// </summary>
    protected override void DrawBox(DrawingContext dc, Rect box, bool enabled, bool pressed)
    {
        float r = box.Width / 2f, cx = box.X + r, cy = box.Y + r;
        dc.FillEllipse(cx, cy, r, r, HighlightColor);                     // Outer ring, bottom-right.
        dc.FillEllipse(cx - 0.5f, cy - 0.5f, r - 0.5f, r - 0.5f, ShadowColor); // Outer ring, top-left.
        dc.FillEllipse(cx, cy, r - 1, r - 1, FaceColor);                  // Inner ring, bottom-right.
        dc.FillEllipse(cx - 0.5f, cy - 0.5f, r - 1.5f, r - 1.5f, DarkShadowColor); // Inner ring, top-left.
        dc.FillEllipse(cx, cy, r - 2, r - 2, enabled && !pressed ? BoxColor : FaceColor);
        if (Checked)
            dc.FillEllipse(cx, cy, 2, 2, enabled ? Color : DisabledColor);
    }

    internal void RaiseCheckedChanged()
    {
        Invalidate();
        OnCheckedChanged(EventArgs.Empty);
    }

    internal void Detach()
    {
        _group = null;
        Invalidate();
    }
}

/// <summary>Radio button carrying a <see cref="Value"/> for its <see cref="RadioGroup{T}"/>.</summary>
public class RadioButton<T> : RadioButton
{
    public RadioButton(RadioGroup<T> group, T value, string text = "")
        : base(group, text) => Value = value;

    public T Value { get; }
}
