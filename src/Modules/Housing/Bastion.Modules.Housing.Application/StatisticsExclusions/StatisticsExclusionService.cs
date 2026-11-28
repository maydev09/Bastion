using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Application.StatisticsExclusions;

// Finds the listed offers the statistics must leave out. The offers stay; only the exclusion says why. Rules from the stored
// offers (2026-11-27, 28 110 listed): the numbers alone show typos and prices that are no monthly rent; the price per m²
// outside the fences of its group (PriceFences) finds the rest, and the text then tells why (workers' quarters, an
// auction). Words alone are no reason: most offers that name a parking space or companies are ordinary flats.
public sealed class StatisticsExclusionService(
    IStatisticsExclusionRepository repository, IOptions<StatisticsExclusionOptions> options, IOptions<PlaceSizeOptions> placeSizes, TimeProvider time)
{
    // The fences from the listed offers, then every listed offer compared with them. A removed offer keeps its last
    // exclusion (the market of its own time).
    public async Task<StatisticsExclusionResult> RecalculateAllAsync(CancellationToken cancellationToken)
    {
        // Taken before the offers are read, so an exclusion saved meanwhile (it reads later) is not overwritten.
        var calculatedAt = time.GetUtcNow();
        var offers = await repository.ListInputsAsync(cancellationToken);
        await repository.ReplaceFencesAsync(PriceFences.Calculate(offers, options.Value, placeSizes.Value, calculatedAt), cancellationToken);
        var fences = await repository.ListFencesAsync(cancellationToken);
        var exclusions = FindExclusions(offers, new PriceFences(fences, placeSizes.Value), calculatedAt);
        await repository.SaveAsync(offers.Select(o => o.OfferId).ToList(), calculatedAt, exclusions, cancellationToken);
        return new StatisticsExclusionResult(offers.Count, fences.Count, exclusions.CountBy(e => e.Reason).ToDictionary());
    }

    private List<StatisticsExclusion> FindExclusions(IReadOnlyList<ExclusionInput> offers, PriceFences fences, DateTimeOffset now)
    {
        var exclusions = new List<StatisticsExclusion>();
        foreach (var offer in offers)
        {
            var fence = fences.Find(offer);
            if (FindReason(offer, fence) is { } reason)
            {
                exclusions.Add(new StatisticsExclusion(offer.OfferId, reason, fence?.Id, now));
            }
        }

        return exclusions;
    }

    // The first rule that matches. A room is told by its title alone; the other rules need a price in PLN and an area.
    private StatisticsExclusionReason? FindReason(ExclusionInput offer, PriceFence? fence)
    {
        if (OfferText.IsRoom(offer.Title))
        {
            return StatisticsExclusionReason.Room;
        }

        if (offer is not { Price: { } price, PriceCurrency: Currency.Pln, AreaM2: { } area } || area <= 0)
        {
            return null;
        }

        var limits = options.Value;
        var perM2 = price / area;
        if (area < limits.MinAreaM2 || area == price)
        {
            return StatisticsExclusionReason.ImplausibleArea;
        }

        if (price < limits.MinPrice || perM2 > limits.MaxPricePerM2)
        {
            return StatisticsExclusionReason.ImplausiblePrice;
        }

        if (OfferText.IsParking(offer.Title) && (area < limits.ParkingMaxAreaM2 || price < limits.ParkingMaxPrice))
        {
            return StatisticsExclusionReason.Parking;
        }

        if (fence is null || (perM2 >= fence.LowPerM2 && perM2 <= fence.HighPerM2))
        {
            return null;
        }

        if (OfferText.IsAuction(offer.Description))
        {
            return StatisticsExclusionReason.Auction;
        }

        if (OfferText.IsPerPerson(offer.Title, offer.Description))
        {
            return StatisticsExclusionReason.PerPerson;
        }

        if (OfferText.IsRentForRenovation(offer.Title, offer.Description))
        {
            return StatisticsExclusionReason.RentForRenovation;
        }

        if (perM2 < fence.LowPerM2 && area > limits.LargeAreaM2)
        {
            return StatisticsExclusionReason.ImplausibleArea;
        }

        return StatisticsExclusionReason.UnusualPrice;
    }
}
