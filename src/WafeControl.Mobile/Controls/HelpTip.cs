using WafeControl.Core.Localization;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile.Controls;

/// <summary>
/// A (?) button after an option. A tap opens <see cref="Tip"/> in a bubble (Android); a long press shows it as the
/// native tooltip, and screen readers read it as the hint. <see cref="Subject"/> names what's explained.
/// </summary>
public sealed class HelpTip : Button
{
    public static readonly BindableProperty TipProperty = BindableProperty.Create(
        nameof(Tip), typeof(string), typeof(HelpTip), null,
        propertyChanged: (bindable, _, value) =>
        {
            ToolTipProperties.SetText(bindable, value);
            SemanticProperties.SetHint(bindable, (string?)value ?? string.Empty);
        });

    public static readonly BindableProperty SubjectProperty = BindableProperty.Create(
        nameof(Subject), typeof(string), typeof(HelpTip), null,
        propertyChanged: (bindable, _, value) =>
            SemanticProperties.SetDescription(bindable, string.Format(Strings.HelpExplain, value)));

    public HelpTip()
    {
        Style = Theme.Style("IconButton");
        Text = FluentIcons.QuestionCircle;
        FontSize = 18;
        VerticalOptions = LayoutOptions.Center;
        this.SetColor(TextColorProperty, "TextSecondary");
        Clicked += OnClicked;
    }

    /// <summary>
    /// The explanation, two or three sentences.
    /// </summary>
    public string? Tip
    {
        get => (string?)GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    /// <summary>
    /// The option explained, for screen readers ("Explain: Stay signed in").
    /// </summary>
    public string? Subject
    {
        get => (string?)GetValue(SubjectProperty);
        set => SetValue(SubjectProperty, value);
    }

    private void OnClicked(object? sender, EventArgs e)
    {
#if ANDROID
        if (!string.IsNullOrEmpty(Tip))
            HelpBubble.Show(this, Tip);
#endif
    }
}
