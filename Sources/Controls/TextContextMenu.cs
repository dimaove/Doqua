using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>The Cut / Copy / Paste / Select all context menu shared by <see cref="Input"/> and <see cref="TextArea"/>.</summary>
internal static class TextContextMenu
{
    /// <summary>
    /// Builds the menu: the editing commands (enabled as they apply), then the items of the user's
    /// <paramref name="userMenu"/> if it has any. Choosing one of the user's items raises its Click and the
    /// user menu's Closed event (with <paramref name="owner"/> as its Owner); the user menu's style is used.
    /// </summary>
    public static PopupMenu Create(Control owner, PopupMenu? userMenu, bool hasSelection, bool canSelectAll,
        Action cut, Action copy, Action paste, Action selectAll)
    {
        var menu = new PopupMenu();
        if (userMenu != null)
        {
            (menu.Font, menu.Color, menu.DisabledColor, menu.Background, menu.SelectionBackground, menu.SelectionColor) =
                (userMenu.Font, userMenu.Color, userMenu.DisabledColor, userMenu.Background, userMenu.SelectionBackground, userMenu.SelectionColor);
        }

        menu.Items.Add(Command("Cut", "Ctrl+X", hasSelection, cut));
        menu.Items.Add(Command("Copy", "Ctrl+C", hasSelection, copy));
        menu.Items.Add(Command("Paste", "Ctrl+V", Clipboard.GetText() is { Length: > 0 }, paste));
        menu.Items.Add(MenuItem.Separator());
        menu.Items.Add(Command("Select all", "Ctrl+A", canSelectAll, selectAll));

        if (userMenu is { Items.Count: > 0 })
        {
            menu.Items.Add(MenuItem.Separator());
            menu.Items.AddRange(userMenu.Items);
            menu.Closed += (sender, e) => userMenu.RaiseClosed(owner, e.SelectedItem);
        }
        return menu;
    }

    private static MenuItem Command(string text, string shortcut, bool enabled, Action action)
    {
        var item = new MenuItem(text, shortcut) { Enabled = enabled };
        item.Click += (sender, e) => action();
        return item;
    }
}
