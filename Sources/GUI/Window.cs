using Doqua.GUI.Platform;

namespace Doqua.GUI;

/// <summary>
/// Top-level window decorated by the OS window manager. Hosts a single <see cref="Content"/>
/// control (usually a <see cref="Doqua.Controls.Panel"/>) that always fills the client area.
/// </summary>
public class Window
{
    private static readonly List<Window> s_openWindows = [];

    private readonly IWindowImpl _impl;
    private string _title = "";
    private IReadOnlyList<Bitmap> _icons = [];
    private int _width = 800;
    private int _height = 600;
    private Color _background = Color.White;
    private Control? _content;
    private int _pressedButtons; // Bit mask of MouseButton values pressed inside the client area.
    private readonly Control?[] _pressedControls = new Control?[3]; // Indexed by MouseButton.
    private Control? _hoveredControl;

    // Last press, for counting double and triple clicks.
    private long _lastPressTime;
    private int _lastPressX;
    private int _lastPressY;
    private MouseButton _lastPressButton;
    private Control? _lastPressControl;
    private int _clickCount;
    private Control? _focusedControl;
    private Control? _popup; // Shown above the content and receives input first (a PopupMenu).
    private (int X, int Y)? _pointer; // Last pointer position over the client area.
    private Cursor _currentCursor = Cursor.Arrow;
    private Action? _popupClosed;
    private Window? _modalChild; // Open dialog shown with ShowModal; this window gets no input meanwhile.
    private bool _resizable = true;

    /// <summary>Creates an 800 x 600 window; it appears with <see cref="Show"/> or <see cref="Application.Run"/>.</summary>
    public Window()
    {
        Application.EnsureGuiThread(); // The first window makes its thread the GUI thread.
        _impl = Application.Platform.CreateWindow(_width, _height);
        _impl.Resized += UpdateSize;
        _impl.Closed += () =>
        {
            IsClosed = true;
            s_openWindows.Remove(this);
            _modalChild?.Close(); // A dialog closes with its owner (and reports first).
            if (Owner is { } owner && owner._modalChild == this)
            {
                owner._modalChild = null;
                owner.Activate();
            }
            OnClosed();
        };
        s_openWindows.Add(this);
        _impl.Paint += Render;
        _impl.MouseDown += HandleMouseDown;
        _impl.MouseUp += HandleMouseUp;
        _impl.MouseMove += HandleMouseMove;
        _impl.MouseWheel += HandleMouseWheel;
        _impl.MouseLeave += () =>
        {
            SetHoveredControl(null);
            _pointer = null;
        };
        _impl.KeyDown += HandleKeyDown;
        _impl.TextInput += HandleTextInput;
        _impl.ActiveChanged += active =>
        {
            if (!active)
                ClosePopup(); // Like native menus, a popup does not survive switching to another window.
            IsActive = active;
            Invalidate(); // Focus visuals such as the caret depend on it.
        };
    }

