using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameScrapbook.Windows.Interop;

namespace GameScrapbook.Windows.Capture;

/// <summary>A real desktop window which neither activates on show nor on a pointer click.</summary>
internal sealed class PassiveWindow : Window
{
    private readonly bool _clickThrough;
    private readonly UIElement _content;
    private HwndSource? _source;
    private NativeMethods.NativeRect _requestedBounds;
    internal nint Handle { get; private set; }

    internal PassiveWindow(UIElement content, bool clickThrough)
    {
        _clickThrough = clickThrough;
        _content = content;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsEnabled = !clickThrough;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SizeToContent = SizeToContent.Manual;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Left = -32000; Top = -32000; Width = 64; Height = 64;
        Opacity = 0;
        Content = content;
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            NativeMethods.MakePassive(Handle, _clickThrough);
            _source = HwndSource.FromHwnd(Handle);
            _source?.AddHook(WindowProc);
        };
        Closed += (_, _) => { _source?.RemoveHook(WindowProc); _source = null; Handle = 0; };
    }

    internal void Present(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Capture windows need a non-empty display area.");

        // Prepare a non-empty managed layout while invisible. Native placement below
        // remains authoritative for physical coordinates and per-monitor DPI. In
        // particular, never render the first selection frame in a 1x1 window.
        Width = width;
        Height = height;
        // EnsureHandle applies WS_EX_NOACTIVATE and hooks BEFORE Show can receive input.
        _ = new WindowInteropHelper(this).EnsureHandle();
        Show();
        MovePhysical(x, y, width, height);
        UpdateLayout();
        ValidatePresented();
        Opacity = 1;
        _content.InvalidateVisual();
    }

    internal void MovePhysical(int x, int y, int width, int height)
    {
        _requestedBounds = new NativeMethods.NativeRect
        {
            Left = x, Top = y, Right = x + Math.Max(1, width), Bottom = y + Math.Max(1, height)
        };
        NativeMethods.PlacePhysical(Handle, x, y, width, height);
    }

    /// <summary>
    /// Establishes native bounds and a non-empty WPF layout, not proof that a GPU/game
    /// compositor actually displayed the overlay. Called once before revealing it.
    /// </summary>
    internal void ValidatePresented()
    {
        bool measured = _content.RenderSize.Width > 0 && _content.RenderSize.Height > 0;
        if (!IsVisible || !measured || !NativeMethods.TryWindowBounds(Handle, out var bounds))
            throw PresentationFailure("The capture overlay could not obtain a visible, non-empty layout.");

        const int pixelTolerance = 4;
        if (Math.Abs(bounds.Left - _requestedBounds.Left) > pixelTolerance ||
            Math.Abs(bounds.Top - _requestedBounds.Top) > pixelTolerance ||
            Math.Abs(bounds.Width - _requestedBounds.Width) > pixelTolerance ||
            Math.Abs(bounds.Height - _requestedBounds.Height) > pixelTolerance)
            throw PresentationFailure("Windows positioned the capture overlay outside the requested display area.");

        DpiScale dpi = VisualTreeHelper.GetDpi(_content);
        if (Math.Abs(_content.RenderSize.Width * dpi.DpiScaleX - bounds.Width) > pixelTolerance ||
            Math.Abs(_content.RenderSize.Height * dpi.DpiScaleY - bounds.Height) > pixelTolerance)
            throw PresentationFailure("The capture overlay layout did not match this display's scaling.");
    }

    internal string DescribeGeometry()
    {
        string native = NativeMethods.TryWindowBounds(Handle, out var bounds)
            ? FormattableString.Invariant($"{bounds.Left},{bounds.Top},{bounds.Width}x{bounds.Height}")
            : "unavailable";
        DpiScale dpi = VisualTreeHelper.GetDpi(_content);
        return FormattableString.Invariant(
            $"requested={_requestedBounds.Left},{_requestedBounds.Top},{_requestedBounds.Width}x{_requestedBounds.Height}; native={native}; content={_content.RenderSize.Width:0.##}x{_content.RenderSize.Height:0.##}; dpi={dpi.DpiScaleX:0.###},{dpi.DpiScaleY:0.###}; visible={IsVisible}; opacity={Opacity:0.##}");
    }

    private InvalidOperationException PresentationFailure(string message)
    {
        // Keep technical geometry in the diagnostic log, while the on-screen error
        // stays readable. A failure to log must not interrupt overlay cleanup.
        try { App.WriteCaptureDiagnostic("Overlay presentation failure: " + message + " " + DescribeGeometry()); } catch { }
        return new InvalidOperationException(message);
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == NativeMethods.WmMouseActivate)
        {
            handled = true;
            return NativeMethods.MaNoActivate;
        }
        if (_clickThrough && message == NativeMethods.WmNcHitTest)
        {
            handled = true;
            return NativeMethods.HtTransparent;
        }
        return 0;
    }
}

