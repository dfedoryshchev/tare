namespace Tare.Core.Tests;

/// <summary>
/// Stands in for the network in every adapter test. Shared by the HTTP and Crossref suites
/// so there is exactly one definition of "no request leaves the process", and it records
/// what was asked so a test can assert that nothing was sent at all.
/// </summary>
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(respond(request));
    }
}
