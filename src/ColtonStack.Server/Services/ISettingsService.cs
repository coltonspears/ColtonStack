using ColtonStack.Contracts;

namespace ColtonStack.Server.Services;

/// <summary>Persisted key/value preferences. Extensions own their keys; the core owns the table and the endpoint.</summary>
public interface ISettingsService
{
    Task<IReadOnlyList<SettingDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<SettingDto?> FindAsync(string key, CancellationToken cancellationToken);

    /// <summary>Insert-or-update. The key must already satisfy <see cref="SettingKey.IsValid"/>.</summary>
    Task<SettingDto> SetAsync(string key, string value, CancellationToken cancellationToken);
}