    /// <summary>Text of the title bar.</summary>
    public string Title
    {
        get => _title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ThrowIfClosed();
            _title = value;
            _impl.SetTitle(value);
        }
    }

    /// <summary>
    /// Window icon for the title bar and taskbar, as one bitmap per size (for example 16, 32 and 256
    /// pixels). The system picks the size it needs and scales the closest one. Empty means no icon.
    /// </summary>
    public IReadOnlyList<Bitmap> Icons
    {
        get => _icons;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Any(icon => icon == null))
                throw new ArgumentException(Localization.Get("Doqua.Error.IconsContainNull"), nameof(value));
            ThrowIfClosed();
            _icons = [.. value];
            _impl.SetIcons(_icons);
        }
    }

    /// <summary>Client area width in pixels.</summary>
    public int Width
    {
        get => _width;
        set => Resize(value, _height);
    }

    /// <summary>Client area height in pixels.</summary>
    public int Height
    {
        get => _height;
        set => Resize(_width, value);
    }

    /// <summary>Color of the client area not covered by <see cref="Content"/>.</summary>
    public Color Background
    {
        get => _background;
        set
        {
            _background = value;
            Invalidate();
        }
    }

    /// <summary>Root control of the window. It is resized to fill the client area.</summary>
    public Control? Content
    {
        get => _content;
        set
        {
            Application.VerifyAccess();
            if (ReferenceEquals(value, _content))
                return;
            if (value != null && (value.Parent != null || value.Host != null))
                throw new InvalidOperationException(Localization.Get("Doqua.Error.ControlHasParent"));

            if (_content != null)
                _content.Host = null;
            _content = value;
            if (value != null)
            {
                value.Host = this;
                value.Bounds = new Rect(0, 0, _width, _height);
            }
            Invalidate();
            ValidateFocus();
        }
    }

    /// <summary>
    /// Control that receives keyboard input, or null. Setting it is the same as <see cref="Control.Focus"/>,
    /// but throws if the control cannot be focused.
    /// </summary>
    public Control? FocusedControl
    {
        get => _focusedControl;
        set
        {
            if (value == null)
                SetFocus(null);
            else if (!TrySetFocus(value))
                throw new InvalidOperationException(Localization.Get("Doqua.Error.CannotFocus"));
        }
    }

    /// <summary>
    /// Whether the user can resize the window with its frame (and maximize it). True by default; dialogs usually set
    /// it to false. <see cref="Width"/> and <see cref="Height"/> can still be changed from code.
    /// </summary>
    public bool Resizable
    {
        get => _resizable;
        set
        {
            ThrowIfClosed();
            _resizable = value;
            _impl.SetResizable(value);
        }
    }

    /// <summary>The window this one was shown for with <see cref="ShowModal"/>, or null.</summary>
    public Window? Owner { get; private set; }

    /// <summary>True while a window shown with <see cref="ShowModal"/> for this one is open: this window then gets no input.</summary>
    public bool HasModalDialog => _modalChild != null;

    /// <summary>True while the window has the keyboard focus of the operating system.</summary>
    public bool IsActive { get; private set; }

    /// <summary>True after the window has been closed.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Raised after the window has closed.</summary>
    public event EventHandler? Closed;

    /// <summary>Raised for keys not handled by the focused control or its parents. Tab navigation runs after it.</summary>
    public event EventHandler<KeyEventArgs>? KeyDown;

    /// <summary>
    /// Raised when a mouse button is pressed and released inside the client area,
    /// after the <see cref="Control.MouseClick"/> of the control under the pointer.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseClick;

    /// <summary>Shows the window (<see cref="Application.Run"/> shows the main window itself).</summary>
    public void Show()
    {
        ThrowIfClosed();
        _impl.Show();
    }

    /// <summary>
    /// Shows this window as a modal dialog of <paramref name="owner"/>, instead of <see cref="Show"/>. It returns at once;
    /// until this window closes, the owner gets no mouse or keyboard input (a click on it brings the dialog to the
    /// front). The dialog is kept above the owner, has no taskbar entry of its own, is centred over the owner and takes
    /// the keyboard focus; it closes with the owner. Handle <see cref="Closed"/> for the result.
    /// If the owner already has a modal dialog, this one becomes a dialog of that dialog.
    /// </summary>
    public void ShowModal(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ThrowIfClosed();
        owner.ThrowIfClosed();
        while (owner._modalChild is { } dialog)
            owner = dialog;
        if (owner == this || Owner != null)
            throw new InvalidOperationException(Localization.Get("Doqua.Error.ModalOwner"));

        Owner = owner;
        owner.ClosePopup();
        owner.SetHoveredControl(null);
        owner._modalChild = this;
        _impl.SetModalOwner(owner._impl);
        _impl.Show();
    }

    /// <summary>Brings the window to the front and gives it the keyboard focus, if the window manager allows it.</summary>
    public void Activate()
    {
        Application.VerifyAccess();
        if (!IsClosed)
            _impl.Activate();
    }

    /// <summary>Closes the window; closing the main window ends <see cref="Application.Run"/>.</summary>
    public void Close()
    {
        Application.VerifyAccess();
        if (!IsClosed)
            _impl.Destroy();
    }

    /// <summary>Redraws every open window (e.g. after the language changed).</summary>
    internal static void InvalidateAll()
    {
        foreach (var window in s_openWindows)
            window.Invalidate();
    }

    /// <summary>
    /// Schedules a redraw. Multiple requests are merged into one. Controls call it whenever they change, so it also
    /// detects controls changed from a thread other than the GUI thread (use <see cref="Application.Post"/>).
    /// </summary>
    public void Invalidate()
    {
        Application.VerifyAccess();
        if (!IsClosed)
            _impl.Invalidate();
    }

    protected virtual void OnClosed() => Closed?.Invoke(this, EventArgs.Empty);

    protected virtual void OnMouseClick(MouseEventArgs e) => MouseClick?.Invoke(this, e);

    protected virtual void OnKeyDown(KeyEventArgs e) => KeyDown?.Invoke(this, e);

    /// <summary>
    /// Shows <paramref name="popup"/> (positioned in client coordinates) above the content, closing any
    /// open popup first. While it is open it gets all mouse and keyboard input; a press outside it
    /// closes it. <paramref name="closed"/> runs whenever it closes, for any reason.
    /// </summary>
    internal void OpenPopup(Control popup, Action closed)
    {
        ClosePopup();
        _popup = popup;
        _popupClosed = closed;
        popup.Host = this;
        SetHoveredControl(null);
        Invalidate();
        UpdateCursor();
    }

    internal void ClosePopup()
    {
        if (_popup == null)
            return;
        var closed = _popupClosed;
        _popup.Host = null;
        (_popup, _popupClosed) = (null, null);
        Array.Clear(_pressedControls);
        SetHoveredControl(null);
        Invalidate();
        UpdateCursor();
        closed?.Invoke();
    }

    /// <summary>
    /// Shows the cursor of the control that holds the mouse (dragging) or else of the one under the
    /// pointer; the arrow over nothing or over a disabled control.
    /// </summary>
    internal void UpdateCursor()
    {
        if (_pointer is not var (x, y) || IsClosed)
            return;
        var target = Array.Find(_pressedControls, control => control != null) ?? EnabledHitTest(x, y, out _, out _);
        Cursor cursor;
        if (target == null)
        {
            cursor = Cursor.Arrow;
        }
        else
        {
            var (localX, localY) = target.PointFromWindow(x, y);
            cursor = target.ResolveCursor(localX, localY);
        }
        if (cursor == _currentCursor)
            return;
        _currentCursor = cursor;
        _impl.SetCursor(cursor);
    }

    /// <summary>Opens the context menu of <paramref name="target"/> or its nearest ancestor that has one.</summary>
    private void OpenContextMenu(Control target, int windowX, int windowY)
    {
        for (var control = target; control != null; control = control.Parent)
        {
            if (control.GetContextMenuForWindow() is { } menu)
            {
                var (x, y) = control.PointFromWindow(windowX, windowY);
                menu.Show(control, x, y);
                return;
            }
        }
    }

    internal bool TrySetFocus(Control control)
    {
        Application.VerifyAccess();
        if (!control.CanFocus || control.GetWindow() != this)
            return false;
        SetFocus(control);
        return true;
    }

    /// <summary>Clears focus if the focused control was hidden, disabled or removed.</summary>
    internal void ValidateFocus()
    {
        if (_focusedControl != null && !(_focusedControl.CanFocus && _focusedControl.GetWindow() == this))
            SetFocus(null);
    }

    private void SetFocus(Control? control)
    {
        if (control == _focusedControl)
            return;
        var previous = _focusedControl;
        _focusedControl = control;
        previous?.RaiseLostFocus();
        control?.RaiseGotFocus();
        for (var ancestor = control?.Parent; ancestor != null; ancestor = ancestor.Parent)
            ancestor.OnDescendantFocused(control!); // Lets scrolling panels bring it into view.
        Invalidate();
    }

    /// <summary>Tab / Shift+Tab: next or previous focusable control in tree order, wrapping around.</summary>
    private void MoveFocus(bool forward)
    {
        var candidates = new List<Control>();
        _content?.CollectFocusable(candidates);
        if (candidates.Count == 0)
            return;

        var index = _focusedControl == null ? -1 : candidates.IndexOf(_focusedControl);
        var next = forward
            ? (index + 1) % candidates.Count
            : (index <= 0 ? candidates.Count : index) - 1;
        SetFocus(candidates[next]);
    }

    private void HandleKeyDown(Key key, KeyModifiers modifiers)
    {
        if (_modalChild != null)
            return; // The window manager may still let the owner have the focus; the keys belong to the dialog.
        var e = new KeyEventArgs(key, modifiers);
        if (_popup != null)
        {
            _popup.RaiseKeyDown(e); // An open popup takes every key.
            return;
        }
        for (var control = _focusedControl; control != null && !e.Handled; control = control.Parent)
            control.RaiseKeyDown(e);
        if (!e.Handled)
            OnKeyDown(e);
        if (!e.Handled && key == Key.Tab && (modifiers & ~KeyModifiers.Shift) == KeyModifiers.None)
            MoveFocus(forward: !modifiers.HasFlag(KeyModifiers.Shift));

        // Shift+F10: the keyboard way to open the focused control's context menu.
        if (!e.Handled && key == Key.F10 && modifiers == KeyModifiers.Shift && _focusedControl is { } focused)
        {
            var (x, y) = focused.PointToWindow(0, focused.Height);
            OpenContextMenu(focused, x, y);
        }
    }

    private void HandleTextInput(string text)
    {
        if (_popup != null || _modalChild != null)
            return;
        var e = new TextInputEventArgs(text);
        for (var control = _focusedControl; control != null && !e.Handled; control = control.Parent)
            control.RaiseTextInput(e);
    }

    private void Render(Framebuffer framebuffer)
    {
        var dc = new DrawingContext(framebuffer);
        dc.Clear(_background);
        _content?.Render(dc);
        _popup?.Render(dc);
    }

    private void HandleMouseDown(MouseButton button, int x, int y, KeyModifiers modifiers)
    {
        if (_modalChild != null)
        {
            _modalChild.Activate(); // Like native modal dialogs: a click on the owner brings the dialog to the front.
            return;
        }
        _pointer = (x, y);
        _pressedButtons |= 1 << (int)button;
        var target = EnabledHitTest(x, y, out var localX, out var localY);
        if (_popup != null && target == null)
        {
            ClosePopup(); // A press outside an open popup closes it and is not passed on.
            return;
        }
        _pressedControls[(int)button] = target;
        var clickCount = CountClicks(button, x, y, target);

        // Focus the nearest focusable control under the pointer; clicks elsewhere keep the focus.
        for (var control = target; control != null; control = control.Parent)
        {
            if (control.CanFocus)
            {
                SetFocus(control);
                break;
            }
        }

        target?.RaiseMouseDown(new MouseEventArgs(button, localX, localY, modifiers, clickCount));
    }

    /// <summary>Returns 2, 3, ... when this press continues a multi-click on the same control, otherwise 1.</summary>
    private int CountClicks(MouseButton button, int x, int y, Control? target)
    {
        var platform = Application.Platform;
        var now = Environment.TickCount64;
        var distance = platform.DoubleClickDistance;
        var continues = _clickCount > 0
            && button == _lastPressButton
            && target == _lastPressControl
            && now - _lastPressTime <= platform.DoubleClickTime
            && Math.Abs(x - _lastPressX) <= distance
            && Math.Abs(y - _lastPressY) <= distance;

        _clickCount = continues ? _clickCount + 1 : 1;
        (_lastPressTime, _lastPressX, _lastPressY, _lastPressButton, _lastPressControl) = (now, x, y, button, target);
        return _clickCount;
    }

    private void HandleMouseUp(MouseButton button, int x, int y, KeyModifiers modifiers)
    {
        if (IsInClientArea(x, y))
            _pointer = (x, y);
        try
        {
            HandleMouseUpCore(button, x, y, modifiers);
        }
        finally
        {
            UpdateCursor(); // The released control no longer holds the cursor.
        }
    }

    private void HandleMouseUpCore(MouseButton button, int x, int y, KeyModifiers modifiers)
    {
        var mask = 1 << (int)button;
        var wasPressed = (_pressedButtons & mask) != 0;
        var pressedControl = _pressedControls[(int)button];
        _pressedButtons &= ~mask;
        _pressedControls[(int)button] = null;

        // The control that got MouseDown always gets MouseUp, wherever the pointer is now.
        if (pressedControl != null)
        {
            var (localX, localY) = pressedControl.PointFromWindow(x, y);
            pressedControl.RaiseMouseUp(new MouseEventArgs(button, localX, localY, modifiers));
        }

        // A modal dialog opened while the button was held: the press is released, but nothing is clicked.
        if (!wasPressed || !IsInClientArea(x, y) || _modalChild != null)
            return;

        // A control is clicked only if the button was pressed and released over it.
        if (pressedControl != null)
        {
            var target = EnabledHitTest(x, y, out var localX, out var localY);
            if (target == pressedControl)
            {
                target.RaiseMouseClick(new MouseEventArgs(button, localX, localY, modifiers));
                if (button == MouseButton.Right && target != _popup)
                    OpenContextMenu(target, x, y);
            }
        }

        OnMouseClick(new MouseEventArgs(button, x, y, modifiers));
    }

    private void HandleMouseMove(int x, int y, KeyModifiers modifiers)
    {
        _pointer = (x, y);
        if (_modalChild != null && !Array.Exists(_pressedControls, control => control != null))
            return; // No hover effects behind a modal dialog (a drag started before it opened still gets its moves).
        var hovered = EnabledHitTest(x, y, out _, out _);
        SetHoveredControl(hovered);

        // While a button is held, the control it was pressed on gets the moves (dragging).
        var target = Array.Find(_pressedControls, control => control != null) ?? hovered;
        if (target != null)
        {
            var (localX, localY) = target.PointFromWindow(x, y);
            target.RaiseMouseMove(new MouseMoveEventArgs(localX, localY, modifiers));
        }
        UpdateCursor();
    }

    /// <summary>The wheel goes to the control under the pointer, then up through its parents until handled.</summary>
    private void HandleMouseWheel(int delta, int x, int y, KeyModifiers modifiers)
    {
        if (_modalChild != null)
            return;
        // While a popup is open only the popup can scroll (e.g. a combo box list); nothing behind it does.
        for (var control = EnabledHitTest(x, y, out _, out _); control != null; control = control.Parent)
        {
            var (localX, localY) = control.PointFromWindow(x, y);
            var e = new MouseWheelEventArgs(delta, localX, localY, modifiers);
            control.RaiseMouseWheel(e);
            if (e.Handled)
                return;
        }
    }

    private void SetHoveredControl(Control? control)
    {
        if (control == _hoveredControl)
            return;
        var previous = _hoveredControl;
        _hoveredControl = control;
        previous?.SetMouseOver(false);
        control?.SetMouseOver(true);
    }

    /// <summary>Topmost control at a client-area point, or null outside the client area.</summary>
    private Control? HitTest(int x, int y, out int localX, out int localY)
    {
        (localX, localY) = (x, y);
        if (!IsInClientArea(x, y))
            return null;
        // While a popup is open only the popup can be hit: the content does not get mouse input.
        return _popup != null ? _popup.HitTest(x, y, out localX, out localY) : _content?.HitTest(x, y, out localX, out localY);
    }

    /// <summary>
    /// Like <see cref="HitTest"/>, but returns null if the control under the pointer is disabled:
    /// it swallows the event instead of passing it to the controls below.
    /// </summary>
    private Control? EnabledHitTest(int x, int y, out int localX, out int localY)
    {
        var target = HitTest(x, y, out localX, out localY);
        return target is { IsEffectivelyEnabled: true } ? target : null;
    }

    private bool IsInClientArea(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;

    private void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ThrowIfClosed();
        UpdateSize(width, height);
        _impl.Resize(width, height);
    }

    private void UpdateSize(int width, int height)
    {
        if (width != _width || height != _height)
            ClosePopup();
        _width = width;
        _height = height;
        if (_content != null)
            _content.Bounds = new Rect(0, 0, width, height);
    }

    private void ThrowIfClosed()
    {
        Application.VerifyAccess();
        ObjectDisposedException.ThrowIf(IsClosed, this);
    }
}
