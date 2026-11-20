using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.FetchDetails;

public sealed class DetailsOptions
{
    public const string Section = "Details";

    // A run works through the queue until it is empty or this time is up; the rest waits for the next run.
    [Range(1, 1_440)]
    public int MaxMinutes { get; set; } = 50;

    // After this many failed attempts an offer waits until its content changes.
    [Range(1, 10)]
    public int MaxFailedAttempts { get; set; } = 3;
}
