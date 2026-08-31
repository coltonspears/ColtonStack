using System.Collections.ObjectModel;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The audit trail viewer, delivered by the audit pane extension — a plain view model with a
/// load command and nothing else. It exists only because the extension registered it; the
/// core app has no idea this pane exists.
///
/// Contrast with the legacy world: auditing was a bool on a base class, invisible and
/// untestable. Here it is a queryable feed of dumb records rendered by an extension.
/// </summary>
public sealed partial class AuditViewModel(
    ColtonStackApiClient api,
    IMessenger messenger,
    ILogger<AuditViewModel> logger) : ObservableObject
{
    public const int PageSize = 200;

    public ObservableCollection<AuditEntryDto> Entries { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>False while the trail is empty — drives the pane's empty-state message.</summary>
    public bool HasEntries => Entries.Count > 0;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(HasEntries));

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        try
        {
            var entries = await api.GetAuditAsync(PageSize, cancellationToken);

            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }

            OnPropertyChanged(nameof(HasEntries));
        }
        catch (Exception ex)
        {
            AuditLoadFailed(ex);
            messenger.Send(new HttpRetryMessage(0, $"Could not load the audit trail: {ex.Message}"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading the audit trail failed after retries")]
    private partial void AuditLoadFailed(Exception exception);
}