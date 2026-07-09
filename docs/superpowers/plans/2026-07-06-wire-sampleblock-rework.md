# 丝/样块改造 + 订单去集成 + 打印合计 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 丝/样块 改成两个独立主数据表（各带多图附件 + 筛选），订单不再引用样块，打印底部增加数量/方数合计。

**Architecture:** 沿用现有分层与模式（无 DI 容器、DbContext-per-op、`IEntityTypeConfiguration<T>` 自动发现、启动 `Database.Migrate()`、Data 层服务用构造参数）。丝/样块附件仿 `OrderAttachment`：独立附件表 + 文件存 `attachments/wires/<id>/`、`attachments/sampleblocks/<id>/`；缩略图/查看器复用 `PathToImageSourceConverter` + `ImageViewWindow`。`WireService`/`SampleBlockService` 构造改为 `(dbPath, dataDir)`。

**Tech Stack:** .NET 8、WPF、EF Core 8.0.12(SQLite)、xUnit、`dotnet-ef` 10.0.3。

## Global Constraints

- 目标框架：Domain/Data/Tests=`net8.0`；App=`net8.0-windows10.0.19041.0`（`EnableWindowsTargeting=true`，Linux 可 build，WPF 仅 Windows 运行）。
- 包版本集中在 `Directory.Packages.props`；不新增包。
- 订单金额公式**不得改动**（`OrderAmountCalculator` 现有方法不变）。
- decimal 用 `HasPrecision(18,4)`；每实体一个 `IEntityTypeConfiguration<T>`；关系单侧配置。
- 丝/样块附件表 FK `OnDelete(Cascade)`；删除记录时同时删磁盘目录。
- 附件 `RelativePath` 相对 dataDir、正斜杠；复制文件重名加 `yyyyMMddHHmmssfff` 时间戳。
- 进货时间/订单时间：`DateTime?`，仅日期语义（`DatePicker`，时间 00:00，保存时取 `.Date`）。
- 样块"客户"=自由文本字符串。
- 迁移经 `DesignTimeDbContextFactory`（env `BILLTRACKER_DATA_DIR`）生成，`--project`/`--startup-project` 均指向 Data 项目。
- UI 文案/异常消息中文；C# 字符串用直引号。
- 可单测任务走 TDD；纯 WPF(App) 任务用 `dotnet build` 编译验证。
- **构建范围注意（重要）**：Wire/SampleBlock 实体改形后，App 的对应管理窗口会**暂时**引用已删成员，直到该子系统的"窗口任务"修好。因此 Data 层任务用 `dotnet test`（只编译 Domain/Data/Tests，不编译 App），迁移任务用**只构建 Data 项目**，整解决方案 `dotnet build sln` 只在 App 一致的任务里跑（Task 1/2/5/8/9/10）。

**常用命令**
- 全解构建：`dotnet build GlassFactory.BillTracker.sln -c Debug`
- 只构建 Data：`dotnet build src/GlassFactory.BillTracker.Data/GlassFactory.BillTracker.Data.csproj -c Debug`
- 全部测试：`dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug`
- 单测：`... --filter "FullyQualifiedName~<name>"`

---

# 阶段 A — 订单去集成

## Task 1: 从订单侧移除样块

