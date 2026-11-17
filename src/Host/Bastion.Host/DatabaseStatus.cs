namespace Bastion.Host;

internal sealed record DatabaseStatus(string User, string ServerVersion, bool CanChangeSchema, IReadOnlyList<SchemaMigration> Migrations);
