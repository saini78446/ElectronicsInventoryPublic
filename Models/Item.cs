using System.ComponentModel.DataAnnotations;

namespace ElectronicsInventory.Models;

public class Item
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Item name is required.")]
    [Display(Name = "Item Name")]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Current price must be zero or greater.")]
    [Display(Name = "Current Price (₹)")]
    public double CurrentPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Original price must be zero or greater.")]
    [Display(Name = "Original Price (₹)")]
    public double OriginalPrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be zero or greater.")]
    [Display(Name = "Quantity Available")]
    public int Quantity { get; set; }

    // Primary / cover photo. Kept exactly as before so every item you've
    // already added keeps working with zero changes.
    [Required(ErrorMessage = "A photo is required.")]
    [Display(Name = "Image Path")]
    public string ImagePath { get; set; } = string.Empty;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Notes { get; set; } = string.Empty;

    // Stored as TEXT ("yyyy-MM-dd HH:mm:ss") in the existing DB, so we keep it as string
    // to avoid EF Core datetime conversion issues with rows the PHP app already wrote.
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

    // ---- NEW fields below. All nullable / defaulted so existing rows load fine. ----

    [Display(Name = "Seller")]
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }

    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    public int? ConditionId { get; set; }   
    public Condition? Condition { get; set; }   

    public List<ItemImage> Images { get; set; } = new();
    public List<ItemAttachment> Attachments { get; set; } = new();
    public List<ItemAttribute> Attributes { get; set; } = new();
    public List<ItemTag> ItemTags { get; set; } = new();
}
