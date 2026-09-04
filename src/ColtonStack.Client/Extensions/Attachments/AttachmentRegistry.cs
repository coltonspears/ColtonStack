using ColtonStack.Contracts;

namespace ColtonStack.Client.Extensions.Attachments;

/// <summary>Concrete attachment registry filled by the client extension list at startup.</summary>
public sealed class AttachmentRegistry : IAttachmentRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider, string, object>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private IServiceProvider? _services;

    public void Register(string kind, Func<IServiceProvider, string, object> materialize)
    {
        if (!_factories.TryAdd(kind, materialize))
        {
            throw new InvalidOperationException($"An attachment renderer for kind '{kind}' is already registered.");
        }
    }

    /// <summary>Called once by the composition root after the host is built.</summary>
    public void Attach(IServiceProvider services) => _services = services;

    public object? Materialize(MessageAttachmentDto? attachment)
    {
        if (attachment is null)
        {
            return null;
        }

        if (!_factories.TryGetValue(attachment.Kind, out var factory))
        {
            return new UnknownAttachmentViewModel(attachment.Kind);
        }

        var services = _services ?? throw new InvalidOperationException("Attachments were materialized before the host finished starting.");
        return factory(services, attachment.PayloadJson);
    }
}