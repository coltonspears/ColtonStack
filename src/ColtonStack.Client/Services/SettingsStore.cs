using System.Globalization;
using ColtonStack.Client.Messages;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.Services;

/// <summary>Server-backed settings snapshot. Values are strings on the wire; the typed getters parse leniently and fall back.</summary>
public sealed partial class SettingsStore(
    IColtonStackApiClient api,
    IMessenger messenger,
    ILogger<SettingsStore> logger) : ISettingsStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public bool IsLoaded { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await api.GetSettingsAsync(cancellationToken).ConfigureAwait(true);
            _values.Clear();
            foreach (var setting in settings)
            {
                _values[setting.Key] = setting.Value;
            }

            SettingsLoaded(_values.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SettingsLoadFailed(ex);
        }
        finally
        {
            IsLoaded = true;
        }
    }

    public string GetString(string key, string fallback) =>
        _values.TryGetValue(key, out var value) ? value : fallback;

    public bool GetBool(string key, bool fallback) =>
        _values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;

    public int GetInt(string key, int fallback) =>
        _values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        var saved = await api.PutSettingAsync(key, value, cancellationToken).ConfigureAwait(true);
        _values[saved.Key] = saved.Value;
        messenger.Send(new SettingChangedMessage(saved.Key, saved.Value));
        SettingSaved(saved.Key);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded {Count} settings from the server")]
    private partial void SettingsLoaded(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading settings failed — defaults apply until the next load")]
    private partial void SettingsLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Setting {Key} saved")]
    private partial void SettingSaved(string key);
}
