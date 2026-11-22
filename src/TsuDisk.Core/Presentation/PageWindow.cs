namespace TsuDisk.Core.Presentation;

/// <summary>
/// Cua so phan trang cua mot vung ban ghi theo DATA_TABLE: moc 10/20/50/100/200, mac dinh 10, doi moc giu ban ghi dau
/// dang thay, toi da 7 o so. Bat bien - moi thay doi tra ve cua so moi.
/// </summary>
public sealed record PageWindow
{
    public static readonly IReadOnlyList<int> Sizes = [10, 20, 50, 100, 200];
    public const int DefaultSize = 10;

    public PageWindow(int total, int pageSize = DefaultSize, int page = 1)
    {
        Total = Math.Max(0, total);
        PageSize = Sanitize(pageSize);
        Page = Math.Clamp(page, 1, PageCount);
    }

    public int Total { get; }
    public int PageSize { get; }
    public int Page { get; }

    public int PageCount => Math.Max(1, (Total + PageSize - 1) / PageSize);

    /// <summary>Chi so (tu 0) cua ban ghi dau tien tren trang.</summary>
    public int FirstIndex => (Page - 1) * PageSize;

    public int CountOnPage => Total == 0 ? 0 : Math.Min(PageSize, Total - FirstIndex);

    public bool IsSinglePage => Total <= PageSize;
    public bool CanGoPrevious => Page > 1;
    public bool CanGoNext => Page < PageCount;

    /// <summary>Gia tri doc tu cau hinh khong nam trong bo moc chuan thi quay ve 10 (DATA_TABLE muc 5).</summary>
    public static int Sanitize(int? size) => size is { } s && Sizes.Contains(s) ? s : DefaultSize;

    /// <summary>DATA_TABLE muc 4: <c>trang_moi = floor(chi_so_ban_ghi_dau / moc_moi) + 1</c>.</summary>
    public PageWindow WithPageSize(int size)
    {
        var sanitized = Sanitize(size);
        return new PageWindow(Total, sanitized, FirstIndex / sanitized + 1);
    }

    public PageWindow WithPage(int page) => new(Total, PageSize, page);

    /// <summary>Tong so thay doi (loc lai, doc lai dia): giu moc, giu trang neu con hop le.</summary>
    public PageWindow WithTotal(int total) => new(total, PageSize, Page);

    public IEnumerable<T> Slice<T>(IEnumerable<T> items) => items.Skip(FirstIndex).Take(PageSize);

    /// <summary>
    /// Cac o phan trang, toi da 7: <c>1 ... 5 6 7 ... 13</c>; null la dau ba cham. It hon 8 trang thi hien du.
    /// </summary>
    public IReadOnlyList<int?> PageCells()
    {
        var count = PageCount;
        if (count <= 7)
            return [.. Enumerable.Range(1, count).Select(p => (int?)p)];
        if (Page <= 4)
            return [1, 2, 3, 4, 5, null, count];
        if (Page >= count - 3)
            return [1, null, count - 4, count - 3, count - 2, count - 1, count];
        return [1, null, Page - 1, Page, Page + 1, null, count];
    }
}
