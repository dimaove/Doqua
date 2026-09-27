using Doqua.GUI.Platform;

namespace Doqua.GUI;

/// <summary>
/// Top-level window decorated by the OS window manager. Hosts a single <see cref="Content"/>
/// control (usually a <see cref="Doqua.Controls.Panel"/>) that always fills the client area.
/// </summary>
public class Window
{
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
    private Action? _popupClosed;

    public Window()
    {
        _impl = Application.Platform.CreateWindow(_width, _height);
        _impl.Resized += UpdateSize;
        _impl.Closed += () => { IsClosed = true; OnClosed(); };
        _impl.Paint += Render;
        _impl.MouseDown += HandleMouseDown;
        _impl.MouseUp += HandleMouseUp;
        _impl.MouseMove += HandleMouseMove;
        _impl.MouseLeave += () => SetHoveredControl(null);
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
                throw new ArgumentException("Icons cannot contain null.", nameof(value));
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
            if (ReferenceEquals(value, _content))
                return;
            if (value != null && (value.Parent != null || value.Host != null))
                throw new InvalidOperationException("The control already has a parent.");

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
                throw new InvalidOperationException("The control cannot receive focus: it must be focusable, visible, enabled and in this window.");
        }
    }

    /// <summary>True while the window has the keyboard focus of the operating system.</summary>
    public bool IsActive { get; private set; }

    public bool IsClosed { get; private set; }

    public event EventHandler? Closed;

    /// <summary>Raised for keys not handled by the focused control or its parents. Tab navigation runs after it.</summary>
    public event EventHandler<KeyEventArgs>? KeyDown;

    /// <summary>
    /// Raised when a mouse button is pressed and released inside the client area,
    /// after the <see cref="Control.MouseClick"/> of the control under the pointer.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseClick;

    public void Show()
    {
        ThrowIfClosed();
        _impl.Show();
    }

    public void Close()
    {
        if (!IsClosed)
            _impl.Destroy();
    }

    /// <summary>Schedules a redraw. Multiple requests are merged into one.</summary>
    public void Invalidate()
    {
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
        closed?.Invoke();
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
        if (_popup != null)
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

        if (!wasPressed || !IsInClientArea(x, y))
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
        var hovered = EnabledHitTest(x, y, out _, out _);
        SetHoveredControl(hovered);

        // While a button is held, the control it was pressed on gets the moves (dragging).
        var target = Array.Find(_pressedControls, control => control != null) ?? hovered;
        if (target != null)
        {
            var (localX, localY) = target.PointFromWindow(x, y);
            target.RaiseMouseMove(new MouseMoveEventArgs(localX, localY, modifiers));
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

    private void ThrowIfClosed() => ObjectDisposedException.ThrowIf(IsClosed, this);
}
