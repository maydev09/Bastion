using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.Places;

// Changes apply after the next ImportPlaces run, which computes the nearby places.
public sealed class PlaceOptions
{
    public const string Section = "Places";

    // A town this big is a big city.
    [Range(1, int.MaxValue)]
    public int BigCityMinPopulation { get; set; } = 100_000;

    // Big cities around a place: at most this many, at most this far.
    [Range(1, 10)]
    public int BigCityCount { get; set; } = 3;

    [Range(1, 1000)]
    public int BigCityMaxKm { get; set; } = 50;
}
