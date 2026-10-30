using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GameScrapbook.Core;
using GameScrapbook.Windows.Capture;
using Microsoft.Win32;

namespace GameScrapbook.Windows;

public partial class MainWindow : Window
{
    private readonly BoardStore _store;
    private readonly BoardHistory _history = new();
    private readonly Dictionary<string, Border> _cards = new();
    private readonly Dictionary<string, Border> _resizeGrips = new();
    private readonly Dictionary<string, Border> _cardOutlines = new();
    private readonly Canvas _snapGuideLayer = new() { IsHitTestVisible = false };
    private readonly Dictionary<string, BitmapSource> _images = new();
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _snipCountdown;
    private readonly BoardLoadResult _load;
    private BoardDocument _document;
    private CaptureCoordinator? _capture;
    private string? _selectedId;
    private bool _initializing = true;
    private bool _dialogOpen;
    private bool _dirty;
    private bool _closing;
    private double _zoom = 1;
    private Border? _dragCard;
    private SnipRecord? _dragSnip;
    private SnipRecord? _dragOriginal;
    private BoardDocument? _dragBefore;
    private Point _dragStart;
    private bool _resizing;
    private bool _changedDuringDrag;
    private int _countdownRemaining;

    public MainWindow() : this(new BoardStore()) { }

