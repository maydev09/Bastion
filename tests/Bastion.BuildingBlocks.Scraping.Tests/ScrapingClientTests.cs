using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bastion.BuildingBlocks.Scraping.Tests;

public class ScrapingClientTests
{
    private const string BrowserUserAgent = "Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0";

    private static readonly Uri Page = new("https://portal.example/pl/wyniki?page=1");

    [Test]
    public async Task Refuses_without_vpn_before_any_request()
    {
        var handler = new FakeHandler();
        var client = CreateClient(handler, vpnUp: false);

        var error = (await Assert.ThrowsAsync<ScrapingException>(() => client.GetAsync(Page, CancellationToken.None)))!;

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

        var first = await client.GetAsync(Page, CancellationToken.None);
        await client.GetAsync(Page, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Encoding.UTF8.GetString(first.Content), Is.EqualTo("<html>page</html>"));
            Assert.That((first.StatusCode, first.ContentType), Is.EqualTo((200, "text/plain; charset=utf-8")));
            Assert.That(handler.UserAgents, Has.All.EqualTo(BrowserUserAgent));
            Assert.That(handler.Requests, Is.EqualTo(new[] { "/robots.txt", "/pl/wyniki?page=1", "/pl/wyniki?page=1" }));
        });
    }

    [Test]
    public async Task Refuses_a_path_disallowed_by_robots()
    {
        var handler = new FakeHandler { ["/robots.txt"] = Ok("User-agent: *\nDisallow: /pl/") };
        var client = CreateClient(handler);

        var error = (await Assert.ThrowsAsync<ScrapingException>(() => client.GetAsync(Page, CancellationToken.None)))!;

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

        var response = await CreateClient(handler).GetAsync(Page, CancellationToken.None);

        Assert.That(Encoding.UTF8.GetString(response.Content), Is.EqualTo("ok"));
    }

    [Test]
    public async Task Robots_server_error_allows_nothing()
    {
        var handler = new FakeHandler { ["/robots.txt"] = () => new HttpResponseMessage(HttpStatusCode.InternalServerError) };

        var error = (await Assert.ThrowsAsync<ScrapingException>(() => CreateClient(handler).GetAsync(Page, CancellationToken.None)))!;

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

        var error = (await Assert.ThrowsAsync<ScrapingException>(() => CreateClient(handler).GetAsync(Page, CancellationToken.None)))!;

        Assert.That(error.Code, Is.EqualTo(ScrapingException.RateLimited));
    }

    [Test]
    public async Task A_removed_page_is_reported_with_its_status()
    {
        var handler = new FakeHandler
        {
            ["/robots.txt"] = Ok("User-agent: *\nAllow: /"),
            ["/pl/wyniki?page=1"] = () => new HttpResponseMessage(HttpStatusCode.Gone),
        };

        var error = (await Assert.ThrowsAsync<ScrapingException>(() => CreateClient(handler).GetAsync(Page, CancellationToken.None)))!;

        Assert.That((error.Code, error.StatusCode), Is.EqualTo((ScrapingException.HttpError, 410)));
    }

    private static ScrapingClient CreateClient(FakeHandler handler, bool vpnUp = true)
    {
        var vpn = Substitute.For<IVpnCheck>();
        if (!vpnUp)
        {
            vpn.EnsureTunnelAsync(default!, default).ThrowsAsyncForAnyArgs(new ScrapingException(ScrapingException.VpnRequired, "no VPN"));
        }

        var userAgent = Substitute.For<IUserAgentSource>();
        userAgent.GetAsync(default).ReturnsForAnyArgs(BrowserUserAgent);

        var httpClients = Substitute.For<IHttpClientFactory>();
        httpClients.CreateClient(ScrapingClient.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));

        var options = Options.Create(new ScrapingOptions { MinDelaySeconds = 0, MaxDelaySeconds = 0 });
        return new ScrapingClient(httpClients, vpn, userAgent, TimeProvider.System, options, NullLogger<ScrapingClient>.Instance);
    }

    private static Func<HttpResponseMessage> Ok(string body) => () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class FakeHandler : HttpMessageHandler, IEnumerable<KeyValuePair<string, Func<HttpResponseMessage>>>
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = [];

        public List<string> Requests { get; } = [];

        public List<string> UserAgents { get; } = [];

        public Func<HttpResponseMessage> this[string pathAndQuery]
        {
            set => _responses[pathAndQuery] = value;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add(path);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            return Task.FromResult(_responses.TryGetValue(path, out var response) ? response() : new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        public IEnumerator<KeyValuePair<string, Func<HttpResponseMessage>>> GetEnumerator() => _responses.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
