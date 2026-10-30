using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GameScrapbook.Core;

namespace GameScrapbook.Windows;

internal sealed class SettingsWindow : Window
{
    public Func<BoardSettings, string?>? ApplySettings { get; set; }
    private readonly BoardSettings _settings;
    private readonly TextBox _shortcut;
    private readonly CheckBox _returnCursor;
    private readonly CheckBox _topmost;
    private readonly TextBlock _error;
    private readonly Dictionary<string, (Border Swatch, TextBlock Value)> _colourRows = [];
    private readonly TextBlock _contrast;

    public SettingsWindow(BoardSettings current, string dataDirectory)
    {
        // All edits stay on this copy until Save succeeds. Cancel also cancels
        // the draft colours without changing the application's resources.
        _settings = current.Clone();
        Title = "Game Scrapbook — Settings";
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 15;
        SetResourceReference(BackgroundProperty, "BackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        Width = 620;
        Height = Math.Min(790, Math.Max(440, SystemParameters.WorkArea.Height - 80));
        MinWidth = 460;
        MinHeight = 420;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _error = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 0, 0, 10) };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        var heading = new StackPanel { Margin = new Thickness(26, 22, 26, 18) };
        heading.Children.Add(Label("Settings", 24, FontWeights.SemiBold));
        heading.Children.Add(Description("Make the scrapbook comfortable to read and use.", new Thickness(0, 5, 0, 0)));
        root.Children.Add(heading);

        var body = new StackPanel { Margin = new Thickness(26, 0, 26, 16) };
        var scroll = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            CanContentScroll = false
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        body.Children.Add(SectionHeading("Appearance"));
        body.Children.Add(Description("Choose a colour to change it, then save your settings to apply it.", new Thickness(0, 5, 0, 12)));
        body.Children.Add(ColourRow("Background", "Background", () => _settings.BackgroundColor, value => _settings.BackgroundColor = value));
        body.Children.Add(ColourRow("Text", "Text", () => _settings.TextColor, value => _settings.TextColor = value));
        body.Children.Add(ColourRow("Accent", "Accent", () => _settings.AccentColor, value => _settings.AccentColor = value));

        _contrast = Description("", new Thickness(0, 8, 0, 10));
        body.Children.Add(_contrast);
        var reset = new Button { Content = "Reset to blue, white and dark grey", HorizontalAlignment = HorizontalAlignment.Left };
        reset.Click += (_, _) =>
        {
            _settings.BackgroundColor = ThemeColors.DefaultBackground;
            _settings.TextColor = ThemeColors.DefaultText;
            _settings.AccentColor = ThemeColors.DefaultAccent;
            RefreshColours();
            _error.Text = "";
        };
        body.Children.Add(reset);

