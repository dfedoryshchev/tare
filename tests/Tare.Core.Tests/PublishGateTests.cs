using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

/// <summary>
/// The exit-code contract, pinned. A build step reads these numbers and nothing else, so the
/// mapping is asserted here rather than left to whichever front-end happens to return it.
/// <para>
/// All nine band/bar pairs are enumerated on purpose. "At or above" is a comparison over the
/// declaration order of <see cref="Band"/>, so an enum reordered by a later edit would change
/// what every bar means without touching a line of gate code; the order itself is asserted
/// below for the same reason.
/// </para>
/// </summary>
public class PublishGateTests
{
    [Theory]
    [InlineData(Band.Clean, Band.Clean)]
    [InlineData(Band.Watch, Band.Clean)]
    [InlineData(Band.Slop, Band.Clean)]
    [InlineData(Band.Watch, Band.Watch)]
    [InlineData(Band.Slop, Band.Watch)]
    [InlineData(Band.Slop, Band.Slop)]
    public void A_band_at_or_above_the_bar_is_refused(Band band, Band bar)
    {
        Assert.True(PublishGate.Trips(band, bar));
        Assert.Equal(PublishGate.Blocked, PublishGate.ExitCode(band, bar));
    }

    [Theory]
    [InlineData(Band.Clean, Band.Watch)]
    [InlineData(Band.Clean, Band.Slop)]
    [InlineData(Band.Watch, Band.Slop)]
    public void A_band_below_the_bar_goes_through(Band band, Band bar)
    {
        Assert.False(PublishGate.Trips(band, bar));
        Assert.Equal(PublishGate.Passed, PublishGate.ExitCode(band, bar));
    }

    [Theory]
    [InlineData(Band.Clean)]
    [InlineData(Band.Watch)]
    [InlineData(Band.Slop)]
    public void No_bar_means_no_gate(Band band) =>
        Assert.Equal(PublishGate.Passed, PublishGate.ExitCode(band, null));

    [Fact]
    public void The_worst_band_is_refused_by_every_bar() =>
        Assert.All(
            Enum.GetValues<Band>(),
            bar => Assert.Equal(PublishGate.Blocked, PublishGate.ExitCode(Band.Slop, bar)));

    [Fact]
    public void Refusing_a_draft_is_not_the_same_answer_as_failing_to_read_one()
    {
        // The whole reason for a second number. A caller has to be able to tell a draft that
        // was read and refused from a run that never produced an opinion, and both from a pass.
        var codes = new[] { PublishGate.Passed, PublishGate.Error, PublishGate.Blocked };
        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.Equal(0, PublishGate.Passed);
    }

    [Fact]
    public void The_bands_are_declared_cleanest_first()
    {
        // Trips is an ordering comparison, so this is the assumption underneath every case
        // above, stated where someone editing the enum has a chance of reading it.
        Assert.True(Band.Clean < Band.Watch);
        Assert.True(Band.Watch < Band.Slop);
    }
}
