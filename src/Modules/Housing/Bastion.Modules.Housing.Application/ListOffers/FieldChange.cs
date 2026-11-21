namespace Bastion.Modules.Housing.Application.ListOffers;

// Field is the name of the offer card's property (availableFrom); Before and After have its type,
// e.g. a number, a date, an enum or a list of features. Before is never null (a first value is not a change).
public sealed record FieldChange(string Field, object Before, object? After);
