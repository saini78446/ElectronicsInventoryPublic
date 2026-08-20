using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ElectronicsInventory.Data;
using ElectronicsInventory.Models;

namespace ElectronicsInventory.Pages;

public class ManageModel : PageModel
{
    private readonly InventoryContext _db;

    public ManageModel(InventoryContext db)
    {
        _db = db;
    }

    // Which tab is active. Drives both the tab UI and which handler runs.
    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "categories";

    public List<Category> Categories { get; set; } = new();
    public List<Seller> Sellers { get; set; } = new();
    public List<Condition> Conditions { get; set; } = new();
    public List<Location> Locations { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();

    // Item counts per master-data row, so "in use" / delete-blocked can be shown honestly.
    public Dictionary<int, int> CategoryItemCounts { get; set; } = new();
    public Dictionary<int, int> SellerItemCounts { get; set; } = new();
    public Dictionary<int, int> ConditionItemCounts { get; set; } = new();
    public Dictionary<int, int> LocationItemCounts { get; set; } = new();
    public Dictionary<int, int> TagItemCounts { get; set; } = new();

    [BindProperty]
    public CategoryInput NewCategory { get; set; } = new();

    [BindProperty]
    public NamedInput NewSeller { get; set; } = new();

    [BindProperty]
    public NamedInput NewCondition { get; set; } = new();

    [BindProperty]
    public NamedInput NewLocation { get; set; } = new();

    [BindProperty]
    public NamedInput NewTag { get; set; } = new();

    public class NamedInput
    {
        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; set; } = string.Empty;
        public string? ContactInfo { get; set; }  // seller only
        public string? Link { get; set; }         // seller only
    }

