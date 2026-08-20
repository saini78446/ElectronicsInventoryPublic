using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ElectronicsInventory.Data;
using ElectronicsInventory.Models;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;

public class EditModel : PageModel
{
    private readonly InventoryContext _db;
    private readonly IWebHostEnvironment _env;

    public EditModel(InventoryContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [BindProperty]
    public ItemInput Input { get; set; } = new();

    public string ExistingImagePath { get; set; } = string.Empty;

    // NEW: existing related data shown/managed on the edit page
    public List<ElectronicsInventory.Models.ItemImage> ExistingExtraImages { get; set; } = new();
    public List<ElectronicsInventory.Models.ItemAttachment> ExistingAttachments { get; set; } = new();
    public string ExistingTagsCsv { get; set; } = string.Empty;

    public class ItemInput
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Item name is required.")]
        public string Name { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Current price must be zero or greater.")]
        public double CurrentPrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Original price must be zero or greater.")]
        public double OriginalPrice { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Quantity must be zero or greater.")]
        public int Quantity { get; set; }

        // Optional on edit: empty means "keep the existing photo".
        public string? ImageData { get; set; }

        public string Notes { get; set; } = string.Empty;

        public int? CategoryId { get; set; }

        public int? ConditionId { get; set; }  // NEW

        public int? LocationId { get; set; }  // NEW

        // NEW ------------------------------------------------------

        public List<string>? NewExtraImageData { get; set; }
        public List<int>? RemoveImageIds { get; set; }

        public List<IFormFile>? NewAttachments { get; set; }
        public List<int>? RemoveAttachmentIds { get; set; }

        public int? SellerId { get; set; }
        public string? NewSellerName { get; set; }
        public string? NewSellerContact { get; set; }
        public string? NewSellerLink { get; set; }

        public string? TagsCsv { get; set; }

        public List<string>? AttributeKeys { get; set; }
        public List<string>? AttributeValues { get; set; }
    }

    public List<Category> Categories { get; set; } = new();
    public List<Seller> Sellers { get; set; } = new();
    public List<Condition> Conditions { get; set; } = new();
    public List<Location> Locations { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var item = await _db.Items
            .Include(i => i.Images)
            .Include(i => i.Attachments)
            .Include(i => i.Attributes)
            .Include(i => i.ItemTags).ThenInclude(it => it.Tag)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item is null)
        {
            TempData["FlashMessage"] = "Item not found.";
            TempData["FlashType"] = "error";
            return RedirectToPage("/Index");
        }

        Input = new ItemInput
        {
            Id = item.Id,
            Name = item.Name,
            CurrentPrice = item.CurrentPrice,
            OriginalPrice = item.OriginalPrice,
            Quantity = item.Quantity,
            Notes = item.Notes,
            CategoryId = item.CategoryId,
            SellerId = item.SellerId,
            ConditionId = item.ConditionId,
            LocationId = item.LocationId
        };
        ExistingImagePath = item.ImagePath;
        ExistingExtraImages = item.Images.OrderBy(im => im.SortOrder).ToList();
        ExistingAttachments = item.Attachments.ToList();
        ExistingTagsCsv = string.Join(", ", item.ItemTags.Select(it => it.Tag!.Name));

        // Attributes get flattened into the two parallel lists the form uses,
        // so existing key/value rows show up pre-filled and editable.
        Input.AttributeKeys = item.Attributes.OrderBy(a => a.SortOrder).Select(a => a.Key).ToList();
        Input.AttributeValues = item.Attributes.OrderBy(a => a.SortOrder).Select(a => a.Value).ToList();

        Categories = await _db.Categories.Include(c => c.Children).OrderBy(c => c.Id).ToListAsync();
        Sellers = await _db.Sellers.OrderBy(s => s.Name).ToListAsync();
        Conditions = await _db.Conditions.OrderBy(c => c.Name).ToListAsync();
        Locations = await _db.Locations.OrderBy(l => l.Name).ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!HttpContext.User.Identity!.IsAuthenticated)
        {
            return Challenge();
        }

        Categories = await _db.Categories.Include(c => c.Children).OrderBy(c => c.Id).ToListAsync();
        Sellers = await _db.Sellers.OrderBy(s => s.Name).ToListAsync();
        Conditions = await _db.Conditions.OrderBy(c => c.Name).ToListAsync();
        Locations = await _db.Locations.OrderBy(l => l.Name).ToListAsync();

        var item = await _db.Items
            .Include(i => i.Images)
            .Include(i => i.Attachments)
            .Include(i => i.Attributes)
            .Include(i => i.ItemTags)
            .FirstOrDefaultAsync(i => i.Id == Input.Id);

        if (item is null)
        {
            TempData["FlashMessage"] = "Item not found.";
            TempData["FlashType"] = "error";
            return RedirectToPage("/Index");
        }

        ExistingImagePath = item.ImagePath;
        ExistingExtraImages = item.Images.OrderBy(im => im.SortOrder).ToList();
        ExistingAttachments = item.Attachments.ToList();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Resolve seller same as Add: pick existing, or quick-add new.
        int? sellerId = Input.SellerId;
        if (sellerId is null && !string.IsNullOrWhiteSpace(Input.NewSellerName))
        {
            var seller = new Seller
            {
                Name = Input.NewSellerName.Trim(),
                ContactInfo = Input.NewSellerContact?.Trim(),
                Link = Input.NewSellerLink?.Trim()
            };
            _db.Sellers.Add(seller);
            await _db.SaveChangesAsync();
            sellerId = seller.Id;
        }

        item.CategoryId = Input.CategoryId;
        item.Notes = (Input.Notes ?? string.Empty).Trim();
        item.Name = Input.Name.Trim();
        item.CurrentPrice = Input.CurrentPrice;
        item.OriginalPrice = Input.OriginalPrice;
        item.Quantity = Input.Quantity;
        item.SellerId = sellerId;
        item.ConditionId = Input.ConditionId;
        item.LocationId = Input.LocationId;
        item.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        if (!string.IsNullOrWhiteSpace(Input.ImageData))
        {
            var newPath = TrySaveImage(Input.ImageData);
            if (newPath is null)
            {
                ModelState.AddModelError(nameof(Input.ImageData), "Could not process the captured image. Please retake the photo.");
                return Page();
            }

            // Delete old image file after the new one is safely saved.
            var oldFullPath = Path.Combine(_env.WebRootPath, item.ImagePath.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(oldFullPath))
            {
                try { System.IO.File.Delete(oldFullPath); } catch { /* non-fatal */ }
            }

            item.ImagePath = newPath;
            ExistingImagePath = newPath;
        }

        // Remove any extra images the user flagged for deletion
        if (Input.RemoveImageIds is { Count: > 0 })
        {
            var toRemove = item.Images.Where(im => Input.RemoveImageIds.Contains(im.Id)).ToList();
            foreach (var img in toRemove)
            {
                var fullPath = Path.Combine(_env.WebRootPath, img.ImagePath.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(fullPath))
                {
                    try { System.IO.File.Delete(fullPath); } catch { /* non-fatal */ }
                }
                _db.ItemImages.Remove(img);
            }
        }

        // Add newly captured extra images
        if (Input.NewExtraImageData is { Count: > 0 })
        {
            int order = item.Images.Any() ? item.Images.Max(im => im.SortOrder) + 1 : 0;
            foreach (var dataUrl in Input.NewExtraImageData)
            {
                if (string.IsNullOrWhiteSpace(dataUrl)) continue;
                var extraPath = TrySaveImage(dataUrl);
                if (extraPath is null) continue;

                _db.ItemImages.Add(new ElectronicsInventory.Models.ItemImage
                {
                    ItemId = item.Id,
                    ImagePath = extraPath,
                    SortOrder = order++
                });
            }
        }

        // Remove attachments flagged for deletion
        if (Input.RemoveAttachmentIds is { Count: > 0 })
        {
            var toRemove = item.Attachments.Where(a => Input.RemoveAttachmentIds.Contains(a.Id)).ToList();
            foreach (var att in toRemove)
            {
                var fullPath = Path.Combine(_env.WebRootPath, att.FilePath.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(fullPath))
                {
                    try { System.IO.File.Delete(fullPath); } catch { /* non-fatal */ }
                }
                _db.ItemAttachments.Remove(att);
            }
        }

        // Add new attachments
        if (Input.NewAttachments is { Count: > 0 })
        {
            foreach (var file in Input.NewAttachments)
            {
                if (file.Length == 0) continue;
                var saved = await TrySaveAttachmentAsync(file);
                if (saved is null) continue;

                _db.ItemAttachments.Add(new ElectronicsInventory.Models.ItemAttachment
                {
                    ItemId = item.Id,
                    FilePath = saved.Value.RelativePath,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSizeBytes = file.Length
                });
            }
        }

        // Tags: replace the full set with whatever's in the box now.
        // Simple and predictable — matches how the textbox reads on screen.
        _db.ItemTags.RemoveRange(item.ItemTags);
        if (!string.IsNullOrWhiteSpace(Input.TagsCsv))
        {
            await AttachTagsAsync(item.Id, Input.TagsCsv);
        }

        // Attributes: replace the full set too.
        _db.ItemAttributes.RemoveRange(item.Attributes);
        if (Input.AttributeKeys is { Count: > 0 } && Input.AttributeValues is { Count: > 0 })
        {
            int order = 0;
            for (int i = 0; i < Input.AttributeKeys.Count && i < Input.AttributeValues.Count; i++)
            {
                var key = Input.AttributeKeys[i]?.Trim();
                var value = Input.AttributeValues[i]?.Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;

                _db.ItemAttributes.Add(new ElectronicsInventory.Models.ItemAttribute
                {
                    ItemId = item.Id,
                    Key = key,
                    Value = value,
                    SortOrder = order++
                });
            }
        }

        await _db.SaveChangesAsync();

        TempData["FlashMessage"] = "Item updated successfully.";
        TempData["FlashType"] = "success";
        return RedirectToPage("/Index");
    }

