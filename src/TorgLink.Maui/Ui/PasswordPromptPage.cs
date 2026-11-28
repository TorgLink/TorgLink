using TorgLink.Localization;

namespace TorgLink.Maui;

/// <summary>
/// Modal password prompt (TRL-10): one secure entry, or two for "new password + confirm".
/// Resolves with the entered password, or null when cancelled.
/// </summary>
internal sealed class PasswordPromptPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Entry _passwordEntry;
    private readonly Entry? _confirmEntry;
    private readonly Label _errorLabel;

    private PasswordPromptPage(string title, string message, string placeholder, string? confirmPlaceholder)
    {
        Title = title;
        this.SetDynamicResource(BackgroundColorProperty, "PageBackground");

        var messageLabel = new Label { LineBreakMode = LineBreakMode.WordWrap };
        messageLabel.SetDynamicResource(Label.TextColorProperty, "MutedText");
        messageLabel.Text = message;

        _passwordEntry = new Entry { IsPassword = true, Placeholder = placeholder };
        _passwordEntry.SetDynamicResource(Entry.TextColorProperty, "MidnightBlue");

        var errorLabel = new Label { IsVisible = false, LineBreakMode = LineBreakMode.WordWrap };
        errorLabel.SetDynamicResource(Label.TextColorProperty, "Danger");
        _errorLabel = errorLabel;

        var cancelButton = new Button { Text = LocalizationUtils.GetStringByKey("cancel") };
        cancelButton.SetDynamicResource(Button.TextColorProperty, "MidnightBlue");
        cancelButton.BackgroundColor = Colors.Transparent;
        cancelButton.Clicked += async (_, _) => await CloseAsync(null).ConfigureAwait(true);

        var okButton = new Button { Text = LocalizationUtils.GetStringByKey("ok") };
        okButton.SetDynamicResource(Button.BackgroundColorProperty, "Accent");
        okButton.SetDynamicResource(Button.TextColorProperty, "ButtonText");
        okButton.Clicked += async (_, _) => await ConfirmAsync().ConfigureAwait(true);

        if (confirmPlaceholder != null)
        {
            _confirmEntry = new Entry { IsPassword = true, Placeholder = confirmPlaceholder };
            _confirmEntry.SetDynamicResource(Entry.TextColorProperty, "MidnightBlue");
        }

        var buttons = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
            ColumnSpacing = 12,
            Children = { cancelButton, okButton }
        };
        Grid.SetColumn(okButton, 1);

        var layout = new VerticalStackLayout
        {
            Padding = new Thickness(24, 32),
            Spacing = 14
        };
        layout.Children.Add(messageLabel);
        layout.Children.Add(_passwordEntry);
        if (_confirmEntry != null)
            layout.Children.Add(_confirmEntry);
        layout.Children.Add(_errorLabel);
        layout.Children.Add(buttons);
        Content = layout;
    }

    /// <summary>Single password entry. Null when the user cancels.</summary>
    public static Task<string?> ShowAsync(Page host, string title, string message, string placeholder) =>
        Show(host, title, message, placeholder, confirmPlaceholder: null);

    /// <summary>Two entries that must match (new master password). Null when the user cancels.</summary>
    public static Task<string?> ShowNewAsync(
        Page host,
        string title,
        string message,
        string placeholder,
        string confirmPlaceholder) =>
        Show(host, title, message, placeholder, confirmPlaceholder);

    private static async Task<string?> Show(
        Page host,
        string title,
        string message,
        string placeholder,
        string? confirmPlaceholder)
    {
        var page = new PasswordPromptPage(title, message, placeholder, confirmPlaceholder);
        await host.Navigation.PushModalAsync(new NavigationPage(page)).ConfigureAwait(true);
        return await page._completion.Task.ConfigureAwait(true);
    }

    private async Task ConfirmAsync()
    {
        var password = _passwordEntry.Text ?? "";
        if (string.IsNullOrEmpty(password))
        {
            ShowError(LocalizationUtils.GetStringByKey("pass.empty"));
            return;
        }

        if (_confirmEntry != null && !string.Equals(password, _confirmEntry.Text ?? "", StringComparison.Ordinal))
        {
            ShowError(LocalizationUtils.GetStringByKey("profilex.master_mismatch"));
            return;
        }

        await CloseAsync(password).ConfigureAwait(true);
    }

    private void ShowError(string text)
    {
        _errorLabel.Text = text;
        _errorLabel.IsVisible = true;
    }

    private async Task CloseAsync(string? result)
    {
        _completion.TrySetResult(result);
        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync().ConfigureAwait(true);
    }

    protected override bool OnBackButtonPressed()
    {
        _completion.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}

