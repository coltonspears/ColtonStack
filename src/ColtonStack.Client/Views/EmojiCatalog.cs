namespace ColtonStack.Client.Views;

/// <summary>
/// The emoji picker's contents. Pure view data (bound via x:Static) — no view model needs to
/// know which emojis exist.
/// </summary>
public static class EmojiCatalog
{
    public static IReadOnlyList<string> All { get; } =
    [
        "😀", "😂", "😊", "😉", "😍", "🤔", "😎", "😅",
        "😢", "😡", "🙃", "🥳", "😴", "🤯", "🫠", "🤝",
        "👍", "👎", "👋", "🙌", "👏", "🙏", "💪", "👀",
        "🎉", "🔥", "❤️", "💯", "⭐", "✅", "❌", "❓",
        "💡", "🚀", "🐛", "☕", "🍕", "🌮", "🐦", "🎯",
    ];
}
