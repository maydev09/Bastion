namespace Bastion.Modules.Housing.Application.LinkPlaces;

// Offers with a location key, counted by whether a place matched it.
public sealed record LinkPlacesResult(int LinkedOfferCount, int UnlinkedOfferCount);
