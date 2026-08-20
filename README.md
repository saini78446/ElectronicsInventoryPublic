# Electronics Inventory — ASP.NET Core (Razor Pages)

A self-hosted inventory tracker for electronics, built with ASP.NET Core 10, Razor Pages,
and EF Core over SQLite. Originally ported from a PHP/SQLite app — it reads the same
database and upload folder layout, so existing data carries over without migration.

<img width="1484" height="956" alt="image" src="https://github.com/user-attachments/assets/a7a6d371-2cbe-48e1-afbb-620c74ce64c0" />

## Features

- Item list with search and quick stats (total items, quantity, value)
- Add items with mandatory camera capture (photo required on create)
- Edit items with optional photo retake, extra images, attachments, tags, and
  custom key/value attributes
- Master data management (Categories, Sellers, Conditions, Locations, Tags) with
  no cascading deletes — a row in use is protected until it's reassigned
- Optional AI-assisted lookup via Groq and SerpApi to help fill in item details faster

<img width="1176" height="950" alt="image" src="https://github.com/user-attachments/assets/adee7cee-3186-4979-8a68-39405e8fa8b5" />

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQLite (bundled via EF Core's SQLite provider — no separate install needed)

## Setup

### 1. Clone and configure

```bash
git clone <this-repo-url>
cd ElectronicsInventory
cp appsettings.json appsettings.Development.json
```

Edit `appsettings.Development.json` (or `appsettings.json` directly, if not committing
per-environment overrides) and fill in:

```json
{
  "Groq": {
    "ApiKey": "your-groq-api-key"
  },
  "SerpApi": {
    "ApiKey": "your-serpapi-key"
  },
  "Auth": {
    "AdminPassword": "pick-a-real-password"
  }
}
```

- **Groq** — create a free API key at [console.groq.com/keys](https://console.groq.com/keys).
  Used for AI-assisted item lookups.
- **SerpApi** — create a free API key at [serpapi.com/manage-api-key](https://serpapi.com/manage-api-key).
  Used for web search backing those lookups.
- **Auth:AdminPassword** — the app ships with a placeholder password. Change this before
  running anywhere other than your own machine — do not deploy with the default value.

> **Never commit real API keys or passwords.** `appsettings.json` in this repo only contains
> empty placeholders. Keep your real `appsettings.Development.json` (or use user secrets /
> environment variables in production) out of version control — see `.gitignore` below.

### 2. Bring your existing data (optional)

If you're migrating from the original PHP/SQLite app, copy your existing files in before
first run:

```bash
cp -r /path/to/php-app/data/electronics.db  ElectronicsInventory/data/electronics.db
cp -r /path/to/php-app/uploads/*            ElectronicsInventory/wwwroot/uploads/
```

The table names, column names, and file layout are mapped 1:1 in `Data/InventoryContext.cs`,
so no SQL migration or re-import is needed — EF Core just reads/writes the existing `.db` file
as-is.

Starting fresh instead? Skip this step; the app will create the schema on first run.

### 3. Run

```bash
dotnet restore
dotnet run
```

Open the URL printed in the console (typically `https://localhost:5001`).

> Camera capture requires HTTPS or `localhost`. `dotnet run` serves HTTPS by default in dev.
> To test from a phone on your LAN, you'll need a trusted certificate (see
> `dotnet dev-certs https --trust`) or a tunnel like `ngrok` — plain HTTP from another device
> will block camera access in most browsers.

## Project layout

```
ElectronicsInventory/
├── Program.cs                    # startup, DI, EF Core wiring
├── appsettings.json               # config template (placeholders only — see Setup)
├── Models/                        # Item, Category, Seller, Condition, Location, Tag, ...
├── Data/InventoryContext.cs       # EF Core mapping to existing column names
├── Pages/
│   ├── Index.cshtml(.cs)          # list, search, stats
│   ├── Add.cshtml(.cs)            # create + mandatory camera capture
│   ├── Edit.cshtml(.cs)           # update + optional retake, tags, attachments
│   ├── Manage.cshtml(.cs)         # master data: categories, sellers, conditions, etc.
│   ├── Delete.cshtml(.cs)         # POST-only delete handler
│   └── Shared/_Layout.cshtml
├── wwwroot/
│   ├── css/site.css
│   └── uploads/                   # item photos live here
└── data/                          # electronics.db lives here
```

## Notes

- **No EF migrations by default.** The `DbContext` maps onto the existing schema as-is.
  If you add new columns later, run
  `dotnet ef migrations add <Name> --context InventoryContext` (requires the `dotnet-ef`
  tool), then review the generated migration before applying it.
- `created_at` / `updated_at` are stored as plain `TEXT` (`yyyy-MM-dd HH:mm:ss`), matching
  the original PHP app's convention — no datetime type conversion to worry about.
- Search does a `LIKE %term%` match on item name.
- Master-data deletes (categories, sellers, conditions, locations, tags) are blocked
  server-side while a row is still referenced by any item — reassign first, then delete.

## Contributing / Issues

This project hasn't been run through a full CI build yet. If you hit a compile error or
runtime issue, please open an issue with the stack trace and your `.NET` SDK version.
