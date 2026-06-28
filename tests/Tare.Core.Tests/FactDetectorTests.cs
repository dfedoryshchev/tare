using Xunit;

namespace Tare.Core.Tests;

public class FactDetectorTests
{
    [Fact]
    public void Detects_a_number()
    {
        Assert.True(FactDetector.HasConcreteFact("Revenue grew 20% last year."));
    }

    [Fact]
    public void Detects_a_date()
    {
        Assert.True(FactDetector.HasConcreteFact("The project shipped in 2019."));
    }

    [Fact]
    public void Detects_a_mid_sentence_proper_noun()
    {
        Assert.True(FactDetector.HasConcreteFact("Our team moved the workload onto Kubernetes."));
    }

    [Fact]
    public void Ignores_a_plain_filler_sentence()
    {
        Assert.False(FactDetector.HasConcreteFact("It is important to note that things move on."));
    }
}
