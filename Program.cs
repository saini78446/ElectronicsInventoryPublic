using Microsoft.EntityFrameworkCore;
using ElectronicsInventory.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.IsEssential = true;
        
    });



builder.Services.AddAuthorization();

// Points at the SAME electronics.db file used by the original PHP app.
// Put your existing data/electronics.db next to this project (see appsettings.json)
// or update the ConnectionStrings:DefaultConnection value to its full path.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=data/electronics.db";

builder.Services.AddDbContext<InventoryContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

// Ensure the data/uploads folders exist (does NOT recreate or alter the DB schema —
// your existing table and rows are left exactly as-is).
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "data"));
Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "uploads"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
