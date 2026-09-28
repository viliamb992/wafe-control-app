using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile.Controls;

public enum BannerSeverity
{
    Informational,
    Success,
    Warning,
    Error,
}

/// <summary>
/// The mobile InfoBar (DESIGN.md, section 7): severity background and icon, title, message, optional action and close.
/// Hidden while it has neither title nor message.
/// </summary>
public sealed class InfoBanner : ContentView
{
    public static readonly BindableProperty SeverityProperty = BindableProperty.Create(
        nameof(Severity), typeof(BannerSeverity), typeof(InfoBanner), BannerSeverity.Informational,
        propertyChanged: (bindable, _, _) => ((InfoBanner)bindable).UpdateSeverity());

    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(InfoBanner), null,
        propertyChanged: (bindable, _, _) => ((InfoBanner)bindable).UpdateSeverity());

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(InfoBanner), null,
        propertyChanged: (bindable, _, _) => ((InfoBanner)bindable).UpdateText());

    public static readonly BindableProperty MessageProperty = BindableProperty.Create(
        nameof(Message), typeof(string), typeof(InfoBanner), null,
        propertyChanged: (bindable, _, _) => ((InfoBanner)bindable).UpdateText());

    public static readonly BindableProperty ActionTextProperty = BindableProperty.Create(
        nameof(ActionText), typeof(string), typeof(InfoBanner), null,
        propertyChanged: (bindable, _, _) => ((InfoBanner)bindable).UpdateText());

    public static readonly BindableProperty ActionCommandProperty = BindableProperty.Create(
        nameof(ActionCommand), typeof(ICommand), typeof(InfoBanner), null,
        propertyChanged: (bindable, _, value) => ((InfoBanner)bindable)._action.Command = (ICommand?)value);

    public static readonly BindableProperty DismissCommandProperty = BindableProperty.Create(
        nameof(DismissCommand), typeof(ICommand), typeof(InfoBanner), null,
        propertyChanged: (bindable, _, value) =>
        {
            var banner = (InfoBanner)bindable;
            banner._dismiss.Command = (ICommand?)value;
            banner._dismiss.IsVisible = value is not null;
        });

    public static readonly BindableProperty IsShownProperty = BindableProperty.Create(
        nameof(IsShown), typeof(bool), typeof(InfoBanner), true,
        propertyChanged: (bindable, _, _) => ((InfoBanner)bindable).UpdateText());

    private readonly Border _border;
    private readonly Label _icon;
    private readonly Label _title;
    private readonly Label _message;
    private readonly Button _action;
    private readonly Button _dismiss;

    public InfoBanner()
    {
        _icon = new Label { Style = Theme.Style("Icon"), VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 1, 0, 0) };
        _title = new Label { Style = Theme.Style("BodyStrong") };
        _message = new Label { Style = Theme.Style("Body") };
        _action = new Button { HorizontalOptions = LayoutOptions.Start, Margin = new Thickness(0, 8, 0, 0) };
        _action.Clicked += (_, _) => ActionClicked?.Invoke(this, EventArgs.Empty);

        _dismiss = new Button
        {
            Style = Theme.Style("IconButton"),
            Text = FluentIcons.Dismiss,
            FontSize = 16,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, -10, -12, 0),
            IsVisible = false,
        };
        SemanticProperties.SetDescription(_dismiss, Core.Localization.Strings.ButtonClose);

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 12,
        };
        grid.Add(_icon, 0);
        grid.Add(new VerticalStackLayout { Spacing = 2, Children = { _title, _message, _action } }, 1);
        grid.Add(_dismiss, 2);

        _border = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(16, 12),
            Content = grid,
        }.SetColor(Border.StrokeProperty, "CardStroke");
        Content = _border;

        UpdateSeverity();
        UpdateText();
    }

    public BannerSeverity Severity
    {
        get => (BannerSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <summary>
    /// Overrides the severity's icon (a <see cref="FluentIcons"/> glyph).
    /// </summary>
    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string? ActionText
    {
        get => (string?)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    /// <summary>
    /// Shows a close button that runs this command; the banner hides when its text goes away.
    /// </summary>
    public ICommand? DismissCommand
    {
        get => (ICommand?)GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    /// <summary>
    /// False hides the banner even when it has text.
    /// </summary>
    public bool IsShown
    {
        get => (bool)GetValue(IsShownProperty);
        set => SetValue(IsShownProperty, value);
    }

    public event EventHandler? ActionClicked;

    private void UpdateSeverity()
    {
        var (background, foreground, glyph) = Severity switch
        {
            BannerSeverity.Success => ("SuccessBackground", "Success", FluentIcons.CheckmarkCircle),
            BannerSeverity.Warning => ("CautionBackground", "Caution", FluentIcons.Warning),
            BannerSeverity.Error => ("CriticalBackground", "Critical", FluentIcons.ErrorCircle),
            _ => ("AttentionBackground", "Attention", FluentIcons.Info),
        };
        _border.SetColor(VisualElement.BackgroundColorProperty, background);
        _icon.SetColor(Label.TextColorProperty, foreground);
        _icon.Text = Icon ?? glyph;
    }

    private void UpdateText()
    {
        _title.Text = Title;
        _title.IsVisible = !string.IsNullOrEmpty(Title);
        _message.Text = Message;
        _message.IsVisible = !string.IsNullOrEmpty(Message);
        _action.Text = ActionText;
        _action.IsVisible = !string.IsNullOrEmpty(ActionText);
        IsVisible = IsShown && (_title.IsVisible || _message.IsVisible);
    }
}
