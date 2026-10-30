using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GameScrapbook.Core;

namespace GameScrapbook.Windows;

internal sealed class ColorPickerWindow : Window
{
    public string SelectedColour { get; private set; }
    private readonly TextBox _hex;
    private readonly Border _sample;
    private readonly TextBlock _sampleText;
    private readonly TextBlock _error;
    private readonly Button _useColour;

    private static readonly (string Colour, string Name)[] Palette =
    [
        ("#000000", "Black"), ("#FFFFFF", "White"), ("#1976D2", "Blue"), ("#242424", "Dark grey"),
        ("#2B3440", "Slate"), ("#697586", "Grey"), ("#CED4DA", "Light grey"), ("#F5EBDC", "Cream"),
        ("#0D47A1", "Deep blue"), ("#45A3FF", "Sky blue"), ("#00B8D9", "Cyan"), ("#009688", "Teal"),
        ("#146B3A", "Forest green"), ("#4CAF50", "Green"), ("#B7E36A", "Lime"), ("#F6D365", "Yellow"),
        ("#F59E0B", "Amber"), ("#F97316", "Orange"), ("#E53935", "Red"), ("#9C1B33", "Burgundy"),
        ("#E879A9", "Pink"), ("#C7A9FF", "Lilac"), ("#8B5CF6", "Purple"), ("#59338E", "Deep purple")
    ];

    public ColorPickerWindow(string colourName, string currentColour)
    {
        SelectedColour = currentColour;
        Title = $"Choose {colourName.ToLowerInvariant()} colour";
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 15;
        SetResourceReference(BackgroundProperty, "BackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        Width = 460;
        Height = Math.Min(590, Math.Max(400, SystemParameters.WorkArea.Height - 80));
        MinWidth = 400;
        MinHeight = 360;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;
        var body = new StackPanel();
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        root.Children.Add(scroll);
        body.Children.Add(new TextBlock { Text = $"{colourName} colour", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 7) });
        var instructions = new TextBlock { Text = "Pick a swatch, or enter any colour using its hex code.", FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
        instructions.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        body.Children.Add(instructions);
        _hex = new TextBox
        {
            Text = currentColour, FontFamily = new FontFamily("Consolas"), FontSize = 18,
            Padding = new Thickness(10, 9, 10, 9), Margin = new Thickness(0, 7, 0, 6),
            MaxLength = 7
        };
        AutomationProperties.SetName(_hex, "Custom colour hex code, for example #1976D2");

        var palette = new UniformGrid { Columns = 6, Margin = new Thickness(-3, 0, -3, 16) };
        foreach (var (colour, name) in Palette)
        {
            var swatch = new Border
            {
                Background = Brush(colour), BorderBrush = Brush(ThemeColors.ReadableForeground(colour)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Height = 31
            };
            var button = new Button
            {
                Content = swatch, Padding = new Thickness(4), Margin = new Thickness(3),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                ToolTip = $"{name} · {colour}"
            };
            AutomationProperties.SetName(button, $"{name}, {colour}");
            button.Click += (_, _) => _hex.Text = colour;
            palette.Children.Add(button);
        }
        body.Children.Add(palette);

        body.Children.Add(new TextBlock { Text = "Custom colour", FontSize = 15, FontWeight = FontWeights.SemiBold });
        body.Children.Add(_hex);
        var format = new TextBlock { Text = "Use #RRGGBB, for example #1976D2 for blue.", FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) };
        format.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        body.Children.Add(format);
        _sampleText = new TextBlock { Text = currentColour, FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _sample = new Border { Child = _sampleText, Height = 66, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1) };
        _sample.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        body.Children.Add(_sample);

        var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        _error = new TextBlock { FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
        footer.Children.Add(_error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        _useColour = new Button { Content = "Use colour", IsDefault = true };
        _useColour.SetResourceReference(BackgroundProperty, "AccentBrush");
        _useColour.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        _useColour.Click += (_, _) =>
        {
            if (!ThemeColors.TryNormalize(_hex.Text, out string normalised)) return;
            SelectedColour = normalised;
            DialogResult = true;
        };
        actions.Children.Add(cancel);
        actions.Children.Add(_useColour);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        _hex.TextChanged += (_, _) => UpdateSample();
        _hex.GotKeyboardFocus += (_, _) => _hex.SelectAll();
        UpdateSample();
    }

    private void UpdateSample()
    {
        bool valid = ThemeColors.TryNormalize(_hex.Text, out string normalised);
        _useColour.IsEnabled = valid;
        _error.Text = valid ? "" : "Enter six colour digits, such as #1976D2.";
        if (!valid) return;
        _sample.Background = Brush(normalised);
        _sampleText.Foreground = Brush(ThemeColors.ReadableForeground(normalised));
        _sampleText.Text = normalised;
    }

    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
}
