namespace Doqua.Controls;

/// <summary>
/// Non-visual owner of a set of <see cref="RadioButton"/>s: at most one of them is selected, and the
/// group, not the buttons, holds that state. The buttons can be anywhere in the control tree.
/// Use <see cref="RadioGroup{T}"/> to give each button a value.
/// </summary>
public abstract class RadioGroup
{
    private readonly List<RadioButton> _buttons = [];

    /// <summary>Buttons in the order they were created; arrow keys move through them in this order.</summary>
    public IReadOnlyList<RadioButton> Buttons => _buttons;

    /// <summary>The checked button, or null if none is.</summary>
    public RadioButton? SelectedButton { get; private set; }

    /// <summary>Raised after the selection changes, by the user or from code.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Unchecks every button.</summary>
    public void Clear() => Select(null);

    /// <summary>Takes <paramref name="button"/> out of the group; it can no longer be checked.</summary>
    public void Remove(RadioButton button)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (!_buttons.Contains(button))
            return;
        if (button == SelectedButton)
            Select(null);
        _buttons.Remove(button);
        button.Detach();
    }

    protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);

    internal void Add(RadioButton button) => _buttons.Add(button);

    internal void Select(RadioButton? button)
    {
        if (button == SelectedButton)
            return;
        var previous = SelectedButton;
        SelectedButton = button;
        previous?.RaiseCheckedChanged();
        button?.RaiseCheckedChanged();
        OnValueChanged(EventArgs.Empty);
    }

    /// <summary>The next (<paramref name="step"/> 1) or previous (-1) button that can take focus, wrapping around.</summary>
    internal RadioButton? Neighbour(RadioButton button, int step)
    {
        var index = _buttons.IndexOf(button);
        for (var i = 1; i < _buttons.Count; i++)
        {
            var candidate = _buttons[((index + step * i) % _buttons.Count + _buttons.Count) % _buttons.Count];
            if (candidate.CanFocus)
                return candidate;
        }
        return null;
    }
}

/// <summary>
/// Radio group whose buttons carry values of type <typeparamref name="T"/>:
/// <code>
/// var size = new RadioGroup&lt;Size&gt;();
/// panel.Children.Add(new RadioButton&lt;Size&gt;(size, Size.Small, "Small"));
/// size.ValueChanged += (s, e) =&gt; Console.WriteLine(size.Value);
/// </code>
/// </summary>
public class RadioGroup<T> : RadioGroup
{
    /// <summary>True when a button is selected.</summary>
    public bool HasValue => SelectedButton != null;

    /// <summary>
    /// Value of the selected button; default(T) when none is selected (check <see cref="HasValue"/>).
    /// Setting it checks the first button with an equal value, and throws if there is none.
    /// </summary>
    public T? Value
    {
        get => SelectedButton is RadioButton<T> button ? button.Value : default;
        set
        {
            foreach (var button in Buttons)
            {
                if (button is RadioButton<T> typed && EqualityComparer<T>.Default.Equals(typed.Value, value))
                {
                    Select(typed);
                    return;
                }
            }
            throw new ArgumentException($"No radio button in the group has the value '{value}'.", nameof(value));
        }
    }
}
