namespace Bastion.Host;

// The commit the running app was built from. CommitDate and PullRequest come only from a CI build;
// a local build has at most the commit.
internal sealed record AppVersion(string? Commit, DateTimeOffset? CommitDate, int? PullRequest, Uri? CommitUrl);
