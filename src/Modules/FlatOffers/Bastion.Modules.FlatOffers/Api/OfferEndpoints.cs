using Bastion.BuildingBlocks.Scraping;
using Bastion.Modules.FlatOffers.Application.Abstractions;
using Bastion.Modules.FlatOffers.Application.ImportOffers;
using Bastion.Modules.FlatOffers.Application.ListOffers;
using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Bastion.Modules.FlatOffers.Api;

internal static class OfferEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/import", async Task<Results<Ok<ImportResult>, ProblemHttpResult>> (ImportOffersHandler handler, CancellationToken cancellationToken) =>
        {
            var outcome = await handler.Handle(cancellationToken);
            if (outcome.Result is not null)
            {
                return TypedResults.Ok(outcome.Result);
            }

            return TypedResults.Problem(
                detail: outcome.ErrorDetail,
                statusCode: outcome.ErrorCode == ScrapingException.VpnRequired
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status502BadGateway,
                title: "Import failed",
                extensions: new Dictionary<string, object?> { ["code"] = outcome.ErrorCode });
        })
        .ProducesProblem(StatusCodes.Status502BadGateway)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
        .WithName("ImportFlatOffers")
        .WithSummary("Fetches one live OtoDom results page, saves new offers and updates changed ones.");

        app.MapGet("/offers", async Task<Results<Ok<PagedResult<OfferListItem>>, ValidationProblem>> (
            IOfferStore store, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
        {
            var request = new PageRequest(page, pageSize);
            var errors = request.Validate();
            return errors.Count > 0
                ? TypedResults.ValidationProblem(errors)
                : TypedResults.Ok(await store.ListAsync(request, cancellationToken));
        })
        .WithName("ListFlatOffers")
        .WithSummary("Returns stored offers, newest first.");
    }
}
