using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>What a <see cref="FileInput"/> chooses.</summary>
public enum FileInputMode
{
    /// <summary>A file; the browser shows folders and the files that match the selected filter.</summary>
    File,

    /// <summary>A folder; the browser shows folders only.</summary>
    Folder,
}

/// <summary>
/// A path field with a "..." button that opens a file browser below the field (above it if there is no room).
/// The path can also be typed. <see cref="Error"/> tells whether it satisfies the options (<see cref="MustExist"/>,
/// <see cref="Required"/>, <see cref="MatchFilter"/>); invalid text is drawn in <see cref="InvalidColor"/>.
/// <list type="bullet">
/// <item>Field: F4 or Alt+Down open the browser.</item>
/// <item>Browser: click selects, double click (or Enter) opens a folder or chooses a file; Backspace or Alt+Up
/// go to the parent folder; arrows, PageUp / PageDown, Home / End move; a letter jumps to the next name
/// starting with it; Escape or a click outside closes it.</item>
/// </list>
/// Relative paths are resolved against <see cref="BaseDirectory"/> (the current directory if it is null).
/// </summary>
public class FileInput : Control
{
    private const int FrameSize = 2;
    private const int ButtonWidth = 22;

    private readonly Input _input;
    private FileInputBrowser? _browser;
    private FileInputMode _mode;
    private bool _mustExist = true;
    private bool _required;
    private bool _matchFilter;
    private int _filterIndex;
    private string? _baseDirectory;
    private Color _color = Color.Black;
    private Color _invalidColor = new(192, 0, 0);
    private bool _invalid;

    /// <summary>Creates an empty file input, 260 pixels wide.</summary>
    public FileInput()
    {
        _input = new Input { ShowFrame = false };
        _input.TextChanged += (sender, e) =>
        {
            UpdateValidity();
            OnTextChanged(EventArgs.Empty);
        };
        Children = [_input];
        _input.Parent = this;
        Width = 260;
        Height = Math.Max(22, _input.Font.LineHeight + 8);
    }

    /// <summary>Raised after <see cref="Text"/> changes: typed, chosen in the browser, or set from code.</summary>
    public event EventHandler? TextChanged;

    /// <summary>Raised after a file or folder was chosen in the browser (after <see cref="TextChanged"/>).</summary>
    public event EventHandler? FileChosen;

    /// <summary>The path as typed or chosen; relative paths are relative to <see cref="BaseDirectory"/>.</summary>
    public string Text
    {
        get => _input.Text;
        set
        {
            _input.Text = value;
            _input.CaretIndex = _input.Text.Length;
        }
    }

    /// <summary>
    /// <see cref="Text"/> as an absolute path (resolved against <see cref="BaseDirectory"/>), or "" if the text is
    /// empty or not a valid path.
    /// </summary>
    public string FullPath => Resolve(Text.Trim()) ?? "";

    /// <summary>Whether a file (the default) or a folder is chosen.</summary>
    public FileInputMode Mode
    {
        get => _mode;
        set => SetOption(ref _mode, value);
    }

    /// <summary>
    /// When true (the default), the file or folder must exist. When false, a new name may be typed, but the folder
    /// it would be in must exist; the browser's Select button then chooses the shown folder with the typed file name.
    /// </summary>
    public bool MustExist
    {
        get => _mustExist;
        set => SetOption(ref _mustExist, value);
    }

    /// <summary>When true, an empty path is an error; when false (the default), empty means "no file".</summary>
    public bool Required
    {
        get => _required;
        set => SetOption(ref _required, value);
    }

    /// <summary>
    /// File masks offered by the browser, e.g. <c>new FileFilter("Keys", "*.pem", "*.key")</c> and
    /// <see cref="FileFilter.AllFiles"/>. Empty (the default) shows all files. Ignored in <see cref="FileInputMode.Folder"/> mode.
    /// </summary>
    public List<FileFilter> Filters { get; } = [];

    /// <summary>Index into <see cref="Filters"/> of the filter the browser starts with; the user's last choice is kept here.</summary>
    public int FilterIndex
    {
        get => Filters.Count == 0 ? 0 : Math.Clamp(_filterIndex, 0, Filters.Count - 1);
        set => _filterIndex = Math.Max(0, value);
    }