        body.Children.Add(SectionHeading("Capture", new Thickness(0, 24, 0, 8)));
        body.Children.Add(Label("Capture shortcut", 15, FontWeights.SemiBold));
        _shortcut = new TextBox
        {
            Text = MainWindow.HotkeyText(_settings), IsReadOnly = true,
            FontSize = 18, FontWeight = FontWeights.SemiBold, Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 8, 0, 6), CaretBrush = Brushes.Transparent
        };
        _shortcut.SetResourceReference(ForegroundProperty, "TextBrush");
        AutomationProperties.SetName(_shortcut, "Capture shortcut. Focus this box and press your new shortcut.");
        _shortcut.PreviewKeyDown += Shortcut_KeyDown;
        body.Children.Add(_shortcut);
        body.Children.Add(Description("Click the shortcut box, then press Ctrl and/or Alt with a letter, number or F1–F11. Shift is optional.", new Thickness(0, 0, 0, 16)));

        _returnCursor = Option(
            "Return mouse after snipping",
            "Move the pointer back to where you started the capture after dropping a new snip.",
            _settings.ReturnCursor);
        _topmost = Option(
            "Keep scrapbook on top",
            "Keep this window above other windows on your desktop.",
            _settings.Topmost);
        body.Children.Add(_returnCursor);
        body.Children.Add(_topmost);
        body.Children.Add(Description("Dropping a new snip returns focus to your game. Clicking a saved snip keeps focus in the scrapbook so you can edit it.", new Thickness(0, 10, 0, 0)));

        body.Children.Add(SectionHeading("Saved board", new Thickness(0, 24, 0, 8)));
        var dataButton = new Button { Content = "Open saved-board folder", HorizontalAlignment = HorizontalAlignment.Left };
        dataButton.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(dataDirectory);
                Process.Start(new ProcessStartInfo(dataDirectory) { UseShellExecute = true });
            }
            catch (Exception ex) { _error.Text = ex.Message; }
        };
        body.Children.Add(dataButton);

        // The action row never scrolls out of view on smaller displays.
        var footer = new StackPanel { Margin = new Thickness(26, 10, 26, 20) };
        footer.Children.Add(_error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        var save = new Button { Content = "Save settings", IsDefault = true };
        save.SetResourceReference(BackgroundProperty, "AccentBrush");
        save.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        save.Click += (_, _) =>
        {
            if (ThemeColors.ContrastRatio(_settings.TextColor, _settings.BackgroundColor) < 4.5)
            {
                _error.Text = "The text blends into the background. Choose more distinct text and background colours, or reset the colours.";
                return;
            }
            _settings.ReturnCursor = _returnCursor.IsChecked == true;
            _settings.Topmost = _topmost.IsChecked == true;
            string? error = ApplySettings?.Invoke(_settings);
            if (error is not null) { _error.Text = error; return; }
            DialogResult = true;
        };
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        RefreshColours();
    }

    private Grid ColourRow(string key, string title, Func<string> getColour, Action<string> setColour)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        var label = Label(title, 15, FontWeights.SemiBold);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label);

        var swatch = new Border { Width = 27, Height = 24, CornerRadius = new CornerRadius(4), BorderBrush = Brushes.White, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0) };
        var value = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
        value.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        var words = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(swatch, Dock.Left);
        words.Children.Add(swatch);
        var change = new TextBlock { Text = "Edit", FontSize = 13, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        change.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        DockPanel.SetDock(change, Dock.Right);
        words.Children.Add(change);
        words.Children.Add(value);
        var button = new Button { Content = words, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 7, 10, 7) };
        AutomationProperties.SetName(button, $"Change {title.ToLowerInvariant()} colour");
        button.Click += (_, _) =>
        {
            var picker = new ColorPickerWindow(title, getColour()) { Owner = this };
            if (picker.ShowDialog() != true) return;
            setColour(picker.SelectedColour);
            RefreshColours();
            _error.Text = "";
        };
        Grid.SetColumn(button, 1);
        row.Children.Add(button);
        _colourRows.Add(key, (swatch, value));
        return row;
    }

    private void RefreshColours()
    {
        UpdateColour("Background", _settings.BackgroundColor);
        UpdateColour("Text", _settings.TextColor);
        UpdateColour("Accent", _settings.AccentColor);
        double contrast = ThemeColors.ContrastRatio(_settings.TextColor, _settings.BackgroundColor);
        _contrast.Text = contrast >= 7 ? "Strong contrast — text should be easy to read."
            : contrast >= 4.5 ? "Readable contrast — ready to save."
            : "Low contrast — choose more distinct text and background colours before saving.";
        _contrast.SetResourceReference(TextBlock.ForegroundProperty, contrast >= 4.5 ? "MutedBrush" : "ErrorBrush");
    }

    private void UpdateColour(string key, string colour)
    {
        var row = _colourRows[key];
        row.Swatch.Background = ColourBrush(colour);
        row.Swatch.BorderBrush = ColourBrush(ThemeColors.ReadableForeground(colour));
        row.Value.Text = colour;
    }

    private static CheckBox Option(string title, string description, bool value)
    {
        // Explicit TextBlocks and brushes avoid the OS checkbox theme producing
        // unlabelled-looking boxes against a dark application background.
        var content = new StackPanel();
        content.Children.Add(Label(title, 15, FontWeights.SemiBold));
        content.Children.Add(Description(description, new Thickness(0, 4, 0, 0)));
        var checkBox = new CheckBox
        {
            Content = content, IsChecked = value, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 14)
        };
        AutomationProperties.SetName(checkBox, title);
        AutomationProperties.SetHelpText(checkBox, description);
        return checkBox;
    }

    private static TextBlock SectionHeading(string text, Thickness? margin = null) =>
        new() { Text = text, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = margin ?? new Thickness(0) };

    private static TextBlock Label(string text, double size, FontWeight weight)
    {
        var label = new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return label;
    }

    private static TextBlock Description(string text, Thickness margin)
    {
        var description = new TextBlock { Text = text, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = margin };
        description.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        return description;
    }

    private static SolidColorBrush ColourBrush(string value) => new((Color)ColorConverter.ConvertFromString(value));

    private void Shortcut_KeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        // Keep keyboard navigation and the dialog's normal Save/Cancel keys.
        if (((key is Key.Tab or Key.Escape or Key.Enter) && Keyboard.Modifiers == ModifierKeys.None) ||
            (key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Shift)) return;
        e.Handled = true;
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift) return;
        bool allowedKey = (key >= Key.A && key <= Key.Z) || (key >= Key.D0 && key <= Key.D9) || (key >= Key.F1 && key <= Key.F11);
        if (!allowedKey || (modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0 || (modifiers & ModifierKeys.Windows) != 0)
        {
            _error.Text = "Use Ctrl and/or Alt with a letter, number or F1–F11. You can also hold Shift.";
            return;
        }
        _settings.CaptureHotkeyModifiers =
            ((modifiers & ModifierKeys.Alt) != 0 ? 1 : 0) |
            ((modifiers & ModifierKeys.Control) != 0 ? 2 : 0) |
            ((modifiers & ModifierKeys.Shift) != 0 ? 4 : 0);
        _settings.CaptureHotkeyKey = KeyInterop.VirtualKeyFromKey(key);
        _shortcut.Text = MainWindow.HotkeyText(_settings);
        _error.Text = "";
    }
}
