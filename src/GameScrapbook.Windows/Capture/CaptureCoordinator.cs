using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GameScrapbook.Windows.Interop;

namespace GameScrapbook.Windows.Capture;

/// <param name="BoardPosition">The desired top-left in the board's own coordinate system.</param>
/// <param name="DisplayScale">Physical screen pixels per board unit, including board zoom and display DPI.</param>
public sealed record CaptureDropTarget(Point BoardPosition, double DisplayScale = 1.0);

/// <summary>
/// Captures once, owns input while selecting/carrying a crop, then returns to the source.
/// All coordinates inside the coordinator are physical display pixels; only findDropTarget converts them.
/// </summary>
public sealed class CaptureCoordinator : IDisposable
{
    private const int CancelHotkeyId = 0x4712;
    private enum Stage { None, AwaitSelection, Selecting, ReadyToDrag, Dragging }
    private readonly Window _owner;
    private readonly nint _ownerHandle;
    private readonly HwndSource _ownerSource;
    private readonly Func<Point, CaptureDropTarget?> _findDropTarget;
    private readonly Action<BitmapSource, Point> _onDrop;
    private readonly Action<string> _status;
    private readonly DispatcherTimer _timer;
    private Stage _stage;
    private OverlayWindow? _selectionWindow;
    private SelectionSurface? _selectionSurface;
    private OverlayWindow? _ghost;
    private DragSurface? _ghostSurface;
    private BitmapSource? _snapshot;
    private BitmapSource? _snip;
    private NativeMethods.NativeRect _monitor;
    private NativeMethods.NativeRect _originalClip;
    private NativeMethods.NativeRect _fullDesktop;
    private bool _releasedClip;
    private nint _sourceWindow;
    private uint _sourceProcess;
    private nint _foregroundAtStart;
    private Point _originalCursor;
    private Point _selectionStart;
    private Rect _selection;
    private bool _previousPrimary;
    private bool _previousSecondary;
    private bool _previousEscape;
    private int _primaryKey;
    private int _secondaryKey;
    private int _hotkeyId;
    private int _hotkeyModifiers;
    private int _hotkeyKey;
    private bool _disposed;
    private bool _insideTick;
    private bool _lastValidTarget;
    private bool _escapeRegistered;

    /// <summary>Host gate for modal dialogs, readonly/recovery state and an existing board drag.</summary>
    public Func<bool>? CanBeginCapture { get; set; }
    public bool ReturnCursor { get; set; } = true;
    public bool IsCapturing => _stage != Stage.None;
    public bool IsHotkeyRegistered => !_disposed && _hotkeyId != 0;
    public event Action<bool>? CaptureStateChanged;
    public event Action<string>? CaptureError;

