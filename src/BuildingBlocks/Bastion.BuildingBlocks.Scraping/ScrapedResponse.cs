namespace Bastion.BuildingBlocks.Scraping;

public sealed record ScrapedResponse(Uri Url, int StatusCode, string ContentType, byte[] Content);
