namespace ElectronicsInventory.Models
{
    // Datasheets, spec PDFs, manuals, invoices — anything that isn't a photo.
    public class ItemAttachment
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;   // original name shown to user
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
