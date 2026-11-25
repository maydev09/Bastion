using System.Globalization;
using System.Reflection;

namespace Bastion.Host;

internal static class AppVersionEndpoint
{
    public static void MapAppVersion(this IEndpointRouteBuilder app)
    {
        var version = ReadVersion(typeof(AppVersionEndpoint).Assembly);
        app.MapGet("/api/platform/version", () => TypedResults.Ok(version))
            .WithTags("Platform")
            .WithName("GetAppVersion")
            .WithSummary("Returns the commit the running app was built from: its full hash, its date on master, the pull request "
                + "it merged and a link to it in the repository. All are null in a local build, except perhaps the commit.");
    }

    // The SDK appends SourceRevisionId to the informational version ("1.0.0+<commit>"); the rest is assembly
    // metadata from the Host project file (see the Dockerfile).
    private static AppVersion ReadVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var plus = informational.IndexOf('+');
        var commit = plus >= 0 ? informational[(plus + 1)..] : null;
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(a => a.Key, a => a.Value);
        return new AppVersion(
            commit,
            DateTimeOffset.TryParse(metadata.GetValueOrDefault("SourceRevisionDate"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : null,
            int.TryParse(metadata.GetValueOrDefault("PullRequestNumber"), NumberStyles.None, CultureInfo.InvariantCulture, out var pullRequest)
                ? pullRequest
                : null,
            commit is not null && metadata.GetValueOrDefault("RepositoryUrl") is { Length: > 0 } repository
                ? new Uri($"{repository}/commit/{commit}")
                : null);
    }
}
