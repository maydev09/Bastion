namespace Bastion.Modules.Housing.Application.Abstractions;

// An offer whose detail page is due: never fetched, its listing content changed, or the last attempts failed.
public sealed record PendingDetails(int OfferId, string ExternalId, Uri Url, string ContentHash);
