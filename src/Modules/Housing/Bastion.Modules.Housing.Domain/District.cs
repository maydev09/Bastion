namespace Bastion.Modules.Housing.Domain;

// Table housing.district: a district of a place, or a neighborhood inside a district (ParentId), named as the portals name it.
public sealed record District(int PlaceId, string Name, int? ParentId = null)
{
    public int Id { get; init; }

    public string NameKey => PlaceNames.Normalize(Name);
}
