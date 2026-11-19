using System.ComponentModel.DataAnnotations;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.ImportOffers;
using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Application.ReparseOffers;
using Bastion.Modules.Housing.Domain;
using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
                    FailureKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
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
            IOfferRepository repository,
            CancellationToken cancellationToken,
            [Range(1, int.MaxValue)] int page = 1,
            [Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize) =>
            TypedResults.Ok(await repository.ListAsync(new PageRequest(page, pageSize), cancellationToken)))
        .WithName("ListHousingOffers")
        .WithSummary("Returns stored offers, newest first.");
    }
}
