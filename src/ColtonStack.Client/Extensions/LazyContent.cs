namespace ColtonStack.Client.Extensions;

/// <summary>
/// "Build this view model from DI the first time somebody looks, then keep it." Shared by
/// sidebar panes and settings sections — composed into both definitions rather than inherited,
/// so each stays a flat sealed class. The cache is the C# 14 <c>field</c> keyword: no declared
/// backing field, custom logic in the getter.
/// </summary>
public sealed class LazyContent(string ownerId, Func<IServiceProvider, object> factory)
{
    private IServiceProvider? _services;

    /// <summary>Called by the owning registry once the DI container exists.</summary>
    internal void Attach(IServiceProvider services) => _services = services;

    /// <summary>True once the container is available, i.e. after the host started.</summary>
    public bool IsAttached => _services is not null;

    /// <summary>The materialized content. Created on first access, then cached.</summary>
    public object Value => field ??= _services is { } services
        ? factory(services)
        : throw new InvalidOperationException($"'{ownerId}' was activated before the host finished starting.");

    /// <summary>Runs an extension hook against the attached provider, or completes immediately when there is none.</summary>
    public Task RunAsync(Func<IServiceProvider, Task>? hook) =>
        hook is null || _services is not { } services ? Task.CompletedTask : hook(services);
}
