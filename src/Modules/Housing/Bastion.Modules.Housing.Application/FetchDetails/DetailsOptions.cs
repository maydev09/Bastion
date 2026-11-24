using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.FetchDetails;

public sealed class DetailsOptions
{
    public const string Section = "Details";

    // A run works through the queue until it is empty or this time is up; the rest waits for the next run.
    [Range(1, 1_440)]
    public int MaxMinutes { get; set; } = 55;

    // After this many failed attempts an offer waits until its content changes or RetryAfterHours pass.
    [Range(1, 10)]
    public int MaxFailedAttempts { get; set; } = 3;

    // An offer that used up its attempts is tried once more each time this long has passed:
    // a portal may answer 404 or 410 only for a while to an offer that it still lists.
    [Range(1, 720)]
    public int RetryAfterHours { get; set; } = 24;

    // Retries of a request when the portal is out of reach (not when it rate limits us); the waits are 1, 2, 4 … times FirstRetrySeconds.
    [Range(0, 10)]
    public int Retries { get; set; } = 4;

    [Range(1, 3_600)]
    public int FirstRetrySeconds { get; set; } = 30;
}
