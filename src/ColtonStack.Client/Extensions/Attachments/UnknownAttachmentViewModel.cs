namespace ColtonStack.Client.Extensions.Attachments;

/// <summary>Rendered when a message carries an attachment kind this client has no extension for — a graceful "install the extension" hint rather than a blank.</summary>
public sealed record UnknownAttachmentViewModel(string Kind);
