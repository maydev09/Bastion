using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bastion.BuildingBlocks.Scraping;

public sealed partial class ScrapingClient : IDisposable
{
    private static readonly TimeSpan FirstBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(15);

    private readonly HttpClient _http;
    private readonly IVpnCheck _vpnCheck;
    private readonly IUserAgentSource _userAgent;
    private readonly TimeProvider _time;
    private readonly ScrapingOptions _options;
    private readonly ILogger<ScrapingClient> _logger;
    private readonly ConcurrentDictionary<string, HostGate> _gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (RobotsRules Rules, DateTimeOffset FetchedAt)> _robots = new(StringComparer.OrdinalIgnoreCase);

    internal ScrapingClient(HttpMessageHandler handler, IVpnCheck vpnCheck, IUserAgentSource userAgent, TimeProvider time, IOptions<ScrapingOptions> options, ILogger<ScrapingClient> logger)
    {
        _vpnCheck = vpnCheck;
        _userAgent = userAgent;
        _time = time;
        _options = options.Value;
        _logger = logger;
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds) };
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pl,en;q=0.5");
    }

    public async Task<string> GetStringAsync(Uri url, CancellationToken cancellationToken)
    {
        await _vpnCheck.EnsureTunnelAsync(url.Host, cancellationToken);
        var userAgent = await _userAgent.GetAsync(cancellationToken);

        var rules = await GetRobotsRulesAsync(url, userAgent, cancellationToken);
        if (!rules.IsAllowed(url.PathAndQuery))
        {
            throw new ScrapingException(ScrapingException.DisallowedByRobots, $"robots.txt of {url.Host} does not allow {url.PathAndQuery}.");
        }

        var gate = Gate(url);
        await gate.Lock.WaitAsync(cancellationToken);
        try
        {
            using var response = await SendAsync(gate, url, userAgent, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new ScrapingException(ScrapingException.HttpError, $"{url} returned HTTP {(int)response.StatusCode}.");
            }

            LogFetched(_logger, url, (int)response.StatusCode);
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        finally
        {
            gate.Lock.Release();
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        foreach (var gate in _gates.Values)
        {
            gate.Lock.Dispose();
        }
    }

    private async Task<RobotsRules> GetRobotsRulesAsync(Uri url, string userAgent, CancellationToken cancellationToken)
    {
        if (FreshRobots(url) is { } cached)
        {
            return cached;
        }

        var gate = Gate(url);
        await gate.Lock.WaitAsync(cancellationToken);
        try
        {
            if (FreshRobots(url) is { } fetchedMeanwhile)
            {
                return fetchedMeanwhile;
            }

            using var response = await SendAsync(gate, new Uri(url, "/robots.txt"), userAgent, cancellationToken);
            var status = (int)response.StatusCode;

            // RFC 9309: 4xx = everything allowed; 5xx = nothing allowed (fail closed, not cached).
            RobotsRules rules;
            if (response.IsSuccessStatusCode)
            {
                rules = RobotsRules.Parse(await response.Content.ReadAsStringAsync(cancellationToken), userAgent);
            }
            else if (status is >= 400 and < 500)
            {
                rules = RobotsRules.AllowAll;
            }
            else
            {
                throw new ScrapingException(ScrapingException.DisallowedByRobots, $"robots.txt of {url.Host} returned HTTP {status}, so nothing is allowed.");
            }

            _robots[url.Host] = (rules, _time.GetUtcNow());
            return rules;
        }
        finally
        {
            gate.Lock.Release();
        }
    }

    private RobotsRules? FreshRobots(Uri url) =>
        _robots.TryGetValue(url.Host, out var cached) && _time.GetUtcNow() - cached.FetchedAt < TimeSpan.FromHours(_options.RobotsCacheHours)
            ? cached.Rules
            : null;

    private HostGate Gate(Uri url) => _gates.GetOrAdd(url.Host, _ => new HostGate());

    private async Task<HttpResponseMessage> SendAsync(HostGate gate, Uri url, string userAgent, CancellationToken cancellationToken)
    {
        var wait = gate.NextRequestAt - _time.GetUtcNow();
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, _time, cancellationToken);
        }

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(userAgent);
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new ScrapingException(ScrapingException.NetworkError, $"Request to {url} failed: {e.Message}", e);
        }
        finally
        {
            gate.NextRequestAt = _time.GetUtcNow() + RandomDelay();
        }

        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {
            var backoff = RetryAfter(response) ?? Backoff(gate.SlowDowns);
            gate.SlowDowns++;
            gate.NextRequestAt = _time.GetUtcNow() + backoff;
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new ScrapingException(ScrapingException.RateLimited, $"{url.Host} returned HTTP {status}; next request to it waits {backoff}.");
        }

        gate.SlowDowns = 0;
        return response;
    }

    private TimeSpan RandomDelay()
    {
        var min = _options.MinDelaySeconds;
        var max = Math.Max(min, _options.MaxDelaySeconds);
#pragma warning disable CA5394 // not security related
        return TimeSpan.FromSeconds(min + (Random.Shared.NextDouble() * (max - min)));
#pragma warning restore CA5394
    }

    private TimeSpan? RetryAfter(HttpResponseMessage response) =>
        response.Headers.RetryAfter?.Delta
        ?? (response.Headers.RetryAfter?.Date is { } date ? date - _time.GetUtcNow() : null);

    private static TimeSpan Backoff(int slowDowns) =>
        TimeSpan.FromTicks(Math.Min(MaxBackoff.Ticks, FirstBackoff.Ticks * (1L << Math.Min(slowDowns, 10))));

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetched {Url} (HTTP {Status})")]
    private static partial void LogFetched(ILogger logger, Uri url, int status);

    private sealed class HostGate
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);

        public DateTimeOffset NextRequestAt { get; set; }

        public int SlowDowns { get; set; }
    }
}