    internal MainWindow(BoardStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        InitializeComponent();
        _load = _store.Load();
        _document = _load.Document;
        SnapEdgesCheckBox.IsChecked = _document.Settings.SnapToSnips;
        ThemeManager.Apply(_document.Settings);
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };
        _snipCountdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _snipCountdown.Tick += SnipCountdown_Tick;
        Topmost = _document.Settings.Topmost;
        _zoom = Math.Clamp(_document.Settings.BoardZoom, .25, 3);
        SetZoom(_zoom, false);
        UpdateShortcutLabels();
        RenderBoard();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_capture is not null) return;
        WindowPlacement.Restore(this, _document.Settings);
        _capture = new CaptureCoordinator(this, FindDropTarget, AddCapture, ShowStatus)
        {
            ReturnCursor = _document.Settings.ReturnCursor,
            CanBeginCapture = () => !_dialogOpen && _load.CanSave && _dragCard is null
        };
        _capture.CaptureStateChanged += active =>
        {
            if (active)
            {
                StopSnipCountdown();
                CaptureNotice.Visibility = Visibility.Collapsed;
            }
            if (!active) SetDropHighlight(false);
            SnipButton.Content = active ? "Cancel snip" : "Snip";
            SettingsButtonEnabled(!active);
        };
        _capture.CaptureError += message =>
        {
            StopSnipCountdown();
            ShowCaptureNotice(message, true);
            UpdateShortcutAvailability();
        };
        if (_load.CanSave)
        {
            if (!_capture.RegisterHotkey(_document.Settings.CaptureHotkeyModifiers, _document.Settings.CaptureHotkeyKey, out string? error))
                ShowCaptureNotice(error ?? "The capture shortcut is unavailable. Use Snip or choose another shortcut in Settings.", true);
        }
        else ShowCaptureNotice("Saving and editing are disabled to protect your existing board. Open the data folder from Settings.", true);
        SnipButton.IsEnabled = _load.CanSave;
        UpdateShortcutAvailability();

        _initializing = false;
        if (_load.Warnings.Count > 0)
        {
            ShowStatus(_load.RecoveredFromBackup ? "Recovered your previous saved board. See the recovery notice." : "Some saved data needs attention. See the recovery notice.");
            _dialogOpen = true;
            try
            {
                MessageBox.Show(this, string.Join("\n\n", _load.Warnings.Take(6)) +
                    (_load.Warnings.Count > 6 ? $"\n\n…and {_load.Warnings.Count - 6} more image warnings." : "") +
                    "\n\nData folder: " + _store.RootDirectory,
                    "Game Scrapbook — recovery notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { _dialogOpen = false; }
        }
    }

    private void Snip_Click(object sender, RoutedEventArgs e)
    {
        if (_capture is null || !_load.CanSave || _dialogOpen) return;
        if (_capture.IsCapturing) { _capture.Cancel(); return; }
        if (_snipCountdown.IsEnabled)
        {
            StopSnipCountdown();
            CaptureNotice.Visibility = Visibility.Collapsed;
            ShowStatus("Snip countdown cancelled.");
            App.WriteCaptureDiagnostic("Button countdown cancelled by user.");
            return;
        }
        FinishCardDrag();
        _countdownRemaining = 3;
        App.WriteCaptureDiagnostic("Snip button pressed; starting 3-second countdown.");
        UpdateCountdownNotice();
        _snipCountdown.Start();
    }

    private void SnipCountdown_Tick(object? sender, EventArgs e)
    {
        if (_closing || _dialogOpen || _capture is null)
        {
            StopSnipCountdown();
            return;
        }
        _countdownRemaining--;
        if (_countdownRemaining > 0) { UpdateCountdownNotice(); return; }
        StopSnipCountdown();
        CaptureNotice.Visibility = Visibility.Collapsed;
        App.WriteCaptureDiagnostic("Button countdown completed; starting pointer-screen capture.");
        _capture.BeginCapture();
    }

    private void UpdateCountdownNotice()
    {
        SnipButton.Content = $"Cancel ({_countdownRemaining})";
        ShowCaptureNotice($"Snipping in {_countdownRemaining}… Move the mouse onto the screen you want to capture. You can click your game before the countdown ends.", false);
    }

    private void StopSnipCountdown()
    {
        _snipCountdown.Stop();
        _countdownRemaining = 0;
        SnipButton.Content = _capture?.IsCapturing == true ? "Cancel snip" : "Snip";
    }

    private void ShowCaptureNotice(string message, bool error)
    {
        CaptureNoticeText.Text = message;
        CaptureNoticeText.SetResourceReference(TextBlock.ForegroundProperty, error ? "ErrorBrush" : "TextBrush");
        CaptureNotice.Visibility = Visibility.Visible;
        ShowStatus(message);
    }

    private void UpdateShortcutAvailability()
    {
        if (!_load.CanSave)
        {
            ShortcutStatusLabel.Text = "Capture disabled while your saved board needs recovery.";
            ShortcutStatusLabel.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            return;
        }
        bool ready = _capture?.IsHotkeyRegistered == true;
        ShortcutStatusLabel.Text = ready
            ? "Shortcut ready · Captures the screen under your mouse. Snip gives you a 3-second countdown."
            : "Shortcut unavailable · Use Snip, or choose another shortcut in Settings.";
        ShortcutStatusLabel.SetResourceReference(TextBlock.ForegroundProperty, ready ? "TextBrush" : "ErrorBrush");
    }

    private void SettingsButtonEnabled(bool enabled)
    {
        // The capture shortcut can be pressed again to cancel, while modal settings
        // must never appear in the middle of the two-stage selection/drag gesture.
        UndoButton.IsEnabled = enabled && _load.CanSave && _history.CanUndo;
        DeleteButton.IsEnabled = enabled && _load.CanSave && _selectedId is not null;
        ArchiveButton.IsEnabled = enabled;
        ArchiveSelectedButton.IsEnabled = enabled && _load.CanSave && _selectedId is not null;
        MagnetiseButton.IsEnabled = enabled && _load.CanSave && _document.Snips.Count(s => !s.IsArchived) > 1;
        ExportButton.IsEnabled = enabled && _document.Snips.Any(s => !s.IsArchived);
        SnapEdgesCheckBox.IsEnabled = enabled && _load.CanSave;
    }

    private CaptureDropTarget? FindDropTarget(Point screenPosition)
    {
        bool accepted = false;
        Point boardPoint = default;
        if (!_closing && !_dialogOpen && _load.CanSave && IsVisible && WindowState != WindowState.Minimized &&
            BoardScroll.ActualWidth > 0 && WindowPlacement.IsUnobscuredAt(this, screenPosition))
        {
            Point local = BoardScroll.PointFromScreen(screenPosition);
            // Exclude scrollbars: only the actual visible canvas accepts a drop.
            double viewWidth = Math.Max(0, BoardScroll.ViewportWidth);
            double viewHeight = Math.Max(0, BoardScroll.ViewportHeight);
            if (local.X >= 0 && local.Y >= 0 && local.X < viewWidth && local.Y < viewHeight)
            {
                boardPoint = BoardCanvas.PointFromScreen(screenPosition);
                accepted = boardPoint.X >= 0 && boardPoint.Y >= 0;
            }
        }
        SetDropHighlight(accepted);
        return accepted ? new CaptureDropTarget(boardPoint, VisualTreeHelper.GetDpi(BoardCanvas).DpiScaleX * _zoom) : null;
    }

    private void SetDropHighlight(bool show)
    {
        DropHint.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        BoardFrame.SetResourceReference(Border.BorderBrushProperty, show ? "AccentBrush" : "BorderBrush");
    }

    private void AddCapture(BitmapSource bitmap, Point boardPosition)
    {
        if (!_load.CanSave) throw new IOException("Saving is disabled to protect your existing board.");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var bytes = new MemoryStream();
        encoder.Save(bytes);
        StoredImage stored = _store.SaveImage(bytes.ToArray());
        var before = _document.DeepClone();
        double scale = Math.Min(1, Math.Min(520.0 / stored.PixelWidth, 420.0 / stored.PixelHeight));
        var snip = new SnipRecord
        {
            ImageFile = stored.FileName,
            X = Math.Max(0, boardPosition.X),
            Y = Math.Max(0, boardPosition.Y),
            Width = stored.PixelWidth * scale,
            Height = stored.PixelHeight * scale,
            PixelWidth = stored.PixelWidth,
            PixelHeight = stored.PixelHeight
        };
        if (_document.Settings.SnapToSnips && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            BoardSnapResult placement = BoardSnapping.Snap(SnapBounds(snip),
                _document.Snips.Select(SnapBounds), 10 / _zoom);
            snip.X = placement.X;
            snip.Y = placement.Y;
        }
        _document.Snips.Add(snip);
        try
        {
            // Complete the durable save before telling the capture coordinator that
            // this drop succeeded. A failure leaves the draggable snip available.
            _store.Save(_document);
        }
        catch
        {
            // Existing card handlers retain their SnipRecord references. Roll back
            // only the new record, leaving those existing model objects intact.
            _document.Snips.Remove(snip);
            throw;
        }
        _history.Record(before);
        // RenderBoard decodes the saved crop, so a cached CroppedBitmap cannot
        // retain the entire original monitor snapshot for every small snip.
        _selectedId = null;
        _dirty = false;
        _saveTimer.Stop();
        RenderBoard();
        SetDropHighlight(false);
        ShowStatus("Snip saved. Returning to your game.");
        // Deliberately do not Activate(), Focus(), or start an OLE drag here.
    }

    private void RenderBoard()
    {
        SnipRecord[] activeSnips = _document.Snips.Where(s => !s.IsArchived).ToArray();
        int archivedCount = _document.Snips.Count - activeSnips.Length;
        var liveIds = activeSnips.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        foreach (string unusedId in _images.Keys.Where(id => !liveIds.Contains(id)).ToArray())
            _images.Remove(unusedId); // Undo reloads immutable PNGs on demand.
        BoardCanvas.Children.Clear();
        _cards.Clear();
        _resizeGrips.Clear();
        _cardOutlines.Clear();
        _snapGuideLayer.Children.Clear();
        foreach (SnipRecord snip in activeSnips)
        {
            Border card = CreateCard(snip);
            _cards.Add(snip.Id, card);
            BoardCanvas.Children.Add(card);
        }
        Panel.SetZIndex(_snapGuideLayer, int.MaxValue);
        BoardCanvas.Children.Add(_snapGuideLayer);
        ResizeCanvas();
        UpdateSelection();
        EmptyState.Visibility = activeSnips.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = archivedCount > 0 ? "Your board is clear" : "Drop your first snip here";
        EmptyArchiveHint.Visibility = archivedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        SnipCount.Text = $"{activeSnips.Length} {(activeSnips.Length == 1 ? "snip" : "snips")}";
        ArchiveButton.Content = $"Archive ({archivedCount})";
    }

    private Border CreateCard(SnipRecord snip)
    {
        var card = new Border
        {
            Width = snip.Width, Height = snip.Height,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.SizeAll,
            Tag = snip.Id,
            ToolTip = "Drag to move and snap · Hold Shift to move freely · × or Delete to remove"
        };
        card.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Canvas.SetLeft(card, snip.X);
        Canvas.SetTop(card, snip.Y);
        var content = new Grid { ClipToBounds = false };
        var picture = new Grid { ClipToBounds = true };
        content.Children.Add(picture);
        BitmapSource? image = GetImage(snip);
        if (image is not null)
        {
            var imageControl = new Image { Source = image, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false };
            RenderOptions.SetBitmapScalingMode(imageControl, BitmapScalingMode.HighQuality);
            picture.Children.Add(imageControl);
        }
        else
        {
            var missingImage = new TextBlock
            {
                Text = "Image unavailable\nIts saved position has been kept.",
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10)
            };
            missingImage.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            picture.Children.Add(missingImage);
        }
        var outline = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false };
        outline.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        _cardOutlines[snip.Id] = outline;
        content.Children.Add(outline);
        var grip = new Border
        {
            Name = "ResizeGrip", Width = Math.Min(17, Math.Min(snip.Width, snip.Height) * .35),
            Height = Math.Min(17, Math.Min(snip.Width, snip.Height) * .35),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
            CornerRadius = new CornerRadius(3, 0, 0, 0),
            Cursor = Cursors.SizeNWSE, Visibility = Visibility.Collapsed,
            Child = new TextBlock { Text = "◢", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center }
        };
        grip.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
        ((TextBlock)grip.Child).SetResourceReference(TextBlock.ForegroundProperty, "AccentTextBrush");
        grip.MouseLeftButtonDown += (_, e) => BeginCardDrag(card, snip, true, e);
        _resizeGrips[snip.Id] = grip;

        // Image bounds are the card bounds, so joined snips also join in exports.
        // Archive selected lives in the toolbar and never covers captured text.
        content.Children.Add(grip);
        var delete = new Button
        {
            Width = 24, Height = 24, MinWidth = 24, MinHeight = 24,
            Padding = new Thickness(0), Margin = new Thickness(3), Cursor = Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            IsEnabled = _load.CanSave, ToolTip = "Delete this snip · Ctrl+Z to undo",
            Visibility = snip.Width >= 36 && snip.Height >= 36 ? Visibility.Visible : Visibility.Collapsed
        };
        delete.SetResourceReference(Button.BackgroundProperty, "ControlBrush");
        delete.SetResourceReference(Button.ForegroundProperty, "TextBrush");
        // Symmetric geometry avoids the font baseline and side bearings of a text ×.
        var deleteGlyph = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 1,1 L 11,11 M 11,1 L 1,11"),
            Width = 12, Height = 12, Stretch = Stretch.None, StrokeThickness = 1.75,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };
        deleteGlyph.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "TextBrush");
        delete.Content = deleteGlyph;
        if (delete.Visibility == Visibility.Collapsed)
            card.ToolTip = "Drag to move · Select and use toolbar Delete or Archive selected · Zoom in to resize a small snip";
        System.Windows.Automation.AutomationProperties.SetName(delete, "Delete this snip");
        delete.Click += (_, e) =>
        {
            e.Handled = true;
            DeleteSnip(snip.Id);
        };
        void UpdateCardControls()
        {
            double shortEdge = Math.Min(card.Width, card.Height);
            grip.Width = grip.Height = Math.Min(17, shortEdge * .35);
            grip.ClipToBounds = true;
            ((TextBlock)grip.Child).FontSize = Math.Min(13, grip.Width * .8);
            delete.Visibility = shortEdge >= 36 ? Visibility.Visible : Visibility.Collapsed;
            card.ToolTip = delete.Visibility == Visibility.Visible
                ? "Drag to move and snap · Hold Shift to move freely · × or Delete to remove"
                : "Drag to move · Select and use toolbar Delete or Archive selected · Zoom in to resize a small snip";
        }
        card.SizeChanged += (_, _) => UpdateCardControls();
        UpdateCardControls();
        Panel.SetZIndex(delete, 1);
        content.Children.Add(delete);
        card.Child = content;
        card.MouseLeftButtonDown += (_, e) => BeginCardDrag(card, snip, false, e);
        card.MouseMove += Card_MouseMove;
        card.MouseLeftButtonUp += Card_MouseUp;
        card.LostMouseCapture += Card_LostCapture;
        return card;
    }

    private BitmapSource? GetImage(SnipRecord snip)
    {
        if (_images.TryGetValue(snip.Id, out BitmapSource? cached)) return cached;
        try
        {
            using var stream = File.OpenRead(_store.GetImagePath(snip.ImageFile));
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapFrame frame = decoder.Frames[0];
            frame.Freeze();
            _images[snip.Id] = frame;
            return frame;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            App.WriteDiagnostic(ex);
            return null;
        }
    }

    private BitmapSource? LoadArchivedImage(SnipRecord snip, int maximumEdge)
    {
        try
        {
            using var stream = File.OpenRead(_store.GetImagePath(snip.ImageFile));
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            // Bound the longest edge, including very tall or narrow snippets.
            if (maximumEdge > 0 && Math.Max(snip.PixelWidth, snip.PixelHeight) > maximumEdge)
            {
                if (snip.PixelWidth >= snip.PixelHeight) image.DecodePixelWidth = maximumEdge;
                else image.DecodePixelHeight = maximumEdge;
            }
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            App.WriteDiagnostic(ex);
            return null;
        }
    }

    private bool SetArchived(string snipId, bool archived)
    {
        if (!_load.CanSave || _capture?.IsCapturing == true) return false;
        FinishCardDrag();
        SnipRecord? snip = _document.Snips.FirstOrDefault(s => s.Id == snipId);
        if (snip is null || snip.IsArchived == archived) return false;
        BoardDocument before = _document.DeepClone();
        DateTimeOffset? previousArchivedUtc = snip.ArchivedUtc;
        snip.IsArchived = archived;
        snip.ArchivedUtc = archived ? DateTimeOffset.UtcNow : null;
        try
        {
            // The board only hides/restores a card after its new state is durable.
            _store.Save(_document);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            snip.IsArchived = !archived;
            snip.ArchivedUtc = previousArchivedUtc;
            App.WriteDiagnostic(ex);
            ShowCaptureNotice($"Could not {(archived ? "archive" : "restore")} this snip. " + ex.Message, true);
            return false;
        }
        _history.Record(before);
        _dirty = false;
        _saveTimer.Stop();
        _selectedId = archived ? null : snip.Id;
        RenderBoard();
        if (!archived)
        {
            BoardCanvas.UpdateLayout();
            BoardScroll.ScrollToHorizontalOffset(Math.Max(0, snip.X * _zoom - 24));
            BoardScroll.ScrollToVerticalOffset(Math.Max(0, snip.Y * _zoom - 24));
        }
        ShowStatus(archived
            ? "Snip archived. Open Archive to view it, or press Ctrl+Z to undo."
            : "Snip restored to its saved position. Ctrl+Z undoes the restore.");
        return true;
    }

    private void Archive_Click(object sender, RoutedEventArgs e)
    {
        if (_capture?.IsCapturing == true || _dialogOpen) return;
        if (_snipCountdown.IsEnabled) { StopSnipCountdown(); CaptureNotice.Visibility = Visibility.Collapsed; }
        FinishCardDrag();
        _dialogOpen = true;
        try
        {
            var archive = new ArchiveWindow(
                _document.Snips.Where(s => s.IsArchived).Select(s => s.Clone()),
                LoadArchivedImage, id => SetArchived(id, false)) { Owner = this };
            archive.ShowDialog();
        }
        finally { _dialogOpen = false; }
    }

    private void BeginCardDrag(Border card, SnipRecord snip, bool resize, MouseButtonEventArgs e)
    {
        if (!_load.CanSave || _capture?.IsCapturing == true || e.Handled) return;
        e.Handled = true;
        _selectedId = snip.Id;
        BoardCanvas.Focus();
        UpdateSelection();
        _dragCard = card;
        _dragSnip = snip;
        _dragOriginal = snip.Clone();
        _dragBefore = _document.DeepClone();
        _dragStart = e.GetPosition(BoardCanvas);
        _resizing = resize;
        _changedDuringDrag = false;
        card.CaptureMouse();
    }

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCard is null || _dragSnip is null || _dragOriginal is null || e.LeftButton != MouseButtonState.Pressed) return;
        Point p = e.GetPosition(BoardCanvas);
        double dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;
        if (!_changedDuringDrag && Math.Abs(dx) + Math.Abs(dy) < 3 / _zoom) return;
        _changedDuringDrag = true;
        if (_resizing)
        {
            double aspect = (double)_dragSnip.PixelWidth / _dragSnip.PixelHeight;
            double deltaWidth = Math.Abs(dx) >= Math.Abs(dy * aspect) ? dx : dy * aspect;
            double minimumWidth = Math.Max(16, 16 * aspect);
            double maximumWidth = Math.Min(12000, 12000 * aspect);
            minimumWidth = Math.Min(minimumWidth, maximumWidth);
            _dragSnip.Width = Math.Clamp(_dragOriginal.Width + deltaWidth, minimumWidth, maximumWidth);
            _dragSnip.Height = _dragSnip.Width / aspect;
            _dragCard.Width = _dragSnip.Width;
            _dragCard.Height = _dragSnip.Height;
            _snapGuideLayer.Children.Clear();
        }
        else
        {
            _dragSnip.X = Math.Clamp(_dragOriginal.X + dx, 0, 90000);
            _dragSnip.Y = Math.Clamp(_dragOriginal.Y + dy, 0, 90000);
            if (_document.Settings.SnapToSnips && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                BoardSnapResult placement = BoardSnapping.Snap(SnapBounds(_dragSnip),
                    _document.Snips.Select(SnapBounds), 10 / _zoom);
                _dragSnip.X = placement.X;
                _dragSnip.Y = placement.Y;
                ShowSnapGuides(placement);
            }
            else _snapGuideLayer.Children.Clear();
            Canvas.SetLeft(_dragCard, _dragSnip.X);
            Canvas.SetTop(_dragCard, _dragSnip.Y);
        }
        ResizeCanvas();
    }

    private void Card_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragCard is null) return;
        e.Handled = true;
        FinishCardDrag();
    }

    private void Card_LostCapture(object sender, MouseEventArgs e)
    {
        if (_dragCard is not null) FinishCardDrag();
    }

    private void FinishCardDrag()
    {
        Border? card = _dragCard;
        _dragCard = null;
        if (card?.IsMouseCaptured == true) card.ReleaseMouseCapture();
        if (_changedDuringDrag && _dragBefore is not null && _dragSnip is not null && _dragOriginal is not null
            && (_dragSnip.X != _dragOriginal.X || _dragSnip.Y != _dragOriginal.Y
                || _dragSnip.Width != _dragOriginal.Width || _dragSnip.Height != _dragOriginal.Height))
        {
            _history.Record(_dragBefore);
            MarkDirty();
            ShowStatus("Layout updated.");
        }
        _dragBefore = null;
        _dragSnip = null;
        _dragOriginal = null;
        _changedDuringDrag = false;
        _snapGuideLayer.Children.Clear();
        UpdateSelection();
    }

    private void ResizeCanvas()
    {
        SnipRecord[] activeSnips = _document.Snips.Where(s => !s.IsArchived).ToArray();
        BoardCanvas.Width = Math.Max(2400, activeSnips.Length == 0 ? 0 : activeSnips.Max(s => s.X + s.Width) + 400);
        BoardCanvas.Height = Math.Max(1600, activeSnips.Length == 0 ? 0 : activeSnips.Max(s => s.Y + s.Height) + 400);
    }

    private void UpdateSelection()
    {
        if (_selectedId is not null && !_cards.ContainsKey(_selectedId)) _selectedId = null;
        int index = 0;
        foreach (var (id, card) in _cards)
        {
            bool selected = id == _selectedId;
            if (_cardOutlines.TryGetValue(id, out Border? outline))
            {
                outline.SetResourceReference(Border.BorderBrushProperty, selected ? "AccentBrush" : "BorderBrush");
                outline.BorderThickness = new Thickness(selected ? 2 : 1);
            }
            Panel.SetZIndex(card, selected ? 100000 : index++);
            if (_resizeGrips.TryGetValue(id, out Border? grip))
                grip.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
        UndoButton.IsEnabled = _load.CanSave && _history.CanUndo && _capture?.IsCapturing != true;
        DeleteButton.IsEnabled = _load.CanSave && _selectedId is not null && _capture?.IsCapturing != true;
        ArchiveButton.IsEnabled = _capture?.IsCapturing != true;
        ArchiveSelectedButton.IsEnabled = _load.CanSave && _selectedId is not null && _capture?.IsCapturing != true;
        MagnetiseButton.IsEnabled = _load.CanSave && _cards.Count > 1 && _capture?.IsCapturing != true;
        ExportButton.IsEnabled = _cards.Count > 0 && _capture?.IsCapturing != true;
        SnapEdgesCheckBox.IsEnabled = _load.CanSave && _capture?.IsCapturing != true;
    }

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != BoardCanvas) return;
        _selectedId = null;
        BoardCanvas.Focus();
        UpdateSelection();
    }

    private static BoardSnapRect SnapBounds(SnipRecord snip) =>
        new(snip.Id, snip.X, snip.Y, snip.Width, snip.Height, snip.IsArchived);

    private void ShowSnapGuides(BoardSnapResult placement)
    {
        _snapGuideLayer.Children.Clear();
        if (placement.VerticalGuide is double x) AddGuide(x, 0, x, BoardCanvas.Height);
        if (placement.HorizontalGuide is double y) AddGuide(0, y, BoardCanvas.Width, y);
        void AddGuide(double x1, double y1, double x2, double y2)
        {
            var guide = new System.Windows.Shapes.Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                StrokeThickness = 1.5 / _zoom, StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            guide.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AccentBrush");
            _snapGuideLayer.Children.Add(guide);
        }
    }

    private void SnapEdges_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || !_load.CanSave) return;
        _document.Settings.SnapToSnips = SnapEdgesCheckBox.IsChecked == true;
        _snapGuideLayer.Children.Clear();
        MarkDirty();
        ShowStatus(_document.Settings.SnapToSnips
            ? "Edge snapping on. Hold Shift while dragging to move freely."
            : "Edge snapping off. Magnetise can still bring the whole board together.");
    }

    private void ArchiveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId is not null && SetArchived(_selectedId, true)) BoardCanvas.Focus();
    }

    private void Magnetise_Click(object sender, RoutedEventArgs e)
    {
        if (!_load.CanSave || _dialogOpen || _capture?.IsCapturing == true) return;
        StopSnipCountdown();
        CaptureNotice.Visibility = Visibility.Collapsed;
        FinishCardDrag();
        BoardDocument before = _document.DeepClone();
        bool wasDirty = _dirty;
        try
        {
            IReadOnlyList<BoardSnapRect> packed = BoardPacking.Magnetise(_document.Snips.Select(SnapBounds));
            var positions = packed.ToDictionary(rect => rect.Id, StringComparer.Ordinal);
            bool changed = _document.Snips.Any(s => !s.IsArchived
                && (s.X != positions[s.Id].X || s.Y != positions[s.Id].Y));
            if (!changed) { ShowStatus("Your snips are already together."); return; }
            foreach (SnipRecord snip in _document.Snips.Where(s => !s.IsArchived))
            {
                snip.X = positions[snip.Id].X;
                snip.Y = positions[snip.Id].Y;
            }
            _store.Save(_document);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _document = before;
            _dirty = wasDirty;
            RenderBoard();
            ShowCaptureNotice("Could not magnetise the board. " + ex.Message, true);
            return;
        }
        _history.Record(before);
        _dirty = false;
        _saveTimer.Stop();
        _selectedId = null;
        RenderBoard();
        BoardScroll.ScrollToHorizontalOffset(0);
        BoardScroll.ScrollToVerticalOffset(0);
        ShowStatus("Snips magnetised. Ctrl+Z restores the previous arrangement.");
    }

    private BitmapSource CreateBoardExport()
    {
        BoardDocument snapshot = _document.DeepClone();
        // Selection raises a card above overlaps on the board. Preserve that
        // visible stacking in the export without changing the saved document.
        SnipRecord? selected = snapshot.Snips.FirstOrDefault(s => s.Id == _selectedId && !s.IsArchived);
        if (selected is not null) { snapshot.Snips.Remove(selected); snapshot.Snips.Add(selected); }
        var background = (Color)ColorConverter.ConvertFromString(snapshot.Settings.BackgroundColor);
        return BoardImageExporter.Create(snapshot, GetImage, background);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_dialogOpen || _capture?.IsCapturing == true || !_document.Snips.Any(s => !s.IsArchived)) return;
        StopSnipCountdown();
        CaptureNotice.Visibility = Visibility.Collapsed;
        FinishCardDrag();
        _dialogOpen = true;
        Cursor? previousCursor = Mouse.OverrideCursor;
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export scrapbook image", FileName = $"Game-Scrapbook-{DateTime.Now:yyyy-MM-dd}",
                Filter = "JPG image (*.jpg;*.jpeg)|*.jpg;*.jpeg|PNG image (*.png)|*.png",
                DefaultExt = ".jpg", AddExtension = true, OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != true) return;
            ValidateExportDestination(dialog.FileName);
            Mouse.OverrideCursor = Cursors.Wait;
            BitmapSource image = CreateBoardExport();
            string extension = Path.GetExtension(dialog.FileName);
            BoardImageFormat format = extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? BoardImageFormat.Png : BoardImageFormat.Jpeg;
            BoardImageExporter.Save(image, dialog.FileName, format);
            ShowStatus($"Exported {image.PixelWidth} × {image.PixelHeight} image to {Path.GetFileName(dialog.FileName)}.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or NotSupportedException or FormatException
            or System.Runtime.InteropServices.COMException or OutOfMemoryException)
        {
            ShowCaptureNotice("Could not export the board. " + ex.Message, true);
        }
        finally { Mouse.OverrideCursor = previousCursor; _dialogOpen = false; }
    }

    private void ValidateExportDestination(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string destination = Path.GetFullPath(path);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_store.RootDirectory));
        string prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (destination.Equals(root, StringComparison.OrdinalIgnoreCase)
            || destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose an export location outside the saved-board folder to protect your original snips.", nameof(path));
    }

    private void DeleteSelected()
    {
        if (_selectedId is not null) DeleteSnip(_selectedId);
    }

    private void DeleteSnip(string snipId)
    {
        if (!_load.CanSave || _capture?.IsCapturing == true) return;
        FinishCardDrag();
        var snip = _document.Snips.FirstOrDefault(s => s.Id == snipId && !s.IsArchived);
        if (snip is null) return;
        _history.Record(_document);
        _document.Snips.Remove(snip);
        if (_selectedId == snipId) _selectedId = null;
        RenderBoard();
        _dirty = true;
        if (SaveNow()) ShowStatus("Snip deleted. Ctrl+Z brings it back.");
        BoardCanvas.Focus();
    }

    private void Undo()
    {
        if (!_load.CanSave || _capture?.IsCapturing == true) return;
        FinishCardDrag();
        BoardDocument? previous = _history.Undo(_document);
        if (previous is null) return;
        _document = previous;
        _selectedId = null;
        RenderBoard();
        _dirty = true;
        if (SaveNow()) ShowStatus("Undone. Ctrl+Y redoes it.");
    }

    private void Redo()
    {
        if (!_load.CanSave || _capture?.IsCapturing == true) return;
        FinishCardDrag();
        BoardDocument? next = _history.Redo(_document);
        if (next is null) return;
        _document = next;
        _selectedId = null;
        RenderBoard();
        _dirty = true;
        if (SaveNow()) ShowStatus("Redone.");
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_dialogOpen || _capture?.IsCapturing == true || e.OriginalSource is TextBox) return;
        if (e.Key == Key.Escape && _snipCountdown.IsEnabled)
        {
            StopSnipCountdown();
            CaptureNotice.Visibility = Visibility.Collapsed;
            ShowStatus("Snip countdown cancelled.");
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None) { DeleteSelected(); e.Handled = true; }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) { Undo(); e.Handled = true; }
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) { Redo(); e.Handled = true; }
        else if (e.Key == Key.Escape) { _selectedId = null; UpdateSelection(); e.Handled = true; }
    }

    private void SetZoom(double zoom, bool save = true)
    {
        _zoom = Math.Clamp(zoom, .25, 3);
        BoardScale.ScaleX = BoardScale.ScaleY = _zoom;
        ZoomButton.Content = $"{_zoom:P0}";
        if (save && !_initializing)
        {
            _document.Settings.BoardZoom = _zoom;
            MarkDirty();
        }
    }

    private void Board_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || _capture?.IsCapturing == true) return;
        Point before = e.GetPosition(BoardCanvas);
        Point viewport = e.GetPosition(BoardScroll);
        SetZoom(_zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15));
        BoardCanvas.UpdateLayout();
        BoardScroll.ScrollToHorizontalOffset(before.X * _zoom - viewport.X);
        BoardScroll.ScrollToVerticalOffset(before.Y * _zoom - viewport.Y);
        e.Handled = true;
    }

    private void MarkDirty()
    {
        if (_initializing || !_load.CanSave) return;
        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private bool SaveNow()
    {
        _saveTimer.Stop();
        if (!_dirty || !_load.CanSave) return !_dirty;
        try
        {
            _store.Save(_document);
            _dirty = false;
            ShowStatus("All changes saved.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            App.WriteDiagnostic(ex);
            ShowStatus("Could not save your latest changes. Keep this window open. " + ex.Message);
            return false;
        }
    }

    private void Window_PlacementChanged(object? sender, EventArgs e)
    {
        if (_initializing || _closing) return;
        WindowPlacement.Remember(this, _document.Settings);
        MarkDirty();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => Window_PlacementChanged(sender, e);

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        StopSnipCountdown();
        _capture?.Cancel();
        FinishCardDrag();
        WindowPlacement.Remember(this, _document.Settings);
        if (_load.CanSave) _dirty = true;
        if (!SaveNow() && _load.CanSave)
        {
            if (MessageBox.Show(this, "The latest changes could not be saved. Keep Game Scrapbook open so you can try again?",
                "Save failed", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            { e.Cancel = true; return; }
        }
        _closing = true;
        _saveTimer.Stop();
        _capture?.Dispose();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_capture?.IsCapturing == true) return;
        if (_snipCountdown.IsEnabled) { StopSnipCountdown(); CaptureNotice.Visibility = Visibility.Collapsed; }
        _dialogOpen = true;
        var settings = new SettingsWindow(_document.Settings, _store.RootDirectory) { Owner = this };
        settings.ApplySettings = proposed =>
        {
            if (!_load.CanSave)
                return "Settings cannot be saved while your board needs recovery. Open the saved-board folder for details.";
            if (_capture is not null && _load.CanSave &&
                !_capture.RegisterHotkey(proposed.CaptureHotkeyModifiers, proposed.CaptureHotkeyKey, out string? error))
                return error ?? "That shortcut is already in use. Choose a different one.";
            _document.Settings.CaptureHotkeyModifiers = proposed.CaptureHotkeyModifiers;
            _document.Settings.CaptureHotkeyKey = proposed.CaptureHotkeyKey;
            _document.Settings.ReturnCursor = proposed.ReturnCursor;
            _document.Settings.Topmost = proposed.Topmost;
            _document.Settings.BackgroundColor = proposed.BackgroundColor;
            _document.Settings.TextColor = proposed.TextColor;
            _document.Settings.AccentColor = proposed.AccentColor;
            _document.Settings.ThemeDefaultsVersion = proposed.ThemeDefaultsVersion;
            ThemeManager.Apply(_document.Settings);
            if (_capture is not null) _capture.ReturnCursor = proposed.ReturnCursor;
            Topmost = proposed.Topmost;
            UpdateShortcutLabels();
            UpdateShortcutAvailability();
            CaptureNotice.Visibility = Visibility.Collapsed;
            MarkDirty();
            return null;
        };
        try { settings.ShowDialog(); }
        finally { _dialogOpen = false; }
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        if (_capture?.IsCapturing == true) return;
        if (_snipCountdown.IsEnabled) { StopSnipCountdown(); CaptureNotice.Visibility = Visibility.Collapsed; }
        _dialogOpen = true;
        try
        {
            MessageBox.Show(this,
                "1. Put this window on your second screen.\n" +
                "2. Put the mouse on the screen you want, then press " + HotkeyText(_document.Settings) + ". Or click Snip and move to that screen during the 3-second countdown.\n" +
                "3. Drag a rectangle around the information you want. Release.\n" +
                "4. Grab the selected cut-out and drag it onto this board.\n" +
                "5. Release to place it and return focus to your game.\n\n" +
                "Snipping temporarily takes focus so the game does not normally read the mouse drag. Some games pause while unfocused.\n\n" +
                "Click a saved snip to select it. Drag to move. Use its bottom-right grip to resize. Click its top-right × to delete it, or select it and press Delete. Ctrl+Z restores it; Ctrl+Y redoes. Ctrl+wheel zooms the board.\n\n" +
                "Escape or right-click cancels a capture. Dropping outside the board lets you try again.\n\n" +
                "Keep the app running for the shortcut to work. Borderless/windowed games are the first target. Games that lock the cursor or use raw mouse input may need their own cursor/menu setting; fullscreen capture and focus behaviour vary.\n\n" +
                "Select a snip and use Archive selected in the toolbar to keep it for later. Open Archive to browse saved images and restore their previous positions. Ctrl+Z undoes archiving or restoring.\n\n" +
                "Snap edges joins nearby images while moving them. Hold Shift to move freely. Magnetise brings all board snips into a compact arrangement; Ctrl+Z restores the old layout.\n\n" +
                "Export image saves all board snips together as JPG or PNG, cropped to the images. Buttons, selection outlines and archived snips are left out. Board zoom and scrolling do not change the export.\n\n" +
                "Open Settings to change the background, text and accent colours. Reset colours restores blue, white and dark grey.\n\n" +
                "If capture fails, its error appears above the board. Capture startup details are saved to capture-diagnostics.log in the data folder.\n\n" +
                "Your board saves locally to:\n" + _store.RootDirectory,
                "Game Scrapbook — quick guide", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally { _dialogOpen = false; }
    }

    private void UpdateShortcutLabels()
    {
        string shortcut = HotkeyText(_document.Settings);
        ShortcutLabel.Text = shortcut;
        EmptyInstructions.Text = $"Move the mouse to your game screen and press {shortcut}.\nOr click Snip, then move there during the countdown.";
    }

    internal static string HotkeyText(BoardSettings settings)
    {
        var parts = new List<string>();
        if ((settings.CaptureHotkeyModifiers & 2) != 0) parts.Add("Ctrl");
        if ((settings.CaptureHotkeyModifiers & 1) != 0) parts.Add("Alt");
        if ((settings.CaptureHotkeyModifiers & 4) != 0) parts.Add("Shift");
        var key = KeyInterop.KeyFromVirtualKey(settings.CaptureHotkeyKey);
        parts.Add(key.ToString().Replace("D0", "0").Replace("D1", "1").Replace("D2", "2").Replace("D3", "3")
            .Replace("D4", "4").Replace("D5", "5").Replace("D6", "6").Replace("D7", "7").Replace("D8", "8").Replace("D9", "9"));
        return string.Join(" + ", parts);
    }

    private void ShowStatus(string message) => StatusLabel.Text = message;
    internal void StopCaptureAfterError()
    {
        StopSnipCountdown();
        try { _capture?.Cancel(); } catch { }
        try { _capture?.Dispose(); } catch { }
    }
    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected();
    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / 1.2);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * 1.2);
    private void ResetZoom_Click(object sender, RoutedEventArgs e) => SetZoom(1);
}
