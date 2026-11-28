using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Application.MonthlyCosts;

// The monthly cost of an offer is its price plus the admin rent, taken in this order (RentSource):
// the rent field when it is plausible; else the amount the description states, or that it is included or none; else a
// nominal field (0 or 1 zł) counts as no rent; else an estimate from similar offers nearby (RentEstimator).
// The rent field is never changed. Rates, estimates and amounts read from descriptions are in PLN.
public sealed class MonthlyCostService(IMonthlyCostRepository repository, IOptions<MonthlyCostOptions> options, TimeProvider time)
{
    // The rates from the listed offers, then the cost of every listed offer with them. A removed offer keeps the cost it had
    // while it was listed (the market of its own time, for statistics over time); one removed before it got a cost gets one
    // now. An offer listed again is saved again, and so gets a new cost.
    public async Task<MonthlyCostResult> RecalculateAllAsync(CancellationToken cancellationToken)
    {
        var samples = await ListRateSamplesAsync(cancellationToken);
        var rates = RentEstimator.CalculateRates(samples, time.GetUtcNow());
        await repository.ReplaceRatesAsync(rates, cancellationToken);

        var calculatedAt = time.GetUtcNow();
        var offers = await repository.ListInputsAsync(cancellationToken);
        var (costs, estimates) = CalculateCosts(offers, new RentEstimator(options.Value, rates, samples), calculatedAt);
        await repository.SaveAsync(offers.Select(o => o.OfferId).ToList(), calculatedAt, costs, estimates, cancellationToken);
        return new MonthlyCostResult(
            rates.Count,
            costs.Count(c => c.RentSource == RentSource.Stated),
            costs.Count(c => c.RentSource == RentSource.Nominal),
            costs.Count(c => c.RentSource == RentSource.Description),
            costs.Count(c => c.RentSource == RentSource.Included),
            costs.Count(c => c.RentSource == RentSource.Estimated),
            offers.Count - costs.Count);
    }

    // The offers whose rent the typical rents are made of: listed offers with a plausible rent field in PLN.
    private async Task<List<CostInput>> ListRateSamplesAsync(CancellationToken cancellationToken) =>
        (await repository.ListRateSamplesAsync(cancellationToken)).Where(IsRateSample).ToList();

    private (List<MonthlyCost> Costs, List<RentEstimate> Estimates) CalculateCosts(
        IReadOnlyList<CostInput> offers, RentEstimator estimator, DateTimeOffset now)
    {
        var costs = new List<MonthlyCost>();
        var estimates = new List<RentEstimate>();
        foreach (var offer in offers)
        {
            var (cost, estimate) = CalculateCost(offer, estimator, now);
            if (cost is not null)
            {
                costs.Add(cost);
            }

            if (estimate is not null)
            {
                estimates.Add(estimate);
            }
        }

        return (costs, estimates);
    }

    // Null without a price, or when the rent needs an estimate that cannot be made. An estimate as high as the price comes
    // from a wrong area (e.g. 5 158 m² for 2 200 zł), so it gives no cost either.
    private (MonthlyCost? Cost, RentEstimate? Estimate) CalculateCost(CostInput offer, RentEstimator estimator, DateTimeOffset now)
    {
        if (offer is not { Price: { } price, PriceCurrency: { } currency })
        {
            return (null, null);
        }

        var stated = offer.RentCurrency == currency ? offer.Rent : null;
        if (stated is { } rent && !IsNominal(rent) && IsPlausible(rent, price, offer.AreaM2))
        {
            return (new MonthlyCost(offer.OfferId, rent, RentSource.Stated, price + rent, currency, now), null);
        }

        if (currency != Currency.Pln)
        {
            return (null, null);
        }

        var reading = RentInDescription.Read(offer.Title, offer.Description);
        if (PickAmount(reading, price, offer.AreaM2) is { } mention)
        {
            return (new MonthlyCost(
                offer.OfferId, mention.Amount, RentSource.Description, price + mention.Amount, currency, now, mention.Quote), null);
        }

        if (reading.Included)
        {
            return (new MonthlyCost(offer.OfferId, 0, RentSource.Included, price, currency, now, reading.IncludedQuote), null);
        }

        // The description says nothing: a 0 or 1 zł is taken as no rent (maintainer's decision; about half of them really are).
        if (stated is { } nominal && IsNominal(nominal))
        {
            return (new MonthlyCost(offer.OfferId, 0, RentSource.Nominal, price, currency, now), null);
        }

        if (estimator.Calculate(offer) is not { } estimate)
        {
            return (null, null);
        }

        var rentEstimate = Math.Round(estimate.RatePerM2 * offer.AreaM2!.Value, 0, MidpointRounding.AwayFromZero);
        if (rentEstimate >= price)
        {
            return (null, null);
        }

        return (new MonthlyCost(offer.OfferId, rentEstimate, RentSource.Estimated, price + rentEstimate, currency, now), estimate);
    }

    // The one plausible amount the description states; two different ones leave the rent unknown. An amount as high as the
    // price is a total with the rent ("łącznie 2 310 zł", "czynsz najmu oraz opłata administracyjna wynoszą 2 049,28 zł").
    private RentInDescription.Mention? PickAmount(RentInDescription.Reading reading, decimal price, decimal? areaM2)
    {
        var plausible = reading.Amounts.Where(m => m.Amount < price && IsPlausible(m.Amount, price, areaM2)).ToList();
        return plausible.Select(m => m.Amount).Distinct().Count() == 1 ? plausible[0] : null;
    }

    // A nominal rent (0, 1 zł) says nothing about the rent per m², so it is no sample.
    private bool IsRateSample(CostInput offer) =>
        offer is { Rent: { } rent, RentCurrency: Currency.Pln, AreaM2: > 0 }
        && !IsNominal(rent)
        && IsPlausible(rent, offer.Price, offer.AreaM2);

    private bool IsNominal(decimal rent) => rent < options.Value.NominalRentBelow;

    // Without an area only a rent typed twice (equal to the price) shows.
    private bool IsPlausible(decimal rent, decimal? price, decimal? areaM2)
    {
        if (rent == price)
        {
            return false;
        }

        if (areaM2 is not > 0)
        {
            return true;
        }

        var perM2 = rent / areaM2.Value;
        return perM2 >= options.Value.MinRentPerM2 && perM2 <= options.Value.MaxRentPerM2;
    }
}
