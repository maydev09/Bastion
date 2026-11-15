using Bastion.BuildingBlocks.Scraping;
using Bastion.Modules.FlatOffers.Application.RunSmokeTest;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Bastion.Modules.FlatOffers.Api;

internal static class RunSmokeTestEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/smoke-test", async Task<Results<Ok<SmokeTestResult>, ProblemHttpResult>> (RunSmokeTestHandler handler, CancellationToken cancellationToken) =>
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
                title: "Smoke test failed",
                extensions: new Dictionary<string, object?> { ["code"] = outcome.ErrorCode });
        })
        .WithName("RunFlatOffersSmokeTest")
        .WithSummary("Fetches one live OtoDom results page and returns the number of parsed offers. Stores nothing.");
}
