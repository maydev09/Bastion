using System.Net;
using Bastion.BuildingBlocks.Scraping;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Infrastructure.OtoDom;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Bastion.IntegrationTests;

public class OtoDomSourceTests
{
    private static readonly Uri OfferPage = new("https://www.otodom.pl/pl/oferta/flat-ID1");

    [TestCase(HttpStatusCode.Gone, FailureKind.Removed)]
    [TestCase(HttpStatusCode.NotFound, FailureKind.Missing)]
    [TestCase(HttpStatusCode.Forbidden, FailureKind.Failed)]
    public async Task A_detail_page_error_tells_a_removed_offer_from_a_refusal(HttpStatusCode status, FailureKind expected)
    {
        var source = CreateSource(new PortalHandler(status));

        var fetch = await source.FetchDetailPageAsync(OfferPage, CancellationToken.None);

        Assert.That(fetch.Failure?.Kind, Is.EqualTo(expected));
    }

    private static OtoDomSource CreateSource(HttpMessageHandler handler)
    {
        var userAgent = Substitute.For<IUserAgentSource>();
        userAgent.GetAsync(default).ReturnsForAnyArgs("Mozilla/5.0");
        var httpClients = Substitute.For<IHttpClientFactory>();
        httpClients.CreateClient(ScrapingClient.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));
        var client = new ScrapingClient(
            httpClients,
            Substitute.For<IVpnCheck>(),
            userAgent,
            TimeProvider.System,
            Options.Create(new ScrapingOptions { MinDelaySeconds = 0, MaxDelaySeconds = 0 }),
            NullLogger<ScrapingClient>.Instance);
        return new OtoDomSource(client, Options.Create(new OtoDomOptions()), Options.Create(new RawCaptureOptions()));
    }

    // robots.txt allows everything; every other page answers the given status.
    private sealed class PortalHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath == "/robots.txt"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("User-agent: *\nAllow: /") }
                : new HttpResponseMessage(status));
    }
}