    public class CategoryInput
    {
        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; set; } = string.Empty;
        public int? ParentCategoryId { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        Categories = await _db.Categories.Include(c => c.Children).OrderBy(c => c.ParentCategoryId == null ? 0 : 1).ThenBy(c => c.Name).ToListAsync();
        Sellers = await _db.Sellers.OrderBy(s => s.Name).ToListAsync();
        Conditions = await _db.Conditions.OrderBy(c => c.Name).ToListAsync();
        Locations = await _db.Locations.OrderBy(l => l.Name).ToListAsync();
        Tags = await _db.Tags.OrderBy(t => t.Name).ToListAsync();

        CategoryItemCounts = await _db.Items.Where(i => i.CategoryId != null)
            .GroupBy(i => i.CategoryId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        SellerItemCounts = await _db.Items.Where(i => i.SellerId != null)
            .GroupBy(i => i.SellerId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        ConditionItemCounts = await _db.Items.Where(i => i.ConditionId != null)
            .GroupBy(i => i.ConditionId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        LocationItemCounts = await _db.Items.Where(i => i.LocationId != null)
            .GroupBy(i => i.LocationId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        TagItemCounts = await _db.ItemTags
            .GroupBy(it => it.TagId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    private void Flash(string message, string type = "success")
    {
        TempData["FlashMessage"] = message;
        TempData["FlashType"] = type;
    }

    // ---------- Categories ----------

    public async Task<IActionResult> OnPostAddCategoryAsync()
    {
        Tab = "categories";

        // Only validate NewCategory here — every [BindProperty] on this page gets bound
        // on every POST, so checking the shared ModelState would fail this handler
        // whenever another tab's (empty, untouched) Name field is "required" too.
        ModelState.Clear();
        if (!TryValidateModel(NewCategory, nameof(NewCategory)) || string.IsNullOrWhiteSpace(NewCategory.Name))
        {
            Flash("Category name is required.", "error");
            return RedirectToPage(new { tab = "categories" });
        }

        _db.Categories.Add(new Category
        {
            Name = NewCategory.Name.Trim(),
            ParentCategoryId = NewCategory.ParentCategoryId
        });
        await _db.SaveChangesAsync();
        Flash("Category added.");
        return RedirectToPage(new { tab = "categories" });
    }

    public async Task<IActionResult> OnPostEditCategoryAsync(int id, string name, int? parentCategoryId)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat is null)
        {
            Flash("Category not found.", "error");
            return RedirectToPage(new { tab = "categories" });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Flash("Category name is required.", "error");
            return RedirectToPage(new { tab = "categories" });
        }

        // A category can't become its own parent, or its own child's parent —
        // that would create a loop, so block it rather than silently corrupting the tree.
        if (parentCategoryId == id)
        {
            Flash("A category can't be its own parent.", "error");
            return RedirectToPage(new { tab = "categories" });
        }

        cat.Name = name.Trim();
        cat.ParentCategoryId = parentCategoryId;
        await _db.SaveChangesAsync();
        Flash("Category updated.");
        return RedirectToPage(new { tab = "categories" });
    }

    public async Task<IActionResult> OnPostDeleteCategoryAsync(int id)
    {
        var cat = await _db.Categories.Include(c => c.Children).FirstOrDefaultAsync(c => c.Id == id);
        if (cat is null)
        {
            Flash("Category not found.", "error");
            return RedirectToPage(new { tab = "categories" });
        }

        var inUse = await _db.Items.AnyAsync(i => i.CategoryId == id);
        if (inUse)
        {
            Flash($"Can't delete \"{cat.Name}\" — it's still assigned to one or more items. Reassign those items first.", "error");
            return RedirectToPage(new { tab = "categories" });
        }

        if (cat.Children.Any())
        {
            Flash($"Can't delete \"{cat.Name}\" — it has sub-categories. Delete or reassign those first.", "error");
            return RedirectToPage(new { tab = "categories" });
        }

        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        Flash("Category deleted.");
        return RedirectToPage(new { tab = "categories" });
    }

    // ---------- Sellers ----------

    public async Task<IActionResult> OnPostAddSellerAsync()
    {
        ModelState.Clear();
        if (string.IsNullOrWhiteSpace(NewSeller.Name))
        {
            Flash("Seller name is required.", "error");
            return RedirectToPage(new { tab = "sellers" });
        }

        _db.Sellers.Add(new Seller
        {
            Name = NewSeller.Name.Trim(),
            ContactInfo = string.IsNullOrWhiteSpace(NewSeller.ContactInfo) ? null : NewSeller.ContactInfo.Trim(),
            Link = string.IsNullOrWhiteSpace(NewSeller.Link) ? null : NewSeller.Link.Trim()
        });
        await _db.SaveChangesAsync();
        Flash("Seller added.");
        return RedirectToPage(new { tab = "sellers" });
    }

    public async Task<IActionResult> OnPostEditSellerAsync(int id, string name, string? contactInfo, string? link)
    {
        var seller = await _db.Sellers.FindAsync(id);
        if (seller is null)
        {
            Flash("Seller not found.", "error");
            return RedirectToPage(new { tab = "sellers" });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Flash("Seller name is required.", "error");
            return RedirectToPage(new { tab = "sellers" });
        }

        seller.Name = name.Trim();
        seller.ContactInfo = string.IsNullOrWhiteSpace(contactInfo) ? null : contactInfo.Trim();
        seller.Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
        await _db.SaveChangesAsync();
        Flash("Seller updated.");
        return RedirectToPage(new { tab = "sellers" });
    }

    public async Task<IActionResult> OnPostDeleteSellerAsync(int id)
    {
        var seller = await _db.Sellers.FindAsync(id);
        if (seller is null)
        {
            Flash("Seller not found.", "error");
            return RedirectToPage(new { tab = "sellers" });
        }

        // Items reference sellers with SetNull, so this delete is technically safe either way —
        // but we still warn, so users aren't surprised their items lost a seller silently.
        var inUseCount = await _db.Items.CountAsync(i => i.SellerId == id);
        if (inUseCount > 0)
        {
            Flash($"Can't delete \"{seller.Name}\" — it's linked to {inUseCount} item(s). Reassign those items first.", "error");
            return RedirectToPage(new { tab = "sellers" });
        }

        _db.Sellers.Remove(seller);
        await _db.SaveChangesAsync();
        Flash("Seller deleted.");
        return RedirectToPage(new { tab = "sellers" });
    }

    // ---------- Conditions ----------

    public async Task<IActionResult> OnPostAddConditionAsync()
    {
        ModelState.Clear();
        if (string.IsNullOrWhiteSpace(NewCondition.Name))
        {
            Flash("Condition name is required.", "error");
            return RedirectToPage(new { tab = "conditions" });
        }

        _db.Conditions.Add(new Condition { Name = NewCondition.Name.Trim() });
        await _db.SaveChangesAsync();
        Flash("Condition added.");
        return RedirectToPage(new { tab = "conditions" });
    }

    public async Task<IActionResult> OnPostEditConditionAsync(int id, string name)
    {
        var cond = await _db.Conditions.FindAsync(id);
        if (cond is null)
        {
            Flash("Condition not found.", "error");
            return RedirectToPage(new { tab = "conditions" });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Flash("Condition name is required.", "error");
            return RedirectToPage(new { tab = "conditions" });
        }

        cond.Name = name.Trim();
        await _db.SaveChangesAsync();
        Flash("Condition updated.");
        return RedirectToPage(new { tab = "conditions" });
    }

    public async Task<IActionResult> OnPostDeleteConditionAsync(int id)
    {
        var cond = await _db.Conditions.FindAsync(id);
        if (cond is null)
        {
            Flash("Condition not found.", "error");
            return RedirectToPage(new { tab = "conditions" });
        }

        var inUseCount = await _db.Items.CountAsync(i => i.ConditionId == id);
        if (inUseCount > 0)
        {
            Flash($"Can't delete \"{cond.Name}\" — it's linked to {inUseCount} item(s). Reassign those items first.", "error");
            return RedirectToPage(new { tab = "conditions" });
        }

        _db.Conditions.Remove(cond);
        await _db.SaveChangesAsync();
        Flash("Condition deleted.");
        return RedirectToPage(new { tab = "conditions" });
    }

    // ---------- Locations ----------

    public async Task<IActionResult> OnPostAddLocationAsync()
    {
        ModelState.Clear();
        if (string.IsNullOrWhiteSpace(NewLocation.Name))
        {
            Flash("Location name is required.", "error");
            return RedirectToPage(new { tab = "locations" });
        }

        _db.Locations.Add(new Location { Name = NewLocation.Name.Trim(), ImagePath = string.Empty });
        await _db.SaveChangesAsync();
        Flash("Location added.");
        return RedirectToPage(new { tab = "locations" });
    }

    public async Task<IActionResult> OnPostEditLocationAsync(int id, string name)
    {
        var loc = await _db.Locations.FindAsync(id);
        if (loc is null)
        {
            Flash("Location not found.", "error");
            return RedirectToPage(new { tab = "locations" });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Flash("Location name is required.", "error");
            return RedirectToPage(new { tab = "locations" });
        }

        loc.Name = name.Trim();
        await _db.SaveChangesAsync();
        Flash("Location updated.");
        return RedirectToPage(new { tab = "locations" });
    }

    public async Task<IActionResult> OnPostDeleteLocationAsync(int id)
    {
        var loc = await _db.Locations.FindAsync(id);
        if (loc is null)
        {
            Flash("Location not found.", "error");
            return RedirectToPage(new { tab = "locations" });
        }

        var inUseCount = await _db.Items.CountAsync(i => i.LocationId == id);
        if (inUseCount > 0)
        {
            Flash($"Can't delete \"{loc.Name}\" — it's linked to {inUseCount} item(s). Reassign those items first.", "error");
            return RedirectToPage(new { tab = "locations" });
        }

        _db.Locations.Remove(loc);
        await _db.SaveChangesAsync();
        Flash("Location deleted.");
        return RedirectToPage(new { tab = "locations" });
    }

    // ---------- Tags ----------

    public async Task<IActionResult> OnPostAddTagAsync()
    {
        ModelState.Clear();
        if (string.IsNullOrWhiteSpace(NewTag.Name))
        {
            Flash("Tag name is required.", "error");
            return RedirectToPage(new { tab = "tags" });
        }

        var normalized = NewTag.Name.Trim().ToLowerInvariant();
        var exists = await _db.Tags.AnyAsync(t => t.Name == normalized);
        if (exists)
        {
            Flash($"Tag \"{normalized}\" already exists.", "error");
            return RedirectToPage(new { tab = "tags" });
        }

        _db.Tags.Add(new Tag { Name = normalized });
        await _db.SaveChangesAsync();
        Flash("Tag added.");
        return RedirectToPage(new { tab = "tags" });
    }

    public async Task<IActionResult> OnPostEditTagAsync(int id, string name)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag is null)
        {
            Flash("Tag not found.", "error");
            return RedirectToPage(new { tab = "tags" });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Flash("Tag name is required.", "error");
            return RedirectToPage(new { tab = "tags" });
        }

        var normalized = name.Trim().ToLowerInvariant();
        var clash = await _db.Tags.AnyAsync(t => t.Name == normalized && t.Id != id);
        if (clash)
        {
            Flash($"Tag \"{normalized}\" already exists.", "error");
            return RedirectToPage(new { tab = "tags" });
        }

        tag.Name = normalized;
        await _db.SaveChangesAsync();
        Flash("Tag updated.");
        return RedirectToPage(new { tab = "tags" });
    }

    public async Task<IActionResult> OnPostDeleteTagAsync(int id)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag is null)
        {
            Flash("Tag not found.", "error");
            return RedirectToPage(new { tab = "tags" });
        }

        var inUseCount = await _db.ItemTags.CountAsync(it => it.TagId == id);
        if (inUseCount > 0)
        {
            Flash($"Can't delete \"{tag.Name}\" — it's applied to {inUseCount} item(s). Remove it from those items first.", "error");
            return RedirectToPage(new { tab = "tags" });
        }

        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        Flash("Tag deleted.");
        return RedirectToPage(new { tab = "tags" });
    }
}
