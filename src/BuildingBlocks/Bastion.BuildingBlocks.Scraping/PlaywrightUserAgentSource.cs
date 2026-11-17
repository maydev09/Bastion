using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Bastion.BuildingBlocks.Scraping;

internal sealed partial class PlaywrightUserAgentSource(ILogger<PlaywrightUserAgentSource> logger) : IUserAgentSource, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _userAgent;

    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        if (_userAgent is not null)
        {
            return _userAgent;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_userAgent is null)
            {
                _userAgent = await ReadFromFirefoxAsync();
                LogUserAgent(logger, _userAgent);
            }

            return _userAgent;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    [LoggerMessage(Level = LogLevel.Information, Message = "User-Agent read from Playwright Firefox: {UserAgent}")]
    private static partial void LogUserAgent(ILogger logger, string userAgent);

    // Headless Firefox reports the same User-Agent as a regular Firefox (headless Chromium adds "HeadlessChrome").
    private static async Task<string> ReadFromFirefoxAsync()
    {
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Firefox.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();
            return await page.EvaluateAsync<string>("navigator.userAgent");
        }
        catch (PlaywrightException e)
        {
            throw new ScrapingException(
                ScrapingException.BrowserUnavailable,
                $"Cannot start Playwright Firefox to read its User-Agent ({e.Message}). Install it: see README, Running locally.",
                e);
        }
    }
}
