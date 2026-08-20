namespace ElectronicsInventory.Models
{
    // Join table for the many-to-many Item <-> Tag relationship.
    public class ItemTag
    {
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public int TagId { get; set; }
        public Tag? Tag { get; set; }
    }
}
