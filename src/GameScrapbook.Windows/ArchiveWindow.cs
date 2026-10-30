using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameScrapbook.Core;

namespace GameScrapbook.Windows;

/// <summary>Browse archived snips without adding their full-sized images to the board cache.</summary>
internal sealed class ArchiveWindow : Window
{
    private readonly ObservableCollection<ArchiveEntry> _entries;
    private readonly Func<SnipRecord, int, BitmapSource?> _loadImage;
    private readonly Func<string, bool> _restoreSnip;
    private readonly ThumbnailConverter _thumbnails;
    private readonly ListBox _list = new();
    private readonly Image _image = new();
    private readonly ScrollViewer _imageScroll = new();
    private readonly TextBlock _count = Label("", 14);
    private readonly TextBlock _details = Label("", 14);
    private readonly TextBlock _placeholder = Label("", 17);
    private readonly TextBlock _message = Label("", 14);
    private readonly Button _restore = new() { Content = "Restore to scrapbook", IsEnabled = false };
    private readonly Button _fitButton = new() { Content = "Fit", ToolTip = "Fit the image in the window", MinWidth = 54 };
    private readonly Button _actualButton = new() { Content = "100%", ToolTip = "Show the image at its original pixel size", MinWidth = 68 };
    private BitmapSource? _selectedImage;
    private bool _fit = true;
    private bool _restoring;

