using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bastion.BuildingBlocks.Scraping.Tests;

public class ScrapingClientTests
{
    private static readonly Uri Page = new("https://portal.example/pl/wyniki?page=1");

    [Test]
    public async Task Refuses_without_vpn_before_any_request()
    {
        var handler = new FakeHandler();
        var client = CreateClient(handler, vpnUp: false);

        var error = (await Assert.ThrowsAsync<ScrapingException>(() => client.GetStringAsync(Page, CancellationToken.None)))!;

        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(ScrapingException.VpnRequired));
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task Fetches_robots_once_then_the_page()
    {
        var handler = new FakeHandler
        {
            ["/robots.txt"] = Ok("User-agent: *\nAllow: /"),
            ["/pl/wyniki?page=1"] = Ok("<html>page</html>"),
        };
        var client = CreateClient(handler);

        var first = await client.GetStringAsync(Page, CancellationToken.None);
        await client.GetStringAsync(Page, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("<html>page</html>"));
            Assert.That(handler.Requests, Is.EqualTo(new[] { "/robots.txt", "/pl/wyniki?page=1", "/pl/wyniki?page=1" }));
        });
    }

    [Test]
    public async Task Refuses_a_path_disallowed_by_robots()
    {
        var handler = new FakeHandler { ["/robots.txt"] = Ok("User-agent: *\nDisallow: /pl/") };
        var client = CreateClient(handler);

        var error = (await Assert.ThrowsAsync<ScrapingException>(() => client.GetStringAsync(Page, CancellationToken.None)))!;

        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(ScrapingException.DisallowedByRobots));
            Assert.That(handler.Requests, Is.EqualTo(new[] { "/robots.txt" }));
        });
    }

    [Test]
    public async Task Missing_robots_allows_everything()
    {
        var handler = new FakeHandler
        {
            ["/robots.txt"] = () => new HttpResponseMessage(HttpStatusCode.NotFound),
            ["/pl/wyniki?page=1"] = Ok("ok"),
        };

        Assert.That(await CreateClient(handler).GetStringAsync(Page, CancellationToken.None), Is.EqualTo("ok"));
    }

    [Test]
    public async Task Robots_server_error_allows_nothing()
    {
        var handler = new FakeHandler { ["/robots.txt"] = () => new HttpResponseMessage(HttpStatusCode.InternalServerError) };

        var error = (await Assert.ThrowsAsync<ScrapingException>(() => CreateClient(handler).GetStringAsync(Page, CancellationToken.None)))!;

        Assert.That(error.Code, Is.EqualTo(ScrapingException.DisallowedByRobots));
    }

    [Test]
    public async Task Too_many_requests_is_reported_as_rate_limited()
    {
        var handler = new FakeHandler
        {
            ["/robots.txt"] = Ok("User-agent: *\nAllow: /"),
            ["/pl/wyniki?page=1"] = () => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
        };

        var error = (await Assert.ThrowsAsync<ScrapingException>(() => CreateClient(handler).GetStringAsync(Page, CancellationToken.None)))!;

        Assert.That(error.Code, Is.EqualTo(ScrapingException.RateLimited));
    }

    private static ScrapingClient CreateClient(FakeHandler handler, bool vpnUp = true)
    {
        var options = new ScrapingOptions { MinDelaySeconds = 0, MaxDelaySeconds = 0, UserAgent = "Mozilla/5.0" };
        return new ScrapingClient(handler, new FakeVpnCheck(vpnUp), TimeProvider.System, Options.Create(options), NullLogger<ScrapingClient>.Instance);
    }

    private static Func<HttpResponseMessage> Ok(string body) => () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class FakeVpnCheck(bool up) : IVpnCheck
    {
        public Task EnsureTunnelAsync(string host, CancellationToken cancellationToken) =>
            up ? Task.CompletedTask : throw new ScrapingException(ScrapingException.VpnRequired, "no VPN");
    }

    private sealed class FakeHandler : HttpMessageHandler, IEnumerable<KeyValuePair<string, Func<HttpResponseMessage>>>
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = [];

        public List<string> Requests { get; } = [];

        public Func<HttpResponseMessage> this[string pathAndQuery]
        {
            set => _responses[pathAndQuery] = value;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add(path);
            return Task.FromResult(_responses.TryGetValue(path, out var response) ? response() : new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        public IEnumerator<KeyValuePair<string, Func<HttpResponseMessage>>> GetEnumerator() => _responses.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
