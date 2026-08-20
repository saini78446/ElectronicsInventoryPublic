namespace ElectronicsInventory.Models
{
     public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TotalRecords { get; set; } = 0;

        // NEW: supports sub-categories. Null = top-level category.
        // Existing rows get NULL by default, so all current categories
        // remain top-level and behave exactly as before.
        public int? ParentCategoryId { get; set; }
        public Category? ParentCategory { get; set; }

        public List<Category> Children { get; set; } = new();

        // Convenience for UI: "Computer Accessories > Cables"
        public string DisplayPath =>
            ParentCategory is null ? Name : $"{ParentCategory.DisplayPath} > {Name}";
    }
    
    // Simple, reusable seller record. Kept lightweight on purpose —
    // if you buy from the same seller repeatedly you can pick them
    // again instead of retyping details.
    public class Condition
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Location
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
    }   

    public class Seller
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ContactInfo { get; set; }   // phone / email / whatever
        public string? Link { get; set; }           // website, store page, etc.
        public string? Notes { get; set; }
    }

     public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

}
