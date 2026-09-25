namespace Reconnect.Api.Common;

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Clamps user input to sane values: page ≥ 1, 1 ≤ pageSize ≤ <see cref="MaxPageSize"/>.</summary>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize) =>
        (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}
