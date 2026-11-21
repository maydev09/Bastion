namespace Bastion.Modules.Housing.Application.Abstractions;

public enum FailureKind
{
    NotFound,

    // The source cannot be reached (no VPN, network, timeout); it may come back soon.
    Unavailable,

    Failed,

    // The page no longer exists (HTTP 404, 410): the portal removed the offer.
    Gone,

    // The source asked us to slow down (HTTP 429, 503); the scraping client already waits before its next request.
    RateLimited,
}
