using ColtonStack.Client.ViewModels;
using ColtonStack.Contracts;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// MessageSearch is the filtering half of the chat pane, extracted so it can be tested here with
/// no WPF, no ICollectionView and no dispatcher. The conversation keeps arriving while a filter
/// is active; the results must stay correct without a full rebuild per message.
/// </summary>
public sealed class MessageSearchTests
{
    private static MessageViewModel Message(long id, string author, string text) => new(
        new MessageDto(id, ChannelId: 1, UserId: 1, author, "#000", text, new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero)),
        isFirstOfGroup: true);

    [Fact]
    public void NoFilter_ResultsMirrorSource()
    {
        using var search = new MessageSearch();
        search.Source.Add(Message(1, "Ann", "hello"));
        search.Source.Add(Message(2, "Bob", "world"));

        Assert.Equal([1L, 2L], search.Results.Select(m => m.Id));
        Assert.False(search.IsFiltering);
        Assert.Equal(2, search.Count);
    }

    [Fact]
    public void Filter_MatchesTextAndAuthor_CaseInsensitively()
    {
        using var search = new MessageSearch();
        search.Source.Add(Message(1, "Ann", "Deploy went fine"));
        search.Source.Add(Message(2, "Bob", "lunch?"));
        search.Source.Add(Message(3, "Deploy Bot", "all green"));

        search.Filter = "deploy";

        Assert.True(search.IsFiltering);
        Assert.Equal([1L, 3L], search.Results.Select(m => m.Id));
    }

    [Fact]
    public void Filter_WhitespaceOnly_MeansNoFilter()
    {
        using var search = new MessageSearch();
        search.Source.Add(Message(1, "Ann", "a"));

        search.Filter = "   ";

        Assert.False(search.IsFiltering);
        Assert.Single(search.Results);
    }

    [Fact]
    public void LiveArrival_WhileFiltering_OnlyAddsMatches()
    {
        using var search = new MessageSearch();
        search.Filter = "bug";

        search.Source.Add(Message(1, "Ann", "found a bug"));
        search.Source.Add(Message(2, "Bob", "nice weather"));
        search.Source.Add(Message(3, "Cy", "BUG fixed"));

        Assert.Equal([1L, 3L], search.Results.Select(m => m.Id));
        Assert.Equal(2, search.Count);
    }

    [Fact]
    public void ClearingSource_ClearsResults()
    {
        using var search = new MessageSearch();
        search.Source.Add(Message(1, "Ann", "a"));
        search.Source.Add(Message(2, "Bob", "b"));

        search.Source.Clear();

        Assert.Empty(search.Results);
        Assert.Equal(0, search.Count);
    }

    [Fact]
    public void ClearingFilter_RestoresEverything()
    {
        using var search = new MessageSearch();
        search.Source.Add(Message(1, "Ann", "alpha"));
        search.Source.Add(Message(2, "Bob", "beta"));
        search.Filter = "alpha";
        Assert.Single(search.Results);

        search.Filter = string.Empty;

        Assert.Equal(2, search.Results.Count);
    }

    [Fact]
    public void Filter_RaisesPropertyChanged_ForFilterAndIsFiltering()
    {
        using var search = new MessageSearch();
        var changed = new List<string?>();
        search.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        search.Filter = "x";

        Assert.Contains(nameof(MessageSearch.Filter), changed);
        Assert.Contains(nameof(MessageSearch.IsFiltering), changed);
    }
}
