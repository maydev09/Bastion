namespace Bastion.Modules.Housing.Application.Abstractions;

public enum FailureKind
{
    NotFound,

    // The source cannot be reached (no VPN, network, timeout); it may come back soon.
    Unavailable,

    Failed,

    // The source asked us to slow down (HTTP 429, 503); the scraping client already waits before its next request.
    RateLimited,
}
