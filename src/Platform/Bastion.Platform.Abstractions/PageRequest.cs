namespace Bastion.Platform.Abstractions;

public sealed record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    // A large page number times the page size does not fit in int.
    public long Offset => (Page - 1L) * PageSize;
}
