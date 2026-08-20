using Microsoft.EntityFrameworkCore;
using ElectronicsInventory.Models;

namespace ElectronicsInventory.Data;

public class InventoryContext : DbContext
{
    public InventoryContext(DbContextOptions<InventoryContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Category> Categories => Set<Category>();

    // NEW
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ItemTag> ItemTags => Set<ItemTag>();
    public DbSet<ItemImage> ItemImages => Set<ItemImage>();
    public DbSet<ItemAttachment> ItemAttachments => Set<ItemAttachment>();
    public DbSet<ItemAttribute> ItemAttributes => Set<ItemAttribute>();

    public DbSet<Condition> Conditions => Set<Condition>();
    public DbSet<Location> Locations => Set<Location>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.TotalRecords).HasColumnName("TotalRecords");

            // NEW: self-referencing parent/child for sub-categories.
            // Existing rows have NULL here, so nothing about old data changes.
            entity.Property(e => e.ParentCategoryId).HasColumnName("parent_category_id");
            entity.HasOne(e => e.ParentCategory)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentCategoryId)
                  .OnDelete(DeleteBehavior.Restrict); // don't cascade-delete a whole tree by accident
        });

        // Map explicitly to the table/column names created by the original PHP app
        // so the existing electronics.db file works without any migration.
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("items");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.CurrentPrice).HasColumnName("current_price");
            entity.Property(e => e.OriginalPrice).HasColumnName("original_price");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.ImagePath).HasColumnName("image_path").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Notes).HasColumnName("notes");

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId);

            // NEW: optional seller link. Old rows get NULL = "no seller set".
            entity.Property(e => e.SellerId).HasColumnName("seller_id");
            entity.HasOne(e => e.Seller)
                  .WithMany()
                  .HasForeignKey(e => e.SellerId)
                  .OnDelete(DeleteBehavior.SetNull);


            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.HasOne(e => e.Location)
                  .WithMany()
                  .HasForeignKey(e => e.LocationId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.ConditionId).HasColumnName("condition_id");
            entity.HasOne(e => e.Condition)
                  .WithMany()
                  .HasForeignKey(e => e.ConditionId)
                  .OnDelete(DeleteBehavior.SetNull);

        });

        // NEW tables — all fresh, all additive, no impact on existing data.

        modelBuilder.Entity<Seller>(entity =>
        {
            entity.ToTable("sellers");
            entity.Property(e => e.Name).IsRequired();
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("tags");
            entity.Property(e => e.Name).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Condition>(entity =>
        {
            entity.ToTable("conditions");
            entity.Property(e => e.Name).IsRequired();
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("locations");
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.ImagePath).HasColumnName("image_path").IsRequired();
        });


        modelBuilder.Entity<ItemTag>(entity =>
        {
            entity.ToTable("item_tags");
            entity.HasKey(e => new { e.ItemId, e.TagId });

            entity.HasOne(e => e.Item)
                  .WithMany(e => e.ItemTags)
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Tag)
                  .WithMany()
                  .HasForeignKey(e => e.TagId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemImage>(entity =>
        {
            entity.ToTable("item_images");
            entity.HasOne(e => e.Item)
                  .WithMany(e => e.Images)
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemAttachment>(entity =>
        {
            entity.ToTable("item_attachments");
            entity.HasOne(e => e.Item)
                  .WithMany(e => e.Attachments)
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemAttribute>(entity =>
        {
            entity.ToTable("item_attributes");
            entity.HasOne(e => e.Item)
                  .WithMany(e => e.Attributes)
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
