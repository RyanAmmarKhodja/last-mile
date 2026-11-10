using LastMile.Profile.DTOs;
using LastMile.Profile.Enums;

namespace LastMile.Profile.Services;

/// <summary>Paging and search helpers shared by the profile services.</summary>
internal static class ProfileQueries
{
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

    /// <summary>ILIKE pattern for a free-text term, with LIKE wildcards in the term escaped.</summary>
    public static string? SearchPattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        var escaped = search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
        return $"%{escaped}%";
    }

    public static string StatusOrDefault(ProfileStatus? status) => (status ?? ProfileStatus.ACTIVE).ToString();

    public static PagedResultDto<T> Page<T>(IReadOnlyList<T> items, int page, int pageSize, int total) =>
        new(items, page, pageSize, total);
}
