using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ElectronicsInventory.Data;

namespace ElectronicsInventory.Pages;

public class DeleteModel : PageModel
{
    private readonly InventoryContext _db;
    private readonly IWebHostEnvironment _env;

    public DeleteModel(InventoryContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!HttpContext.User.Identity!.IsAuthenticated)
        {
            return Challenge();
        }

        var item = await _db.Items.FindAsync(id);

        if (item is not null)
        {
            _db.Items.Remove(item);
            await _db.SaveChangesAsync();

            var fullPath = Path.Combine(_env.WebRootPath, item.ImagePath.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fullPath))
            {
                try { System.IO.File.Delete(fullPath); } catch { /* non-fatal */ }
            }

            TempData["FlashMessage"] = "Item deleted.";
            TempData["FlashType"] = "success";
        }
        else
        {
            TempData["FlashMessage"] = "Item not found.";
            TempData["FlashType"] = "error";
        }

        return RedirectToPage("/Index");
    }
}