    private async Task AttachTagsAsync(int itemId, string tagsCsv)
    {
        var names = tagsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => n.ToLowerInvariant())
            .Distinct()
            .ToList();

        foreach (var name in names)
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == name);
            if (tag is null)
            {
                tag = new Tag { Name = name };
                _db.Tags.Add(tag);
                await _db.SaveChangesAsync();
            }

            _db.ItemTags.Add(new ItemTag { ItemId = itemId, TagId = tag.Id });
        }
    }

    private string? TrySaveImage(string dataUrl)
    {
        var match = System.Text.RegularExpressions.Regex.Match(dataUrl, @"^data:image/(\w+);base64,(.+)$");
        if (!match.Success) return null;

        var ext = match.Groups[1].Value.ToLowerInvariant();
        if (ext == "jpeg") ext = "jpg";

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(match.Groups[2].Value);
        }
        catch (FormatException)
        {
            return null;
        }

        var uniquePart = Guid.NewGuid().ToString("N").Substring(0, 8);
        var fileName = $"item_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{uniquePart}.{ext}";
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);
        var fullPath = Path.Combine(uploadsDir, fileName);
        System.IO.File.WriteAllBytes(fullPath, bytes);

        return $"uploads/{fileName}";
    }

    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".txt", ".png", ".jpg", ".jpeg"
    };

    private async Task<(string RelativePath, string FullPath)?> TrySaveAttachmentAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedAttachmentExtensions.Contains(ext))
            return null;

        if (file.Length > 20 * 1024 * 1024)
            return null;

        var uniquePart = Guid.NewGuid().ToString("N").Substring(0, 8);
        var fileName = $"doc_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{uniquePart}{ext}";
        var docsDir = Path.Combine(_env.WebRootPath, "attachments");
        Directory.CreateDirectory(docsDir);
        var fullPath = Path.Combine(docsDir, fileName);

        using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return ($"attachments/{fileName}", fullPath);
    }
}
