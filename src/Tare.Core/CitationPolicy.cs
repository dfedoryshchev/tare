using System.Net;
using System.Net.Sockets;

namespace Tare.Core;

/// <summary>
/// Decides which cited URLs a checker is allowed to fetch. A document under analysis is
/// untrusted input, and fetching whatever it names turns the tool into a request forwarder
/// for whoever wrote the draft: a link to <c>169.254.169.254</c> or to a host on the
/// analyst's own network would be fetched with the analyst's reach, and the verdict would
/// report back whether it answered. So the allowed set is narrow and stated positively -
/// http or https, no credentials, a public hostname - and everything else is declined and
/// reported as <see cref="CitationStatus.Skipped"/> rather than checked.
/// <para>
/// Pure by design: it reads the URL and nothing else, which keeps it in the core and
/// testable offline. That leaves one gap worth naming - a public hostname that resolves to
/// a private address passes here, because catching it needs the resolved address, which
/// only the adapter holding the socket can see.
/// </para>
/// </summary>
public static class CitationPolicy
{
    /// <summary>Suffixes that name a private network by convention rather than by address.</summary>
    private static readonly IReadOnlyList<string> PrivateSuffixes = new[]
    {
        ".local", ".localhost", ".internal", ".intranet", ".lan", ".home.arpa",
    };

    /// <summary>
    /// Returns why the URL must not be fetched, or null when it is safe to probe. Mirrors
    /// <see cref="GroundingSignal.Detect"/>: a phrase when a rule fires, null when none does.
    /// </summary>
    public static string? Reject(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "not an absolute url";
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return $"{uri.Scheme} is not a fetchable scheme";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return "carries credentials";
        }

        // Uri.Host keeps the brackets around a literal IPv6 address; IPAddress does not want them.
        var host = uri.Host.Trim('[', ']');
        return IPAddress.TryParse(host, out var address) ? RejectAddress(address) : RejectHost(host);
    }

    private static string? RejectAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return "loopback address";
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal ? "link-local address"
                : address.IsIPv6UniqueLocal ? "private address"
                : address.Equals(IPAddress.IPv6Any) ? "unspecified address"
                : null;
        }

        var octets = address.GetAddressBytes();
        return octets[0] switch
        {
            0 => "unspecified address",
            10 => "private address",
            127 => "loopback address",
            169 when octets[1] == 254 => "link-local address",
            172 when octets[1] is >= 16 and <= 31 => "private address",
            192 when octets[1] == 168 => "private address",
            _ => null,
        };
    }

    private static string? RejectHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return "loopback address";
        }

        if (PrivateSuffixes.Any(s => host.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
        {
            return "private network name";
        }

        // A single-label host is a name only the local network can resolve, so it is the same
        // risk as a private suffix with none of the convention behind it.
        return host.Contains('.') ? null : "not a public hostname";
    }
}