    // A positive image limit requests a thumbnail whose longest edge is bounded
    // by that many pixels. Zero requests the original selected image.
    public ArchiveWindow(IEnumerable<SnipRecord> archivedSnips,
        Func<SnipRecord, int, BitmapSource?> loadImage, Func<string, bool> restoreSnip)
    {
        ArgumentNullException.ThrowIfNull(archivedSnips);
        _loadImage = loadImage ?? throw new ArgumentNullException(nameof(loadImage));
        _restoreSnip = restoreSnip ?? throw new ArgumentNullException(nameof(restoreSnip));
        _entries = new ObservableCollection<ArchiveEntry>(archivedSnips
            .Where(snip => snip.IsArchived)
            .OrderByDescending(snip => snip.ArchivedUtc ?? snip.CreatedUtc)
            .ThenByDescending(snip => snip.CreatedUtc)
            .Select(snip => new ArchiveEntry(snip.Clone())));
        _thumbnails = new ThumbnailConverter(LoadSafely);

        Title = "Game Scrapbook — Archive";
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 15;
        SetResourceReference(BackgroundProperty, "BackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        Width = Math.Min(1000, Math.Max(680, SystemParameters.WorkArea.Width - 80));
        Height = Math.Min(760, Math.Max(440, SystemParameters.WorkArea.Height - 80));
        MinWidth = 620;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        heading.Children.Add(Label("Archive", 24, FontWeights.SemiBold));
        _count.Margin = new Thickness(0, 5, 0, 0);
        _count.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        heading.Children.Add(_count);
        root.Children.Add(heading);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        _list.ItemsSource = _entries;
        _list.SetResourceReference(BackgroundProperty, "PanelBrush");
        _list.SetResourceReference(ForegroundProperty, "TextBrush");
        _list.SetResourceReference(BorderBrushProperty, "BorderBrush");
        _list.BorderThickness = new Thickness(1);
        _list.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(_list, ScrollBarVisibility.Auto);
        ScrollViewer.SetCanContentScroll(_list, true);
        VirtualizingPanel.SetIsVirtualizing(_list, true);
        VirtualizingPanel.SetVirtualizationMode(_list, VirtualizationMode.Recycling);
        _list.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        _list.ItemContainerStyle = ItemStyle();
        _list.ItemTemplate = ItemTemplate(_thumbnails);
        AutomationProperties.SetName(_list, "Archived snips. Select a snip to view it.");
        body.Children.Add(_list);

        var viewer = new Grid();
        viewer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        viewer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        viewer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(viewer, 2);
        body.Children.Add(viewer);

        var viewControls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var viewLabel = Label("View", 15, FontWeights.SemiBold);
        viewLabel.VerticalAlignment = VerticalAlignment.Center;
        viewLabel.Margin = new Thickness(0, 0, 12, 0);
        viewControls.Children.Add(viewLabel);
        _fitButton.Margin = new Thickness(0, 0, 6, 0);
        viewControls.Children.Add(_fitButton);
        viewControls.Children.Add(_actualButton);
        viewer.Children.Add(viewControls);

        _details.Margin = new Thickness(0, 0, 0, 10);
        _details.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        Grid.SetRow(_details, 1);
        viewer.Children.Add(_details);

        var surface = new Grid();
        surface.SetResourceReference(Panel.BackgroundProperty, "BoardBrush");
        var frame = new Border { Child = surface, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), ClipToBounds = true };
        frame.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Grid.SetRow(frame, 2);
        viewer.Children.Add(frame);

        _image.Stretch = Stretch.Fill;
        _image.HorizontalAlignment = HorizontalAlignment.Center;
        _image.VerticalAlignment = VerticalAlignment.Center;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        _imageScroll.Content = _image;
        _imageScroll.CanContentScroll = false;
        _imageScroll.HorizontalContentAlignment = HorizontalAlignment.Center;
        _imageScroll.VerticalContentAlignment = VerticalAlignment.Center;
        surface.Children.Add(_imageScroll);
        _placeholder.HorizontalAlignment = HorizontalAlignment.Center;
        _placeholder.VerticalAlignment = VerticalAlignment.Center;
        _placeholder.TextAlignment = TextAlignment.Center;
        _placeholder.Margin = new Thickness(20);
        _placeholder.MaxWidth = 340;
        _placeholder.IsHitTestVisible = false;
        surface.Children.Add(_placeholder);

        var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _message.Margin = new Thickness(0, 0, 0, 10);
        _message.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        footer.Children.Add(_message);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _restore.SetResourceReference(BackgroundProperty, "AccentBrush");
        _restore.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        _restore.Margin = new Thickness(0, 0, 8, 0);
        actions.Children.Add(_restore);
        actions.Children.Add(new Button { Content = "Close", IsCancel = true, MinWidth = 78 });
        Grid.SetRow(actions, 1);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        _list.SelectionChanged += (_, _) => ShowSelection();
        _fitButton.Click += (_, _) => SetImageMode(true);
        _actualButton.Click += (_, _) => SetImageMode(false);
        _restore.Click += (_, _) => RestoreSelection();
        _imageScroll.SizeChanged += (_, _) => UpdateImageSize();
        _imageScroll.ScrollChanged += (_, e) =>
        {
            if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) UpdateImageSize();
        };
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(UpdateImageSize));
        Loaded += (_, _) => { UpdateImageSize(); _list.Focus(); };
        Closed += (_, _) =>
        {
            _image.Source = null;
            _selectedImage = null;
            _list.ItemsSource = null;
            _entries.Clear();
            _thumbnails.Clear();
        };
        SetImageMode(true);
        RefreshCount();
        if (_entries.Count > 0) _list.SelectedIndex = 0;
        else ShowSelection();
    }

    private void ShowSelection()
    {
        _image.Source = null;
        _selectedImage = null;
        _message.Text = "";
        var entry = _list.SelectedItem as ArchiveEntry;
        _restore.IsEnabled = entry is not null && !_restoring;
        if (entry is null)
        {
            _details.Text = "";
            _placeholder.Text = _entries.Count == 0
                ? "Your archive is empty.\nSelect a board snip and use Archive selected to keep it here for later."
                : "Select an archived snip to view it.";
        }
        else
        {
            _details.Text = $"{entry.Snip.PixelWidth:N0} × {entry.Snip.PixelHeight:N0} pixels · Captured {entry.Snip.CreatedUtc.ToLocalTime():g}";
            _selectedImage = LoadSafely(entry.Snip, 0);
            _image.Source = _selectedImage;
            _placeholder.Text = "This image is unavailable.\nIts saved record and position have been kept.";
        }
        _placeholder.Visibility = _selectedImage is null ? Visibility.Visible : Visibility.Collapsed;
        _fitButton.IsEnabled = _actualButton.IsEnabled = _selectedImage is not null;
        UpdateImageSize();
        _imageScroll.ScrollToHorizontalOffset(0);
        _imageScroll.ScrollToVerticalOffset(0);
    }

    private void RestoreSelection()
    {
        if (_restoring || _list.SelectedItem is not ArchiveEntry entry) return;
        _restoring = true;
        _restore.IsEnabled = false;
        try
        {
            if (!_restoreSnip(entry.Snip.Id))
            {
                _message.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
                _message.Text = "Could not restore this snip. It is still in the archive. Check the scrapbook's status message for details.";
                return;
            }
            int previousIndex = _list.SelectedIndex;
            _entries.Remove(entry);
            _thumbnails.Remove(entry.Snip.Id);
            RefreshCount();
            if (_entries.Count > 0) _list.SelectedIndex = Math.Min(previousIndex, _entries.Count - 1);
            else ShowSelection();
            _message.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            _message.Text = "Restored to the scrapbook at its saved position.";
        }
        finally
        {
            _restoring = false;
            _restore.IsEnabled = _list.SelectedItem is ArchiveEntry;
        }
    }

    private void RefreshCount() => _count.Text = _entries.Count switch
    {
        0 => "No archived snips yet.",
        1 => "1 archived snip · Select it to read or restore it.",
        _ => $"{_entries.Count:N0} archived snips · Select one to read or restore it."
    };

    private void SetImageMode(bool fit)
    {
        _fit = fit;
        _imageScroll.HorizontalScrollBarVisibility = fit ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        _imageScroll.VerticalScrollBarVisibility = fit ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        _fitButton.FontWeight = fit ? FontWeights.SemiBold : FontWeights.Normal;
        _actualButton.FontWeight = fit ? FontWeights.Normal : FontWeights.SemiBold;
        _fitButton.SetResourceReference(BorderBrushProperty, fit ? "AccentBrush" : "BorderBrush");
        _actualButton.SetResourceReference(BorderBrushProperty, fit ? "BorderBrush" : "AccentBrush");
        UpdateImageSize();
        _imageScroll.ScrollToHorizontalOffset(0);
        _imageScroll.ScrollToVerticalOffset(0);
    }

    private void UpdateImageSize()
    {
        if (_selectedImage is null) return;
        DpiScale dpi = VisualTreeHelper.GetDpi(_image);
        // 100% is one image pixel per physical display pixel, independent of
        // image DPI metadata and the monitor's Windows scaling setting.
        double width = _selectedImage.PixelWidth / dpi.DpiScaleX;
        double height = _selectedImage.PixelHeight / dpi.DpiScaleY;
        if (_fit)
        {
            double availableWidth = _imageScroll.ViewportWidth > 0 ? _imageScroll.ViewportWidth : _imageScroll.ActualWidth;
            double availableHeight = _imageScroll.ViewportHeight > 0 ? _imageScroll.ViewportHeight : _imageScroll.ActualHeight;
            if (availableWidth <= 0 || availableHeight <= 0) return;
            double scale = Math.Min(1, Math.Min(availableWidth / width, availableHeight / height));
            width *= scale;
            height *= scale;
        }
        _image.Width = Math.Max(0.1, width);
        _image.Height = Math.Max(0.1, height);
    }

    private BitmapSource? LoadSafely(SnipRecord snip, int longestEdge)
    {
        try { return _loadImage(snip, longestEdge); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            App.WriteDiagnostic(ex);
            return null;
        }
    }

    private static TextBlock Label(string text, double size, FontWeight? weight = null)
    {
        var label = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return label;
    }

    private static Style ItemStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(5)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("ControlBrush")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextBrush")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("BorderBrush")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetBinding(Border.BackgroundProperty, ParentBinding("Background"));
        border.SetBinding(Border.BorderBrushProperty, ParentBinding("BorderBrush"));
        border.SetBinding(Border.BorderThicknessProperty, ParentBinding("BorderThickness"));
        border.SetBinding(Border.PaddingProperty, ParentBinding("Padding"));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetBinding(ContentPresenter.ContentProperty, ParentBinding("Content"));
        content.SetBinding(ContentPresenter.ContentTemplateProperty, ParentBinding("ContentTemplate"));
        border.AppendChild(content);
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) { VisualTree = border }));
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("AccentBrush")));
        selected.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
        selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("NoticeBrush")));
        style.Triggers.Add(selected);
        var focused = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focused.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("TextBrush")));
        style.Triggers.Add(focused);
        return style;
    }

    private static Binding ParentBinding(string property) => new(property) { RelativeSource = RelativeSource.TemplatedParent };

    private static DataTemplate ItemTemplate(ThumbnailConverter thumbnails)
    {
        var stack = new FrameworkElementFactory(typeof(StackPanel));
        var image = new FrameworkElementFactory(typeof(Image));
        image.SetValue(FrameworkElement.HeightProperty, 95d);
        image.SetValue(Image.StretchProperty, Stretch.Uniform);
        image.SetBinding(Image.SourceProperty, new Binding { Converter = thumbnails });
        stack.AppendChild(image);
        var date = new FrameworkElementFactory(typeof(TextBlock));
        date.SetValue(TextBlock.FontSizeProperty, 13d);
        date.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        date.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 7, 0, 0));
        date.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        date.SetBinding(TextBlock.TextProperty, new Binding(nameof(ArchiveEntry.DisplayDate)));
        stack.AppendChild(date);
        var dimensions = new FrameworkElementFactory(typeof(TextBlock));
        dimensions.SetValue(TextBlock.FontSizeProperty, 13d);
        dimensions.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        dimensions.SetBinding(TextBlock.TextProperty, new Binding(nameof(ArchiveEntry.Dimensions)));
        stack.AppendChild(dimensions);
        return new DataTemplate(typeof(ArchiveEntry)) { VisualTree = stack };
    }

    private sealed record ArchiveEntry(SnipRecord Snip)
    {
        public string DisplayDate => $"{Snip.CreatedUtc.ToLocalTime():dd MMM yyyy · HH:mm}";
        public string Dimensions => $"{Snip.PixelWidth:N0} × {Snip.PixelHeight:N0} pixels";
    }

    private sealed class ThumbnailConverter(Func<SnipRecord, int, BitmapSource?> loadImage) : IValueConverter
    {
        private readonly Dictionary<string, BitmapSource?> _cache = [];
        private readonly Queue<string> _order = [];

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ArchiveEntry entry) return null;
            if (_cache.TryGetValue(entry.Snip.Id, out BitmapSource? image)) return image;
            while (_cache.Count >= 48 && _order.TryDequeue(out string? oldest)) _cache.Remove(oldest);
            image = loadImage(entry.Snip, 160);
            _cache[entry.Snip.Id] = image;
            _order.Enqueue(entry.Snip.Id);
            return image;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
        public void Remove(string id) => _cache.Remove(id);
        public void Clear() { _cache.Clear(); _order.Clear(); }
    }
}