    public CaptureCoordinator(Window owner, Func<Point, CaptureDropTarget?> findDropTarget,
        Action<BitmapSource, Point> onDrop, Action<string> status)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _findDropTarget = findDropTarget ?? throw new ArgumentNullException(nameof(findDropTarget));
        _onDrop = onDrop ?? throw new ArgumentNullException(nameof(onDrop));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _ownerHandle = new WindowInteropHelper(owner).EnsureHandle();
        _ownerSource = HwndSource.FromHwnd(_ownerHandle) ?? throw new InvalidOperationException("The scrapbook window is not ready.");
        _ownerSource.AddHook(OwnerWindowProc);
        _timer = new DispatcherTimer(DispatcherPriority.Input, owner.Dispatcher) { Interval = TimeSpan.FromMilliseconds(12) };
        _timer.Tick += Tick;
    }

    public bool RegisterHotkey(int modifiers, int virtualKey, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _owner.Dispatcher.VerifyAccess();
        error = null;
        if (virtualKey < 1 || virtualKey > 254 || virtualKey == 0x7B || (modifiers & ~0xF) != 0)
        {
            error = "Choose a valid shortcut. F12 is reserved by Windows.";
            ReportError(error);
            return false;
        }
        if (_hotkeyId != 0 && modifiers == _hotkeyModifiers && virtualKey == _hotkeyKey) return true;

        // Register the replacement before releasing the old key so a conflict does not disable capture.
        int nextId = _hotkeyId == 0x4710 ? 0x4711 : 0x4710;
        if (!NativeMethods.RegisterHotKey(_ownerHandle, nextId, (uint)modifiers | NativeMethods.ModNoRepeat, (uint)virtualKey))
        {
            int nativeError = Marshal.GetLastWin32Error();
            error = $"The shortcut could not be registered (Windows error {nativeError}). Choose another shortcut in Settings or use the Snip button.";
            Diagnostic($"Hotkey registration failed: Win32={nativeError}; priorRegistrationRetained={_hotkeyId != 0}.");
            ReportError(error);
            return false;
        }
        if (_hotkeyId != 0) NativeMethods.UnregisterHotKey(_ownerHandle, _hotkeyId);
        _hotkeyId = nextId;
        _hotkeyModifiers = modifiers;
        _hotkeyKey = virtualKey;
        Diagnostic("Capture hotkey registered successfully.");
        return true;
    }

    public void BeginCapture() => BeginCaptureCore("button");

    private void BeginCaptureCore(string origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _owner.Dispatcher.VerifyAccess();
        if (IsCapturing) { Cancel(); return; }
        Diagnostic($"Capture begin requested: trigger={origin}.");
        try
        {
            if (CanBeginCapture is not null && !CanBeginCapture())
            {
                ReportError("Finish the current scrapbook action, then move the mouse to the screen you want and start a snip.");
                return;
            }
        }
        catch (Exception ex)
        {
            ReportError("Capture is unavailable: " + ex.Message);
            return;
        }
        bool boardMinimized = _owner.WindowState == WindowState.Minimized || NativeMethods.IsIconic(_ownerHandle);
        if (!_owner.IsVisible && !boardMinimized)
        {
            ReportError("Open the scrapbook window before starting a snip so you have somewhere to drop it.");
            return;
        }
        try
        {
            _originalCursor = NativeMethods.PhysicalCursor();
            _foregroundAtStart = NativeMethods.GetForegroundWindow();
            _sourceWindow = NativeMethods.FocusReturnTarget(_foregroundAtStart, _originalCursor);
            _sourceProcess = 0;
            if (_sourceWindow != 0) NativeMethods.GetWindowThreadProcessId(_sourceWindow, out _sourceProcess);
            _monitor = NativeMethods.MonitorBoundsAt(_originalCursor);
            // Games often release their cursor clip on deactivation. Remember it
            // before moving focus, then release any remaining clip after acquiring input.
            _fullDesktop = NativeMethods.VirtualDesktopBounds();
            _releasedClip = NativeMethods.GetClipCursor(out _originalClip)
                && !_originalClip.SameAs(_fullDesktop);
            Diagnostic($"Capture screen selected from pointer: {_monitor.Left},{_monitor.Top} {_monitor.Width}x{_monitor.Height}; externalFocusTarget={_sourceWindow != 0}.");
            _snapshot = ScreenCapture.Snapshot(_monitor); // Never capture our own tint/ghost.
            Diagnostic($"Screen snapshot acquired: {_snapshot.PixelWidth}x{_snapshot.PixelHeight}.");
            if (_owner.WindowState == WindowState.Minimized || NativeMethods.IsIconic(_ownerHandle))
            {
                if (!NativeMethods.RestoreWithoutActivation(_ownerHandle))
                    throw new InvalidOperationException("The scrapbook could not be restored. Open its window and try the shortcut or Snip button again.");
                Diagnostic("Minimized scrapbook restored without an activation request, after snapshot capture.");
            }
            _selection = Rect.Empty;
            _selectionSurface = new SelectionSurface(_snapshot);
            _selectionWindow = new OverlayWindow(_selectionSurface, false);
            _selectionWindow.Present(_monitor.Left, _monitor.Top, _monitor.Width, _monitor.Height);
            Diagnostic("Selection overlay presented: " + _selectionWindow.DescribeGeometry() + ".");
            if (!_selectionWindow.TakeInputFocus())
                throw new InvalidOperationException("The snipping overlay could not take focus. Select Game Scrapbook and try again; capture was cancelled to avoid moving your game.");
            Diagnostic("Selection overlay owns input focus; the source window is inactive.");
            if (NativeMethods.GetClipCursor(out var currentClip) && !currentClip.SameAs(_fullDesktop)
                && !NativeMethods.ClipCursor(0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The mouse could not be released for snipping.");

            bool swapped = NativeMethods.GetSystemMetrics(23) != 0; // Respect left-handed mouse settings.
            _primaryKey = swapped ? 0x02 : 0x01;
            _secondaryKey = swapped ? 0x01 : 0x02;
            _previousPrimary = NativeMethods.IsDown(_primaryKey);
            _previousSecondary = NativeMethods.IsDown(_secondaryKey);
            _previousEscape = NativeMethods.IsDown(0x1B);
            _stage = Stage.AwaitSelection;
            // Keep Escape available across the selection and drag. Games that
            // explicitly read background raw input remain a compatibility limit.
            _escapeRegistered = NativeMethods.RegisterHotKey(_ownerHandle, CancelHotkeyId, NativeMethods.ModNoRepeat, 0x1B);
            if (!_escapeRegistered) Diagnostic("Capture Escape hotkey unavailable; using session-only polling fallback.");
            _timer.Start();
            NotifyState(true);
            Notify("Drag a rectangle around the information you want. Esc or right-click cancels.");
        }
        catch (Exception ex)
        {
            // Finish restores only while our selection overlay still owns focus.
            Finish(true, true);
            ReportError("Capture could not start: " + ex.Message);
        }
    }

    public void Cancel()
    {
        if (_disposed || !IsCapturing) return;
        _owner.Dispatcher.VerifyAccess();
        bool hadFocusTarget = _sourceWindow != 0;
        Diagnostic("Capture cancelled by the user.");
        bool restored = Finish(true, true);
        Notify(!hadFocusTarget ? "Snip cancelled." : restored ? "Snip cancelled. Back to the original window." : "Snip cancelled. Select your game to continue.");
    }

    private nint OwnerWindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == CancelHotkeyId && _escapeRegistered)
        {
            handled = true;
            Cancel();
            return 0;
        }
        if (message == NativeMethods.WmHotkey && _hotkeyId != 0 && wParam.ToInt32() == _hotkeyId && !_disposed)
        {
            handled = true;
            Diagnostic("Capture hotkey message received.");
            BeginCaptureCore("hotkey");
        }
        return 0;
    }

    private void Tick(object? sender, EventArgs args)
    {
        if (_insideTick || !IsCapturing) return;
        _insideTick = true;
        try
        {
            if (!OwnsInputFocus())
            {
                // Stop before reading a gesture if the game or any other window
                // becomes active. Never keep dragging while the game owns input.
                Finish(false, false);
                Diagnostic("Capture cancelled because the selection overlay lost input focus.");
                Notify("Snip cancelled because another window became active.");
                return;
            }

            bool primary = NativeMethods.IsDown(_primaryKey);
            bool secondary = NativeMethods.IsDown(_secondaryKey);
            bool escape = NativeMethods.IsDown(0x1B);
            bool pressed = primary && !_previousPrimary;
            bool released = !primary && _previousPrimary;
            _previousPrimary = primary;
            bool cancel = (secondary && !_previousSecondary) || (!_escapeRegistered && escape && !_previousEscape);
            _previousSecondary = secondary;
            _previousEscape = escape;
            if (cancel) { Cancel(); return; }

            Point cursor = NativeMethods.PhysicalCursor();
            Point local = ClampToMonitor(cursor);
            bool onSource = _monitor.ToRect().Contains(cursor);
            switch (_stage)
            {
                case Stage.AwaitSelection:
                    if (pressed && onSource) StartSelection(local);
                    break;
                case Stage.Selecting:
                    _selection = new Rect(_selectionStart, local);
                    _selectionSurface!.SetSelection(_selection);
                    if (released) CompleteSelection();
                    break;
                case Stage.ReadyToDrag:
                    if (pressed && onSource)
                    {
                        if (_selection.Contains(local)) StartDrag(cursor);
                        else StartSelection(local);
                    }
                    break;
                case Stage.Dragging:
                    UpdateDrag(cursor);
                    if (released) CompleteDrop(cursor);
                    break;
            }
        }
        catch (Exception ex)
        {
            Finish(true, true);
            ReportError("Snipping stopped safely: " + ex.Message);
        }
        finally { _insideTick = false; }
    }

    private Point ClampToMonitor(Point physical) => new(
        Math.Clamp(physical.X - _monitor.Left, 0, _monitor.Width),
        Math.Clamp(physical.Y - _monitor.Top, 0, _monitor.Height));

    private void StartSelection(Point start)
    {
        _selectionStart = start;
        _selection = new Rect(start, start);
        _snip = null;
        _stage = Stage.Selecting;
        _selectionSurface!.SetSelection(_selection);
    }

    private void CompleteSelection()
    {
        int x = Math.Clamp((int)Math.Floor(_selection.Left), 0, _monitor.Width - 1);
        int y = Math.Clamp((int)Math.Floor(_selection.Top), 0, _monitor.Height - 1);
        int right = Math.Clamp((int)Math.Ceiling(_selection.Right), x, _monitor.Width);
        int bottom = Math.Clamp((int)Math.Ceiling(_selection.Bottom), y, _monitor.Height);
        if (right - x < 4 || bottom - y < 4)
        {
            _stage = Stage.AwaitSelection;
            _selection = Rect.Empty;
            _selectionSurface!.SetSelection(_selection);
            return;
        }
        _selection = new Rect(x, y, right - x, bottom - y);
        var crop = new CroppedBitmap(_snapshot!, new Int32Rect(x, y, right - x, bottom - y));
        crop.Freeze();
        _snip = crop;
        Diagnostic($"Selection ready: crop={crop.PixelWidth}x{crop.PixelHeight}.");
        _stage = Stage.ReadyToDrag;
        _selectionSurface!.SetSelection(_selection, true);
        Notify("Grab the bright snip and drag it onto your scrapbook. Release there to place it.");
    }

    private void StartDrag(Point cursor)
    {
        _ghostSurface = new DragSurface(_snip!);
        _ghost = new OverlayWindow(_ghostSurface, true);
        var size = PreviewSize(1);
        _ghost.Present((int)cursor.X, (int)cursor.Y, size.Width, size.Height);
        _selectionSurface!.SetSelection(_selection, true, true);
        _stage = Stage.Dragging;
        Diagnostic("Selected snip is being dragged.");
        _lastValidTarget = false;
        UpdateDrag(cursor);
    }

    private (int Width, int Height) PreviewSize(double displayScale)
    {
        double scale = Math.Min(1, Math.Min(520d / _snip!.PixelWidth, 420d / _snip.PixelHeight));
        if (!double.IsFinite(displayScale) || displayScale <= 0) displayScale = 1;
        displayScale = Math.Clamp(displayScale, 0.1, 8);
        return (Math.Max(1, (int)Math.Round(_snip.PixelWidth * scale * displayScale)),
            Math.Max(1, (int)Math.Round(_snip.PixelHeight * scale * displayScale)));
    }

    private void UpdateDrag(Point cursor)
    {
        var target = _findDropTarget(cursor);
        var size = PreviewSize(target?.DisplayScale ?? 1);
        _ghost!.MovePhysical((int)cursor.X, (int)cursor.Y, size.Width, size.Height);
        bool valid = target != null;
        _ghostSurface!.ValidTarget = valid;
        _ghostSurface.InvalidateVisual();
        if (valid != _lastValidTarget)
        {
            _lastValidTarget = valid;
            Notify(valid ? "Release to place the snip, then return to your game." : "Drag onto the scrapbook canvas. Esc cancels.");
        }
    }

    private void CompleteDrop(Point cursor)
    {
        var target = _findDropTarget(cursor);
        if (target == null)
        {
            ReturnToReady("That was outside the scrapbook. Grab the snip again and drop it onto the canvas.");
            return;
        }
        try
        {
            // Host writes the PNG and document synchronously/atomically here. Throwing preserves the snip.
            _onDrop(_snip!, target.BoardPosition);
        }
        catch (Exception ex)
        {
            Diagnostic("Capture drop save failed: " + ex.Message);
            try { CaptureError?.Invoke("The snip could not be saved. It remains available to drag again. " + ex.Message); } catch { }
            ReturnToReady("The snip was not saved: " + ex.Message + " You can drag it again or press Esc.");
            return;
        }
        bool hadFocusTarget = _sourceWindow != 0;
        bool restored = Finish(true, true);
        Diagnostic($"Snip placed and saved: externalFocusTarget={hadFocusTarget}; restored={restored}.");
        Notify(!hadFocusTarget ? "Snip placed and saved." : restored ? "Snip placed. Back to the original window." : "Snip placed. Windows kept another window active; select your game to continue.");
    }

    private void ReturnToReady(string message)
    {
        CloseGhost();
        _stage = Stage.ReadyToDrag;
        _selectionSurface!.SetSelection(_selection, true);
        Diagnostic("Capture returned to selected-snippet state for another drop attempt.");
        Notify(message);
    }

    /// <returns>True only if the remembered source is actually foreground after cleanup.</returns>
    private bool Finish(bool restoreFocus, bool restoreCursor)
    {
        bool hadSession = IsCapturing;
        bool mayReturn = restoreFocus && OwnsInputFocus();
        _timer.Stop();
        if (_escapeRegistered) NativeMethods.UnregisterHotKey(_ownerHandle, CancelHotkeyId);
        _escapeRegistered = false;
        _stage = Stage.None;
        // Reposition the pointer while the game is still inactive. Warping after
        // returning focus can move a game's camera even though dragging has ended.
        if (mayReturn && restoreCursor && ReturnCursor && (_sourceWindow == 0 || TargetStillExists()))
            NativeMethods.SetPhysicalCursorPos((int)_originalCursor.X, (int)_originalCursor.Y);
        CloseGhost();
        // Make one ordinary return request while we still own focus, before
        // closing the overlay can make Windows activate an unrelated window.
        if (mayReturn && OwnsInputFocus())
        {
            if (TargetStillExists()) NativeMethods.SetForegroundWindow(_sourceWindow);
            else if (_sourceWindow == 0 && _foregroundAtStart == _ownerHandle)
                NativeMethods.SetForegroundWindow(_ownerHandle);
        }
        try { _selectionWindow?.Close(); } catch (InvalidOperationException) { }
        _selectionWindow = null;
        _selectionSurface = null;
        _snapshot = null;
        _snip = null;
        _selection = Rect.Empty;
        bool restored = mayReturn && TargetStillExists() && NativeMethods.GetForegroundWindow() == _sourceWindow;
        // Do not replace a clip rectangle another app installed while we were active, or trap an unrelated app.
        if (_releasedClip && restored
            && NativeMethods.GetClipCursor(out var currentClip) && currentClip.SameAs(_fullDesktop))
            NativeMethods.RestoreClipCursor(ref _originalClip);
        _releasedClip = false;
        if (hadSession) NotifyState(false);
        return restored;
    }

    private bool OwnsInputFocus() => _selectionWindow is { Handle: not 0 }
        && NativeMethods.GetForegroundWindow() == _selectionWindow.Handle;

    private bool TargetStillExists()
    {
        if (_sourceWindow == 0 || !NativeMethods.IsWindow(_sourceWindow) || NativeMethods.IsIconic(_sourceWindow)) return false;
        NativeMethods.GetWindowThreadProcessId(_sourceWindow, out uint currentProcess);
        return currentProcess == _sourceProcess;
    }

    private void CloseGhost()
    {
        try { _ghost?.Close(); } catch (InvalidOperationException) { }
        _ghost = null;
        _ghostSurface = null;
    }

    private void Notify(string message)
    {
        // Status is informational; a host formatting/notification failure must never strand an overlay.
        try { _status(message); } catch { }
    }

    private void NotifyState(bool active)
    {
        try { CaptureStateChanged?.Invoke(active); } catch { }
    }

    private void ReportError(string message)
    {
        Diagnostic(message);
        Notify(message);
        try { CaptureError?.Invoke(message); } catch { }
    }

    private static void Diagnostic(string message)
    {
        try { App.WriteCaptureDiagnostic(message); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _owner.Dispatcher.VerifyAccess();
        Finish(true, true);
        _disposed = true;
        _timer.Tick -= Tick;
        if (_hotkeyId != 0) NativeMethods.UnregisterHotKey(_ownerHandle, _hotkeyId);
        _hotkeyId = 0;
        _ownerSource.RemoveHook(OwnerWindowProc);
    }
}
