using Tare.Core;
using Xunit;

namespace Tare.Core.Tests;

public class CitationPolicyTests
{
    [Theory]
    [InlineData("https://example.com/report")]
    [InlineData("http://example.com:8443/report")]
    [InlineData("https://sub.domain.example.org/a/b?c=1#d")]
    public void Accepts_an_ordinary_public_url(string url)
    {
        Assert.Null(CitationPolicy.Reject(url));
    }

    [Theory]
    [InlineData("ftp://example.com/report")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url at all")]
    public void Rejects_anything_that_is_not_an_http_url(string url)
    {
        Assert.NotNull(CitationPolicy.Reject(url));
    }

    [Theory]
    [InlineData("http://localhost:8080/admin")]
    [InlineData("https://127.0.0.1/admin")]
    [InlineData("https://10.0.0.5/admin")]
    [InlineData("https://172.16.4.1/admin")]
    [InlineData("https://192.168.1.9/admin")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://[::1]/admin")]
    [InlineData("https://box.local/admin")]
    [InlineData("https://wiki.internal/admin")]
    public void Rejects_a_host_that_is_not_on_the_public_internet(string url)
    {
        Assert.NotNull(CitationPolicy.Reject(url));
    }

    [Fact]
    public void Rejects_a_url_carrying_credentials()
    {
        Assert.NotNull(CitationPolicy.Reject("https://user:secret@example.com/report"));
    }

    [Fact]
    public void Explains_why_it_rejected()
    {
        Assert.Contains("loopback", CitationPolicy.Reject("https://127.0.0.1/admin")!);
    }
}
