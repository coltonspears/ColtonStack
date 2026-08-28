using System.Collections.ObjectModel;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The workspace directory shown when the rail's People tile is active. Loads members over
/// HTTP and hears about profile changes through the messenger — it has no reference to the
/// settings view model that caused them.
/// </summary>
public sealed partial class PeopleViewModel(
    ColtonStackApiClient api,
    IMessenger messenger,
    ILogger<PeopleViewModel> logger) : ObservableObject, IRecipient<ProfileUpdatedMessage>
{
    public ObservableCollection<PersonViewModel> People { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        try
        {
            var users = await api.GetUsersAsync(cancellationToken);

            People.Clear();
            foreach (var user in users)
            {
                People.Add(new PersonViewModel(user));
            }

            PeopleLoaded(People.Count);
        }
        catch (Exception ex)
        {
            PeopleLoadFailed(ex);
            messenger.Send(new HttpRetryMessage(0, $"Could not load people: {ex.Message}"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Receive(ProfileUpdatedMessage message)
    {
        var existing = People.FirstOrDefault(person => person.Id == message.User.Id);
        if (existing is not null)
        {
            People[People.IndexOf(existing)] = new PersonViewModel(message.User);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded {Count} workspace members")]
    private partial void PeopleLoaded(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading workspace members failed after retries")]
    private partial void PeopleLoadFailed(Exception exception);
}
