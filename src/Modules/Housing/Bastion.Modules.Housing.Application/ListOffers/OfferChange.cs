namespace Bastion.Modules.Housing.Application.ListOffers;

// One row of housing.offer_change: the changes seen on one page.
public sealed record OfferChange(int Id, DateTimeOffset ChangedAt, IReadOnlyList<FieldChange> Changes);
