using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// What a status number means for a citation is a judgement about the draft, so it is pinned
/// here in the core suite rather than once per adapter.
/// </summary>
public class CitationOutcomeTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(204)]
    [InlineData(299)]
    public void A_success_means_the_source_is_there(int status) =>
        Assert.Equal(CitationStatus.Resolves, CitationOutcome.FromHttpStatus(status));

    [Theory]
    [InlineData(404)]
    [InlineData(410)]
    public void Only_a_definite_no_is_dead(int status) =>
        Assert.Equal(CitationStatus.Dead, CitationOutcome.FromHttpStatus(status));

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(301)]
    [InlineData(403)]
    public void Everything_else_says_nothing_about_the_draft(int status) =>
        Assert.Equal(CitationStatus.Unreachable, CitationOutcome.FromHttpStatus(status));
}
