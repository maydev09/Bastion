using System.ComponentModel.DataAnnotations;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.ImportOffers;
using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Application.ListPlaces;
using Bastion.Modules.Housing.Application.ReparseOffers;
using Bastion.Modules.Housing.Domain;
using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Bastion.Modules.Housing.Api;

public static class OfferEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/import/{source}", async Task<Results<Ok<ImportResult>, ProblemHttpResult>> (
            Source source, ImportOffersHandler handler, CancellationToken cancellationToken) =>
        {
            var outcome = await handler.Handle(source, ImportMode.NewOffers, cancellationToken);
            if (outcome.Failure is not { } failure)
            {
                return TypedResults.Ok(outcome.Result!);
            }

            return TypedResults.Problem(
                detail: failure.Detail,
                statusCode: failure.Kind switch
                {
                    FailureKind.NotFound => StatusCodes.Status404NotFound,
                    FailureKind.Unavailable or FailureKind.RateLimited => StatusCodes.Status503ServiceUnavailable,
                    _ => StatusCodes.Status502BadGateway,
                },
                title: "Import failed",
                extensions: new Dictionary<string, object?> { ["code"] = failure.Code });
        })
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status502BadGateway)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
        .WithName("ImportHousingOffers")
        .WithSummary("Fetches one live results page of the source, stores it as raw data, saves new offers and updates changed ones.");

        app.MapPost("/reparse", async (ReparseOffersHandler handler, CancellationToken cancellationToken) =>
            TypedResults.Ok(await handler.Handle(cancellationToken)))
        .WithName("ReparseHousingOffers")
        .WithSummary("Parses all stored raw pages again, oldest first, and saves the offers. Nothing is fetched.");

        app.MapGet("/offers", async (
            ListOffersHandler handler,
            CancellationToken cancellationToken,
            [AsParameters] OfferFilterQuery filter,
            [Range(1, int.MaxValue)] int page = 1,
            [Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize,
            OfferSort sort = OfferSort.FirstSeen,
            SortDirection direction = SortDirection.Descending) =>
            TypedResults.Ok(await handler.Handle(filter.ToFilter(), new PageRequest(page, pageSize), sort, direction, cancellationToken)))
        .WithName("ListHousingOffers")
        .WithSummary("Returns stored offers that match all given filters, by default newest first; offers without the sorted value come last. "
            + "Prices are in PLN; place and voivodeship together match offers in any of them, and so do rooms and roomsMin; "
            + "amenities match all of them unless featureMatch is Any.");

        app.MapGet("/offers/facets", async (
            ListOffersHandler handler,
            CancellationToken cancellationToken,
            [AsParameters] OfferFilterQuery filter,
            [FromQuery] DateTimeOffset[]? countAddedSince) =>
            TypedResults.Ok(await handler.CountFacets(filter.ToFilter(), countAddedSince ?? [], cancellationToken)))
        .WithName("CountHousingOfferFacets")
        .WithSummary("Returns how many offers each filter choice would show with the other given filters, "
            + "the spread of prices and areas without the price and area bounds, and the offers first seen since each countAddedSince.");

        app.MapGet("/offers/summary", async (
            ListOffersHandler handler, CancellationToken cancellationToken, [AsParameters] OfferFilterQuery filter) =>
            TypedResults.Ok(await handler.Summarize(filter.ToFilter(), cancellationToken)))
        .WithName("SummarizeHousingOffers")
        .WithSummary("Returns the typical price, price per m², rent and area of the offers that match the filters: "
            + "the median, the middle half and how many offers have the value. Money is in PLN.");

        app.MapGet("/features", () =>
            TypedResults.Ok(FeatureCategories.All.Select(f => new FeatureListItem(f, FeatureCategories.Of(f))).ToList()))
        .WithName("ListHousingFeatures")
        .WithSummary("Returns every amenity with its category, in display order: the same groups for every portal.");

        app.MapGet("/places", async (ListPlacesHandler handler, CancellationToken cancellationToken, [Required] string search) =>
            TypedResults.Ok(await handler.Search(search, cancellationToken)))
        .WithName("SearchHousingPlaces")
        .WithSummary($"Returns up to {ListPlacesHandler.MaxResults} places whose name or a word of it starts with the search, most listed offers first, also places without offers.");

        app.MapGet("/places/{id:int}", async Task<Results<Ok<PlaceListItem>, NotFound>> (
            int id, ListPlacesHandler handler, CancellationToken cancellationToken) =>
            await handler.Get(id, cancellationToken) is { } place ? TypedResults.Ok(place) : TypedResults.NotFound())
        .WithName("GetHousingPlace")
        .WithSummary("Returns one place with the number of its listed offers, also a place without offers.");

        app.MapGet("/offers/{id:int}", async Task<Results<Ok<OfferListItem>, NotFound>> (
            int id, ListOffersHandler handler, CancellationToken cancellationToken) =>
            await handler.Get(id, cancellationToken) is { } offer ? TypedResults.Ok(offer) : TypedResults.NotFound())
        .WithName("GetHousingOffer")
        .WithSummary("Returns one stored offer with its place and the nearest big cities, also a removed one.");
    }
}