    /// <summary>When true, a file name that matches none of the <see cref="Filters"/> is an error (default false).</summary>
    public bool MatchFilter
    {
        get => _matchFilter;
        set => SetOption(ref _matchFilter, value);
    }

    /// <summary>
    /// Folder that relative paths are resolved against, or null (the default) for the current directory.
    /// With <see cref="RelativePaths"/>, chosen paths inside it are written relative to it.
    /// </summary>
    public string? BaseDirectory
    {
        get => _baseDirectory;
        set => SetOption(ref _baseDirectory, string.IsNullOrEmpty(value) ? null : Path.GetFullPath(value));
    }

    /// <summary>When true, a path chosen inside <see cref="BaseDirectory"/> is written relative to it (default false).</summary>
    public bool RelativePaths { get; set; }

    /// <summary>
    /// Folder the browser opens while the field is empty or its folder does not exist, or null (the default) for
    /// <see cref="BaseDirectory"/>, then the current directory.
    /// </summary>
    public string? InitialDirectory { get; set; }

    /// <summary>Whether the browser lists hidden (and, on Windows, system) files and folders; default false.</summary>
    public bool ShowHidden { get; set; }

    /// <summary>
    /// Why the current path is not acceptable (in the current language), or null if it is. Checked against the file
    /// system each time it is read.
    /// </summary>
    public string? Error
    {
        get
        {
            var error = Validate();
            SetInvalid(error != null);
            return error;
        }
    }

    /// <summary>True if <see cref="Error"/> is null.</summary>
    public bool IsValid => Error == null;

    /// <summary>True while the file browser is open.</summary>
    public bool IsDroppedDown => _browser != null;

    /// <summary>Font of the field and of the browser.</summary>
    public Font Font
    {
        get => _input.Font;
        set => _input.Font = value;
    }

