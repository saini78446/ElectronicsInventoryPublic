namespace ElectronicsInventory.Models
{
    // Additional photos beyond the primary Item.ImagePath.
    // Item.ImagePath stays as-is for backward compatibility with
    // everything you've already added — this table is purely extra.
    public class ItemImage
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public string ImagePath { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
