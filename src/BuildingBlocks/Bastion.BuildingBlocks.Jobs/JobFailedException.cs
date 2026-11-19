namespace Bastion.BuildingBlocks.Jobs;

// An expected failure; the code is shown to the user through the UI translations.
public sealed class JobFailedException(string code, string message) : Exception(message)
{
    public const string Unexpected = "jobs.unexpected";

    public string Code { get; } = code;
}