internal sealed class SelectionSurface : FrameworkElement
{
    private static readonly Brush Dim = FrozenBrush(Color.FromArgb(155, 0, 0, 0));
    private static readonly Brush InputShield = FrozenBrush(Color.FromArgb(1, 0, 0, 0));
    private readonly BitmapSource _snapshot;
    private readonly Brush _accent;
    private readonly Brush _hintBackground;
    private readonly Brush _hintForeground;
    private readonly Brush _hintBorder;
    private Rect _selection;
    internal bool IsDragging { get; set; }
    internal bool IsReady { get; set; }

    internal SelectionSurface(BitmapSource snapshot)
    {
        _snapshot = snapshot;
        _accent = (Brush)Application.Current.FindResource("AccentBrush");
        _hintBackground = (Brush)Application.Current.FindResource("PanelBrush");
        _hintForeground = (Brush)Application.Current.FindResource("TextBrush");
        _hintBorder = (Brush)Application.Current.FindResource("BorderBrush");
        Focusable = false;
        Cursor = Cursors.Cross;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    internal void SetSelection(Rect selection, bool ready = false, bool dragging = false)
    {
        _selection = selection;
        IsReady = ready;
        IsDragging = dragging;
        Cursor = ready ? Cursors.Hand : Cursors.Cross;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        var all = new Rect(0, 0, ActualWidth, ActualHeight);
        if (all.Width <= 0 || all.Height <= 0) return;
        if (IsDragging)
        {
            // Visually reveal the running game while retaining a hit-testable input shield on its display.
            drawing.DrawRectangle(InputShield, null, all);
            return;
        }

        drawing.DrawImage(_snapshot, all);
        drawing.DrawRectangle(Dim, null, all);
        if (!_selection.IsEmpty && _selection.Width > 0 && _selection.Height > 0)
        {
            double sx = ActualWidth / _snapshot.PixelWidth, sy = ActualHeight / _snapshot.PixelHeight;
            var shown = new Rect(_selection.X * sx, _selection.Y * sy, _selection.Width * sx, _selection.Height * sy);
            drawing.PushClip(new RectangleGeometry(shown));
            drawing.DrawImage(_snapshot, all);
            drawing.Pop();
            // A two-tone edge remains visible over arbitrary game pixels even
            // when the chosen accent is very dark or very light.
            drawing.DrawRectangle(null, new Pen(Brushes.Black, 5), shown);
            drawing.DrawRectangle(null, new Pen(Brushes.White, 3), shown);
            drawing.DrawRectangle(null, new Pen(_accent, 2), shown);
        }

        string instruction = IsReady
            ? "Grab the bright snip and drag it into Game Scrapbook  ·  Esc cancels"
            : "Drag around a recipe, quest or clue  ·  Esc cancels";
        // A temporary resize/layout transition can legitimately offer a very small
        // rectangle. Draw the image/tint, but never construct a negative-width hint.
        if (ActualWidth < 96 || ActualHeight < 72) return;
        var text = new FormattedText(instruction, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            16, _hintForeground, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { MaxTextWidth = Math.Max(80, ActualWidth - 64) };
        var hint = new Rect(Math.Max(12, (ActualWidth - text.Width - 32) / 2), 20, Math.Min(ActualWidth - 24, text.Width + 32), text.Height + 24);
        drawing.DrawRoundedRectangle(_hintBackground, new Pen(_hintBorder, 1), hint, 10, 10);
        drawing.DrawText(text, new Point(hint.X + 16, hint.Y + 12));
    }

    private static SolidColorBrush FrozenBrush(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }
}

internal sealed class DragSurface : FrameworkElement
{
    private readonly BitmapSource _image;
    private readonly Brush _accent;
    private readonly Brush _targetAccent;
    internal bool ValidTarget { get; set; }

    internal DragSurface(BitmapSource image)
    {
        _image = image;
        _accent = (Brush)Application.Current.FindResource("AccentBrush");
        _targetAccent = (Brush)Application.Current.FindResource("SuccessBrush");
        IsHitTestVisible = false;
        Focusable = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        drawing.DrawImage(_image, bounds);
        var border = ValidTarget ? _targetAccent : _accent;
        var outline = new Rect(2.5, 2.5, Math.Max(0, bounds.Width - 5), Math.Max(0, bounds.Height - 5));
        drawing.DrawRectangle(null, new Pen(Brushes.Black, 5), outline);
        drawing.DrawRectangle(null, new Pen(Brushes.White, 3), outline);
        drawing.DrawRectangle(null, new Pen(border, 2), outline);
    }
}
