using ColtonStack.Contracts;

namespace ColtonStack.Client.Extensions.Attachments;

/// <summary>
/// Maps an attachment <c>Kind</c> to the view model that renders it. Extensions register a
/// factory (services + JSON in, presentation object out) and ship the matching implicit
/// DataTemplate; the chat pane calls <see cref="Materialize"/> and binds the result to a
/// ContentControl. The core never learns what a "pokemon" is.
/// </summary>
public interface IAttachmentRegistry
{
    /// <summary>Registers the renderer factory for one attachment kind. Duplicate kinds throw at startup.</summary>
    void Register(string kind, Func<IServiceProvider, string, object> materialize);

    /// <summary>Turns a wire attachment into its presentation object, or an <see cref="UnknownAttachmentViewModel"/> for kinds nobody registered.</summary>
    object? Materialize(MessageAttachmentDto? attachment);
}
