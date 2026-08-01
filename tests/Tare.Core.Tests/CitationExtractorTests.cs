using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

public class CitationExtractorTests
{
    [Fact]
    public void Extracts_the_target_of_a_markdown_link()
    {
        const string source = "The queue drained in 40 minutes, see [the incident log](https://example.com/incidents/74).\n";

        var citation = Assert.Single(CitationExtractor.Extract(source));

        Assert.Equal("https://example.com/incidents/74", citation.Url);
    }

    [Fact]
    public void Spans_point_back_at_the_url_in_the_source()
    {
        const string source = "Throughput held at 12k/s, see [the run](https://example.com/bench) for the raw numbers.\n";

        var citation = Assert.Single(CitationExtractor.Extract(source));

        Assert.Equal(citation.Url, source.Substring(citation.StartChar, citation.EndChar - citation.StartChar));
    }

    [Fact]
    public void Extracts_a_bare_url_without_the_sentence_full_stop()
    {
        const string source = "The failure rate is published at https://example.com/status.\n";

        var citation = Assert.Single(CitationExtractor.Extract(source));

        Assert.Equal("https://example.com/status", citation.Url);
    }

    [Fact]
    public void Extracts_an_angle_bracket_autolink()
    {
        const string source = "The schedule is <https://example.com/calendar> and it changes weekly.\n";

        var citation = Assert.Single(CitationExtractor.Extract(source));

        Assert.Equal("https://example.com/calendar", citation.Url);
    }

    [Fact]
    public void Ignores_a_url_inside_a_code_fence()
    {
        const string source = "Fetch it directly:\n\n```\ncurl https://example.com/api/v1\n```\n";

        Assert.Empty(CitationExtractor.Extract(source));
    }

    [Fact]
    public void Ignores_a_scheme_it_cannot_resolve()
    {
        const string source = "Write to mailto:someone@example.com or ask on irc://example.com/room.\n";

        Assert.Empty(CitationExtractor.Extract(source));
    }

    [Fact]
    public void Extracts_every_entry_of_a_reference_list()
    {
        const string source = "- [First](https://a.example/one)\n- [Second](https://b.example/two)\n";

        var citations = CitationExtractor.Extract(source);

        Assert.Equal(2, citations.Count);
        Assert.Equal("https://a.example/one", citations[0].Url);
        Assert.Equal("https://b.example/two", citations[1].Url);
    }

    [Fact]
    public void Records_the_block_each_citation_came_from()
    {
        const string source =
            "# Rollout\n\nThe first window is documented at https://example.com/one.\n\n"
            + "The second window is documented at https://example.com/two.\n";

        var citations = CitationExtractor.Extract(source);

        Assert.Equal(2, citations.Count);
        Assert.Equal(1, citations[0].BlockIndex);
        Assert.Equal(2, citations[1].BlockIndex);
    }

    [Fact]
    public void Keeps_both_occurrences_when_one_url_is_cited_twice()
    {
        const string source =
            "The report at https://example.com/report says one thing.\n\n"
            + "The same report at https://example.com/report says it again.\n";

        var citations = CitationExtractor.Extract(source);

        Assert.Equal(2, citations.Count);
        Assert.NotEqual(citations[0].StartChar, citations[1].StartChar);
    }
}
