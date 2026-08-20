# Electronics Inventory — ASP.NET Core (Razor Pages) version

This is a straight port of your PHP/SQLite app to ASP.NET Core 10 + Razor Pages + EF Core.
**It reads the exact same SQLite database file and folder layout** the PHP app used, so your
existing hundreds of rows and photos carry over with zero data migration.

## What matches the old app, on purpose

- Table name `items`, column names (`current_price`, `original_price`, `image_path`,
  `created_at`, `updated_at`, etc.) are mapped 1:1 in `Data/InventoryContext.cs` via
  `HasColumnName(...)`. EF Core will **not** try to create or alter this table.
- `created_at` / `updated_at` stay as plain TEXT strings (`yyyy-MM-dd HH:mm:ss`), same as
  PHP's `datetime('now')` — no datetime type conversion that could choke on old rows.
- Images still live under `wwwroot/uploads/`, referenced from the DB as `uploads/xxx.jpg`,
  same relative-path convention as before.
- Same camera-capture flow: `getUserMedia` → canvas → base64 JPEG → posted to the server →
  decoded and saved as a file. Mandatory on Add, optional retake on Edit.

## 1. Copy over your existing data

From your PHP project folder, copy these into this project:

```bash
# from the PHP app root, into this .NET project root
cp -r electronics_app/data/electronics.db  ElectronicsInventory/data/electronics.db
cp -r electronics_app/uploads/*            ElectronicsInventory/wwwroot/uploads/
```

That's it — no SQL migration scripts, no re-import. The `.db` file format is identical;
EF Core is just a different client reading/writing the same file.

## 2. Restore & run

```bash
cd ElectronicsInventory
dotnet restore
dotnet run
```

Then open the URL it prints (typically `https://localhost:5001` or similar).

> Camera access requires HTTPS or `localhost`. `dotnet run` serves HTTPS by default in dev.
> If testing from a phone on your LAN, you'll need a trusted cert or to tunnel via something
> like `ngrok`/`dotnet dev-certs` trust setup — plain HTTP from a phone will block the camera.

## 3. Verify your data loaded

Once running, the home page (`/`) should immediately show your existing items, stats
(total items / qty / value), and thumbnails from `wwwroot/uploads/`.

If the list is empty:
- Check `appsettings.json` → `ConnectionStrings:DefaultConnection` points at the right
  `.db` path (relative to the project's working directory when run).
- Confirm `data/electronics.db` actually has rows: any SQLite browser, or
  `sqlite3 data/electronics.db "SELECT COUNT(*) FROM items;"` if you have the sqlite3 CLI.

## Project layout

```
ElectronicsInventory/
├── Program.cs                  # startup, DI, EF Core wiring
├── appsettings.json             # connection string (points at data/electronics.db)
├── Models/Item.cs                # POCO — mirrors existing table exactly
├── Data/InventoryContext.cs      # EF Core mapping to existing column names
├── Pages/
│   ├── Index.cshtml(.cs)         # list, search, stats
│   ├── Add.cshtml(.cs)           # create + mandatory camera capture
│   ├── Edit.cshtml(.cs)          # update + optional retake
│   ├── Delete.cshtml(.cs)        # POST-only delete handler
│   └── Shared/_Layout.cshtml
├── wwwroot/
│   ├── css/site.css
│   └── uploads/                  # <- copy your existing photos here
└── data/                         # <- copy your existing electronics.db here
```

## Notes / things worth knowing

- **No EF migrations are used.** Since the table already exists with a working schema,
  adding EF Core migrations would be redundant and risks EF trying to "helpfully" alter
  columns. The `DbContext` just maps onto what's already there.
- If you ever *do* want migrations going forward (e.g. adding a new column later), run
  `dotnet ef migrations add InitialBaseline --context InventoryContext` once you've
  installed the `dotnet-ef` tool, then edit the generated migration to be a no-op for existing
  columns before applying — but for now, none of that is needed.
- Search on the list page does a `LIKE %term%` on `name`, same as the PHP version.
- Validation, mandatory-photo-on-add, optional-retake-on-edit, and image cleanup on
  delete/replace all behave the same as the PHP version.

## Note on this build

I wasn't able to compile this with the actual .NET SDK in the sandbox I built it in (no
internet access to the NuGet/Microsoft feeds), so this hasn't been run through `dotnet build`
or `dotnet run`. I reviewed every file by hand for correctness, and the schema mapping was
checked against a real SQLite DB seeded with your exact table structure. Please run
`dotnet build` first thing and let me know if anything doesn't compile — happy to fix fast.
