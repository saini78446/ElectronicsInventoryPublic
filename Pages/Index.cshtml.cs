using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ElectronicsInventory.Data;
using ElectronicsInventory.Models;
namespace ElectronicsInventory.Pages;

public class IndexModel : PageModel
{
    private readonly InventoryContext _db;
    public IndexModel(InventoryContext db)
    {
        _db = db;
    }
    public List<Item> Items { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }
    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? Tag { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    private static readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };

    [BindProperty(SupportsGet = true)]
    public int PageNo { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 50;

    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public int TotalQty { get; set; }
    public double TotalValue { get; set; }
    [TempData]
    public string? FlashMessage { get; set; }
    [TempData]
    public string? FlashType { get; set; }
    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        // Export ignores paging - it should include the full filtered set.
        await LoadAsync(exportAll: true);

        var sb = new StringBuilder();
        sb.AppendLine("Id,Name,Category,Seller,Tags,Quantity,OriginalPrice,CurrentPrice,Notes,CreatedAt,UpdatedAt,Condition,Location");

        foreach (var item in Items)
        {
            var catName = item.Category?.Name ?? "Uncategorized";
            var sellerName = item.Seller?.Name ?? "";
            var tags = string.Join(" ", item.ItemTags.Select(it => it.Tag!.Name));

            var LocationName = item.Location?.Name ?? "";
            var ConditionName = item.Condition?.Name ?? "";

            sb.AppendLine(string.Join(",",
                item.Id,
                CsvEscape(item.Name),
                CsvEscape(catName),
                CsvEscape(sellerName),
                CsvEscape(tags),
                item.Quantity,
                item.OriginalPrice.ToString("F2"),
                item.CurrentPrice.ToString("F2"),
                CsvEscape(item.Notes ?? ""),
                item.CreatedAt,
                item.UpdatedAt,
                CsvEscape(ConditionName),
                CsvEscape(LocationName)));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"inventory_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    private async Task LoadAsync(bool exportAll = false)
    {
        // Single grouped query instead of one COUNT(*) per category (fixes N+1).
        Categories = await _db.Categories.Include(c => c.Children).OrderBy(c => c.Name).ToListAsync();
        var countsByCategory = await _db.Items
            .Where(i => i.CategoryId != null)
            .GroupBy(i => i.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CategoryId!.Value, g => g.Count);

        foreach (var cat in Categories)
        {
            cat.TotalRecords = countsByCategory.TryGetValue(cat.Id, out var c) ? c : 0;
        }

        var query = _db.Items
            .Include(i => i.Category)
            .Include(i => i.Seller)
            .Include(i => i.ItemTags).ThenInclude(it => it.Tag)
            .Include(i => i.Condition)
            .Include(i => i.Location)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            Q = Q.Trim();
            query = query.Where(i =>
                EF.Functions.Like(
                    EF.Functions.Collate(i.Name, "NOCASE"),
                    $"%{Q}%"));
        }
        if (CategoryId.HasValue)
        {
            // Selecting a category also shows items in its sub-categories,
            // so picking "Computer Accessories" surfaces "Cables" items too.
            var childIds = await _db.Categories
                .Where(c => c.ParentCategoryId == CategoryId.Value)
                .Select(c => c.Id)
                .ToListAsync();
            var idsToMatch = new List<int> { CategoryId.Value };
            idsToMatch.AddRange(childIds);

            query = query.Where(i => i.CategoryId.HasValue && idsToMatch.Contains(i.CategoryId.Value));
        }
        if (!string.IsNullOrWhiteSpace(Tag))
        {
            var tagLower = Tag.Trim().ToLowerInvariant();
            query = query.Where(i => i.ItemTags.Any(it => it.Tag!.Name == tagLower));
        }

        query = Sort switch
        {
            "name_asc" => query.OrderBy(i => i.Name),
            "name_desc" => query.OrderByDescending(i => i.Name),
            "qty_asc" => query.OrderBy(i => i.Quantity),
            "qty_desc" => query.OrderByDescending(i => i.Quantity),
            "price_asc" => query.OrderBy(i => i.CurrentPrice),
            "price_desc" => query.OrderByDescending(i => i.CurrentPrice),
            "created_asc" => query.OrderBy(i => i.CreatedAt),
            "created_desc" => query.OrderByDescending(i => i.CreatedAt),
            "updated_asc" => query.OrderBy(i => i.UpdatedAt),
            "updated_desc" => query.OrderByDescending(i => i.UpdatedAt),
            _ => query.OrderByDescending(i => i.Id)
        };

        // Stats reflect the full filtered set, not just the current page.
        TotalItems = await query.CountAsync();
        TotalQty = await query.SumAsync(i => i.Quantity);
        TotalValue = await query.SumAsync(i => i.CurrentPrice * i.Quantity);

        if (exportAll)
        {
            Items = await query.ToListAsync();
            return;
        }

        if (!AllowedPageSizes.Contains(PageSize))
        {
            PageSize = 50;
        }

        TotalPages = TotalItems == 0 ? 1 : (int)Math.Ceiling(TotalItems / (double)PageSize);
        if (PageNo < 1) PageNo = 1;
        if (PageNo > TotalPages) PageNo = TotalPages;

        Console.WriteLine($"[PAGING DEBUG] RequestedPage={Request.Query["Page"]} BoundPage={PageNo} ...");

        Items = await query
            .Skip((PageNo - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}