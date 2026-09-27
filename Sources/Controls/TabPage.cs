using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// A page of a <see cref="TabControl"/>: a <see cref="Panel"/> whose <see cref="Title"/> is shown on its tab.
/// While it belongs to a TabControl, its <see cref="Control.Visible"/> and <see cref="Control.Bounds"/>
/// are managed by the TabControl; set <see cref="Control.Enabled"/> to false to make it unselectable.
/// </summary>
public class TabPage : Panel
{
    private string _title = "";

    public TabPage()
    {
    }

    public TabPage(string title) => Title = title;

    public string Title
    {
        get => _title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _title = value;
            Invalidate(); // Redraws the window, including the tab strip.
        }
    }
}
