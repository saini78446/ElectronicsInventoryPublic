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

namespace ElectronicsInventory.Pages;

public class AddModel : PageModel
{
    public class EstimateRequest
    {
        public string ItemName { get; set; } = string.Empty;
    }

    public class IdentifyRequest
    {
        public string ImageData { get; set; } = string.Empty;
    }

    private readonly InventoryContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public AddModel(InventoryContext db, IWebHostEnvironment env, IConfiguration config)
    {
        _db = db;
        _env = env;
        _config = config;
    }

    [BindProperty]
    public ItemInput Input { get; set; } = new();

    public class ItemInput
    {

        public int? LocationId { get; set; }

        public int? ConditionId { get; set; }

        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Item name is required.")]
        public string Name { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Current price must be zero or greater.")]
        public double CurrentPrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Original price must be zero or greater.")]
        public double OriginalPrice { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Quantity must be zero or greater.")]
        public int Quantity { get; set; }

        // Populated by JS from the camera capture as a data: URL (base64 JPEG).
        public string? ImageData { get; set; }

        public string Notes { get; set; } = string.Empty;

        // Extra photos, captured the same way as the primary image (data: URLs).
        public List<string> ExtraImageData { get; set; } = new();

        // Spec sheets / manuals / invoices uploaded via a normal file input.
        public List<IFormFile>? Attachments { get; set; }

        public int? SellerId { get; set; }

        // Quick-add a new seller inline instead of picking an existing one.
        public string? NewSellerName { get; set; }
        public string? NewSellerContact { get; set; }
        public string? NewSellerLink { get; set; }

        // Comma-separated tags, e.g. "gaming, spare, cable"
        public string? TagsCsv { get; set; }

        // Custom attribute key/value pairs from dynamic form rows.
        public List<string>? AttributeKeys { get; set; }
        public List<string>? AttributeValues { get; set; }
    }

    public List<Category> Categories { get; set; } = new();
    public List<Seller> Sellers { get; set; } = new();

    public List<Location> Locations { get; set; } = new();

    public List<Condition> Conditions { get; set; } = new();

    public async Task OnGetAsync()
    {
        Categories = await _db.Categories.Include(c => c.Children).OrderBy(c => c.Id).ToListAsync();
        Sellers = await _db.Sellers.OrderBy(s => s.Name).ToListAsync();
        Locations = await _db.Locations.OrderBy(l => l.Name).ToListAsync();
        Conditions = await _db.Conditions.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<IActionResult> OnGetSuggestAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return new JsonResult(new List<object>());

        var results = await _db.Items
            .Where(i => EF.Functions.Like(EF.Functions.Collate(i.Name, "NOCASE"), $"%{term}%"))
            .OrderBy(i => i.Name)
            .Take(6)
            .Select(i => new { i.Id, i.Name })
            .ToListAsync();

        return new JsonResult(results);
    }

    public async Task<IActionResult> OnPostEstimateAsync([FromBody] EstimateRequest req)
    {
        var apiKey = _config["Groq:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new JsonResult(new { success = false, error = "Server missing Groq API key." });
        }

        if (string.IsNullOrWhiteSpace(req.ItemName))
        {
            return new JsonResult(new { success = false, error = "No item name provided." });
        }

        try
        {
            string price = await EstimatePriceAsync(apiKey, req.ItemName.Trim());
            return new JsonResult(new { success = true, price });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    // ===== NEW: Identify product name from the captured photo via SerpApi (Google Lens) =====
    public async Task<IActionResult> OnPostIdentifyAsync([FromBody] IdentifyRequest req)
    {
        var apiKey = _config["SerpApi:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new JsonResult(new { success = false, error = "Server missing SerpApi key." });
        }

        if (string.IsNullOrWhiteSpace(req.ImageData))
        {
            return new JsonResult(new { success = false, error = "No image provided." });
        }

        // Save the captured photo to wwwroot/uploads so SerpApi can fetch it via a public URL.
        string? relativePath = TrySaveImage(req.ImageData);
        if (relativePath is null)
        {
            return new JsonResult(new { success = false, error = "Could not process the captured image." });
        }

        // Build a full public URL. Assumes the app is deployed and reachable
        // (Request.Scheme/Host will resolve correctly once behind a real domain).
        var imageUrl = $"{Request.Scheme}://{Request.Host}/{relativePath}";

        try
        {
            var (name, matches) = await IdentifyProductAsync(apiKey, imageUrl);

            if (string.IsNullOrWhiteSpace(name))
            {
                return new JsonResult(new { success = false, error = "No matches found for this image." });
            }

            return new JsonResult(new { success = true, name, matches });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static async Task<string> EstimatePriceAsync(
    string apiKey,
    string itemName)
    {
        const string model = "openai/gpt-oss-20b";

        var payload = new
        {
            model,
            temperature = 0.0,
            max_completion_tokens = 256,
            reasoning_effort = "medium",

            messages = new object[]
            {
            new
            {
                role = "user",
                content =
$@"You are estimating the purchase price of an item in INDIA.

Item name:
""{itemName}""

Your task:
1. Identify what the item most likely is.
2. Estimate the realistic CURRENT Indian retail price for this exact type of item.
3. Assume the buyer wants a reasonably cheap Indian online price, from sites such as Robu.in, ElectronicsComp, Amazon India, Flipkart, etc.
4. Do NOT use international/US prices.
5. Do NOT give the price of a different product merely because the brand name is similar.
6. If the exact model is unknown, estimate the price of a typical item matching the description.
7. For tools such as pliers, cutters, screwdrivers, etc., estimate the price of the actual individual tool, NOT a complete tool kit.
8. Give a conservative lower-end but realistic Indian purchase price.

Return ONLY the numeric price in INR.
No ₹ symbol.
No explanation.
No decimal places.

Example:
450"
            }
            }
        };

        string json = JsonSerializer.Serialize(payload);

        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.groq.com/openai/v1/chat/completions");

        req.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        req.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var resp = await _http.SendAsync(req);

        string body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception(body);

        using var doc = JsonDocument.Parse(body);

        string text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!
            .Trim();

        return text;
    }

    // ===== NEW: Calls SerpApi's Google Lens engine and extracts a best-guess product name =====
    private static async Task<(string? Name, List<string> Matches)> IdentifyProductAsync(string apiKey, string imageUrl)
    {
        var url = $"https://serpapi.com/search.json?engine=google_lens&url={Uri.EscapeDataString(imageUrl)}&api_key={apiKey}";

        using var resp = await _http.GetAsync(url);
        string body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception(body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var matches = new List<string>();
        string? topName = null;

        if (root.TryGetProperty("visual_matches", out var visualMatches) && visualMatches.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in visualMatches.EnumerateArray())
            {
                if (m.TryGetProperty("title", out var t))
                {
                    var title = t.GetString();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        matches.Add(title);
                        topName ??= title;
                    }
                }
                if (matches.Count >= 5) break;
            }
        }

        return (topName, matches);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!HttpContext.User.Identity!.IsAuthenticated)
        {
            return Challenge();
        }

        if (string.IsNullOrWhiteSpace(Input.ImageData))
        {
            ModelState.AddModelError(nameof(Input.ImageData), "Camera photo is mandatory. Please capture an image.");
        }

        if (!ModelState.IsValid)
        {
            Categories = await _db.Categories.Include(c => c.Children).OrderBy(c => c.Id).ToListAsync();
            Sellers = await _db.Sellers.OrderBy(s => s.Name).ToListAsync();
            return Page();
        }

        string? relativePath = TrySaveImage(Input.ImageData!);
        if (relativePath is null)
        {
            ModelState.AddModelError(nameof(Input.ImageData), "Could not process the captured image. Please retake the photo.");
            Categories = await _db.Categories.Include(c => c.Children).OrderBy(c => c.Id).ToListAsync();
            Sellers = await _db.Sellers.OrderBy(s => s.Name).ToListAsync();
            return Page();
        }

        // Default category stays "Electronics" for anything not picked,
        // so old behavior is preserved for people not using the new categories.
        int categoryId = Input.CategoryId ?? (await _db.Categories.FirstAsync(c => c.Name == "Electronics")).Id;

        // Resolve seller: either the picked existing one, or a quick-added new one.
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

        int? locationId = Input.LocationId;
        int? conditionId = Input.ConditionId;

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var item = new Item
        {
            Name = Input.Name.Trim(),
            CurrentPrice = Input.CurrentPrice,
            OriginalPrice = Input.OriginalPrice,
            Quantity = Input.Quantity,
            ImagePath = relativePath,
            CategoryId = categoryId,
            CreatedAt = now,
            UpdatedAt = now,
            Notes = Input.Notes,
            SellerId = sellerId,
            LocationId = locationId,
            ConditionId = conditionId
        };

        _db.Items.Add(item);
        await _db.SaveChangesAsync(); // need item.Id before saving related rows

        // Extra images
        if (Input.ExtraImageData is { Count: > 0 })
        {
            int order = 0;
            foreach (var dataUrl in Input.ExtraImageData)
            {
                if (string.IsNullOrWhiteSpace(dataUrl)) continue;
                var extraPath = TrySaveImage(dataUrl);
                if (extraPath is null) continue;

                _db.ItemImages.Add(new ItemImage
                {
                    ItemId = item.Id,
                    ImagePath = extraPath,
                    SortOrder = order++
                });
            }
        }

        // Attachments (spec sheets, manuals, invoices, etc.)
        if (Input.Attachments is { Count: > 0 })
        {
            foreach (var file in Input.Attachments)
            {
                if (file.Length == 0) continue;
                var saved = await TrySaveAttachmentAsync(file);
                if (saved is null) continue;

                _db.ItemAttachments.Add(new ItemAttachment
                {
                    ItemId = item.Id,
                    FilePath = saved.Value.RelativePath,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSizeBytes = file.Length
                });
            }
        }

        // Tags
        if (!string.IsNullOrWhiteSpace(Input.TagsCsv))
        {
            await AttachTagsAsync(item.Id, Input.TagsCsv);
        }

        // Custom attributes
        if (Input.AttributeKeys is { Count: > 0 } && Input.AttributeValues is { Count: > 0 })
        {
            int order = 0;
            for (int i = 0; i < Input.AttributeKeys.Count && i < Input.AttributeValues.Count; i++)
            {
                var key = Input.AttributeKeys[i]?.Trim();
                var value = Input.AttributeValues[i]?.Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) continue;

                _db.ItemAttributes.Add(new ItemAttribute
                {
                    ItemId = item.Id,
                    Key = key,
                    Value = value,
                    SortOrder = order++
                });
            }
        }

        await _db.SaveChangesAsync();

        TempData["FlashMessage"] = "Item added successfully.";
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
                await _db.SaveChangesAsync(); // need Id
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

        // 20MB cap per attachment — generous for spec PDFs, keeps disk usage sane.
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