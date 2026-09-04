using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// Connectivity chip + retry counter + the chaos switch. Subscribes to resilience pipeline
/// events (retries, send failures) through the messenger — the pipeline in App.xaml.cs has no
/// direct reference to this class, and this class has none to the pipeline. Messages arrive
/// on the UI thread (see UiThreadMessenger), so Receive() just sets properties.
/// </summary>
public sealed partial class StatusBarViewModel(
    IColtonStackApiClient api,
    ILogger<StatusBarViewModel> logger) : ObservableObject, IRecipient<ConnectionStatusMessage>, IRecipient<HttpRetryMessage>
{
    [ObservableProperty]
    public partial ConnectionState Status { get; set; } = ConnectionState.Connecting;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Starting…";

    /// <summary>Retries observed this session — the resilience pipeline made visible.</summary>
    public int RetryCount
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(RetryCountText));
            }
        }
    }

    public string RetryCountText => RetryCount == 0 ? "no retries" : $"{RetryCount} retr{(RetryCount == 1 ? "y" : "ies")}";

    [ObservableProperty]
    public partial bool ChaosEnabled { get; set; }

    /// <summary>Mirrors the server's chat-activity simulator; synced from the server on startup.</summary>
    [ObservableProperty]
    public partial bool SimEnabled { get; set; }

    private bool _suppressSimulationToggle;

    partial void OnChaosEnabledChanged(bool value) => _ = ToggleChaosOnServerAsync(value);

    partial void OnSimEnabledChanged(bool value)
    {
        if (_suppressSimulationToggle)
        {
            return;
        }

        _ = ToggleSimulationOnServerAsync(value);
    }

    /// <summary>Fetches the simulator's current state so the toggle reflects reality after startup.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var enabled = await api.GetSimulationAsync(CancellationToken.None);

            _suppressSimulationToggle = true;
            SimEnabled = enabled;
            _suppressSimulationToggle = false;
        }
        catch (Exception ex)
        {
            SimulationStateUnknown(ex);
        }
    }

    // Deliberately not a [RelayCommand]: the toggle's state lives in the checkbox itself.
    private async Task ToggleChaosOnServerAsync(bool enabled)
    {
        try
        {
            await api.SetChaosAsync(enabled, CancellationToken.None);
            ChaosToggled(enabled);
        }
        catch (Exception ex)
        {
            ChaosToggleFailed(ex);
            StatusText = "Chaos toggle failed";
        }
    }

    private async Task ToggleSimulationOnServerAsync(bool enabled)
    {
        try
        {
            await api.SetSimulationAsync(enabled, CancellationToken.None);
            SimulationToggled(enabled);
        }
        catch (Exception ex)
        {
            SimulationToggleFailed(ex);
            StatusText = "Simulator toggle failed";
        }
    }

    public void Receive(ConnectionStatusMessage message)
    {
        Status = message.State;
        StatusText = message.Detail;
    }

    public void Receive(HttpRetryMessage message)
    {
        RetryCount++;
        StatusText = message.Detail.Length == 0 ? $"Retrying (attempt {message.Attempt})…" : message.Detail;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Chaos mode toggled to {Enabled}")]
    private partial void ChaosToggled(bool enabled);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to toggle chaos mode on the server")]
    private partial void ChaosToggleFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Chat simulation toggled to {Enabled}")]
    private partial void SimulationToggled(bool enabled);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to toggle chat simulation on the server")]
    private partial void SimulationToggleFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Could not read the simulator state at startup — toggle shows off")]
    private partial void SimulationStateUnknown(Exception exception);
}
