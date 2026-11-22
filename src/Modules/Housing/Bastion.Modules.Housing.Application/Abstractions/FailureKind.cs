namespace Bastion.Modules.Housing.Application.Abstractions;

public enum FailureKind
{
    NotFound,

    // The source cannot be reached (no VPN, network, timeout); it may come back soon.
    Unavailable,

    Failed,

    // The page is not found (HTTP 404): it may be temporary for a listed offer, or the URL is wrong; not a removal.
    Missing,

    // The portal removed the offer (HTTP 410, "this offer is no longer available").
    Removed,

    // The source asked us to slow down (HTTP 429, 503); the scraping client already waits before its next request.
    RateLimited,
}
