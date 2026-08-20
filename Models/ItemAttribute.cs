namespace ElectronicsInventory.Models
{
    // Flexible key/value attributes so different item types (electronics vs
    // computer accessories vs cables) can carry different extra fields
    // without needing a rigid schema per category.
    // e.g. ("Wattage", "65W"), ("Cable Length", "1.5m"), ("Warranty", "1 year")
    public class ItemAttribute
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
    }
}
