namespace Bastion.Modules.Housing.Application.ListOffers;

// How many offers fall into each bucket of a value: Counts[i] are the offers from From + i * Width up to the next bucket.
// The scale covers the usual values; the first bucket also holds the unusually low offers and the last one the
// unusually high ones, so a few extreme values do not stretch the scale.
public sealed record Histogram(decimal From, decimal Width, IReadOnlyList<int> Counts);
