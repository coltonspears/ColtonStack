namespace ColtonStack.Client.Services;

/// <summary>
/// Key/value preferences persisted on the server (<c>/api/settings</c>). Extensions own their keys
/// (<c>pokemon.artwork</c>, <c>audit.pageSize</c>); the store owns transport, caching and change
/// notification. Reads are synchronous against the in-memory snapshot so bindings stay simple.
/// </summary>
public interface ISettingsStore
{
    /// <summary>True once the first server load completed (successfully or not).</summary>
    bool IsLoaded { get; }

    /// <summary>Pulls every setting from the server. Safe to call repeatedly.</summary>
    Task LoadAsync(CancellationToken cancellationToken);

    string GetString(string key, string fallback);

    bool GetBool(string key, bool fallback);

    int GetInt(string key, int fallback);

    /// <summary>Saves and publishes <see cref="Messages.SettingChangedMessage"/>. Throws if the server rejected it.</summary>
    Task SetAsync(string key, string value, CancellationToken cancellationToken);
}