    /// <summary>Color of a valid path.</summary>
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            UpdateInputColor();
        }
    }

    /// <summary>Color of a path that is not valid (see <see cref="Error"/>); dark red by default.</summary>
    public Color InvalidColor
    {
        get => _invalidColor;
        set
        {
            _invalidColor = value;
            UpdateInputColor();
        }
    }

    /// <summary>Background of the text field.</summary>
    public Color Background
    {
        get => _input.Background;
        set => _input.Background = value;
    }

    private IReadOnlyList<Control> Children { get; }

    protected override IReadOnlyList<Control> VisualChildren => Children;

    /// <summary>Opens the file browser at the folder of the current path.</summary>
    public void ShowBrowser()
    {
        if (_browser != null || GetWindow() is not { } window || !IsEffectivelyEnabled)
            return;
        var browser = new FileInputBrowser(this)
        {
            Width = Math.Min(Math.Max(Width, 380), window.Width),
            Height = Math.Min(320, window.Height),
        };
        var (x, below) = PointToWindow(0, Height);
        var above = below - Height - browser.Height;
        browser.X = Math.Clamp(x, 0, Math.Max(0, window.Width - browser.Width));
        browser.Y = below + browser.Height <= window.Height ? below : above >= 0 ? above : Math.Max(0, window.Height - browser.Height);
        _browser = browser;
        window.OpenPopup(browser, () =>
        {
            _browser = null;
            Invalidate();
        });
        browser.Open(StartLocation());
        Invalidate();
    }

    /// <summary>Closes the file browser without changing the path.</summary>
    public void CloseBrowser() => GetWindow()?.ClosePopup();

    protected virtual void OnTextChanged(EventArgs e) => TextChanged?.Invoke(this, e);

    protected virtual void OnFileChosen(EventArgs e) => FileChosen?.Invoke(this, e);

    protected override void OnSizeChanged(EventArgs e)
    {
        _input.Bounds = Rect.FromEdges(FrameSize, FrameSize, Math.Max(FrameSize, Width - FrameSize - ButtonWidth), Math.Max(FrameSize, Height - FrameSize));
        base.OnSizeChanged(e);
    }

    /// <summary>F4 and Alt+Down arrive here from the focused text field.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        if (e.Key == Key.F4 && e.Modifiers == KeyModifiers.None || e.Key == Key.Down && e.Modifiers == KeyModifiers.Alt)
        {
            e.Handled = true;
            ShowBrowser();
        }
    }

    /// <summary>Only the button gets here: the text field covers the rest.</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButton.Left)
            return;
        _input.Focus();
        ShowBrowser();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var enabled = IsEffectivelyEnabled;
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, enabled ? _input.Background : _input.DisabledBackground);
        ClassicStyle.DrawSunkenEdge(dc, bounds, ClassicStyle.Highlight, ClassicStyle.Face, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        var button = Rect.FromEdges(Math.Max(FrameSize, Width - FrameSize - ButtonWidth), FrameSize, Width - FrameSize, Height - FrameSize);
        var pressed = IsDroppedDown;
        dc.FillRectangle(button, ClassicStyle.Face);
        if (pressed)
            dc.DrawRectangle(button, ClassicStyle.Shadow);
        else
            ClassicStyle.DrawRaisedEdge(dc, button, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        // Three dots, like the classic "Browse..." button.
        var shift = pressed ? 1 : 0;
        var dotsX = button.X + (button.Width - 8) / 2 + shift;
        var dotsY = button.Y + button.Height / 2 + 2 + shift;
        var ink = enabled ? Color.Black : ClassicStyle.Shadow;
        for (var i = 0; i < 3; i++)
        {
            if (!enabled)
                dc.FillRectangle(dotsX + i * 3 + 1, dotsY + 1, 2, 2, ClassicStyle.Highlight);
            dc.FillRectangle(dotsX + i * 3, dotsY, 2, 2, ink);
        }
    }

    /// <summary>The browser chose <paramref name="fullPath"/>: write it (relative if configured), close and notify.</summary>
    internal void Choose(string fullPath)
    {
        CloseBrowser();
        Text = MakeRelative(fullPath);
        _input.Focus();
        OnFileChosen(EventArgs.Empty);
    }

    /// <summary>File name typed in the field (for Select in <see cref="MustExist"/> = false mode), or "".</summary>
    internal string TypedFileName
    {
        get
        {
            var text = Text.Trim();
            if (text.Length == 0 || text.EndsWith(Path.DirectorySeparatorChar) || text.EndsWith(Path.AltDirectorySeparatorChar))
                return "";
            try
            {
                return Path.GetFileName(text);
            }
            catch (ArgumentException)
            {
                return "";
            }
        }
    }

    /// <summary>Folder to open, and the entry to select in it (or null).</summary>
    private (string? Folder, string? Selected) StartLocation()
    {
        if (Resolve(Text.Trim()) is { } full)
        {
            if (File.Exists(full) || Directory.Exists(full) && _mode == FileInputMode.Folder)
                return (Path.GetDirectoryName(full) ?? full, full);
            if (Directory.Exists(full))
                return (full, null);
            // Nearest existing parent folder of a path that does not exist (yet).
            for (var folder = Path.GetDirectoryName(full); folder != null; folder = Path.GetDirectoryName(folder))
            {
                if (Directory.Exists(folder))
                    return (folder, null);
            }
        }
        foreach (var candidate in new[] { InitialDirectory, _baseDirectory, Environment.CurrentDirectory })
        {
            if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
                return (Path.GetFullPath(candidate), null);
        }
        return (null, null);
    }

    private string? Validate()
    {
        var text = Text.Trim();
        if (text.Length == 0)
            return _required ? Localization.Get(_mode == FileInputMode.File ? "Doqua.FileInput.FileRequired" : "Doqua.FileInput.FolderRequired") : null;
        if (Resolve(text) is not { } full)
            return Localization.Get("Doqua.FileInput.InvalidPath");

        if (_mode == FileInputMode.Folder)
        {
            if (File.Exists(full))
                return Localization.Get("Doqua.FileInput.IsFile");
            if (_mustExist && !Directory.Exists(full))
                return Localization.Get("Doqua.FileInput.FolderNotFound");
            return null;
        }

        if (Directory.Exists(full))
            return Localization.Get("Doqua.FileInput.IsFolder");
        if (_mustExist && !File.Exists(full))
            return Localization.Get("Doqua.FileInput.FileNotFound");
        if (!_mustExist && Path.GetDirectoryName(full) is { } folder && !Directory.Exists(folder))
            return Localization.Get("Doqua.FileInput.FolderOfFileNotFound");
        if (_matchFilter && Filters.Count > 0 && !Filters.Any(filter => filter.Matches(full)))
            return Localization.Format("Doqua.FileInput.FilterMismatch", string.Join(", ", Filters.SelectMany(f => f.Patterns).Distinct()));
        return null;
    }

    /// <summary>Absolute form of <paramref name="text"/>, or null if it is empty or not a valid path.</summary>
    private string? Resolve(string text)
    {
        if (text.Length == 0)
            return null;
        try
        {
            return _baseDirectory != null ? Path.GetFullPath(text, _baseDirectory) : Path.GetFullPath(text);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private string MakeRelative(string fullPath)
    {
        if (!RelativePaths || _baseDirectory == null)
            return fullPath;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var root = Path.TrimEndingDirectorySeparator(_baseDirectory) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, comparison) ? fullPath[root.Length..] : fullPath;
    }

    private void UpdateValidity() => SetInvalid(Validate() != null);

    private void SetInvalid(bool invalid)
    {
        if (_invalid == invalid)
            return;
        _invalid = invalid;
        UpdateInputColor();
    }

    private void UpdateInputColor() => _input.Color = _invalid ? _invalidColor : _color;

    private void SetOption<T>(ref T field, T value)
    {
        field = value;
        UpdateValidity();
    }
}

/// <summary>The open file browser of a <see cref="FileInput"/>, hosted by the window above its content.</summary>
internal sealed class FileInputBrowser : Control
{
    private const int Border = 1;
    private const int Margin = 4;
    private const int HeaderHeight = 28;
    private const int FooterRowHeight = 30;
    private const int ButtonWidth = 80;
    private const int ButtonHeight = 24;
    private const int SmallButton = 20;
    private const int IconSize = 16;
    private const int WheelRows = 3;

    private readonly FileInput _owner;
    private readonly ClassicScrollBar _scrollBar;
    private readonly List<Entry> _entries = [];
    private string? _folder;     // Shown folder; null = the list of drives (Windows).
    private string? _message;    // Shown instead of entries, e.g. when the folder cannot be read.
    private int _filterIndex;
    private int _top;
    private int _highlighted = -1;
    private Part _pressed;

    private enum Kind
    {
        Drive,
        Folder,
        File,
    }

    private enum Part
    {
        None,
        Up,
        Select,
        Cancel,
        PreviousFilter,
        NextFilter,
    }

    private readonly record struct Entry(string Name, string Path, Kind Kind);

    public FileInputBrowser(FileInput owner)
    {
        _owner = owner;
        _filterIndex = owner.FilterIndex;
        _scrollBar = new ClassicScrollBar(this, () => _top, ScrollTo);
        Cursor = Cursor.Arrow;
    }

    private Font Font => _owner.Font;

    private int RowHeight => Math.Max(IconSize + 2, Font.LineHeight + 2);

    private bool HasFilterRow => _owner.Mode == FileInputMode.File && _owner.Filters.Count > 0;

    private FileFilter? Filter => HasFilterRow ? _owner.Filters[Math.Clamp(_filterIndex, 0, _owner.Filters.Count - 1)] : null;

    private int FooterHeight => FooterRowHeight * (HasFilterRow ? 2 : 1);

    private Rect UpButton => new(Border + Margin, Border + (HeaderHeight - SmallButton) / 2, SmallButton, SmallButton);

    private Rect PathArea => Rect.FromEdges(UpButton.Right + 6, Border, Width - Border - Margin, Border + HeaderHeight);

    /// <summary>The sunken list frame; rows are inside its 2 px edge.</summary>
    private Rect ListFrame => Rect.FromEdges(Border + Margin, Border + HeaderHeight, Width - Border - Margin, Height - Border - FooterHeight);

    private Rect ListInner
    {
        get
        {
            var frame = ListFrame;
            var right = frame.Right - 2 - (HasScrollBar ? ClassicScrollBar.Thickness : 0);
            return Rect.FromEdges(frame.X + 2, frame.Y + 2, Math.Max(frame.X + 2, right), Math.Max(frame.Y + 2, frame.Bottom - 2));
        }
    }

    private int VisibleRows => Math.Max(1, (ListFrame.Height - 4) / RowHeight);

    private bool HasScrollBar => _entries.Count > VisibleRows && ListFrame.Height - 4 >= 2 * ClassicScrollBar.Thickness;

    private Rect CancelButton => new(Width - Border - Margin - ButtonWidth, Height - Border - (FooterRowHeight + ButtonHeight) / 2, ButtonWidth, ButtonHeight);

    private Rect SelectButton => CancelButton with { X = CancelButton.X - 6 - ButtonWidth };

    private Rect FilterRow => new(Border + Margin, Height - Border - FooterHeight + (FooterRowHeight - SmallButton) / 2, Width - 2 * (Border + Margin), SmallButton);

    private Rect PreviousFilterButton => FilterRow with { Width = SmallButton };

    private Rect NextFilterButton => FilterRow with { X = FilterRow.X + SmallButton + 2, Width = SmallButton };

    /// <summary>Loads the start folder, selecting <paramref name="start"/>.Selected in it.</summary>
    public void Open((string? Folder, string? Selected) start) => Navigate(start.Folder, start.Selected);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButton.Left)
            return;
        var (x, y) = (e.X, e.Y);
        if (HasScrollBar)
        {
            SyncScrollBar();
            if (_scrollBar.Bounds.Contains(x, y))
            {
                _scrollBar.Press(x, y);
                return;
            }
        }
        if (PartAt(x, y) is var part and not Part.None)
        {
            if (IsPartEnabled(part))
            {
                _pressed = part;
                Invalidate();
            }
            return;
        }
        if (IndexAt(x, y) is var index and >= 0)
        {
            Highlight(index);
            if (e.ClickCount == 2)
                Activate(index);
        }
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        _scrollBar.Drag(e.X, e.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _scrollBar.Release();
        var pressed = _pressed;
        if (pressed == Part.None)
            return;
        _pressed = Part.None;
        Invalidate();
        if (PartAt(e.X, e.Y) == pressed)
            Perform(pressed);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = true;
        ScrollTo(_top - e.Delta * WheelRows);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        e.Handled = true; // The open browser takes all keys.
        var none = e.Modifiers == KeyModifiers.None;
        var last = _entries.Count - 1;
        switch (e.Key)
        {
            case Key.Escape:
                _owner.CloseBrowser();
                return;
            case Key.Enter:
                if (_highlighted >= 0)
                    Activate(_highlighted);
                else if (IsPartEnabled(Part.Select))
                    Perform(Part.Select);
                return;
            case Key.Backspace:
            case Key.Up when e.Modifiers == KeyModifiers.Alt:
                GoUp();
                return;
            case Key.Up when none:
                MoveHighlight(_highlighted < 0 ? 0 : _highlighted - 1);
                return;
            case Key.Down when none:
                MoveHighlight(_highlighted + 1);
                return;
            case Key.PageUp when none:
                MoveHighlight(_highlighted - VisibleRows);
                return;
            case Key.PageDown when none:
                MoveHighlight(Math.Max(0, _highlighted) + VisibleRows);
                return;
            case Key.Home when none:
                MoveHighlight(0);
                return;
            case Key.End when none:
                MoveHighlight(last);
                return;
        }
        if (none && KeyChar(e.Key) is { } ch)
            JumpTo(ch);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, ClassicStyle.Face);
        dc.DrawRectangle(bounds, Color.Black);
        var font = Font;
        var color = _owner.Color;

        // Header: Up button and the folder.
        DrawArrow(dc, UpButton, ArrowDirection.Up, Part.Up);
        var path = PathArea;
        var folderText = FitLeft(_folder ?? Localization.Get("Doqua.FileInput.Computer"), font with { Style = FontStyle.Bold }, path.Width);
        dc.DrawText(folderText, font with { Style = FontStyle.Bold }, color, path.X, path.Y + (path.Height - (font.Ascent + font.Descent)) / 2);

        // List.
        var frame = ListFrame;
        dc.FillRectangle(frame, _owner.Background);
        ClassicStyle.DrawSunkenEdge(dc, frame, ClassicStyle.Highlight, ClassicStyle.Face, ClassicStyle.Shadow, ClassicStyle.DarkShadow);
        var inner = ListInner;
        using (dc.PushClip(inner))
        {
            if (_message != null)
                dc.DrawText(_message, font, ClassicStyle.Shadow, inner.X + 4, inner.Y + 4);
            var textOffset = (RowHeight - (font.Ascent + font.Descent)) / 2;
            for (var i = 0; i < VisibleRows + 1 && _top + i < _entries.Count; i++)
            {
                var index = _top + i;
                var entry = _entries[index];
                var y = inner.Y + i * RowHeight;
                var highlighted = index == _highlighted;
                var nameX = inner.X + 4 + IconSize + 4;
                if (highlighted)
                {
                    var width = Math.Min(inner.Right - nameX + 2, font.MeasureText(entry.Name).Width + 4);
                    dc.FillRectangle(nameX - 2, y, width, RowHeight, SelectionBackground);
                }
                DrawIcon(dc, entry.Kind, inner.X + 4, y + (RowHeight - IconSize) / 2);
                dc.DrawText(entry.Name, font, highlighted ? Color.White : color, nameX, y + textOffset);
            }
        }
        if (HasScrollBar)
        {
            SyncScrollBar();
            _scrollBar.Draw(dc, enabled: true);
        }

        // Footer: filter row, then the buttons.
        if (HasFilterRow)
        {
            DrawArrow(dc, PreviousFilterButton, ArrowDirection.Left, Part.PreviousFilter);
            DrawArrow(dc, NextFilterButton, ArrowDirection.Right, Part.NextFilter);
            var row = FilterRow;
            var textX = NextFilterButton.Right + 6;
            var filterText = FitRight(Filter!.ToString(), font, row.Right - textX);
            dc.DrawText(filterText, font, color, textX, row.Y + (row.Height - (font.Ascent + font.Descent)) / 2);
        }
        DrawButton(dc, SelectButton, Localization.Get("Doqua.FileInput.Select"), Part.Select);
        DrawButton(dc, CancelButton, Localization.Get("Doqua.FileInput.Cancel"), Part.Cancel);
    }

    private static Color SelectionBackground => new(0, 0, 128);

    private Part PartAt(int x, int y)
    {
        if (UpButton.Contains(x, y))
            return Part.Up;
        if (SelectButton.Contains(x, y))
            return Part.Select;
        if (CancelButton.Contains(x, y))
            return Part.Cancel;
        if (HasFilterRow && PreviousFilterButton.Contains(x, y))
            return Part.PreviousFilter;
        if (HasFilterRow && NextFilterButton.Contains(x, y))
            return Part.NextFilter;
        return Part.None;
    }

    private bool IsPartEnabled(Part part) => part switch
    {
        Part.Up => _folder != null && (Path.GetDirectoryName(_folder) != null || OperatingSystem.IsWindows()),
        Part.Select => SelectTarget() != null,
        Part.PreviousFilter or Part.NextFilter => _owner.Filters.Count > 1,
        _ => true,
    };

    private void Perform(Part part)
    {
        switch (part)
        {
            case Part.Up:
                GoUp();
                break;
            case Part.Cancel:
                _owner.CloseBrowser();
                break;
            case Part.Select:
                if (SelectTarget() is { } target)
                {
                    if (target.Kind is Kind.Drive or Kind.Folder && _owner.Mode == FileInputMode.File)
                        Navigate(target.Path, null);
                    else
                        _owner.Choose(target.Path);
                }
                break;
            case Part.PreviousFilter or Part.NextFilter:
                var count = _owner.Filters.Count;
                _filterIndex = ((_filterIndex + (part == Part.NextFilter ? 1 : -1)) % count + count) % count;
                _owner.FilterIndex = _filterIndex;
                Navigate(_folder, _highlighted >= 0 ? _entries[_highlighted].Path : null);
                break;
        }
    }

    /// <summary>
    /// What Select acts on: the highlighted entry; with nothing highlighted, the shown folder in folder mode, or the
    /// shown folder plus the typed file name when the file need not exist.
    /// </summary>
    private Entry? SelectTarget()
    {
        if (_highlighted >= 0)
            return _entries[_highlighted];
        if (_folder == null)
            return null;
        if (_owner.Mode == FileInputMode.Folder)
            return new Entry("", _folder, Kind.Folder);
        if (!_owner.MustExist && _owner.TypedFileName is { Length: > 0 } name)
            return new Entry(name, Path.Combine(_folder, name), Kind.File);
        return null;
    }

    /// <summary>Double click or Enter: open a folder or drive, choose a file.</summary>
    private void Activate(int index)
    {
        var entry = _entries[index];
        if (entry.Kind == Kind.File)
            _owner.Choose(entry.Path);
        else
            Navigate(entry.Path, null);
    }

    private void GoUp()
    {
        if (_folder == null)
            return;
        var parent = Path.GetDirectoryName(_folder);
        if (parent == null && !OperatingSystem.IsWindows())
            return; // "/" has no parent.
        Navigate(parent, _folder); // On Windows, above a drive's root is the list of drives.
    }

    /// <summary>Shows <paramref name="folder"/> (null = drives), selecting the entry whose path is <paramref name="select"/>.</summary>
    private void Navigate(string? folder, string? select)
    {
        // "/usr/bin/" -> "/usr/bin", so that the parent is "/usr" (roots such as "/" and "C:\" are kept).
        folder = folder == null ? null : Path.TrimEndingDirectorySeparator(folder);
        _folder = folder;
        _entries.Clear();
        _message = null;
        _top = 0;
        _highlighted = -1;
        try
        {
            if (folder == null)
                LoadDrives();
            else
                LoadFolder(folder);
            if (_entries.Count == 0)
                _message = Localization.Get("Doqua.FileInput.Empty");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            _entries.Clear();
            _message = Localization.Get("Doqua.FileInput.CannotRead");
        }

        if (select != null)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var target = Path.TrimEndingDirectorySeparator(select);
            _highlighted = _entries.FindIndex(entry => string.Equals(Path.TrimEndingDirectorySeparator(entry.Path), target, comparison));
            if (_highlighted >= 0)
                ScrollTo(_highlighted - VisibleRows / 2);
        }
        Invalidate();
    }

    private void LoadDrives()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            bool ready;
            try
            {
                ready = drive.IsReady;
            }
            catch (IOException)
            {
                ready = false;
            }
            if (ready)
                _entries.Add(new Entry(drive.Name, drive.RootDirectory.FullName, Kind.Drive));
        }
    }

    private void LoadFolder(string folder)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = false, // Else a folder that cannot be read looks empty; there is no recursion anyway.
            RecurseSubdirectories = false,
            AttributesToSkip = _owner.ShowHidden ? 0 : FileAttributes.Hidden | FileAttributes.System,
        };
        var comparer = StringComparer.Create(Localization.Culture, ignoreCase: true);
        var folders = new List<Entry>();
        var files = new List<Entry>();
        var filter = Filter;
        foreach (var item in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options))
        {
            if (item is DirectoryInfo)
                folders.Add(new Entry(item.Name, item.FullName, Kind.Folder));
            else if (_owner.Mode == FileInputMode.File && (filter == null || filter.Matches(item.Name)))
                files.Add(new Entry(item.Name, item.FullName, Kind.File));
        }
        folders.Sort((a, b) => comparer.Compare(a.Name, b.Name));
        files.Sort((a, b) => comparer.Compare(a.Name, b.Name));
        _entries.AddRange(folders);
        _entries.AddRange(files);
    }

    /// <summary>Typing a letter or digit selects the next entry whose name starts with it.</summary>
    private void JumpTo(char ch)
    {
        for (var i = 1; i <= _entries.Count; i++)
        {
            var index = (_highlighted + i + _entries.Count) % _entries.Count;
            if (_entries[index].Name.StartsWith(ch.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                MoveHighlight(index);
                return;
            }
        }
    }

    private static char? KeyChar(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (char)('a' + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        _ => null,
    };

    private int IndexAt(int x, int y)
    {
        var inner = ListInner;
        if (!inner.Contains(x, y))
            return -1;
        var index = _top + (y - inner.Y) / RowHeight;
        return index < _entries.Count ? index : -1;
    }

    private void Highlight(int index)
    {
        if (_highlighted == index)
            return;
        _highlighted = index;
        Invalidate();
    }

    private void MoveHighlight(int index)
    {
        if (_entries.Count == 0)
            return;
        Highlight(Math.Clamp(index, 0, _entries.Count - 1));
        if (_highlighted < _top)
            ScrollTo(_highlighted);
        else if (_highlighted >= _top + VisibleRows)
            ScrollTo(_highlighted - VisibleRows + 1);
    }

    private void ScrollTo(int top)
    {
        var clamped = Math.Clamp(top, 0, Math.Max(0, _entries.Count - VisibleRows));
        if (clamped == _top)
            return;
        _top = clamped;
        Invalidate();
    }

    private void SyncScrollBar()
    {
        var frame = ListFrame;
        _scrollBar.Bounds = Rect.FromEdges(frame.Right - 2 - ClassicScrollBar.Thickness, frame.Y + 2, frame.Right - 2, frame.Bottom - 2);
        _scrollBar.Maximum = Math.Max(0, _entries.Count - VisibleRows);
        _scrollBar.ViewSize = VisibleRows;
        _scrollBar.ContentSize = _entries.Count;
        _scrollBar.SmallChange = 1;
        _scrollBar.LargeChange = Math.Max(1, VisibleRows - 1);
    }

    private void DrawArrow(DrawingContext dc, Rect r, ArrowDirection direction, Part part) =>
        ClassicScrollBar.DrawArrowButton(dc, r, direction, _pressed == part, IsPartEnabled(part));

    private void DrawButton(DrawingContext dc, Rect r, string text, Part part)
    {
        var pressed = _pressed == part;
        var enabled = IsPartEnabled(part);
        dc.FillRectangle(r, ClassicStyle.Face);
        if (pressed)
        {
            dc.DrawRectangle(r, ClassicStyle.DarkShadow);
            dc.DrawRectangle(Rect.FromEdges(r.X + 1, r.Y + 1, r.Right - 1, r.Bottom - 1), ClassicStyle.Shadow);
        }
        else
        {
            ClassicStyle.DrawRaisedEdge(dc, r, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);
        }
        var font = Font;
        var shift = pressed ? 1 : 0;
        var x = r.X + (r.Width - font.MeasureText(text).Width) / 2 + shift;
        var y = r.Y + (r.Height - (font.Ascent + font.Descent)) / 2 + shift;
        if (enabled)
            dc.DrawText(text, font, Color.Black, x, y);
        else
            ClassicStyle.DrawEmbossedText(dc, text, font, ClassicStyle.Shadow, ClassicStyle.Highlight, x, y);
    }

    /// <summary>16 x 16 classic icons: a yellow folder, a white page with a folded corner, a grey drive.</summary>
    private static void DrawIcon(DrawingContext dc, Kind kind, int x, int y)
    {
        var outline = ClassicStyle.DarkShadow;
        switch (kind)
        {
            case Kind.Folder:
                var yellow = new Color(255, 214, 90);
                dc.FillRectangle(x + 1, y + 3, 6, 2, yellow);            // Tab.
                dc.DrawRectangle(new Rect(x + 1, y + 2, 6, 3), outline);
                dc.FillRectangle(x + 1, y + 4, 14, 10, yellow);          // Body.
                dc.DrawRectangle(new Rect(x + 1, y + 4, 14, 10), outline);
                dc.FillRectangle(x + 2, y + 5, 12, 1, new Color(255, 240, 180));
                break;
            case Kind.File:
                dc.FillRectangle(x + 3, y + 1, 10, 14, Color.White);
                dc.DrawRectangle(new Rect(x + 3, y + 1, 10, 14), outline);
                dc.FillRectangle(x + 9, y + 1, 4, 4, ClassicStyle.Face); // Folded corner.
                dc.DrawRectangle(new Rect(x + 9, y + 1, 4, 4), outline);
                for (var line = 0; line < 3; line++)
                    dc.FillRectangle(x + 5, y + 7 + line * 2, 6, 1, ClassicStyle.Shadow);
                break;
            default:
                dc.FillRectangle(x + 1, y + 5, 14, 7, ClassicStyle.Face);
                dc.DrawRectangle(new Rect(x + 1, y + 5, 14, 7), outline);
                dc.FillRectangle(x + 2, y + 10, 12, 1, ClassicStyle.Shadow);
                dc.FillRectangle(x + 11, y + 7, 2, 1, new Color(0, 160, 0)); // Activity light.
                break;
        }
    }

    /// <summary>Text that fits <paramref name="width"/>, cut on the left with "…" (keeps the end of a path).</summary>
    private static string FitLeft(string text, Font font, int width)
    {
        if (font.MeasureText(text).Width <= width)
            return text;
        for (var start = 1; start < text.Length; start++)
        {
            var candidate = "…" + text[start..];
            if (font.MeasureText(candidate).Width <= width)
                return candidate;
        }
        return "…";
    }

    /// <summary>Text that fits <paramref name="width"/>, cut on the right with "…".</summary>
    private static string FitRight(string text, Font font, int width)
    {
        if (font.MeasureText(text).Width <= width)
            return text;
        for (var length = text.Length - 1; length > 0; length--)
        {
            var candidate = text[..length] + "…";
            if (font.MeasureText(candidate).Width <= width)
                return candidate;
        }
        return "…";
    }
}
