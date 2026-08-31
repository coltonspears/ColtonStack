using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The Preferences overlay: display name and avatar color, loaded from and saved to the server
/// (a real round-trip through the resilience pipeline, persisted with a Dapper.Contrib UPDATE).
/// On save it publishes <see cref="ProfileUpdatedMessage"/> — whoever cares (the people pane)
/// listens; this class doesn't know or mind.
/// Validation uses <see cref="UpdateProfileRequestValidator"/> — the same rules the server enforces,
/// shared through the Contracts project, so neither side can drift.
/// </summary>
public sealed partial class SettingsViewModel(
    ColtonStackApiClient api,
    IMessenger messenger,
    IValidator<UpdateProfileRequest> validator,
    ILogger<SettingsViewModel> logger) : ObservableObject
{
    /// <summary>Slack-ish swatches; the selected one is stored on the server per profile.</summary>
    public IReadOnlyList<string> Palette { get; } =
        ["#E01E5A", "#2EB67D", "#ECB22E", "#36C5F0", "#E8912D", "#611F69", "#1264A3", "#FF6B6B"];

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initials))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AvatarColor { get; set; } = "#E01E5A";

    /// <summary>Live avatar preview while the user types their name.</summary>
    public string Initials => NameInitials.From(DisplayName);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task OpenAsync(CancellationToken cancellationToken)
    {
        ErrorText = string.Empty;
        IsOpen = true;
        IsBusy = true;
        try
        {
            var users = await api.GetUsersAsync(cancellationToken);
            if (users.FirstOrDefault(user => user.IsSelf) is { } self)
            {
                DisplayName = self.DisplayName;
                AvatarColor = self.AvatarColor;
            }
        }
        catch (Exception ex)
        {
            ProfileLoadFailed(ex);
            ErrorText = "Could not load your profile — is the server up?";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    private bool CanSave()
    {
        if (DisplayName.Trim().Length == 0)
        {
            return false;
        }

        // Reuse the shared server-side validator rules for the CanExecute check.
        var request = new UpdateProfileRequest(DisplayName.Trim(), AvatarColor);
        return validator.Validate(request).IsValid;
    }

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorText = string.Empty;

        // Validate client-side using the same rules the server enforces.
        var request = new UpdateProfileRequest(DisplayName.Trim(), AvatarColor);
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            ErrorText = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            IsBusy = false;
            return;
        }

        try
        {
            var updated = await api.UpdateProfileAsync(DisplayName.Trim(), AvatarColor, cancellationToken);
            messenger.Send(new ProfileUpdatedMessage(updated));
            ProfileSaved(updated.DisplayName);
            IsOpen = false;
        }
        catch (Exception ex)
        {
            ProfileSaveFailed(ex);
            ErrorText = "Saving failed after retries — try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Profile saved as {DisplayName}")]
    private partial void ProfileSaved(string displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading the profile failed after retries")]
    private partial void ProfileLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Saving the profile failed after retries")]
    private partial void ProfileSaveFailed(Exception exception);
}
