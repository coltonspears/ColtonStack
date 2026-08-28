using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ColtonStack.Client.Behaviors;

/// <summary>
/// Attached property that renders Slack-style message markup into a TextBlock's inlines:
/// *bold*, _italic_, ~strike~, `code`, and clickable http(s) links. One level deep, on
/// purpose — a full markdown engine would bury the demo. Unmatched markers render literally.
/// </summary>
public static class MessageTextFormatter
{
    private static readonly SolidColorBrush LinkBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x1D, 0x9B, 0xD1)));
    private static readonly SolidColorBrush CodeForeground = Frozen(new SolidColorBrush(Color.FromRgb(0xF0, 0x8E, 0xA2)));
    private static readonly SolidColorBrush CodeBackground = Frozen(new SolidColorBrush(Color.FromRgb(0x2B, 0x2F, 0x33)));
    private static readonly FontFamily CodeFont = new("Consolas");

    public static readonly DependencyProperty FormattedTextProperty = DependencyProperty.RegisterAttached(
        "FormattedText", typeof(string), typeof(MessageTextFormatter), new PropertyMetadata(null, OnFormattedTextChanged));

    public static string? GetFormattedText(DependencyObject element) => (string?)element.GetValue(FormattedTextProperty);

    public static void SetFormattedText(DependencyObject element, string? value) => element.SetValue(FormattedTextProperty, value);

    private static void OnFormattedTextChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBlock textBlock)
        {
            return;
        }

        textBlock.Inlines.Clear();
        foreach (var inline in Parse((string?)e.NewValue ?? string.Empty))
        {
            textBlock.Inlines.Add(inline);
        }
    }

    private static List<Inline> Parse(string text)
    {
        var inlines = new List<Inline>();
        var plain = new System.Text.StringBuilder();
        var index = 0;

        void FlushPlain()
        {
            if (plain.Length > 0)
            {
                inlines.Add(new Run(plain.ToString()));
                plain.Clear();
            }
        }

        while (index < text.Length)
        {
            var current = text[index];

            if (current is '*' or '_' or '~' or '`')
            {
                var closing = text.IndexOf(current, index + 1);
                if (closing > index + 1)
                {
                    FlushPlain();
                    inlines.Add(StyledRun(text[(index + 1)..closing], current));
                    index = closing + 1;
                    continue;
                }
            }

            if (current == 'h' && TryReadUrl(text, index, out var url))
            {
                FlushPlain();
                inlines.Add(MakeLink(url));
                index += url.Length;
                continue;
            }

            plain.Append(current);
            index++;
        }

        FlushPlain();
        return inlines;
    }

    private static Run StyledRun(string inner, char marker)
    {
        var run = new Run(inner);
        switch (marker)
        {
            case '*':
                run.FontWeight = FontWeights.Bold;
                break;
            case '_':
                run.FontStyle = FontStyles.Italic;
                break;
            case '~':
                run.TextDecorations = TextDecorations.Strikethrough;
                break;
            case '`':
                run.FontFamily = CodeFont;
                run.Foreground = CodeForeground;
                run.Background = CodeBackground;
                break;
        }

        return run;
    }

    private static bool TryReadUrl(string text, int index, out string url)
    {
        url = string.Empty;
        var rest = text.AsSpan(index);
        if (!rest.StartsWith("http://", StringComparison.Ordinal) && !rest.StartsWith("https://", StringComparison.Ordinal))
        {
            return false;
        }

        var end = 0;
        while (end < rest.Length && !char.IsWhiteSpace(rest[end]))
        {
            end++;
        }

        // Trailing sentence punctuation belongs to the sentence, not the link.
        var candidate = rest[..end].TrimEnd(".,;:!?)".AsSpan()).ToString();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out _))
        {
            return false;
        }

        url = candidate;
        return true;
    }

    private static Hyperlink MakeLink(string url)
    {
        var link = new Hyperlink(new Run(url))
        {
            NavigateUri = new Uri(url),
            Foreground = LinkBrush,
            TextDecorations = null,
        };
        link.RequestNavigate += static (_, args) =>
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // No browser? The demo goes on.
            }
        };
        return link;
    }

    private static SolidColorBrush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
