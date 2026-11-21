namespace Bastion.BuildingBlocks.Scraping;

public sealed class ScrapingException : Exception
{
    public const string VpnRequired = "scraping.vpnRequired";
    public const string DisallowedByRobots = "scraping.disallowedByRobots";
    public const string RateLimited = "scraping.rateLimited";
    public const string HttpError = "scraping.httpError";
    public const string NetworkError = "scraping.networkError";
    public const string BrowserUnavailable = "scraping.browserUnavailable";

    public ScrapingException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }

    // The HTTP status of an HttpError.
    public int? StatusCode { get; init; }
}