**Files:**
- Modify: `src/GlassFactory.BillTracker.Domain/Entities/OrderItem.cs`
- Modify: `src/GlassFactory.BillTracker.Data/Persistence/Configurations/OrderItemConfiguration.cs`
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/Rows/OrderItemRowViewModel.cs`
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/OrderEditViewModel.cs`
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/GlassFactory.BillTracker.App/Services/OrderService.cs`
- Modify: `src/GlassFactory.BillTracker.App/Views/OrderEditWindow.xaml`

**Interfaces:**
- Produces: `OrderEditViewModel(IReadOnlyList<Customer> customers, string orderNo, Order? existing = null)`; `OrderItemRowViewModel(Action? recalculateCallback = null)`.

- [ ] **Step 1: OrderItem.cs** — remove the `public string? SampleBlockModel { get; set; }` property.

- [ ] **Step 2: OrderItemConfiguration.cs** — remove `builder.Property(x => x.SampleBlockModel).HasMaxLength(100);`. Keep both `HasIndex` lines + their comment.

- [ ] **Step 3: OrderItemRowViewModel.cs** — remove `_sampleBlockModel` field, `_sampleBlockResolver` field, `SampleBlockModel` property, and the 2nd ctor param (revert to `public OrderItemRowViewModel(Action? recalculateCallback = null)` storing `_recalculateCallback`). In `FromEntity`/`CloneForCopy` remove the `sampleBlockResolver` parameter + the `SampleBlockModel = ...` initializer line; in `ToEntity` remove the `SampleBlockModel = ...` line. Keep `AreaM2`.

- [ ] **Step 4: OrderEditViewModel.cs** — remove the `IReadOnlyList<SampleBlock> sampleBlocks` ctor param, `_sampleBlocks` field, `SampleBlockModels` property, `ResolveSampleBlock` method. At every row-VM creation site drop the resolver arg: `new OrderItemRowViewModel(RecalculateTotal)`, `OrderItemRowViewModel.FromEntity(item, RecalculateTotal)`, `rowToCopy.CloneForCopy(RecalculateTotal)`.

- [ ] **Step 5: MainWindowViewModel.cs** — in `OpenOrderDialogAsync` remove the `GetSampleBlocksAsync()` load and revert to `var vm = new OrderEditViewModel(customerEntities, orderNo, existingOrder);`. Leave `_sampleBlockService` (still used by `OpenSampleBlockManagement`).

- [ ] **Step 6: OrderService.cs** — remove the two `SampleBlockModel = ...` lines from both `new OrderItem { ... }` initializers, and in `ApplyIncomingItemIfChanged` remove the `SampleBlockModel` comparison line and the `trackedItem.SampleBlockModel = ...` assignment.

- [ ] **Step 7: OrderEditWindow.xaml** — remove the `样块型号` `DataGridTemplateColumn`, the `丝` column (`WireType`), the `价格(参考)` column (`WireUnitPrice`). Keep the `平方(㎡)` column + 总计 row. Change `Width="1300"` to `Width="1100"`.

- [ ] **Step 8: Build (whole solution — App is consistent here)**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug`
Expected: Build succeeded, 0 errors.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "refactor(app): remove sample-block selection from order editor"
```

---

## Task 2: 迁移 RemoveOrderItemSampleBlockModel

**Files:** Create (generated) `src/GlassFactory.BillTracker.Data/Migrations/*_RemoveOrderItemSampleBlockModel.cs`

- [ ] **Step 1: Generate**

```bash
BILLTRACKER_DATA_DIR=/tmp/bt-ef dotnet ef migrations add RemoveOrderItemSampleBlockModel \
  --project src/GlassFactory.BillTracker.Data --startup-project src/GlassFactory.BillTracker.Data
```

- [ ] **Step 2: Verify** — `Up` must ONLY `DropColumn("SampleBlockModel", "OrderItems")`; no ops on other tables/indexes. If unexpected, report BLOCKED.

- [ ] **Step 3: Build + test**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug && dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug`
Expected: Build succeeded; all tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/GlassFactory.BillTracker.Data/Migrations/
git commit -m "feat(data): drop OrderItems.SampleBlockModel column"
```

---

# 阶段 B — 丝管理改造

## Task 3: 丝 Data 层（实体 + 附件 + 配置 + 过滤 + 服务）

> 一次性改完 Data 层里所有与丝相关的代码（实体改形会牵连同项目的服务，必须同任务完成，Data 才能编译）。

**Files:**
- Modify: `src/GlassFactory.BillTracker.Domain/Entities/Wire.cs` (replace)
- Create: `src/GlassFactory.BillTracker.Domain/Entities/WireAttachment.cs`
- Modify: `src/GlassFactory.BillTracker.Data/Persistence/Configurations/WireConfiguration.cs` (replace)
- Create: `src/GlassFactory.BillTracker.Data/Persistence/Configurations/WireAttachmentConfiguration.cs`
- Modify: `src/GlassFactory.BillTracker.Data/Persistence/BillTrackerDbContext.cs`
- Create: `src/GlassFactory.BillTracker.Data/Services/WireFilter.cs`
- Modify: `src/GlassFactory.BillTracker.Data/Services/IWireService.cs` (replace)
- Modify: `src/GlassFactory.BillTracker.Data/Services/WireService.cs` (replace)
- Modify: `tests/GlassFactory.BillTracker.Tests/WireMappingTests.cs` (replace)
- Modify: `tests/GlassFactory.BillTracker.Tests/WireServiceTests.cs` (replace)

**Interfaces:**
- Produces: `Wire { Guid Id; string Model; decimal Price; DateTime? PurchaseDate; string? Note; DateTime CreatedAt/UpdatedAt; ICollection<WireAttachment> Attachments; ICollection<SampleBlock> SampleBlocks }` (SampleBlocks kept until Task 6); `WireAttachment { Guid Id; Guid WireId; Wire Wire; string RelativePath; DateTime CreatedAt }`; `DbSet<WireAttachment> WireAttachments`; `WireFilter { string? Model; decimal? PriceMin; decimal? PriceMax; DateTime? PurchaseFrom; DateTime? PurchaseTo; string? Note }`; `WireService(string dbPath, string dataDir)` with `GetWiresAsync(WireFilter?)`, `GetByIdAsync`, `SaveAsync`, `DeleteAsync`, `AddAttachmentAsync(Guid,string)`, `RemoveAttachmentAsync(Guid)`.

- [ ] **Step 1: Replace WireMappingTests.cs**

```csharp
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class WireMappingTests
{
    private static BillTrackerDbContext NewDb()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Wire_PersistsPurchaseDateAndAttachments_CascadeDelete()
    {
        await using var db = NewDb();
        var wire = new Wire { Model = "W-100", Price = 12.5m, PurchaseDate = new DateTime(2026, 3, 1), Note = "n" };
        wire.Attachments.Add(new WireAttachment { RelativePath = "attachments/wires/x/a.png" });
        db.Wires.Add(wire);
        await db.SaveChangesAsync();

        var reloaded = await db.Wires.AsNoTracking().Include(x => x.Attachments).SingleAsync(x => x.Model == "W-100");
        Assert.Equal(new DateTime(2026, 3, 1), reloaded.PurchaseDate);
        Assert.Single(reloaded.Attachments);

        db.Wires.Remove(await db.Wires.SingleAsync(x => x.Id == wire.Id));
        await db.SaveChangesAsync();
        Assert.Empty(await db.WireAttachments.ToListAsync());
    }
}
```

- [ ] **Step 2: Replace WireServiceTests.cs**

```csharp
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class WireServiceTests
{
    private static (string dbPath, string dataDir) NewEnv()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        var dataDir = Path.Combine(dir, "data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        using var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return (dbPath, dataDir);
    }

    [Fact]
    public async Task SaveAsync_RejectsDuplicateModel()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new WireService(dbPath, dataDir);
        await svc.SaveAsync(new Wire { Model = "W-1", Price = 1m });
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SaveAsync(new Wire { Model = "W-1", Price = 2m }));
    }

    [Fact]
    public async Task GetWiresAsync_FiltersByModelPriceDateNote()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new WireService(dbPath, dataDir);
        await svc.SaveAsync(new Wire { Model = "钢丝A", Price = 10m, PurchaseDate = new DateTime(2026, 1, 10), Note = "红色" });
        await svc.SaveAsync(new Wire { Model = "铜丝B", Price = 50m, PurchaseDate = new DateTime(2026, 6, 20), Note = "蓝色" });

        Assert.Single(await svc.GetWiresAsync(new WireFilter { Model = "钢丝" }));
        Assert.Single(await svc.GetWiresAsync(new WireFilter { PriceMin = 30m }));
        Assert.Single(await svc.GetWiresAsync(new WireFilter { PriceMin = 5m, PriceMax = 20m }));
        Assert.Single(await svc.GetWiresAsync(new WireFilter { PurchaseFrom = new DateTime(2026, 6, 1) }));
        Assert.Single(await svc.GetWiresAsync(new WireFilter { Note = "蓝" }));
        Assert.Equal(2, (await svc.GetWiresAsync()).Count);
    }

    [Fact]
    public async Task Attachment_AddThenRemove_CopiesAndDeletesFileAndRow()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new WireService(dbPath, dataDir);
        var wire = await svc.SaveAsync(new Wire { Model = "W-A", Price = 1m });
        var src = Path.Combine(dataDir, "src.png");
        await File.WriteAllBytesAsync(src, new byte[] { 1, 2, 3 });

        var att = await svc.AddAttachmentAsync(wire.Id, src);
        var abs = Path.Combine(dataDir, att.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(abs));
        Assert.StartsWith("attachments/wires/", att.RelativePath);
        Assert.Single((await svc.GetByIdAsync(wire.Id))!.Attachments);

        await svc.RemoveAttachmentAsync(att.Id);
        Assert.False(File.Exists(abs));
        Assert.Empty((await svc.GetByIdAsync(wire.Id))!.Attachments);
    }
}
```

- [ ] **Step 3: Run → RED**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~Wire"`
Expected: compile failure (WireAttachment/PurchaseDate/WireFilter/new ctor missing).

- [ ] **Step 4: Wire.cs** — replace:

```csharp
namespace GlassFactory.BillTracker.Domain.Entities;

public class Wire
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Model { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ICollection<WireAttachment> Attachments { get; set; } = new List<WireAttachment>();
    // Kept until the sample-block decoupling task (Task 6); removed there.
    public ICollection<SampleBlock> SampleBlocks { get; set; } = new List<SampleBlock>();
}
```

- [ ] **Step 5: WireAttachment.cs** — create:

```csharp
namespace GlassFactory.BillTracker.Domain.Entities;

public class WireAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WireId { get; set; }
    public Wire Wire { get; set; } = null!;
    public string RelativePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
```

- [ ] **Step 6: WireConfiguration.cs** — replace:

```csharp
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class WireConfiguration : IEntityTypeConfiguration<Wire>
{
    public void Configure(EntityTypeBuilder<Wire> builder)
    {
        builder.ToTable("Wires");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Model).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Price).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.HasIndex(x => x.Model).IsUnique();

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.Wire)
            .HasForeignKey(x => x.WireId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kept until the sample-block decoupling task (Task 6).
        builder.HasMany(x => x.SampleBlocks)
            .WithOne(x => x.Wire)
            .HasForeignKey(x => x.WireId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 7: WireAttachmentConfiguration.cs** — create:

```csharp
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class WireAttachmentConfiguration : IEntityTypeConfiguration<WireAttachment>
{
    public void Configure(EntityTypeBuilder<WireAttachment> builder)
    {
        builder.ToTable("WireAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RelativePath).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.WireId);
    }
}
```

- [ ] **Step 8: BillTrackerDbContext.cs** — add after the `Wires` DbSet: `public DbSet<WireAttachment> WireAttachments => Set<WireAttachment>();`

- [ ] **Step 9: WireFilter.cs** — create:

```csharp
namespace GlassFactory.BillTracker.Data.Services;

public sealed class WireFilter
{
    public string? Model { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public DateTime? PurchaseFrom { get; set; }
    public DateTime? PurchaseTo { get; set; }
    public string? Note { get; set; }
}
```

- [ ] **Step 10: IWireService.cs** — replace:

```csharp
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Data.Services;

public interface IWireService
{
    Task<List<Wire>> GetWiresAsync(WireFilter? filter = null, CancellationToken cancellationToken = default);
    Task<Wire?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wire> SaveAsync(Wire wire, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WireAttachment> AddAttachmentAsync(Guid wireId, string sourcePath, CancellationToken cancellationToken = default);
    Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 11: WireService.cs** — replace:

```csharp
using System.IO;
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Data.Services;

public sealed class WireService : IWireService
{
    private readonly string _dbPath;
    private readonly string _dataDir;

    public WireService(string dbPath, string dataDir)
    {
        _dbPath = dbPath;
        _dataDir = dataDir;
    }

    private BillTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        return new BillTrackerDbContext(options);
    }

    public async Task<List<Wire>> GetWiresAsync(WireFilter? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new WireFilter();
        await using var db = CreateDbContext();
        var query = db.Wires.AsNoTracking().Include(x => x.Attachments).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Model))
        {
            var k = filter.Model.Trim();
            query = query.Where(x => x.Model.Contains(k));
        }
        if (filter.PriceMin.HasValue) query = query.Where(x => x.Price >= filter.PriceMin.Value);
        if (filter.PriceMax.HasValue) query = query.Where(x => x.Price <= filter.PriceMax.Value);
        if (filter.PurchaseFrom.HasValue) query = query.Where(x => x.PurchaseDate != null && x.PurchaseDate >= filter.PurchaseFrom.Value);
        if (filter.PurchaseTo.HasValue) query = query.Where(x => x.PurchaseDate != null && x.PurchaseDate <= filter.PurchaseTo.Value);
        if (!string.IsNullOrWhiteSpace(filter.Note))
        {
            var n = filter.Note.Trim();
            query = query.Where(x => (x.Note ?? string.Empty).Contains(n));
        }
        return await query.OrderBy(x => x.Model).ToListAsync(cancellationToken);
    }

    public async Task<Wire?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.Wires.AsNoTracking().Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Wire> SaveAsync(Wire wire, CancellationToken cancellationToken = default)
    {
        var normalizedModel = (wire.Model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
            throw new InvalidOperationException("丝型号不能为空。");

        await using var db = CreateDbContext();
        var id = wire.Id == Guid.Empty ? Guid.NewGuid() : wire.Id;
        var duplicate = await db.Wires.AsNoTracking().AnyAsync(x => x.Model == normalizedModel && x.Id != id, cancellationToken);
        if (duplicate) throw new InvalidOperationException("丝型号已存在，请使用其他型号。");

        var note = string.IsNullOrWhiteSpace(wire.Note) ? null : wire.Note.Trim();
        var now = DateTime.Now;
        var existing = await db.Wires.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            var created = new Wire { Id = id, Model = normalizedModel, Price = wire.Price, PurchaseDate = wire.PurchaseDate, Note = note, CreatedAt = now, UpdatedAt = now };
            await db.Wires.AddAsync(created, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        existing.Model = normalizedModel;
        existing.Price = wire.Price;
        existing.PurchaseDate = wire.PurchaseDate;
        existing.Note = note;
        existing.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var entity = await db.Wires.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("丝不存在。");
        db.Wires.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        var dir = Path.Combine(_dataDir, "attachments", "wires", id.ToString());
        if (Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* non-fatal */ }
        }
    }

    public async Task<WireAttachment> AddAttachmentAsync(Guid wireId, string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("附件文件不存在。", sourcePath);
        await using var db = CreateDbContext();
        _ = await db.Wires.FirstOrDefaultAsync(x => x.Id == wireId, cancellationToken)
            ?? throw new InvalidOperationException("丝不存在，无法添加附件。");

        var dir = Path.Combine(_dataDir, "attachments", "wires", wireId.ToString());
        Directory.CreateDirectory(dir);
        var fileName = Path.GetFileName(sourcePath);
        var target = Path.Combine(dir, fileName);
        if (File.Exists(target))
        {
            var unique = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddHHmmssfff}{Path.GetExtension(fileName)}";
            target = Path.Combine(dir, unique);
        }
        File.Copy(sourcePath, target, overwrite: false);

        var rel = Path.GetRelativePath(_dataDir, target).Replace('\\', '/');
        var att = new WireAttachment { Id = Guid.NewGuid(), WireId = wireId, RelativePath = rel, CreatedAt = DateTime.Now };
        await db.WireAttachments.AddAsync(att, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return att;
    }

    public async Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var att = await db.WireAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId, cancellationToken)
            ?? throw new InvalidOperationException("附件记录不存在。");
        var abs = Path.Combine(_dataDir, att.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(abs)) File.Delete(abs);
        db.WireAttachments.Remove(att);
        await db.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 12: Run → GREEN**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug`
Expected: all pass. (This builds Domain/Data/Tests only — App is intentionally not built yet; its wire window is fixed in Task 5.)

- [ ] **Step 13: Commit**

```bash
git add -A
git commit -m "feat(data): reshape Wire data layer (PurchaseDate, attachments, filter service)"
```

---

## Task 4: 迁移 ReworkWire

**Files:** Create (generated) `src/GlassFactory.BillTracker.Data/Migrations/*_ReworkWire.cs`

- [ ] **Step 1: Generate**

```bash
BILLTRACKER_DATA_DIR=/tmp/bt-ef dotnet ef migrations add ReworkWire \
  --project src/GlassFactory.BillTracker.Data --startup-project src/GlassFactory.BillTracker.Data
```

- [ ] **Step 2: Verify** — `Up` only: `DropColumn Wires.Manufacturer`; `AddColumn Wires.PurchaseDate` (TEXT null); `CreateTable WireAttachments` (FK→Wires Cascade, index WireId). NO other-table ops; the SampleBlock↔Wire FK untouched. If unexpected, report BLOCKED.

- [ ] **Step 3: Build Data + test** (App is inconsistent until Task 5 — do NOT build the whole solution here)

Run: `dotnet build src/GlassFactory.BillTracker.Data/GlassFactory.BillTracker.Data.csproj -c Debug && dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug`
Expected: Build succeeded; tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/GlassFactory.BillTracker.Data/Migrations/
git commit -m "feat(data): migration ReworkWire (PurchaseDate + WireAttachments)"
```

---

## Task 5: 丝管理窗口重塑 + 共享附件行 VM + 装配

**Files:**
- Create: `src/GlassFactory.BillTracker.App/ViewModels/Rows/ManagedAttachmentViewModel.cs`
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/WireManagementViewModel.cs` (replace)
- Modify: `src/GlassFactory.BillTracker.App/Views/WireManagementWindow.xaml` (replace)
- Modify: `src/GlassFactory.BillTracker.App/Views/WireManagementWindow.xaml.cs` (replace)
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs` (`OpenWireManagement`)
- Modify: `src/GlassFactory.BillTracker.App/Views/MainWindow.xaml.cs` (WireService ctor)

**Interfaces:**
- Consumes: `IWireService`, `WireFilter`, `Wire`, `WireAttachment` (Task 3); `ImageViewWindow(string)`, `PathToImageSourceConverter`.
- Produces: `ManagedAttachmentViewModel { Guid? AttachmentId; string? RelativePath; string? SourcePath; string? AbsolutePath; string DisplayName; bool IsPersisted }`.

- [ ] **Step 1: ManagedAttachmentViewModel.cs** — create:

```csharp
using System.IO;

namespace GlassFactory.BillTracker.App.ViewModels.Rows;

public sealed class ManagedAttachmentViewModel
{
    public Guid? AttachmentId { get; init; }
    public string? RelativePath { get; init; }
    public string? SourcePath { get; init; }

    public string? AbsolutePath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SourcePath) && File.Exists(SourcePath)) return SourcePath;
            if (string.IsNullOrWhiteSpace(RelativePath)) return null;
            var abs = Path.Combine(Services.AppRuntimeContext.DataDir, RelativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(abs) ? abs : null;
        }
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SourcePath)) return Path.GetFileName(SourcePath);
            if (!string.IsNullOrWhiteSpace(RelativePath)) return Path.GetFileName(RelativePath);
            return "未命名附件";
        }
    }

    public bool IsPersisted => AttachmentId.HasValue && AttachmentId.Value != Guid.Empty;
}
```

- [ ] **Step 2: WireManagementViewModel.cs** — replace:

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.App.ViewModels.Rows;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class WireManagementViewModel : ObservableObject
{
    private readonly IWireService _wireService;

    private Guid _editingId;
    private string _model = string.Empty;
    private string? _priceText;
    private DateTime? _purchaseDate;
    private string? _note;
    private Wire? _selectedWire;

    private string? _filterModel;
    private string? _filterPriceMin;
    private string? _filterPriceMax;
    private DateTime? _filterFrom;
    private DateTime? _filterTo;
    private string? _filterNote;

    private readonly List<string> _newAttachmentPaths = new();
    private readonly List<Guid> _removedAttachmentIds = new();

    public ObservableCollection<Wire> Wires { get; } = new();
    public ObservableCollection<ManagedAttachmentViewModel> Attachments { get; } = new();

    public WireManagementViewModel(IWireService wireService)
    {
        _wireService = wireService;
        NewCommand = new RelayCommand(ResetForm);
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        DeleteCommand = new RelayCommand(() => _ = DeleteAsync());
        SearchCommand = new RelayCommand(() => _ = LoadAsync());
        ClearFilterCommand = new RelayCommand(ClearFilter);
        AddImageCommand = new RelayCommand(AddImages);
        RemoveImageCommand = new RelayCommand<ManagedAttachmentViewModel>(RemoveImage);
        _ = LoadAsync();
    }

    public string? FilterModel { get => _filterModel; set => SetProperty(ref _filterModel, value); }
    public string? FilterPriceMin { get => _filterPriceMin; set => SetProperty(ref _filterPriceMin, value); }
    public string? FilterPriceMax { get => _filterPriceMax; set => SetProperty(ref _filterPriceMax, value); }
    public DateTime? FilterFrom { get => _filterFrom; set => SetProperty(ref _filterFrom, value); }
    public DateTime? FilterTo { get => _filterTo; set => SetProperty(ref _filterTo, value); }
    public string? FilterNote { get => _filterNote; set => SetProperty(ref _filterNote, value); }

    public Wire? SelectedWire
    {
        get => _selectedWire;
        set { if (SetProperty(ref _selectedWire, value) && value is not null) LoadForm(value); }
    }

    public string Model { get => _model; set => SetProperty(ref _model, value); }
    public string? PriceText { get => _priceText; set => SetProperty(ref _priceText, value); }
    public DateTime? PurchaseDate { get => _purchaseDate; set => SetProperty(ref _purchaseDate, value); }
    public string? Note { get => _note; set => SetProperty(ref _note, value); }

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFilterCommand { get; }
    public RelayCommand AddImageCommand { get; }
    public RelayCommand<ManagedAttachmentViewModel> RemoveImageCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            var filter = new WireFilter
            {
                Model = FilterModel,
                PriceMin = ParseDecimal(FilterPriceMin),
                PriceMax = ParseDecimal(FilterPriceMax),
                PurchaseFrom = FilterFrom?.Date,
                PurchaseTo = FilterTo?.Date,
                Note = FilterNote
            };
            var items = await _wireService.GetWiresAsync(filter);
            Wires.Clear();
            foreach (var w in items) Wires.Add(w);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载丝列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearFilter()
    {
        FilterModel = null; FilterPriceMin = null; FilterPriceMax = null;
        FilterFrom = null; FilterTo = null; FilterNote = null;
        _ = LoadAsync();
    }

    private void LoadForm(Wire wire)
    {
        _editingId = wire.Id;
        Model = wire.Model;
        PriceText = wire.Price.ToString(CultureInfo.InvariantCulture);
        PurchaseDate = wire.PurchaseDate;
        Note = wire.Note;
        _newAttachmentPaths.Clear();
        _removedAttachmentIds.Clear();
        Attachments.Clear();
        foreach (var a in wire.Attachments)
            Attachments.Add(new ManagedAttachmentViewModel { AttachmentId = a.Id, RelativePath = a.RelativePath });
    }

    private void ResetForm()
    {
        _editingId = Guid.Empty;
        Model = string.Empty; PriceText = "0"; PurchaseDate = null; Note = null;
        SelectedWire = null;
        _newAttachmentPaths.Clear();
        _removedAttachmentIds.Clear();
        Attachments.Clear();
    }

    private void AddImages()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|所有文件|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;
        foreach (var path in dialog.FileNames)
        {
            if (!File.Exists(path)) continue;
            _newAttachmentPaths.Add(path);
            Attachments.Add(new ManagedAttachmentViewModel { SourcePath = path });
        }
    }

    private void RemoveImage(ManagedAttachmentViewModel? att)
    {
        if (att is null) return;
        if (att.IsPersisted && att.AttachmentId.HasValue) _removedAttachmentIds.Add(att.AttachmentId.Value);
        else if (!string.IsNullOrWhiteSpace(att.SourcePath)) _newAttachmentPaths.Remove(att.SourcePath);
        Attachments.Remove(att);
    }

    private async Task SaveAsync()
    {
        var price = ParseDecimal(PriceText);
        if (price is null || price < 0)
        {
            MessageBox.Show("价格必须为非负数字。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var saved = await _wireService.SaveAsync(new Wire
            {
                Id = _editingId, Model = Model, Price = price.Value, PurchaseDate = PurchaseDate?.Date, Note = Note
            });
            foreach (var rid in _removedAttachmentIds) await _wireService.RemoveAttachmentAsync(rid);
            foreach (var p in _newAttachmentPaths) await _wireService.AddAttachmentAsync(saved.Id, p);
            await LoadAsync();
            SelectedWire = Wires.FirstOrDefault(x => x.Id == saved.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DeleteAsync()
    {
        if (_editingId == Guid.Empty)
        {
            MessageBox.Show("请先在列表中选择要删除的丝。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"确认删除丝 \"{Model}\" 吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            await _wireService.DeleteAsync(_editingId);
            ResetForm();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
```

- [ ] **Step 3: WireManagementWindow.xaml** — replace:

```xml
<Window x:Class="GlassFactory.BillTracker.App.Views.WireManagementWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:conv="clr-namespace:GlassFactory.BillTracker.App.Converters"
        Title="丝管理" Width="1000" Height="620" WindowStartupLocation="CenterOwner">
    <Window.Resources>
        <conv:PathToImageSourceConverter x:Key="PathToImageSourceConverter" />
    </Window.Resources>
    <DockPanel Margin="10">
        <Border DockPanel.Dock="Top" BorderBrush="#DDD" BorderThickness="1" Padding="8" Margin="0,0,0,8">
            <WrapPanel>
                <TextBlock Text="型号" VerticalAlignment="Center" Margin="0,0,4,0" />
                <TextBox Width="120" Text="{Binding FilterModel, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,12,0" />
                <TextBlock Text="价格" VerticalAlignment="Center" Margin="0,0,4,0" />
                <TextBox Width="70" Text="{Binding FilterPriceMin, UpdateSourceTrigger=PropertyChanged}" />
                <TextBlock Text="~" VerticalAlignment="Center" Margin="4,0" />
                <TextBox Width="70" Text="{Binding FilterPriceMax, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,12,0" />
                <TextBlock Text="进货时间" VerticalAlignment="Center" Margin="0,0,4,0" />
                <DatePicker Width="120" SelectedDate="{Binding FilterFrom}" />
                <TextBlock Text="~" VerticalAlignment="Center" Margin="4,0" />
                <DatePicker Width="120" SelectedDate="{Binding FilterTo}" Margin="0,0,12,0" />
                <TextBlock Text="备注" VerticalAlignment="Center" Margin="0,0,4,0" />
                <TextBox Width="120" Text="{Binding FilterNote, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,12,0" />
                <Button Content="查询" Command="{Binding SearchCommand}" Width="60" Margin="0,0,8,0" />
                <Button Content="清除" Command="{Binding ClearFilterCommand}" Width="60" Margin="0,0,8,0" />
                <Button Content="新增" Command="{Binding NewCommand}" Width="60" />
            </WrapPanel>
        </Border>

        <Border DockPanel.Dock="Right" BorderBrush="#DDD" BorderThickness="1" Padding="10" Margin="8,0,0,0" Width="320">
            <StackPanel>
                <TextBlock Text="型号 *" Margin="0,0,0,2" />
                <TextBox Text="{Binding Model, UpdateSourceTrigger=PropertyChanged}" MaxLength="100" Margin="0,0,0,8" />
                <TextBlock Text="价格" Margin="0,0,0,2" />
                <TextBox Text="{Binding PriceText, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,0,8" />
                <TextBlock Text="进货时间" Margin="0,0,0,2" />
                <DatePicker SelectedDate="{Binding PurchaseDate}" Margin="0,0,0,8" />
                <TextBlock Text="备注" Margin="0,0,0,2" />
                <TextBox Text="{Binding Note, UpdateSourceTrigger=PropertyChanged}" AcceptsReturn="True" Height="60" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto" Margin="0,0,0,8" />
                <DockPanel Margin="0,0,0,4">
                    <TextBlock Text="附件（双击放大）" FontWeight="Bold" />
                    <Button Content="添加图片" Command="{Binding AddImageCommand}" HorizontalAlignment="Right" Width="80" />
                </DockPanel>
                <ListBox x:Name="AttachmentListBox" ItemsSource="{Binding Attachments}" Height="150"
                         ScrollViewer.HorizontalScrollBarVisibility="Auto"
                         MouseDoubleClick="AttachmentListBox_MouseDoubleClick">
                    <ListBox.ItemsPanel>
                        <ItemsPanelTemplate><WrapPanel Orientation="Horizontal" /></ItemsPanelTemplate>
                    </ListBox.ItemsPanel>
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <StackPanel Width="92" Margin="0,0,6,6">
                                <Border BorderBrush="#CCC" BorderThickness="1" Padding="2">
                                    <Image Width="84" Height="60" Stretch="UniformToFill"
                                           Source="{Binding AbsolutePath, Converter={StaticResource PathToImageSourceConverter}}" />
                                </Border>
                                <Button Content="移除" Command="{Binding DataContext.RemoveImageCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                        CommandParameter="{Binding}" Margin="0,2,0,0" />
                            </StackPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
                <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                    <Button Content="保存" Command="{Binding SaveCommand}" Width="90" Margin="0,0,8,0" />
                    <Button Content="删除" Command="{Binding DeleteCommand}" Width="90" />
                </StackPanel>
            </StackPanel>
        </Border>

        <DataGrid ItemsSource="{Binding Wires}" SelectedItem="{Binding SelectedWire}"
                  AutoGenerateColumns="False" IsReadOnly="True" CanUserAddRows="False" SelectionMode="Single">
            <DataGrid.Columns>
                <DataGridTextColumn Header="型号" Binding="{Binding Model}" Width="140" />
                <DataGridTextColumn Header="价格" Binding="{Binding Price, StringFormat=F2}" Width="90" />
                <DataGridTextColumn Header="进货时间" Binding="{Binding PurchaseDate, StringFormat=yyyy-MM-dd}" Width="110" />
                <DataGridTextColumn Header="备注" Binding="{Binding Note}" Width="*" />
                <DataGridTextColumn Header="附件" Binding="{Binding Attachments.Count, StringFormat={}{0}张}" Width="60" />
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
```

- [ ] **Step 4: WireManagementWindow.xaml.cs** — replace:

```csharp
using System.Windows;
using GlassFactory.BillTracker.App.ViewModels.Rows;

namespace GlassFactory.BillTracker.App.Views;

public partial class WireManagementWindow : Window
{
    public WireManagementWindow()
    {
        InitializeComponent();
    }

    private void AttachmentListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (AttachmentListBox.SelectedItem is not ManagedAttachmentViewModel att || string.IsNullOrWhiteSpace(att.AbsolutePath)) return;
        new ImageViewWindow(att.AbsolutePath) { Owner = this }.ShowDialog();
    }
}
```

- [ ] **Step 5: MainWindowViewModel.cs** — update `OpenWireManagement` so it constructs `new WireManagementViewModel(_wireService)` (single arg — the sample-block reverse-lookup is gone).

- [ ] **Step 6: MainWindow.xaml.cs** — change the wire service construction to `new WireService(AppRuntimeContext.DbPath, AppRuntimeContext.DataDir)`. (Sample-block service ctor is updated in Task 8.)

- [ ] **Step 7: Build (whole solution — App wire side is now consistent; sample-block window still uses old shape → the solution still builds because Task 6 hasn't run. WAIT: verify order.)**

> **Ordering note:** Task 6 (sample-block Data reshape) runs AFTER this task. So at this point the SampleBlock entity is still the old shape (WireId/Price), and `SampleBlockManagementViewModel` (old) still compiles. The whole solution builds. Run:

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug`
Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(app): rework wire management window (filter + attachments)"
```

---

# 阶段 C — 样块管理改造（与丝解绑）

## Task 6: 样块 Data 层（实体 + 附件 + 配置 + 解绑丝 + 过滤 + 服务）

**Files:**
- Modify: `src/GlassFactory.BillTracker.Domain/Entities/SampleBlock.cs` (replace)
- Create: `src/GlassFactory.BillTracker.Domain/Entities/SampleBlockAttachment.cs`
- Modify: `src/GlassFactory.BillTracker.Domain/Entities/Wire.cs` (remove SampleBlocks nav)
- Modify: `src/GlassFactory.BillTracker.Data/Persistence/Configurations/SampleBlockConfiguration.cs` (replace)
- Create: `src/GlassFactory.BillTracker.Data/Persistence/Configurations/SampleBlockAttachmentConfiguration.cs`
- Modify: `src/GlassFactory.BillTracker.Data/Persistence/Configurations/WireConfiguration.cs` (remove SampleBlocks relationship)
- Modify: `src/GlassFactory.BillTracker.Data/Persistence/BillTrackerDbContext.cs`
- Create: `src/GlassFactory.BillTracker.Data/Services/SampleBlockFilter.cs`
- Modify: `src/GlassFactory.BillTracker.Data/Services/ISampleBlockService.cs` (replace)
- Modify: `src/GlassFactory.BillTracker.Data/Services/SampleBlockService.cs` (replace)
- Modify: `tests/GlassFactory.BillTracker.Tests/SampleBlockMappingTests.cs` (replace)
- Modify: `tests/GlassFactory.BillTracker.Tests/SampleBlockServiceTests.cs` (replace)

**Interfaces:**
- Produces: `SampleBlock { Guid Id; string Model; string? Customer; DateTime? OrderTime; string? Note; DateTime CreatedAt/UpdatedAt; ICollection<SampleBlockAttachment> Attachments }`; `SampleBlockAttachment { Guid Id; Guid SampleBlockId; SampleBlock SampleBlock; string RelativePath; DateTime CreatedAt }`; `DbSet<SampleBlockAttachment> SampleBlockAttachments`; `SampleBlockFilter { string? Model; string? Customer; DateTime? OrderFrom; DateTime? OrderTo; string? Note }`; `SampleBlockService(string dbPath, string dataDir)` with `GetSampleBlocksAsync(SampleBlockFilter?)`, `GetByIdAsync`, `SaveAsync`, `DeleteAsync`, `AddAttachmentAsync`, `RemoveAttachmentAsync`. `Wire.SampleBlocks` removed.

- [ ] **Step 1: Replace SampleBlockMappingTests.cs**

```csharp
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class SampleBlockMappingTests
{
    private static BillTrackerDbContext NewDb()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task SampleBlock_PersistsCustomerOrderTimeAndAttachments_CascadeDelete()
    {
        await using var db = NewDb();
        var sb = new SampleBlock { Model = "SB-1", Customer = "老王", OrderTime = new DateTime(2026, 5, 2), Note = "n" };
        sb.Attachments.Add(new SampleBlockAttachment { RelativePath = "attachments/sampleblocks/x/a.png" });
        db.SampleBlocks.Add(sb);
        await db.SaveChangesAsync();

        var reloaded = await db.SampleBlocks.AsNoTracking().Include(x => x.Attachments).SingleAsync(x => x.Model == "SB-1");
        Assert.Equal("老王", reloaded.Customer);
        Assert.Equal(new DateTime(2026, 5, 2), reloaded.OrderTime);
        Assert.Single(reloaded.Attachments);

        db.SampleBlocks.Remove(await db.SampleBlocks.SingleAsync(x => x.Id == sb.Id));
        await db.SaveChangesAsync();
        Assert.Empty(await db.SampleBlockAttachments.ToListAsync());
    }
}
```

- [ ] **Step 2: Replace SampleBlockServiceTests.cs**

```csharp
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class SampleBlockServiceTests
{
    private static (string dbPath, string dataDir) NewEnv()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        var dataDir = Path.Combine(dir, "data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        using var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return (dbPath, dataDir);
    }

    [Fact]
    public async Task SaveAsync_RejectsDuplicateModel()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new SampleBlockService(dbPath, dataDir);
        await svc.SaveAsync(new SampleBlock { Model = "SB-1" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SaveAsync(new SampleBlock { Model = "SB-1" }));
    }

    [Fact]
    public async Task GetSampleBlocksAsync_FiltersByModelCustomerOrderTimeNote()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new SampleBlockService(dbPath, dataDir);
        await svc.SaveAsync(new SampleBlock { Model = "样块甲", Customer = "老王", OrderTime = new DateTime(2026, 1, 5), Note = "红" });
        await svc.SaveAsync(new SampleBlock { Model = "样块乙", Customer = "老李", OrderTime = new DateTime(2026, 6, 5), Note = "蓝" });

        Assert.Single(await svc.GetSampleBlocksAsync(new SampleBlockFilter { Model = "甲" }));
        Assert.Single(await svc.GetSampleBlocksAsync(new SampleBlockFilter { Customer = "李" }));
        Assert.Single(await svc.GetSampleBlocksAsync(new SampleBlockFilter { OrderFrom = new DateTime(2026, 3, 1) }));
        Assert.Single(await svc.GetSampleBlocksAsync(new SampleBlockFilter { Note = "红" }));
        Assert.Equal(2, (await svc.GetSampleBlocksAsync()).Count);
    }

    [Fact]
    public async Task Attachment_AddThenRemove_CopiesAndDeletes()
    {
        var (dbPath, dataDir) = NewEnv();
        var svc = new SampleBlockService(dbPath, dataDir);
        var sb = await svc.SaveAsync(new SampleBlock { Model = "SB-A" });
        var src = Path.Combine(dataDir, "src.png");
        await File.WriteAllBytesAsync(src, new byte[] { 1, 2, 3 });

        var att = await svc.AddAttachmentAsync(sb.Id, src);
        var abs = Path.Combine(dataDir, att.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(abs));
        Assert.StartsWith("attachments/sampleblocks/", att.RelativePath);
        Assert.Single((await svc.GetByIdAsync(sb.Id))!.Attachments);

        await svc.RemoveAttachmentAsync(att.Id);
        Assert.False(File.Exists(abs));
        Assert.Empty((await svc.GetByIdAsync(sb.Id))!.Attachments);
    }
}
```

- [ ] **Step 3: Run → RED**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~SampleBlock"`
Expected: compile failure.

- [ ] **Step 4: SampleBlock.cs** — replace:

```csharp
namespace GlassFactory.BillTracker.Domain.Entities;

public class SampleBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Model { get; set; } = string.Empty;
    public string? Customer { get; set; }
    public DateTime? OrderTime { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ICollection<SampleBlockAttachment> Attachments { get; set; } = new List<SampleBlockAttachment>();
}
```

- [ ] **Step 5: SampleBlockAttachment.cs** — create:

```csharp
namespace GlassFactory.BillTracker.Domain.Entities;

public class SampleBlockAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SampleBlockId { get; set; }
    public SampleBlock SampleBlock { get; set; } = null!;
    public string RelativePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
```

- [ ] **Step 6: Wire.cs** — remove the `public ICollection<SampleBlock> SampleBlocks ...` line and its comment. Keep `Attachments`.

- [ ] **Step 7: WireConfiguration.cs** — remove the entire `builder.HasMany(x => x.SampleBlocks)...OnDelete(DeleteBehavior.Restrict);` block and its comment. Keep the `Attachments` relationship.

- [ ] **Step 8: SampleBlockConfiguration.cs** — replace:

```csharp
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class SampleBlockConfiguration : IEntityTypeConfiguration<SampleBlock>
{
    public void Configure(EntityTypeBuilder<SampleBlock> builder)
    {
        builder.ToTable("SampleBlocks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Model).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Customer).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.HasIndex(x => x.Model).IsUnique();

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.SampleBlock)
            .HasForeignKey(x => x.SampleBlockId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 9: SampleBlockAttachmentConfiguration.cs** — create:

```csharp
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlassFactory.BillTracker.Data.Persistence.Configurations;

public class SampleBlockAttachmentConfiguration : IEntityTypeConfiguration<SampleBlockAttachment>
{
    public void Configure(EntityTypeBuilder<SampleBlockAttachment> builder)
    {
        builder.ToTable("SampleBlockAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RelativePath).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.SampleBlockId);
    }
}
```

- [ ] **Step 10: BillTrackerDbContext.cs** — add after `SampleBlocks`: `public DbSet<SampleBlockAttachment> SampleBlockAttachments => Set<SampleBlockAttachment>();`

- [ ] **Step 11: SampleBlockFilter.cs** — create:

```csharp
namespace GlassFactory.BillTracker.Data.Services;

public sealed class SampleBlockFilter
{
    public string? Model { get; set; }
    public string? Customer { get; set; }
    public DateTime? OrderFrom { get; set; }
    public DateTime? OrderTo { get; set; }
    public string? Note { get; set; }
}
```

- [ ] **Step 12: ISampleBlockService.cs** — replace:

```csharp
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Data.Services;

public interface ISampleBlockService
{
    Task<List<SampleBlock>> GetSampleBlocksAsync(SampleBlockFilter? filter = null, CancellationToken cancellationToken = default);
    Task<SampleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SampleBlock> SaveAsync(SampleBlock sampleBlock, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SampleBlockAttachment> AddAttachmentAsync(Guid sampleBlockId, string sourcePath, CancellationToken cancellationToken = default);
    Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 13: SampleBlockService.cs** — replace:

```csharp
using System.IO;
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Data.Services;

public sealed class SampleBlockService : ISampleBlockService
{
    private readonly string _dbPath;
    private readonly string _dataDir;

    public SampleBlockService(string dbPath, string dataDir)
    {
        _dbPath = dbPath;
        _dataDir = dataDir;
    }

    private BillTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        return new BillTrackerDbContext(options);
    }

    public async Task<List<SampleBlock>> GetSampleBlocksAsync(SampleBlockFilter? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new SampleBlockFilter();
        await using var db = CreateDbContext();
        var query = db.SampleBlocks.AsNoTracking().Include(x => x.Attachments).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Model))
        {
            var k = filter.Model.Trim();
            query = query.Where(x => x.Model.Contains(k));
        }
        if (!string.IsNullOrWhiteSpace(filter.Customer))
        {
            var c = filter.Customer.Trim();
            query = query.Where(x => (x.Customer ?? string.Empty).Contains(c));
        }
        if (filter.OrderFrom.HasValue) query = query.Where(x => x.OrderTime != null && x.OrderTime >= filter.OrderFrom.Value);
        if (filter.OrderTo.HasValue) query = query.Where(x => x.OrderTime != null && x.OrderTime <= filter.OrderTo.Value);
        if (!string.IsNullOrWhiteSpace(filter.Note))
        {
            var n = filter.Note.Trim();
            query = query.Where(x => (x.Note ?? string.Empty).Contains(n));
        }
        return await query.OrderBy(x => x.Model).ToListAsync(cancellationToken);
    }

    public async Task<SampleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.SampleBlocks.AsNoTracking().Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<SampleBlock> SaveAsync(SampleBlock sampleBlock, CancellationToken cancellationToken = default)
    {
        var normalizedModel = (sampleBlock.Model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
            throw new InvalidOperationException("样块型号不能为空。");

        await using var db = CreateDbContext();
        var id = sampleBlock.Id == Guid.Empty ? Guid.NewGuid() : sampleBlock.Id;
        var duplicate = await db.SampleBlocks.AsNoTracking().AnyAsync(x => x.Model == normalizedModel && x.Id != id, cancellationToken);
        if (duplicate) throw new InvalidOperationException("样块型号已存在，请使用其他型号。");

        var customer = string.IsNullOrWhiteSpace(sampleBlock.Customer) ? null : sampleBlock.Customer.Trim();
        var note = string.IsNullOrWhiteSpace(sampleBlock.Note) ? null : sampleBlock.Note.Trim();
        var now = DateTime.Now;
        var existing = await db.SampleBlocks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            var created = new SampleBlock { Id = id, Model = normalizedModel, Customer = customer, OrderTime = sampleBlock.OrderTime, Note = note, CreatedAt = now, UpdatedAt = now };
            await db.SampleBlocks.AddAsync(created, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        existing.Model = normalizedModel;
        existing.Customer = customer;
        existing.OrderTime = sampleBlock.OrderTime;
        existing.Note = note;
        existing.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var entity = await db.SampleBlocks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("样块不存在。");
        db.SampleBlocks.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        var dir = Path.Combine(_dataDir, "attachments", "sampleblocks", id.ToString());
        if (Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* non-fatal */ }
        }
    }

    public async Task<SampleBlockAttachment> AddAttachmentAsync(Guid sampleBlockId, string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("附件文件不存在。", sourcePath);
        await using var db = CreateDbContext();
        _ = await db.SampleBlocks.FirstOrDefaultAsync(x => x.Id == sampleBlockId, cancellationToken)
            ?? throw new InvalidOperationException("样块不存在，无法添加附件。");

        var dir = Path.Combine(_dataDir, "attachments", "sampleblocks", sampleBlockId.ToString());
        Directory.CreateDirectory(dir);
        var fileName = Path.GetFileName(sourcePath);
        var target = Path.Combine(dir, fileName);
        if (File.Exists(target))
        {
            var unique = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddHHmmssfff}{Path.GetExtension(fileName)}";
            target = Path.Combine(dir, unique);
        }
        File.Copy(sourcePath, target, overwrite: false);

        var rel = Path.GetRelativePath(_dataDir, target).Replace('\\', '/');
        var att = new SampleBlockAttachment { Id = Guid.NewGuid(), SampleBlockId = sampleBlockId, RelativePath = rel, CreatedAt = DateTime.Now };
        await db.SampleBlockAttachments.AddAsync(att, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return att;
    }

    public async Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var att = await db.SampleBlockAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId, cancellationToken)
            ?? throw new InvalidOperationException("附件记录不存在。");
        var abs = Path.Combine(_dataDir, att.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(abs)) File.Delete(abs);
        db.SampleBlockAttachments.Remove(att);
        await db.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 14: Run → GREEN**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug`
Expected: all pass. (App not built here; sample-block window fixed in Task 8.)

- [ ] **Step 15: Commit**

```bash
git add -A
git commit -m "feat(data): reshape SampleBlock data layer, decouple from Wire"
```

---

## Task 7: 迁移 ReworkSampleBlock

**Files:** Create (generated) `src/GlassFactory.BillTracker.Data/Migrations/*_ReworkSampleBlock.cs`

- [ ] **Step 1: Generate**

```bash
BILLTRACKER_DATA_DIR=/tmp/bt-ef dotnet ef migrations add ReworkSampleBlock \
  --project src/GlassFactory.BillTracker.Data --startup-project src/GlassFactory.BillTracker.Data
```

- [ ] **Step 2: Verify** — `Up` should drop `SampleBlocks.WireId` + `SampleBlocks.Price` (+ `IX_SampleBlocks_WireId` + FK to Wires), add `SampleBlocks.Customer` (TEXT null) + `SampleBlocks.OrderTime` (TEXT null), create `SampleBlockAttachments` (FK→SampleBlocks Cascade, index). NO ops on OrderItems/Customers/Orders/Wire-columns beyond the sample-block FK removal. If unexpected, report BLOCKED.

- [ ] **Step 3: Build Data + test** (App inconsistent until Task 8)

Run: `dotnet build src/GlassFactory.BillTracker.Data/GlassFactory.BillTracker.Data.csproj -c Debug && dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug`
Expected: Build succeeded; tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/GlassFactory.BillTracker.Data/Migrations/
git commit -m "feat(data): migration ReworkSampleBlock (customer/orderTime/attachments, drop wire+price)"
```

---

## Task 8: 样块管理窗口重塑 + 装配

**Files:**
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/SampleBlockManagementViewModel.cs` (replace)
- Modify: `src/GlassFactory.BillTracker.App/Views/SampleBlockManagementWindow.xaml` (replace)
- Modify: `src/GlassFactory.BillTracker.App/Views/SampleBlockManagementWindow.xaml.cs` (replace)
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs` (`OpenSampleBlockManagement`)
- Modify: `src/GlassFactory.BillTracker.App/Views/MainWindow.xaml.cs` (SampleBlockService ctor)

**Interfaces:** Consumes `ISampleBlockService`, `SampleBlockFilter`, `SampleBlock`, `SampleBlockAttachment`, `ManagedAttachmentViewModel` (Tasks 5/6).

- [ ] **Step 1: SampleBlockManagementViewModel.cs** — replace:

```csharp
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.App.ViewModels.Rows;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class SampleBlockManagementViewModel : ObservableObject
{
    private readonly ISampleBlockService _service;

    private Guid _editingId;
    private string _model = string.Empty;
    private string? _customer;
    private DateTime? _orderTime;
    private string? _note;
    private SampleBlock? _selected;

    private string? _filterModel;
    private string? _filterCustomer;
    private DateTime? _filterFrom;
    private DateTime? _filterTo;
    private string? _filterNote;

    private readonly List<string> _newAttachmentPaths = new();
    private readonly List<Guid> _removedAttachmentIds = new();

    public ObservableCollection<SampleBlock> SampleBlocks { get; } = new();
    public ObservableCollection<ManagedAttachmentViewModel> Attachments { get; } = new();

    public SampleBlockManagementViewModel(ISampleBlockService service)
    {
        _service = service;
        NewCommand = new RelayCommand(ResetForm);
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        DeleteCommand = new RelayCommand(() => _ = DeleteAsync());
        SearchCommand = new RelayCommand(() => _ = LoadAsync());
        ClearFilterCommand = new RelayCommand(ClearFilter);
        AddImageCommand = new RelayCommand(AddImages);
        RemoveImageCommand = new RelayCommand<ManagedAttachmentViewModel>(RemoveImage);
        _ = LoadAsync();
    }

    public string? FilterModel { get => _filterModel; set => SetProperty(ref _filterModel, value); }
    public string? FilterCustomer { get => _filterCustomer; set => SetProperty(ref _filterCustomer, value); }
    public DateTime? FilterFrom { get => _filterFrom; set => SetProperty(ref _filterFrom, value); }
    public DateTime? FilterTo { get => _filterTo; set => SetProperty(ref _filterTo, value); }
    public string? FilterNote { get => _filterNote; set => SetProperty(ref _filterNote, value); }

    public SampleBlock? Selected
    {
        get => _selected;
        set { if (SetProperty(ref _selected, value) && value is not null) LoadForm(value); }
    }

    public string Model { get => _model; set => SetProperty(ref _model, value); }
    public string? Customer { get => _customer; set => SetProperty(ref _customer, value); }
    public DateTime? OrderTime { get => _orderTime; set => SetProperty(ref _orderTime, value); }
    public string? Note { get => _note; set => SetProperty(ref _note, value); }

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFilterCommand { get; }
    public RelayCommand AddImageCommand { get; }
    public RelayCommand<ManagedAttachmentViewModel> RemoveImageCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            var filter = new SampleBlockFilter
            {
                Model = FilterModel, Customer = FilterCustomer,
                OrderFrom = FilterFrom?.Date, OrderTo = FilterTo?.Date, Note = FilterNote
            };
            var items = await _service.GetSampleBlocksAsync(filter);
            SampleBlocks.Clear();
            foreach (var s in items) SampleBlocks.Add(s);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载样块列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearFilter()
    {
        FilterModel = null; FilterCustomer = null; FilterFrom = null; FilterTo = null; FilterNote = null;
        _ = LoadAsync();
    }

    private void LoadForm(SampleBlock sb)
    {
        _editingId = sb.Id;
        Model = sb.Model; Customer = sb.Customer; OrderTime = sb.OrderTime; Note = sb.Note;
        _newAttachmentPaths.Clear(); _removedAttachmentIds.Clear(); Attachments.Clear();
        foreach (var a in sb.Attachments)
            Attachments.Add(new ManagedAttachmentViewModel { AttachmentId = a.Id, RelativePath = a.RelativePath });
    }

    private void ResetForm()
    {
        _editingId = Guid.Empty;
        Model = string.Empty; Customer = null; OrderTime = null; Note = null;
        Selected = null;
        _newAttachmentPaths.Clear(); _removedAttachmentIds.Clear(); Attachments.Clear();
    }

    private void AddImages()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|所有文件|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;
        foreach (var path in dialog.FileNames)
        {
            if (!File.Exists(path)) continue;
            _newAttachmentPaths.Add(path);
            Attachments.Add(new ManagedAttachmentViewModel { SourcePath = path });
        }
    }

    private void RemoveImage(ManagedAttachmentViewModel? att)
    {
        if (att is null) return;
        if (att.IsPersisted && att.AttachmentId.HasValue) _removedAttachmentIds.Add(att.AttachmentId.Value);
        else if (!string.IsNullOrWhiteSpace(att.SourcePath)) _newAttachmentPaths.Remove(att.SourcePath);
        Attachments.Remove(att);
    }

    private async Task SaveAsync()
    {
        try
        {
            var saved = await _service.SaveAsync(new SampleBlock
            {
                Id = _editingId, Model = Model, Customer = Customer, OrderTime = OrderTime?.Date, Note = Note
            });
            foreach (var rid in _removedAttachmentIds) await _service.RemoveAttachmentAsync(rid);
            foreach (var p in _newAttachmentPaths) await _service.AddAttachmentAsync(saved.Id, p);
            await LoadAsync();
            Selected = SampleBlocks.FirstOrDefault(x => x.Id == saved.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DeleteAsync()
    {
        if (_editingId == Guid.Empty)
        {
            MessageBox.Show("请先在列表中选择要删除的样块。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"确认删除样块 \"{Model}\" 吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            await _service.DeleteAsync(_editingId);
            ResetForm();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
```

- [ ] **Step 2: SampleBlockManagementWindow.xaml** — replace:

```xml
<Window x:Class="GlassFactory.BillTracker.App.Views.SampleBlockManagementWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:conv="clr-namespace:GlassFactory.BillTracker.App.Converters"
        Title="样块管理" Width="1000" Height="620" WindowStartupLocation="CenterOwner">
    <Window.Resources>
        <conv:PathToImageSourceConverter x:Key="PathToImageSourceConverter" />
    </Window.Resources>
    <DockPanel Margin="10">
        <Border DockPanel.Dock="Top" BorderBrush="#DDD" BorderThickness="1" Padding="8" Margin="0,0,0,8">
            <WrapPanel>
                <TextBlock Text="型号" VerticalAlignment="Center" Margin="0,0,4,0" />
                <TextBox Width="120" Text="{Binding FilterModel, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,12,0" />
                <TextBlock Text="客户" VerticalAlignment="Center" Margin="0,0,4,0" />
                <TextBox Width="120" Text="{Binding FilterCustomer, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,12,0" />
                <TextBlock Text="订单时间" VerticalAlignment="Center" Margin="0,0,4,0" />
                <DatePicker Width="120" SelectedDate="{Binding FilterFrom}" />
                <TextBlock Text="~" VerticalAlignment="Center" Margin="4,0" />
                <DatePicker Width="120" SelectedDate="{Binding FilterTo}" Margin="0,0,12,0" />
                <TextBlock Text="备注" VerticalAlignment="Center" Margin="0,0,4,0" />
                <TextBox Width="120" Text="{Binding FilterNote, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,12,0" />
                <Button Content="查询" Command="{Binding SearchCommand}" Width="60" Margin="0,0,8,0" />
                <Button Content="清除" Command="{Binding ClearFilterCommand}" Width="60" Margin="0,0,8,0" />
                <Button Content="新增" Command="{Binding NewCommand}" Width="60" />
            </WrapPanel>
        </Border>

        <Border DockPanel.Dock="Right" BorderBrush="#DDD" BorderThickness="1" Padding="10" Margin="8,0,0,0" Width="320">
            <StackPanel>
                <TextBlock Text="型号 *" Margin="0,0,0,2" />
                <TextBox Text="{Binding Model, UpdateSourceTrigger=PropertyChanged}" MaxLength="100" Margin="0,0,0,8" />
                <TextBlock Text="客户" Margin="0,0,0,2" />
                <TextBox Text="{Binding Customer, UpdateSourceTrigger=PropertyChanged}" MaxLength="200" Margin="0,0,0,8" />
                <TextBlock Text="订单时间" Margin="0,0,0,2" />
                <DatePicker SelectedDate="{Binding OrderTime}" Margin="0,0,0,8" />
                <TextBlock Text="备注" Margin="0,0,0,2" />
                <TextBox Text="{Binding Note, UpdateSourceTrigger=PropertyChanged}" AcceptsReturn="True" Height="60" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto" Margin="0,0,0,8" />
                <DockPanel Margin="0,0,0,4">
                    <TextBlock Text="附件（双击放大）" FontWeight="Bold" />
                    <Button Content="添加图片" Command="{Binding AddImageCommand}" HorizontalAlignment="Right" Width="80" />
                </DockPanel>
                <ListBox x:Name="AttachmentListBox" ItemsSource="{Binding Attachments}" Height="150"
                         ScrollViewer.HorizontalScrollBarVisibility="Auto"
                         MouseDoubleClick="AttachmentListBox_MouseDoubleClick">
                    <ListBox.ItemsPanel>
                        <ItemsPanelTemplate><WrapPanel Orientation="Horizontal" /></ItemsPanelTemplate>
                    </ListBox.ItemsPanel>
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <StackPanel Width="92" Margin="0,0,6,6">
                                <Border BorderBrush="#CCC" BorderThickness="1" Padding="2">
                                    <Image Width="84" Height="60" Stretch="UniformToFill"
                                           Source="{Binding AbsolutePath, Converter={StaticResource PathToImageSourceConverter}}" />
                                </Border>
                                <Button Content="移除" Command="{Binding DataContext.RemoveImageCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                        CommandParameter="{Binding}" Margin="0,2,0,0" />
                            </StackPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
                <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                    <Button Content="保存" Command="{Binding SaveCommand}" Width="90" Margin="0,0,8,0" />
                    <Button Content="删除" Command="{Binding DeleteCommand}" Width="90" />
                </StackPanel>
            </StackPanel>
        </Border>

        <DataGrid ItemsSource="{Binding SampleBlocks}" SelectedItem="{Binding Selected}"
                  AutoGenerateColumns="False" IsReadOnly="True" CanUserAddRows="False" SelectionMode="Single">
            <DataGrid.Columns>
                <DataGridTextColumn Header="型号" Binding="{Binding Model}" Width="140" />
                <DataGridTextColumn Header="客户" Binding="{Binding Customer}" Width="120" />
                <DataGridTextColumn Header="订单时间" Binding="{Binding OrderTime, StringFormat=yyyy-MM-dd}" Width="110" />
                <DataGridTextColumn Header="备注" Binding="{Binding Note}" Width="*" />
                <DataGridTextColumn Header="附件" Binding="{Binding Attachments.Count, StringFormat={}{0}张}" Width="60" />
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
```

- [ ] **Step 3: SampleBlockManagementWindow.xaml.cs** — replace:

```csharp
using System.Windows;
using GlassFactory.BillTracker.App.ViewModels.Rows;

namespace GlassFactory.BillTracker.App.Views;

public partial class SampleBlockManagementWindow : Window
{
    public SampleBlockManagementWindow()
    {
        InitializeComponent();
    }

    private void AttachmentListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (AttachmentListBox.SelectedItem is not ManagedAttachmentViewModel att || string.IsNullOrWhiteSpace(att.AbsolutePath)) return;
        new ImageViewWindow(att.AbsolutePath) { Owner = this }.ShowDialog();
    }
}
```

- [ ] **Step 4: MainWindowViewModel.cs** — update `OpenSampleBlockManagement` to construct `new SampleBlockManagementViewModel(_sampleBlockService)` (single arg).

- [ ] **Step 5: MainWindow.xaml.cs** — change sample-block service construction to `new SampleBlockService(AppRuntimeContext.DbPath, AppRuntimeContext.DataDir)`.

- [ ] **Step 6: Build (whole solution — now fully consistent)**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(app): rework sample-block management window (filter + attachments)"
```

---

# 阶段 D — 打印合计

## Task 9: 打印底部数量/方数合计

**Files:** Modify `src/GlassFactory.BillTracker.App/Services/PrintService.cs`

- [ ] **Step 1: Add footer-text helper** — in `PrintService` add:

```csharp
private static string BuildTotalsFooterText(OrderExportDto order)
{
    var items = (order.Items ?? Array.Empty<OrderExportItemDto>()).Where(i => i is not null).ToList();
    var totalQty = items.Sum(i => i.Quantity);
    var totalArea = items.Sum(i => OrderAmountCalculator.CalculateAreaM2Rounded(i.GlassLengthMm, i.GlassWidthMm));
    return $"总数量：{totalQty}    总方数：{totalArea.ToString("F2", CultureInfo.InvariantCulture)}    合计：{FormatMoney2(order.TotalAmount)}";
}
```
(`CultureInfo` and `OrderAmountCalculator` namespaces are already imported at the top of the file.)

- [ ] **Step 2: Dot-matrix** — in `RenderDotMatrixTriplicate`, the `BuildPageTable(...)` call passes `includeFooter ? $"合计：{FormatMoney2(order.TotalAmount)}" : null` as `totalText`. Replace that argument with `includeFooter ? BuildTotalsFooterText(order) : null`.

- [ ] **Step 3: A4** — in `CreateBillCopyPanel`, the `CreateTotalFooterCell($"合计：{FormatMoney2(order.TotalAmount)}", ...)` first argument becomes `BuildTotalsFooterText(order)`.

- [ ] **Step 4: Build**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug`
Expected: Build succeeded, 0 errors.

> No unit test added: the area/quantity math is covered by `OrderAmountCalculatorTests`; the footer string is presentational (visual check is a Windows manual-acceptance item).

- [ ] **Step 5: Commit**

```bash
git add src/GlassFactory.BillTracker.App/Services/PrintService.cs
git commit -m "feat(app): show quantity/area totals in printed bill footer"
```

---

# 阶段 E — 回归 + 文档

## Task 10: 全量回归 + 文档

**Files:** Modify `README.md`, `CLAUDE.md`

- [ ] **Step 1: Full regression**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug && dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug`
Expected: Build succeeded; all tests pass, pristine.

- [ ] **Step 2: README.md** — update 功能清单: 丝管理(型号/价格/进货时间/备注/附件 + 按型号/价格区间/进货时间/备注筛选)；样块管理(型号/客户/订单时间/备注/附件 + 按型号/客户/订单时间/备注筛选)；订单不再选择样块；打印底部显示 总数量/总方数/合计。删除关于订单选样块的过时文字。

- [ ] **Step 3: CLAUDE.md** — rewrite the "Wire / SampleBlock subsystem" section: 丝 与 样块 为独立主数据、互不绑定、均不接入订单；字段如上；各带独立附件表(`WireAttachments`/`SampleBlockAttachments`，文件 `attachments/wires|sampleblocks/<id>/`，级联删)；服务构造 `(dbPath, dataDir)`；订单明细已无 SampleBlockModel。删除过时的快照/派生丝描述。

- [ ] **Step 4: Commit**

```bash
git add README.md CLAUDE.md
git commit -m "docs: document decoupled wire/sample-block subsystems + print totals"
```

---

## 手动验收清单（Windows）

1. 订单编辑器：明细无 样块型号/丝/价格 列；保留 平方列 + 总计(数量/方数)。
2. 丝管理：增删改；型号唯一拦截；按 型号/价格区间/进货时间/备注 筛选；多图上传/缩略图/双击放大/移除/保存回显；删除记录后目录清除。
3. 样块管理：同上，字段 型号/客户/订单时间/备注/附件；筛选 型号/客户/订单时间/备注。
4. 丝窗口无"该丝涉及的样块"反查。
5. 打印(点阵 + A4)底部显示 `总数量：X  总方数：Y  合计：Z`。
6. 旧 bills 库导入后自动迁移，历史订单/客户/明细完好。
7. `dotnet test` 全绿。
