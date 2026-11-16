namespace Bastion.Platform.Abstractions;

public sealed record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public int Offset => (Page - 1) * PageSize;

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Page < 1)
        {
            errors["page"] = ["Page must be 1 or greater."];
        }

        if (PageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        return errors;
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
