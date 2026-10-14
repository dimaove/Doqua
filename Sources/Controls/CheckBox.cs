using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Check box with a title: a 13 x 13 box with a check mark, drawn by <see cref="Style.Current"/>.
/// A left click (on the box or the text) or Space while focused toggles <see cref="Checked"/>.
/// </summary>
public class CheckBox : ToggleControl
{
    private bool _checked;

    /// <summary>Creates a check box without text.</summary>
    public CheckBox()
    {
    }

    /// <summary>Creates a check box with the given text.</summary>
    public CheckBox(string text) => Text = text;

    /// <summary>Raised after <see cref="Checked"/> changes, by the user or from code.</summary>
    public event EventHandler? CheckedChanged;

    /// <summary>Whether the box is checked; a click or Space toggles it.</summary>
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;
            _checked = value;
            Invalidate();
            OnCheckedChanged(EventArgs.Empty);
        }
    }

    protected override int BoxSize => 13;

    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);

    protected override void OnActivated() => Checked = !Checked;

    /// <summary>Draws the box with <see cref="Style.DrawCheckBox"/>.</summary>
    protected override void DrawBox(DrawingContext dc, Rect box, PartState state)
    {
        if (_checked)
            state |= PartState.Checked;
        var enabled = !state.HasFlag(PartState.Disabled);
        Style.Current.DrawCheckBox(dc, box, BoxColor, enabled ? Color : DisabledColor, state);
    }
}
