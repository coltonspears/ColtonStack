using System.Collections.ObjectModel;
using ColtonStack.Client.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The slide-over diagnostics panel: a bounded, live view of the app's own ILogger output.
/// It subscribes like every other view model — IRecipient on the messenger — and because the
/// provider publishes through UiThreadMessenger, it never touches a Dispatcher.
///
/// The buffer is capped: a panel must never become the app's biggest memory consumer.
/// </summary>
public sealed partial class DiagnosticsViewModel : ObservableObject, IRecipient<DiagnosticEntryMessage>
{
    /// <summary>Oldest entries are dropped beyond this count — live tail, not a history database.</summary>
    private const int MaxEntries = 400;

    public ObservableCollection<DiagnosticEntryMessage> Entries { get; } = [];

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    public void Receive(DiagnosticEntryMessage message)
    {
        Entries.Add(message);
        if (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(0);
        }
    }

    [RelayCommand]
    private void Clear() => Entries.Clear();

    [RelayCommand]
    private void Close() => IsOpen = false;
}