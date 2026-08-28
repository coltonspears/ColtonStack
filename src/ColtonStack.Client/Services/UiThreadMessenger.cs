using CommunityToolkit.Mvvm.Messaging;

namespace ColtonStack.Client.Services;

/// <summary>
/// Decorates any <see cref="IMessenger"/> so that every message is delivered on the UI thread,
/// no matter which thread sent it. SignalR callbacks and resilience-pipeline events arrive on
/// thread-pool threads; marshaling once here — at the boundary — means no view model ever
/// touches a Dispatcher or thinks about threads at all.
///
/// Composition over inheritance: this class doesn't extend a messenger, it wraps one. The
/// wrapped messenger keeps all of its own behavior (weak references, cleanup, tokens).
/// </summary>
public sealed class UiThreadMessenger : IMessenger
{
    private readonly IMessenger _inner;
    private readonly SynchronizationContext _uiContext;
    private readonly int _uiThreadId;

    /// <summary>Captures the calling thread as "the UI thread" — the composition root constructs this during startup.</summary>
    public UiThreadMessenger(IMessenger inner)
    {
        _inner = inner;
        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException($"{nameof(UiThreadMessenger)} must be created on the UI thread.");
        _uiThreadId = Environment.CurrentManagedThreadId;
    }

    public TMessage Send<TMessage, TToken>(TMessage message, TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken>
    {
        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            return _inner.Send(message, token);
        }

        _uiContext.Post(state => _inner.Send((TMessage)state!, token), message);
        return message;
    }

    // Everything below is pure delegation — registration and cleanup are already thread-safe.

    public bool IsRegistered<TMessage, TToken>(object recipient, TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken>
        => _inner.IsRegistered<TMessage, TToken>(recipient, token);

    public void Register<TRecipient, TMessage, TToken>(TRecipient recipient, TToken token, MessageHandler<TRecipient, TMessage> handler)
        where TRecipient : class
        where TMessage : class
        where TToken : IEquatable<TToken>
        => _inner.Register(recipient, token, handler);

    public void Unregister<TMessage, TToken>(object recipient, TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken>
        => _inner.Unregister<TMessage, TToken>(recipient, token);

    public void UnregisterAll<TToken>(object recipient, TToken token)
        where TToken : IEquatable<TToken>
        => _inner.UnregisterAll(recipient, token);

    public void UnregisterAll(object recipient) => _inner.UnregisterAll(recipient);

    public void Cleanup() => _inner.Cleanup();

    public void Reset() => _inner.Reset();
}
