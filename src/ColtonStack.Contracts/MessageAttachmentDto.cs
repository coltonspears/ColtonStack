namespace ColtonStack.Contracts;

/// <summary>
/// A structured payload riding along with a chat message — a Pokémon card, a poll, a file. The
/// core knows only the <see cref="Kind"/> discriminator and an opaque JSON body; the extension
/// that owns the kind supplies the type on the server and the renderer on the client. This is
/// how the message pipeline stays closed to modification while extensions add rich content.
/// </summary>
public sealed record MessageAttachmentDto(string Kind, string PayloadJson);
