# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Offline desktop system for a laminated-wire glass factory: customers, orders, bills, exports. WPF (.NET 8, Windows) over a single-file SQLite DB. UI text and domain terms are Chinese; the README is the authoritative product spec (but see the amount-calculation caveat below).

## Commands

```bash
# Build whole solution
dotnet build GlassFactory.BillTracker.sln -c Debug

# Run all tests
dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug

# Run a single test (or class) by name filter
dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~OrderAmountCalculatorTests"

# DB smoke test (creates schema in a throwaway data dir; expect "SMOKE_TEST_PASS" in output)
dotnet run --project tools/GlassFactory.BillTracker.DbSmokeTest/GlassFactory.BillTracker.DbSmokeTest.csproj -- <temp_data_dir>
```

Windows publish (single-file, self-contained) and post-publish checks go through PowerShell scripts, not raw `dotnet publish`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_windows.ps1            # restore+build+test+publish -> dist/win-x64/
powershell -ExecutionPolicy Bypass -File .\scripts\build_windows.ps1 -Runtime win-arm64
powershell -ExecutionPolicy Bypass -File .\scripts\publish_smoke_check.ps1       # run the published exe headlessly against a temp data dir
```

The App project targets `net8.0-windows10.0.19041.0` with `EnableWindowsTargeting=true`, so it *builds* on Linux, but WPF can only *run* on Windows. Central package versions live in `Directory.Packages.props`; common build props (`Nullable`/`ImplicitUsings`/`LangVersion`) in `Directory.Build.props`. Don't add `<Version>` to individual `<PackageReference>`s — versions are managed centrally.

### EF Core migrations

Migrations are applied automatically at app startup via `Database.Migrate()` (see `App.OnStartup`). To add one, use the design-time factory, which resolves the DB path from an env var:

```bash
BILLTRACKER_DATA_DIR=/tmp/bt-ef dotnet ef migrations add <Name> \
  --project src/GlassFactory.BillTracker.Data
