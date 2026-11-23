namespace Bastion.Modules.Housing.Application.ListOffers;

// A value across offers: the median, and the middle half of the offers between Low and High (the quartiles).
// Count: how many offers have the value.
public sealed record Spread(decimal Low, decimal Median, decimal High, int Count);
