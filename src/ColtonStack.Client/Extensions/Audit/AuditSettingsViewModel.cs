using ColtonStack.Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColtonStack.Client.Extensions.Audit;

/// <summary>
/// The audit extension's settings section: one preference (page size) persisted through the
/// core settings store under a key the extension owns. Shows the whole pattern in 40 lines —
/// an extension contributes a setting without the core learning what it means.
/// </summary>
public sealed partial class AuditSettingsViewModel(ISettingsStore store) : ObservableObject
{
    public const string PageSizeKey = "audit.pagesize";
    public const int DefaultPageSize = 200;

    public IReadOnlyList<int> PageSizes { get; } = [50, 100, 200, 500];

    [ObservableProperty]
    public partial int PageSize { get; set; } = store.GetInt(PageSizeKey, DefaultPageSize);

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    partial void OnPageSizeChanged(int value) => _ = SaveAsync(value);

    private async Task SaveAsync(int value)
    {
        try
        {
            await store.SetAsync(PageSizeKey, value.ToString(System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
            StatusText = "Saved";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not save: {ex.Message}";
        }
    }

    /// <summary>Re-reads the store — used when the section is shown after settings finished loading.</summary>
    [RelayCommand]
    private void Reload() => PageSize = store.GetInt(PageSizeKey, DefaultPageSize);
}
