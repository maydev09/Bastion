namespace Bastion.Modules.Housing.Domain;

public static class FeatureCategories
{
    // Every amenity in display order: by category, then the most wanted first.
    public static IReadOnlyList<Feature> All { get; } =
    [
        Feature.Balcony, Feature.Terrace, Feature.Garden,
        Feature.Lift, Feature.Parking, Feature.Basement, Feature.UsableRoom,
        Feature.Stove, Feature.Fridge, Feature.Oven, Feature.Dishwasher, Feature.WashingMachine,
        Feature.SeparateKitchen, Feature.AirConditioning, Feature.TwoStorey,
        Feature.Internet, Feature.CableTelevision,
        Feature.Entryphone, Feature.Monitoring, Feature.ClosedArea, Feature.AntiBurglaryDoor,
        Feature.NonSmokersOnly,
    ];

    public static FeatureCategory Of(Feature feature) => feature switch
    {
        Feature.Balcony or Feature.Terrace or Feature.Garden => FeatureCategory.Outdoor,
        Feature.Lift or Feature.Parking or Feature.Basement or Feature.UsableRoom => FeatureCategory.Building,
        Feature.Stove or Feature.Fridge or Feature.Oven or Feature.Dishwasher or Feature.WashingMachine => FeatureCategory.Appliances,
        Feature.SeparateKitchen or Feature.AirConditioning or Feature.TwoStorey => FeatureCategory.Comfort,
        Feature.Internet or Feature.CableTelevision => FeatureCategory.Media,
        Feature.Entryphone or Feature.Monitoring or Feature.ClosedArea or Feature.AntiBurglaryDoor => FeatureCategory.Security,
        Feature.NonSmokersOnly => FeatureCategory.Rules,
        _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, null),
    };
}
