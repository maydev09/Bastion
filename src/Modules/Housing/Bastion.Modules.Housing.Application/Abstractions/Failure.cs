namespace Bastion.Modules.Housing.Application.Abstractions;

public sealed record Failure(string Code, string Detail, FailureKind Kind);
