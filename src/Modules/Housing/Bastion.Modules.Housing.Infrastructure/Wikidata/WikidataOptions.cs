using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Infrastructure.Wikidata;

public sealed class WikidataOptions
{
    public const string Section = "Wikidata";

    [Required]
    public Uri QueryUrl { get; set; } = new("https://query.wikidata.org/sparql");

    // Wikidata refuses requests without a User-Agent naming the client.
    [Required]
    public string UserAgent { get; set; } = "Bastion/1.0";

    // The query service stops a query after 60 seconds; the response itself can take longer.
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 180;
}
