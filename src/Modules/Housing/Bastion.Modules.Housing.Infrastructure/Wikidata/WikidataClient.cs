using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Bastion.Modules.Housing.Infrastructure.Wikidata;

// Runs SPARQL queries on the Wikidata query service; each result row maps a variable to its value.
public sealed partial class WikidataClient(HttpClient http, ILogger<WikidataClient> logger)
{
    private const string EntityPrefix = "http://www.wikidata.org/entity/";
    private const int Attempts = 3;

    // The same query takes 10 or 50 seconds depending on the load, and the service cuts it off at 60 seconds,
    // so a failed query is tried again. A retry changes the query text: the service may have cached the cut-off response.
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryAsync(string sparql, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await QueryOnceAsync(attempt == 1 ? sparql : $"{sparql}\n# retry {attempt}", cancellationToken);
            }
            catch (Exception e) when (attempt < Attempts && IsTransient(e, cancellationToken))
            {
                LogRetry(attempt, Attempts, e.Message);
                await Task.Delay(TimeSpan.FromSeconds(10 * attempt), cancellationToken);
            }
        }
    }

    public static bool IsTransient(Exception e, CancellationToken cancellationToken) =>
        e is HttpRequestException or JsonException or InvalidDataException
        || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> QueryOnceAsync(string sparql, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, (Uri?)null)
        {
            Content = new FormUrlEncodedContent([new("query", sparql)]),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        // A response cut off by a timeout is invalid JSON and fails here instead of giving partial data.
        var result = await response.Content.ReadFromJsonAsync<SparqlResult>(cancellationToken)
            ?? throw new InvalidDataException("The Wikidata response is empty.");
        return result.Results.Bindings
            .Select(row => (IReadOnlyDictionary<string, string>)row.ToDictionary(v => v.Key, v => EntityId(v.Value.Value)))
            .ToList();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Wikidata query failed (attempt {Attempt} of {Attempts}): {Error}")]
    private partial void LogRetry(int attempt, int attempts, string error);

    // Entities come as URIs, e.g. http://www.wikidata.org/entity/Q270 -> Q270.
    private static string EntityId(string value) =>
        value.StartsWith(EntityPrefix, StringComparison.Ordinal) ? value[EntityPrefix.Length..] : value;

    private sealed record SparqlResult([property: JsonPropertyName("results")] SparqlRows Results);

    private sealed record SparqlRows([property: JsonPropertyName("bindings")] IReadOnlyList<Dictionary<string, SparqlValue>> Bindings);

    private sealed record SparqlValue([property: JsonPropertyName("value")] string Value);
}