```

Without `BILLTRACKER_DATA_DIR`, `DesignTimeDbContextFactory` falls back to a folder under `%LOCALAPPDATA%`.

## Architecture

Four layers under `src/`, plus `tests/` and `tools/`:

- **Domain** — entities (`Customer`, `Order`, `OrderItem`, `OrderAttachment`, `Wire`, `SampleBlock`), enums (`OrderStatus`, `PaymentMethod`), and pure calculation services. No dependencies.
- **Data** — `BillTrackerDbContext`, per-entity `IEntityTypeConfiguration` classes (auto-discovered via `ApplyConfigurationsFromAssembly`), EF migrations, and the Excel/JSON `ExportService`. Also contains `WireService` and `SampleBlockService` — both use a `(string dbPath, string dataDir)` constructor, no static state, directly testable by unit tests without the App layer.
- **Infrastructure** — data-directory selection, attachment storage, DB-path provider. Depends only on its own `Abstractions/` interfaces.
- **App** — WPF views + MVVM ViewModels + interaction services (order/customer/print/export orchestration).

### Wiring: no DI container

There is **no IoC container**. Dependencies are constructed by hand:

- `App.OnStartup` news up `DataDirectoryService` + `AppDbPathProvider`, resolves the data dir, runs migrations, then shows `MainWindow`.
- `MainWindow`'s constructor news up all the App-layer services and injects them into `MainWindowViewModel`.

`AppRuntimeContext` (App layer) is a **static service locator** initialized once at startup holding `DataDir` and `DbPath`. When adding a service that touches the DB, follow the existing pattern rather than introducing constructor-injected `DbContext`.

### DbContext lifecycle: one per operation

Services do **not** hold a long-lived `DbContext`. Each method opens a fresh short-lived context via `AppRuntimeContext.CreateDbContext()` inside an `await using`, and reads use `.AsNoTracking()`. Writes wrap `SaveChangesAsync` + explicit transaction. Keep this pattern — don't cache a context on a service instance.

### Startup & crash handling

`App` configures Serilog to an **emergency temp log dir** *before* the data dir is known, then re-points logging to `<dataDir>/logs` once resolved. Global handlers (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) route to `HandleFatal`, which flushes logs and shows a message box. `--force-crash` arg triggers a test crash path.

### Data directory

On first run, `DataDirectoryService` prompts (WinForms `FolderBrowserDialog`) for a data dir (warns on C:), persists the choice to `%APPDATA%/GlassFactoryBillTracker/settings.json`, and ensures `attachments/`, `exports/`, `logs/` subfolders + `billtracker.db`. Subsequent runs reuse the saved path.

## Amount calculation — READ BEFORE TOUCHING

`OrderAmountCalculator` (Domain) is the single source of truth for money math, and **the current code does NOT match the README's stated rules** (README §5 describes an older 4-decimal / `WireUnitPrice`-in-line-amount design). Trust the code, and treat the tests in `OrderAmountCalculatorTests` as the real spec:

- Area (㎡) = `(LengthMm / 1000) * (WidthMm / 1000)`.
- Displayed line area / square count (㎡) = `Area * Quantity`; order and print area totals use this quantity-adjusted value.
- Glass cost = `Area * Quantity * GlassUnitPricePerM2`.
- Line amount = `GlassCost + HoleFee + OtherFee`. Note `WireUnitPrice` is stored on `OrderItem` but is **not** part of the amount.
- `RoundAmount` rounds to **0 decimals** (whole yuan) `AwayFromZero`; `Round` rounds to 2. Order total = sum of per-line rounded amounts, rounded again.

When aggregating totals in EF queries, note the deliberate scaled-`long` trick (`SumAsync(x => (long)(x.TotalAmount * 10000m))`) used to avoid SQLite decimal-aggregation issues — mirror it rather than summing `decimal` directly.

### Wire / SampleBlock subsystem

`Wire` and `SampleBlock` entities are defined in the **Domain** layer. Their services (`WireService`, `SampleBlockService`) live in the **Data** layer — not App — using a `(string dbPath, string dataDir)` constructor so they can be exercised by unit tests directly (same pattern as `ExportService`).

**丝 and 样块 are independent master-data records; they are not linked to each other and neither is connected to orders.**

- **Wire** fields: `Model` (unique), `Price`, `PurchaseDate`, `Note`. Attachments stored in `attachments/wires/<id>/` via the `WireAttachments` table (FK Cascade — deleting a Wire deletes its attachment rows and the on-disk directory).
- **SampleBlock** fields: `Model` (unique), `Customer`, `OrderTime`, `Note`. Attachments stored in `attachments/sampleblocks/<id>/` via the `SampleBlockAttachments` table (same Cascade + directory-cleanup pattern).
- `WireService.GetWiresAsync` accepts a `WireFilter` with fields: `Model`, `PriceMin`, `PriceMax`, `PurchaseFrom`, `PurchaseTo`, `Note`.
- `SampleBlockService.GetSampleBlocksAsync` accepts a `SampleBlockFilter` with fields: `Model`, `Customer`, `OrderFrom`, `OrderTo`, `Note`.
- `OrderItem` no longer has a `SampleBlockModel` column (dropped in migration `RemoveOrderItemSampleBlockModel`). `WireType` and `WireUnitPrice` remain on `OrderItem` as legacy snapshot fields but **do not affect the amount formula**.
- Print output (both dot-matrix and A4) includes a footer line: `总数量：X    总方数：Y    合计：Z`.

Two management windows — 丝管理 and 样块管理 — are opened from buttons on the main-window toolbar.

### Other domain conventions

- `OrderNo` format is `yyyyMMdd-NNNN`, sequential per day, generated in `OrderService.GenerateOrderNoAsync`.
- Order items are ordered by `SortIndex` then `Id`; `SortIndex` is assigned from list position on save.
- Deleting an order also recursively deletes its `attachments/<OrderNo>/` folder; failures are logged but non-fatal.
- Deleting a customer with existing orders is blocked at the DB level (`Restrict`).
