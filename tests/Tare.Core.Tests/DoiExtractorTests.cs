using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

public class DoiExtractorTests
{
    [Fact]
    public void Finds_a_bare_doi()
    {
        var found = DoiExtractor.Extract("As shown in 10.1038/nphys1170, the effect holds.");

        Assert.Equal("10.1038/nphys1170", Assert.Single(found).Value);
    }

    [Theory]
    [InlineData("see doi:10.1038/nphys1170 for the method")]
    [InlineData("see DOI: 10.1038/nphys1170 for the method")]
    [InlineData("see https://doi.org/10.1038/nphys1170 for the method")]
    [InlineData("see http://dx.doi.org/10.1038/nphys1170 for the method")]
    public void Finds_a_doi_however_it_is_written(string source)
    {
        Assert.Equal("10.1038/nphys1170", Assert.Single(DoiExtractor.Extract(source)).Value);
    }

    [Fact]
    public void Lowercases_the_doi_so_the_same_work_is_one_entry()
    {
        // DOIs are case-insensitive by spec, so two spellings of one work must not read as
        // two citations - and must not cost two lookups.
        var found = DoiExtractor.Extract("10.1038/NPhys1170 and 10.1038/nphys1170");

        Assert.Equal(2, found.Count);
        Assert.All(found, d => Assert.Equal("10.1038/nphys1170", d.Value));
        Assert.Single(DoiExtractor.Distinct(found));
    }

    [Fact]
    public void Skips_a_doi_inside_a_code_fence()
    {
        // Same reasoning as CitationExtractor: a DOI in a snippet is an argument to a
        // command, not a source the author is standing behind.
        var source = "text\n\n```\ncurl https://doi.org/10.1038/nphys1170\n```\n\nmore text";

        Assert.Empty(DoiExtractor.Extract(source));
    }

    [Fact]
    public void Records_the_span_so_a_report_can_point_at_it()
    {
        const string source = "As shown in 10.1038/nphys1170, the effect holds.";
        var doi = Assert.Single(DoiExtractor.Extract(source));

        Assert.Equal("10.1038/nphys1170", source.Substring(doi.StartChar, doi.EndChar - doi.StartChar));
    }

    [Theory]
    [InlineData("version 10.2 of the spec")]
    [InlineData("10.1038 on its own")]
    [InlineData("ratio 9.1234/5678 is unrelated")]
    // A registrant prefix is 10. followed by at least four digits, so a short one is a
    // version or a measurement that happens to have a slash after it.
    [InlineData("10.2/3 of the budget")]
    [InlineData("section 10.15/b applies")]
    public void Ignores_things_that_only_look_like_a_doi(string source)
    {
        Assert.Empty(DoiExtractor.Extract(source));
    }

    [Fact]
    public void Does_not_swallow_trailing_sentence_punctuation()
    {
        var doi = Assert.Single(DoiExtractor.Extract("we cite 10.1038/nphys1170."));

        Assert.Equal("10.1038/nphys1170", doi.Value);
    }
}
