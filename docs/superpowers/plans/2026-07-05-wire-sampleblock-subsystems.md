# 平方列/总计行 + 丝管理 + 样块管理 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 给订单明细加"平方(㎡)"列与"总计"行，并新增丝管理、样块管理两个子系统；订单明细可选样块型号，自动带出对应的丝与价格（仅参考，不计入金额）。

**Architecture:** 沿用现有分层（Domain/Data/Infrastructure/App）与既有模式：无 DI 容器、手工装配、DbContext-per-op、`IEntityTypeConfiguration<T>` 自动发现、启动时 `Database.Migrate()`。新实体 `Wire`/`SampleBlock` 放 Domain；EF 配置与 DbSet 放 Data；**新服务 `WireService`/`SampleBlockService` 放 Data 层**并以 `(string dbPath)` 构造（与 `ExportService` 一致），使测试项目（仅引用 Domain+Data）可直接单测。App 层新增两个管理窗口与订单编辑改造。

**Tech Stack:** .NET 8、WPF、EF Core 8.0.12（SQLite）、xUnit、`dotnet-ef` 10.0.3（tools manifest）。

## 与设计文档的差异（须知）

设计文档 §5.2/§6.2 写"服务置于 App 层（同 `CustomerService`）"。**本计划改为置于 Data 层**并用 `(string dbPath)` 构造。理由：测试项目 `tests/GlassFactory.BillTracker.Tests` 只引用 Domain+Data（`net8.0`），不引用 App（`net8.0-windows`/WPF）；`CustomerService`/`OrderService` 因在 App 层而无法被现有测试框架单测。放 Data 层可对新服务做 TDD，且与既有可测服务 `ExportService` 完全一致。App 通过 `new WireService(AppRuntimeContext.DbPath)` 装配（与 `new ExportService(AppRuntimeContext.DbPath)` 同）。

## Global Constraints

- 目标框架：Domain/Data/Tests = `net8.0`；App = `net8.0-windows10.0.19041.0`（`EnableWindowsTargeting=true`，可在 Linux 上 `dotnet build`，但 WPF 运行需 Windows）。
- 包版本集中管理：不在单个 `<PackageReference>` 写 `<Version>`；新增包版本加到 `Directory.Packages.props`。本计划无新增包。
- 金额公式**不得改动**：行金额 = 玻璃费 + 打孔费(HoleFee) + 其他费(OtherFee)；`OrderAmountCalculator.RoundAmount` 保留 0 位、`Round` 保留 2 位、`MidpointRounding.AwayFromZero`。样块价存入 `OrderItem.WireUnitPrice`（现有字段，不计入金额），仅作参考。
- decimal 金额/面积用 `HasPrecision(18,4)`。
- EF 配置每实体一个 `IEntityTypeConfiguration<T>`；关系只在单侧配置以免重复。
- 迁移经 `DesignTimeDbContextFactory`（读环境变量 `BILLTRACKER_DATA_DIR`）生成；命令用 `--project`/`--startup-project` 均指向 Data 项目。
- UI 文案、异常消息用中文，风格与现有 `MessageBox` 一致。
- 每个可单测任务走 TDD：先写失败测试→跑红→最小实现→跑绿→提交。纯 WPF（App）任务无法被现有测试框架单测，改用 `dotnet build` 编译验证 + 提交（与代码库现状一致：`OrderService`/`CustomerService`/VM 均无单测）。

**常用命令**
- 构建：`dotnet build GlassFactory.BillTracker.sln -c Debug`
- 全部测试：`dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug`
- 单个测试：`dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~<方法名>"`

---

# 阶段 1 — 订单：平方列 + 总计行

## Task 1: Domain 面积与汇总计算

**Files:**
- Modify: `src/GlassFactory.BillTracker.Domain/Services/OrderAmountCalculator.cs`
- Test: `tests/GlassFactory.BillTracker.Tests/OrderAmountCalculatorTests.cs`

**Interfaces:**
- Produces:
  - `OrderAmountCalculator.CalculateAreaM2Rounded(decimal glassLengthMm, decimal glassWidthMm) -> decimal`（2 位，AwayFromZero）
  - `OrderAmountCalculator.CalculateTotalAreaM2(IEnumerable<OrderItem> items) -> decimal`（原始面积求和后 Round 2 位）
  - `OrderAmountCalculator.CalculateTotalQuantity(IEnumerable<OrderItem> items) -> int`

- [ ] **Step 1: 追加失败测试**

在 `OrderAmountCalculatorTests.cs` 末尾类内追加：

```csharp
    [Fact]
    public void CalculateAreaM2Rounded_ShouldConvertMmToSquareMeters_TwoDecimals_AwayFromZero()
    {
        // 1234mm * 567mm => 1.234m * 0.567m = 0.699678 m^2 => round2 => 0.70
        Assert.Equal(0.70m, OrderAmountCalculator.CalculateAreaM2Rounded(1234m, 567m));
        // 1000mm * 1000mm => 1.00
        Assert.Equal(1.00m, OrderAmountCalculator.CalculateAreaM2Rounded(1000m, 1000m));
    }

    [Fact]
    public void CalculateTotalAreaM2AndQuantity_ShouldAggregateAcrossItems()
    {
        var items = new[]
        {
            new OrderItem { GlassLengthMm = 1000m, GlassWidthMm = 1000m, Quantity = 2 }, // 1.00 m^2
            new OrderItem { GlassLengthMm = 500m, GlassWidthMm = 400m, Quantity = 3 }    // 0.20 m^2
        };

        Assert.Equal(1.20m, OrderAmountCalculator.CalculateTotalAreaM2(items));
        Assert.Equal(5, OrderAmountCalculator.CalculateTotalQuantity(items));
    }
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~CalculateAreaM2Rounded"`
Expected: 编译失败 / FAIL —"CalculateAreaM2Rounded"不存在。

- [ ] **Step 3: 实现**

在 `OrderAmountCalculator.cs` 内（`CalculateAreaM2` 之后）追加：

```csharp
    public static decimal CalculateAreaM2Rounded(decimal glassLengthMm, decimal glassWidthMm)
    {
        return Math.Round(CalculateAreaM2(glassLengthMm, glassWidthMm), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateTotalAreaM2(IEnumerable<OrderItem> items)
    {
        var sum = items.Sum(item => CalculateAreaM2(item.GlassLengthMm, item.GlassWidthMm));
        return Math.Round(sum, 2, MidpointRounding.AwayFromZero);
    }

    public static int CalculateTotalQuantity(IEnumerable<OrderItem> items)
    {
        return items.Sum(item => item.Quantity);
    }
```

> 说明：`CalculateTotalAreaM2` 为**单件面积**求和（不乘数量），与逐行"平方"列口径一致（设计 §4.2 已确认）。逐行显示为逐行 Round、总方数为求和后 Round，极端情况下可能相差 0.01，属预期。

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~OrderAmountCalculatorTests"`
Expected: PASS（含原有回归用例）。

- [ ] **Step 5: 提交**

```bash
git add src/GlassFactory.BillTracker.Domain/Services/OrderAmountCalculator.cs tests/GlassFactory.BillTracker.Tests/OrderAmountCalculatorTests.cs
git commit -m "feat(domain): add area/total helpers for order items"
```

---

## Task 2: 行 ViewModel 暴露 AreaM2

**Files:**
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/Rows/OrderItemRowViewModel.cs`

**Interfaces:**
- Consumes: `OrderAmountCalculator.CalculateAreaM2Rounded` (Task 1)
- Produces: `OrderItemRowViewModel.AreaM2 { get; }`（只读，长/宽变化时刷新）

- [ ] **Step 1: 加 AreaM2 只读属性**

在 `Amount` 属性之后追加：

```csharp
    public decimal AreaM2 => OrderAmountCalculator.CalculateAreaM2Rounded(GlassLengthMm, GlassWidthMm);
```

- [ ] **Step 2: 长/宽变化时通知 AreaM2**

`GlassLengthMm` setter 内 `Recalculate();` 之后加一行 `OnPropertyChanged(nameof(AreaM2));`；`GlassWidthMm` setter 同样处理。修改后两处 setter 形如：

```csharp
    public decimal GlassLengthMm
    {
        get => _glassLengthMm;
        set
        {
            if (SetProperty(ref _glassLengthMm, value))
            {
                Recalculate();
                OnPropertyChanged(nameof(AreaM2));
            }
        }
    }
```

（`GlassWidthMm` 同理，仅字段与属性名不同。`ObservableObject` 已提供 `OnPropertyChanged(string)`——若命名不同，参照该基类现有调用方式。）

- [ ] **Step 3: 编译验证**

Run: `dotnet build src/GlassFactory.BillTracker.App/GlassFactory.BillTracker.App.csproj -c Debug`
Expected: Build succeeded.

- [ ] **Step 4: 提交**

```bash
git add src/GlassFactory.BillTracker.App/ViewModels/Rows/OrderItemRowViewModel.cs
git commit -m "feat(app): expose per-row AreaM2 on order item row"
```

---

## Task 3: 订单编辑窗口——平方列 + 总计行

**Files:**
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/OrderEditViewModel.cs`
- Modify: `src/GlassFactory.BillTracker.App/Views/OrderEditWindow.xaml`

**Interfaces:**
- Consumes: `OrderAmountCalculator.CalculateTotalAreaM2` / `CalculateTotalQuantity` (Task 1)；`OrderItemRowViewModel.AreaM2` (Task 2)
- Produces: `OrderEditViewModel.TotalQuantity { get; }`、`OrderEditViewModel.TotalAreaM2 { get; }`

- [ ] **Step 1: VM 加 TotalQuantity/TotalAreaM2**

在 `TotalAmount` 属性之后追加两个属性字段：

```csharp
    private int _totalQuantity;
    private decimal _totalAreaM2;

    public int TotalQuantity
    {
        get => _totalQuantity;
        private set => SetProperty(ref _totalQuantity, value);
    }

    public decimal TotalAreaM2
    {
        get => _totalAreaM2;
        private set => SetProperty(ref _totalAreaM2, value);
    }
```

- [ ] **Step 2: 在 RecalculateTotal 内计算**

把 `RecalculateTotal()` 方法体改为：

```csharp
    private void RecalculateTotal()
    {
        var entities = BuildItems();
        TotalAmount = OrderAmountCalculator.CalculateOrderTotal(entities);
        TotalQuantity = OrderAmountCalculator.CalculateTotalQuantity(entities);
        TotalAreaM2 = OrderAmountCalculator.CalculateTotalAreaM2(entities);
    }
```

- [ ] **Step 3: XAML 加"平方(㎡)"列**

在 `OrderEditWindow.xaml` 明细 `DataGrid` 的列定义中，"数量"列与"单价(元/㎡)"列之间插入：

```xml
                    <DataGridTextColumn Header="平方(㎡)" Binding="{Binding AreaM2, StringFormat=F2}" IsReadOnly="True" Width="70" MinWidth="66" MaxWidth="80" ElementStyle="{StaticResource RightAlignedDisplayCellStyle}" />
```

- [ ] **Step 4: XAML 加"总计"行**

将现有合计行的 `Grid`（`Grid.Row="1"`，含 `TotalAmount` 的 `TextBlock`）替换为左右两块：

```xml
            <Grid Grid.Row="1" Margin="0,10,0,6">
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Left">
                    <TextBlock Text="总计" FontWeight="Bold" FontSize="15" Margin="0,0,16,0" />
                    <TextBlock Text="{Binding TotalQuantity, StringFormat=总数量：{0}}" FontSize="15" Margin="0,0,16,0" />
                    <TextBlock Text="{Binding TotalAreaM2, StringFormat=总方数：{0:F2}}" FontSize="15" />
                </StackPanel>
                <TextBlock Text="{Binding TotalAmount, StringFormat=合计：{0:F0}}"
                           HorizontalAlignment="Right"
                           FontWeight="Bold"
                           FontSize="15" />
            </Grid>
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build src/GlassFactory.BillTracker.App/GlassFactory.BillTracker.App.csproj -c Debug`
Expected: Build succeeded.

- [ ] **Step 6: 提交**

```bash
git add src/GlassFactory.BillTracker.App/ViewModels/OrderEditViewModel.cs src/GlassFactory.BillTracker.App/Views/OrderEditWindow.xaml
git commit -m "feat(app): add AreaM2 column and totals row to order editor"
```

---

# 阶段 2 — 丝管理系统

## Task 4: Wire 实体 + EF 配置 + DbSet

**Files:**
- Create: `src/GlassFactory.BillTracker.Domain/Entities/Wire.cs`
- Create: `src/GlassFactory.BillTracker.Data/Persistence/Configurations/WireConfiguration.cs`
- Modify: `src/GlassFactory.BillTracker.Data/Persistence/BillTrackerDbContext.cs`
- Test: `tests/GlassFactory.BillTracker.Tests/WireMappingTests.cs`

**Interfaces:**
- Produces: 实体 `Wire { Guid Id; string Model; string? Manufacturer; decimal Price; string? Note; DateTime CreatedAt; DateTime UpdatedAt; ICollection<SampleBlock> SampleBlocks }`；`BillTrackerDbContext.Wires`。
- 说明：`Wire` 引用 `SampleBlock`（Task 8 创建）。本任务同时创建一个 **占位** `SampleBlock` 里的反向关系尚不需要——`Wire.SampleBlocks` 集合类型引用 `SampleBlock`，因此本任务的编译依赖 Task 8 的实体存在。**为解耦，本任务先不加 `SampleBlocks` 导航属性**，留到 Task 8 一并补齐关系配置。

- [ ] **Step 1: 写失败测试**

Create `tests/GlassFactory.BillTracker.Tests/WireMappingTests.cs`:

```csharp
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class WireMappingTests
{
    private static BillTrackerDbContext NewDb(out string dbPath)
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        dbPath = Path.Combine(dir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>()
            .UseSqlite($"Data Source={dbPath}").Options;
        var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Wire_ShouldPersistAndReload()
    {
        await using var db = NewDb(out _);
        var wire = new Wire { Model = "W-100", Manufacturer = "厂A", Price = 12.5m, Note = "n" };
        db.Wires.Add(wire);
        await db.SaveChangesAsync();

        var reloaded = await db.Wires.AsNoTracking().SingleAsync(x => x.Model == "W-100");
        Assert.Equal("厂A", reloaded.Manufacturer);
        Assert.Equal(12.5m, reloaded.Price);
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~WireMappingTests"`
Expected: 编译失败——`Wire` 不存在。

- [ ] **Step 3: 建实体**

Create `src/GlassFactory.BillTracker.Domain/Entities/Wire.cs`:

```csharp
namespace GlassFactory.BillTracker.Domain.Entities;

public class Wire
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Model { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public decimal Price { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
```

- [ ] **Step 4: 建 EF 配置**

Create `src/GlassFactory.BillTracker.Data/Persistence/Configurations/WireConfiguration.cs`:

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
        builder.Property(x => x.Manufacturer).HasMaxLength(200);
        builder.Property(x => x.Price).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.HasIndex(x => x.Model).IsUnique();
    }
}
```

- [ ] **Step 5: DbContext 加 DbSet**

在 `BillTrackerDbContext.cs` 现有 DbSet 之后加：

```csharp
    public DbSet<Wire> Wires => Set<Wire>();
```

- [ ] **Step 6: 跑测试确认通过**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~WireMappingTests"`
Expected: PASS。

- [ ] **Step 7: 提交**

```bash
git add src/GlassFactory.BillTracker.Domain/Entities/Wire.cs src/GlassFactory.BillTracker.Data/Persistence/Configurations/WireConfiguration.cs src/GlassFactory.BillTracker.Data/Persistence/BillTrackerDbContext.cs tests/GlassFactory.BillTracker.Tests/WireMappingTests.cs
git commit -m "feat(data): add Wire entity, config, and DbSet"
```

---

## Task 5: WireService（Data 层）+ 测试

**Files:**
- Create: `src/GlassFactory.BillTracker.Data/Services/IWireService.cs`
- Create: `src/GlassFactory.BillTracker.Data/Services/WireService.cs`
- Test: `tests/GlassFactory.BillTracker.Tests/WireServiceTests.cs`

**Interfaces:**
- Consumes: `Wire`、`BillTrackerDbContext.Wires` (Task 4)
- Produces:
  - `IWireService.GetWiresAsync(string? keyword, CancellationToken) -> Task<List<Wire>>`
  - `GetByIdAsync(Guid, CancellationToken) -> Task<Wire?>`
  - `GetByModelAsync(string, CancellationToken) -> Task<Wire?>`
  - `SaveAsync(Wire, CancellationToken) -> Task<Wire>`（型号非空、型号唯一）
  - `DeleteAsync(Guid, CancellationToken) -> Task`（被样块引用抛 `InvalidOperationException`）
  - `WireService(string dbPath)` 构造。

- [ ] **Step 1: 写失败测试**

Create `tests/GlassFactory.BillTracker.Tests/WireServiceTests.cs`:

```csharp
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class WireServiceTests
{
    private static string NewDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        using var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return dbPath;
    }

    [Fact]
    public async Task SaveAsync_ShouldRejectDuplicateModel()
    {
        var dbPath = NewDbPath();
        var svc = new WireService(dbPath);
        await svc.SaveAsync(new Wire { Model = "W-1", Price = 1m });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SaveAsync(new Wire { Model = "W-1", Price = 2m }));
    }

    [Fact]
    public async Task GetByModelAsync_ShouldReturnManufacturerAndPrice()
    {
        var dbPath = NewDbPath();
        var svc = new WireService(dbPath);
        await svc.SaveAsync(new Wire { Model = "W-2", Manufacturer = "厂B", Price = 9.5m });

        var found = await svc.GetByModelAsync("W-2");
        Assert.NotNull(found);
        Assert.Equal("厂B", found!.Manufacturer);
        Assert.Equal(9.5m, found.Price);
    }
}
```

（被样块引用的删除拦截测试放到 Task 9，因需要 `SampleBlock`。）

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~WireServiceTests"`
Expected: 编译失败——`WireService` 不存在。

- [ ] **Step 3: 建接口**

Create `src/GlassFactory.BillTracker.Data/Services/IWireService.cs`:

```csharp
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Data.Services;

public interface IWireService
{
    Task<List<Wire>> GetWiresAsync(string? keyword = null, CancellationToken cancellationToken = default);
    Task<Wire?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wire?> GetByModelAsync(string model, CancellationToken cancellationToken = default);
    Task<Wire> SaveAsync(Wire wire, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: 建实现**

Create `src/GlassFactory.BillTracker.Data/Services/WireService.cs`:

```csharp
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Data.Services;

public sealed class WireService : IWireService
{
    private readonly string _dbPath;

    public WireService(string dbPath)
    {
        _dbPath = dbPath;
    }

    private BillTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>()
            .UseSqlite($"Data Source={_dbPath}").Options;
        return new BillTrackerDbContext(options);
    }

    public async Task<List<Wire>> GetWiresAsync(string? keyword = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var query = db.Wires.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x => x.Model.Contains(k) || (x.Manufacturer ?? string.Empty).Contains(k));
        }
        return await query.OrderBy(x => x.Model).ToListAsync(cancellationToken);
    }

    public async Task<Wire?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.Wires.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Wire?> GetByModelAsync(string model, CancellationToken cancellationToken = default)
    {
        var normalized = (model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        await using var db = CreateDbContext();
        return await db.Wires.AsNoTracking().FirstOrDefaultAsync(x => x.Model == normalized, cancellationToken);
    }

    public async Task<Wire> SaveAsync(Wire wire, CancellationToken cancellationToken = default)
    {
        var normalizedModel = (wire.Model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            throw new InvalidOperationException("丝型号不能为空。");
        }

        await using var db = CreateDbContext();
        var id = wire.Id == Guid.Empty ? Guid.NewGuid() : wire.Id;
        var duplicate = await db.Wires.AsNoTracking()
            .AnyAsync(x => x.Model == normalizedModel && x.Id != id, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("丝型号已存在，请使用其他型号。");
        }

        var manufacturer = string.IsNullOrWhiteSpace(wire.Manufacturer) ? null : wire.Manufacturer.Trim();
        var note = string.IsNullOrWhiteSpace(wire.Note) ? null : wire.Note.Trim();
        var now = DateTime.Now;

        var existing = await db.Wires.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            var created = new Wire
            {
                Id = id,
                Model = normalizedModel,
                Manufacturer = manufacturer,
                Price = wire.Price,
                Note = note,
                CreatedAt = now,
                UpdatedAt = now
            };
            await db.Wires.AddAsync(created, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }

        existing.Model = normalizedModel;
        existing.Manufacturer = manufacturer;
        existing.Price = wire.Price;
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
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("该丝已被样块引用，无法删除。请先删除相关样块。");
        }
    }
}
```

- [ ] **Step 5: 跑测试确认通过**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~WireServiceTests"`
Expected: PASS。

- [ ] **Step 6: 提交**

```bash
git add src/GlassFactory.BillTracker.Data/Services/IWireService.cs src/GlassFactory.BillTracker.Data/Services/WireService.cs tests/GlassFactory.BillTracker.Tests/WireServiceTests.cs
git commit -m "feat(data): add WireService with model uniqueness and lookup"
```

---

## Task 6: 迁移 AddWire

**Files:**
- Create（生成）: `src/GlassFactory.BillTracker.Data/Migrations/*_AddWire.cs` 等

- [ ] **Step 1: 还原工具**

Run: `dotnet tool restore`
Expected: 恢复 `dotnet-ef` 10.0.3。

- [ ] **Step 2: 生成迁移**

Run:
```bash
BILLTRACKER_DATA_DIR=/tmp/bt-ef dotnet ef migrations add AddWire \
  --project src/GlassFactory.BillTracker.Data \
  --startup-project src/GlassFactory.BillTracker.Data
```
Expected: 生成 `Migrations/<timestamp>_AddWire.cs`（`Up` 内 `CreateTable("Wires")` + `Model` 唯一索引），并更新 `BillTrackerDbContextModelSnapshot.cs`。

> 若 `dotnet ef` 因工具/运行时版本报错：手写迁移，结构参照现有 `Migrations/20260325093000_AddOrderItemSortIndex.cs`（`CreateTable` + `CreateIndex(unique:true)`），并手动在 `BillTrackerDbContextModelSnapshot.cs` 补 `Wire` 实体块（参照现有实体块格式）。

- [ ] **Step 3: 构建验证**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug`
Expected: Build succeeded。

- [ ] **Step 4: 提交**

```bash
git add src/GlassFactory.BillTracker.Data/Migrations/
git commit -m "feat(data): add EF migration for Wires table"
```

---

## Task 7: 丝管理窗口 + 主界面入口

**Files:**
- Create: `src/GlassFactory.BillTracker.App/ViewModels/WireManagementViewModel.cs`
- Create: `src/GlassFactory.BillTracker.App/Views/WireManagementWindow.xaml`
- Create: `src/GlassFactory.BillTracker.App/Views/WireManagementWindow.xaml.cs`
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/GlassFactory.BillTracker.App/Views/MainWindow.xaml`
- Modify: `src/GlassFactory.BillTracker.App/Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `IWireService` (Task 5)
- Produces: `MainWindowViewModel.OpenWireManagementCommand`；`MainWindowViewModel` 构造新增 `IWireService` 参数。

- [ ] **Step 1: 建 WireManagementViewModel**

Create `src/GlassFactory.BillTracker.App/ViewModels/WireManagementViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class WireManagementViewModel : ObservableObject
{
    private readonly IWireService _wireService;

    private Guid _editingId;
    private string _model = string.Empty;
    private string? _manufacturer;
    private decimal _price;
    private string? _note;
    private string? _searchKeyword;
    private Wire? _selectedWire;

    public ObservableCollection<Wire> Wires { get; } = new();

    public WireManagementViewModel(IWireService wireService)
    {
        _wireService = wireService;

        NewCommand = new RelayCommand(ResetForm);
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        DeleteCommand = new RelayCommand(() => _ = DeleteAsync());
        SearchCommand = new RelayCommand(() => _ = LoadAsync());

        _ = LoadAsync();
    }

    public string? SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }

    public Wire? SelectedWire
    {
        get => _selectedWire;
        set
        {
            if (SetProperty(ref _selectedWire, value) && value is not null)
            {
                LoadForm(value);
            }
        }
    }

    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    public string? Manufacturer
    {
        get => _manufacturer;
        set => SetProperty(ref _manufacturer, value);
    }

    public decimal Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }

    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand SearchCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            var items = await _wireService.GetWiresAsync(SearchKeyword);
            Wires.Clear();
            foreach (var w in items)
            {
                Wires.Add(w);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载丝列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadForm(Wire wire)
    {
        _editingId = wire.Id;
        Model = wire.Model;
        Manufacturer = wire.Manufacturer;
        Price = wire.Price;
        Note = wire.Note;
    }

    private void ResetForm()
    {
        _editingId = Guid.Empty;
        Model = string.Empty;
        Manufacturer = null;
        Price = 0m;
        Note = null;
        SelectedWire = null;
    }

    private async Task SaveAsync()
    {
        try
        {
            var saved = await _wireService.SaveAsync(new Wire
            {
                Id = _editingId,
                Model = Model,
                Manufacturer = Manufacturer,
                Price = Price,
                Note = Note
            });
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

        if (MessageBox.Show($"确认删除丝“{Model}”吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

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
}
```

- [ ] **Step 2: 建窗口 XAML**

Create `src/GlassFactory.BillTracker.App/Views/WireManagementWindow.xaml`:

```xml
<Window x:Class="GlassFactory.BillTracker.App.Views.WireManagementWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="丝管理"
        Width="820"
        Height="560"
        WindowStartupLocation="CenterOwner">
    <DockPanel Margin="10">
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,0,0,8">
            <TextBox Width="220" Text="{Binding SearchKeyword, UpdateSourceTrigger=PropertyChanged}" />
            <Button Content="搜索" Command="{Binding SearchCommand}" Margin="6,0,0,0" Width="70" />
            <Button Content="新增" Command="{Binding NewCommand}" Margin="16,0,0,0" Width="70" />
        </StackPanel>

        <Border DockPanel.Dock="Right" BorderBrush="#DDD" BorderThickness="1" Padding="10" Margin="8,0,0,0" Width="300">
            <StackPanel>
                <TextBlock Text="型号 *" Margin="0,0,0,2" />
                <TextBox Text="{Binding Model, UpdateSourceTrigger=PropertyChanged}" MaxLength="100" Margin="0,0,0,8" />
                <TextBlock Text="厂家" Margin="0,0,0,2" />
                <TextBox Text="{Binding Manufacturer, UpdateSourceTrigger=PropertyChanged}" MaxLength="200" Margin="0,0,0,8" />
                <TextBlock Text="价格" Margin="0,0,0,2" />
                <TextBox Text="{Binding Price}" Margin="0,0,0,8" />
                <TextBlock Text="备注" Margin="0,0,0,2" />
                <TextBox Text="{Binding Note, UpdateSourceTrigger=PropertyChanged}" AcceptsReturn="True" Height="80" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto" Margin="0,0,0,12" />
                <StackPanel Orientation="Horizontal">
                    <Button Content="保存" Command="{Binding SaveCommand}" Width="90" Margin="0,0,8,0" />
                    <Button Content="删除" Command="{Binding DeleteCommand}" Width="90" />
                </StackPanel>
            </StackPanel>
        </Border>

        <DataGrid ItemsSource="{Binding Wires}"
                  SelectedItem="{Binding SelectedWire}"
                  AutoGenerateColumns="False"
                  IsReadOnly="True"
                  CanUserAddRows="False"
                  SelectionMode="Single">
            <DataGrid.Columns>
                <DataGridTextColumn Header="型号" Binding="{Binding Model}" Width="140" />
                <DataGridTextColumn Header="厂家" Binding="{Binding Manufacturer}" Width="140" />
                <DataGridTextColumn Header="价格" Binding="{Binding Price, StringFormat=F2}" Width="90" />
                <DataGridTextColumn Header="备注" Binding="{Binding Note}" Width="*" />
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
```

- [ ] **Step 3: 建窗口 code-behind**

Create `src/GlassFactory.BillTracker.App/Views/WireManagementWindow.xaml.cs`:

```csharp
using System.Windows;

namespace GlassFactory.BillTracker.App.Views;

public partial class WireManagementWindow : Window
{
    public WireManagementWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: MainWindowViewModel 加服务与命令**

在 `MainWindowViewModel` 中：字段区加 `private readonly IWireService _wireService;`（需 `using GlassFactory.BillTracker.Data.Services;`）。构造函数签名末尾加参数 `IWireService wireService`，函数体加 `_wireService = wireService;`。命令属性区加 `public RelayCommand OpenWireManagementCommand { get; }`，构造函数内加：

```csharp
        OpenWireManagementCommand = new RelayCommand(OpenWireManagement);
```

在类内加方法：

```csharp
    private void OpenWireManagement()
    {
        var window = new WireManagementWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = new WireManagementViewModel(_wireService)
        };
        window.ShowDialog();
    }
```

（`GlassFactory.BillTracker.App.Views` 已在 using 列表中。）

- [ ] **Step 5: MainWindow.xaml 加按钮**

在工具栏"刷新"按钮后、`<Separator />` 前（或末尾 `<Separator />` 之后）加：

```xml
                <Separator />
                <Button Content="丝管理" Command="{Binding OpenWireManagementCommand}" Margin="2" />
```

- [ ] **Step 6: MainWindow.xaml.cs 装配丝服务**

在 `MainWindow` 构造函数中，`var exportService = ...;` 之后加：

```csharp
        var wireService = new WireService(AppRuntimeContext.DbPath);
```

并把 `_viewModel = new MainWindowViewModel(customerService, orderService, exportService, fileDialogService, printService);` 改为：

```csharp
        _viewModel = new MainWindowViewModel(customerService, orderService, exportService, fileDialogService, printService, wireService);
```

顶部加 `using GlassFactory.BillTracker.Data.Services;`。

- [ ] **Step 7: 编译验证**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug`
Expected: Build succeeded。

- [ ] **Step 8: 提交**

```bash
git add src/GlassFactory.BillTracker.App/ViewModels/WireManagementViewModel.cs src/GlassFactory.BillTracker.App/Views/WireManagementWindow.xaml src/GlassFactory.BillTracker.App/Views/WireManagementWindow.xaml.cs src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs src/GlassFactory.BillTracker.App/Views/MainWindow.xaml src/GlassFactory.BillTracker.App/Views/MainWindow.xaml.cs
git commit -m "feat(app): add wire management window and main-window entry"
```

---

# 阶段 3 — 样块管理系统 + 订单接入

## Task 8: SampleBlock 实体 + OrderItem.SampleBlockModel + 配置 + 关系

**Files:**
- Create: `src/GlassFactory.BillTracker.Domain/Entities/SampleBlock.cs`
- Modify: `src/GlassFactory.BillTracker.Domain/Entities/OrderItem.cs`
- Modify: `src/GlassFactory.BillTracker.Domain/Entities/Wire.cs`
- Create: `src/GlassFactory.BillTracker.Data/Persistence/Configurations/SampleBlockConfiguration.cs`
- Modify: `src/GlassFactory.BillTracker.Data/Persistence/Configurations/WireConfiguration.cs`
- Modify: `src/GlassFactory.BillTracker.Data/Persistence/Configurations/OrderItemConfiguration.cs`
- Modify: `src/GlassFactory.BillTracker.Data/Persistence/BillTrackerDbContext.cs`
- Test: `tests/GlassFactory.BillTracker.Tests/SampleBlockMappingTests.cs`

**Interfaces:**
- Produces: 实体 `SampleBlock { Guid Id; string Model; Guid WireId; Wire Wire; decimal Price; string? Note; DateTime CreatedAt; DateTime UpdatedAt }`；`OrderItem.SampleBlockModel { get; set; }`（`string?`）；`Wire.SampleBlocks`；`BillTrackerDbContext.SampleBlocks`；关系 `SampleBlock.WireId -> Wire`（`Restrict`）。

- [ ] **Step 1: 写失败测试**

Create `tests/GlassFactory.BillTracker.Tests/SampleBlockMappingTests.cs`:

```csharp
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using GlassFactory.BillTracker.Domain.Services;
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
    public async Task SampleBlock_ShouldPersistWithWire()
    {
        await using var db = NewDb();
        var wire = new Wire { Model = "W-1", Price = 3m };
        db.Wires.Add(wire);
        await db.SaveChangesAsync();

        db.SampleBlocks.Add(new SampleBlock { Model = "SB-1", WireId = wire.Id, Price = 20m });
        await db.SaveChangesAsync();

        var reloaded = await db.SampleBlocks.AsNoTracking().Include(x => x.Wire).SingleAsync(x => x.Model == "SB-1");
        Assert.Equal("W-1", reloaded.Wire.Model);
        Assert.Equal(20m, reloaded.Price);
    }

    [Fact]
    public async Task OrderItem_ShouldPersistSampleBlockSnapshot_WithoutAffectingAmount()
    {
        await using var db = NewDb();
        var customer = new Customer { Name = "客户", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };
        db.Customers.Add(customer);

        var item = new OrderItem
        {
            GlassLengthMm = 1000m,
            GlassWidthMm = 1000m,
            Quantity = 1,
            GlassUnitPricePerM2 = 10m,
            Model = "M-1",
            SampleBlockModel = "SB-1",
            WireType = "W-1",
            WireUnitPrice = 99m, // 样块价快照，不计入金额
            HoleFee = 3m,
            OtherFee = 2m
        };
        var order = new Order
        {
            OrderNo = "20260705-0001",
            DateTime = DateTime.Now,
            Customer = customer,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Items = new List<OrderItem> { item }
        };
        OrderAmountCalculator.ApplyOrderTotal(order);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var reloaded = await db.OrderItems.AsNoTracking().SingleAsync(x => x.Model == "M-1");
        Assert.Equal("SB-1", reloaded.SampleBlockModel);
        Assert.Equal("W-1", reloaded.WireType);
        // 金额 = 玻璃费(1.00*1*10=10) + 打孔3 + 其他2 = 15，样块价99 不参与
        Assert.Equal(15m, reloaded.Amount);
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~SampleBlockMappingTests"`
Expected: 编译失败——`SampleBlock` / `SampleBlockModel` 不存在。

- [ ] **Step 3: 建 SampleBlock 实体**

Create `src/GlassFactory.BillTracker.Domain/Entities/SampleBlock.cs`:

```csharp
namespace GlassFactory.BillTracker.Domain.Entities;

public class SampleBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Model { get; set; } = string.Empty;
    public Guid WireId { get; set; }
    public Wire Wire { get; set; } = null!;
    public decimal Price { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
```

- [ ] **Step 4: OrderItem 加 SampleBlockModel**

在 `OrderItem.cs` 的 `Model` 属性之后加：

```csharp
    public string? SampleBlockModel { get; set; }
```

- [ ] **Step 5: Wire 加反向导航**

在 `Wire.cs` 末尾（`UpdatedAt` 之后）加：

```csharp
    public ICollection<SampleBlock> SampleBlocks { get; set; } = new List<SampleBlock>();
```

- [ ] **Step 6: WireConfiguration 配置关系**

在 `WireConfiguration.Configure` 末尾加：

```csharp
        builder.HasMany(x => x.SampleBlocks)
            .WithOne(x => x.Wire)
            .HasForeignKey(x => x.WireId)
            .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 7: 建 SampleBlockConfiguration**

Create `src/GlassFactory.BillTracker.Data/Persistence/Configurations/SampleBlockConfiguration.cs`:

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
        builder.Property(x => x.Price).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.HasIndex(x => x.Model).IsUnique();
        builder.HasIndex(x => x.WireId);
        // WireId -> Wire 关系在 WireConfiguration 单侧配置
    }
}
```

- [ ] **Step 8: OrderItemConfiguration 加 SampleBlockModel**

在 `OrderItemConfiguration.Configure` 内 `Model` 配置之后加：

```csharp
        builder.Property(x => x.SampleBlockModel).HasMaxLength(100);
```

- [ ] **Step 9: DbContext 加 DbSet**

在 `BillTrackerDbContext.cs` 加：

```csharp
    public DbSet<SampleBlock> SampleBlocks => Set<SampleBlock>();
```

- [ ] **Step 10: 跑测试确认通过**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~SampleBlockMappingTests"`
Expected: PASS。

- [ ] **Step 11: 提交**

```bash
git add src/GlassFactory.BillTracker.Domain/Entities/SampleBlock.cs src/GlassFactory.BillTracker.Domain/Entities/OrderItem.cs src/GlassFactory.BillTracker.Domain/Entities/Wire.cs src/GlassFactory.BillTracker.Data/Persistence/Configurations/ src/GlassFactory.BillTracker.Data/Persistence/BillTrackerDbContext.cs tests/GlassFactory.BillTracker.Tests/SampleBlockMappingTests.cs
git commit -m "feat(data): add SampleBlock entity, OrderItem.SampleBlockModel, wire relation"
```

---

## Task 9: SampleBlockService（Data 层）+ 测试（含丝删除拦截）

**Files:**
- Create: `src/GlassFactory.BillTracker.Data/Services/ISampleBlockService.cs`
- Create: `src/GlassFactory.BillTracker.Data/Services/SampleBlockService.cs`
- Test: `tests/GlassFactory.BillTracker.Tests/SampleBlockServiceTests.cs`

**Interfaces:**
- Consumes: `SampleBlock`、`Wire`、`WireService` (Tasks 5, 8)
- Produces:
  - `ISampleBlockService.GetSampleBlocksAsync(string? keyword, CancellationToken) -> Task<List<SampleBlock>>`（含 Wire）
  - `GetByIdAsync(Guid, CancellationToken) -> Task<SampleBlock?>`
  - `GetByModelAsync(string, CancellationToken) -> Task<SampleBlock?>`（含 Wire）
  - `GetByWireIdAsync(Guid, CancellationToken) -> Task<List<SampleBlock>>`
  - `SaveAsync(SampleBlock, CancellationToken) -> Task<SampleBlock>`（型号非空+唯一、WireId 必选且存在）
  - `DeleteAsync(Guid, CancellationToken) -> Task`
  - `SampleBlockService(string dbPath)` 构造。

- [ ] **Step 1: 写失败测试**

Create `tests/GlassFactory.BillTracker.Tests/SampleBlockServiceTests.cs`:

```csharp
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Tests;

public class SampleBlockServiceTests
{
    private static string NewDbPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "GlassFactoryBillTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "billtracker.db");
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>().UseSqlite($"Data Source={dbPath}").Options;
        using var db = new BillTrackerDbContext(options);
        db.Database.EnsureCreated();
        return dbPath;
    }

    [Fact]
    public async Task SaveAsync_ShouldRequireExistingWire()
    {
        var dbPath = NewDbPath();
        var svc = new SampleBlockService(dbPath);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SaveAsync(new SampleBlock { Model = "SB-1", WireId = Guid.NewGuid(), Price = 1m }));
    }

    [Fact]
    public async Task GetByModelAsync_ShouldReturnSampleBlockWithWire()
    {
        var dbPath = NewDbPath();
        var wireSvc = new WireService(dbPath);
        var sbSvc = new SampleBlockService(dbPath);
        var wire = await wireSvc.SaveAsync(new Wire { Model = "W-9", Manufacturer = "厂C", Price = 7m });
        await sbSvc.SaveAsync(new SampleBlock { Model = "SB-9", WireId = wire.Id, Price = 30m });

        var found = await sbSvc.GetByModelAsync("SB-9");
        Assert.NotNull(found);
        Assert.Equal("W-9", found!.Wire.Model);
        Assert.Equal(30m, found.Price);
    }

    [Fact]
    public async Task GetByWireIdAsync_ShouldReturnRelatedSampleBlocks()
    {
        var dbPath = NewDbPath();
        var wireSvc = new WireService(dbPath);
        var sbSvc = new SampleBlockService(dbPath);
        var wire = await wireSvc.SaveAsync(new Wire { Model = "W-10", Price = 1m });
        await sbSvc.SaveAsync(new SampleBlock { Model = "SB-A", WireId = wire.Id, Price = 1m });
        await sbSvc.SaveAsync(new SampleBlock { Model = "SB-B", WireId = wire.Id, Price = 2m });

        var related = await sbSvc.GetByWireIdAsync(wire.Id);
        Assert.Equal(2, related.Count);
    }

    [Fact]
    public async Task WireDelete_ShouldBeBlocked_WhenReferencedBySampleBlock()
    {
        var dbPath = NewDbPath();
        var wireSvc = new WireService(dbPath);
        var sbSvc = new SampleBlockService(dbPath);
        var wire = await wireSvc.SaveAsync(new Wire { Model = "W-11", Price = 1m });
        await sbSvc.SaveAsync(new SampleBlock { Model = "SB-C", WireId = wire.Id, Price = 1m });

        await Assert.ThrowsAsync<InvalidOperationException>(() => wireSvc.DeleteAsync(wire.Id));
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~SampleBlockServiceTests"`
Expected: 编译失败——`SampleBlockService` 不存在。

- [ ] **Step 3: 建接口**

Create `src/GlassFactory.BillTracker.Data/Services/ISampleBlockService.cs`:

```csharp
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.Data.Services;

public interface ISampleBlockService
{
    Task<List<SampleBlock>> GetSampleBlocksAsync(string? keyword = null, CancellationToken cancellationToken = default);
    Task<SampleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SampleBlock?> GetByModelAsync(string model, CancellationToken cancellationToken = default);
    Task<List<SampleBlock>> GetByWireIdAsync(Guid wireId, CancellationToken cancellationToken = default);
    Task<SampleBlock> SaveAsync(SampleBlock sampleBlock, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: 建实现**

Create `src/GlassFactory.BillTracker.Data/Services/SampleBlockService.cs`:

```csharp
using GlassFactory.BillTracker.Data.Persistence;
using GlassFactory.BillTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlassFactory.BillTracker.Data.Services;

public sealed class SampleBlockService : ISampleBlockService
{
    private readonly string _dbPath;

    public SampleBlockService(string dbPath)
    {
        _dbPath = dbPath;
    }

    private BillTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillTrackerDbContext>()
            .UseSqlite($"Data Source={_dbPath}").Options;
        return new BillTrackerDbContext(options);
    }

    public async Task<List<SampleBlock>> GetSampleBlocksAsync(string? keyword = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        var query = db.SampleBlocks.AsNoTracking().Include(x => x.Wire).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x => x.Model.Contains(k) || x.Wire.Model.Contains(k));
        }
        return await query.OrderBy(x => x.Model).ToListAsync(cancellationToken);
    }

    public async Task<SampleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.SampleBlocks.AsNoTracking().Include(x => x.Wire)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<SampleBlock?> GetByModelAsync(string model, CancellationToken cancellationToken = default)
    {
        var normalized = (model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        await using var db = CreateDbContext();
        return await db.SampleBlocks.AsNoTracking().Include(x => x.Wire)
            .FirstOrDefaultAsync(x => x.Model == normalized, cancellationToken);
    }

    public async Task<List<SampleBlock>> GetByWireIdAsync(Guid wireId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        return await db.SampleBlocks.AsNoTracking().Include(x => x.Wire)
            .Where(x => x.WireId == wireId)
            .OrderBy(x => x.Model)
            .ToListAsync(cancellationToken);
    }

    public async Task<SampleBlock> SaveAsync(SampleBlock sampleBlock, CancellationToken cancellationToken = default)
    {
        var normalizedModel = (sampleBlock.Model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            throw new InvalidOperationException("样块型号不能为空。");
        }

        if (sampleBlock.WireId == Guid.Empty)
        {
            throw new InvalidOperationException("请为样块选择丝。");
        }

        await using var db = CreateDbContext();

        var wireExists = await db.Wires.AsNoTracking().AnyAsync(x => x.Id == sampleBlock.WireId, cancellationToken);
        if (!wireExists)
        {
            throw new InvalidOperationException("所选丝不存在，请重新选择。");
        }

        var id = sampleBlock.Id == Guid.Empty ? Guid.NewGuid() : sampleBlock.Id;
        var duplicate = await db.SampleBlocks.AsNoTracking()
            .AnyAsync(x => x.Model == normalizedModel && x.Id != id, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("样块型号已存在，请使用其他型号。");
        }

        var note = string.IsNullOrWhiteSpace(sampleBlock.Note) ? null : sampleBlock.Note.Trim();
        var now = DateTime.Now;

        var existing = await db.SampleBlocks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            var created = new SampleBlock
            {
                Id = id,
                Model = normalizedModel,
                WireId = sampleBlock.WireId,
                Price = sampleBlock.Price,
                Note = note,
                CreatedAt = now,
                UpdatedAt = now
            };
            await db.SampleBlocks.AddAsync(created, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }

        existing.Model = normalizedModel;
        existing.WireId = sampleBlock.WireId;
        existing.Price = sampleBlock.Price;
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
    }
}
```

- [ ] **Step 5: 跑测试确认通过**

Run: `dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj --filter "FullyQualifiedName~SampleBlockServiceTests"`
Expected: PASS（4 个用例，含丝删除拦截）。

- [ ] **Step 6: 提交**

```bash
git add src/GlassFactory.BillTracker.Data/Services/ISampleBlockService.cs src/GlassFactory.BillTracker.Data/Services/SampleBlockService.cs tests/GlassFactory.BillTracker.Tests/SampleBlockServiceTests.cs
git commit -m "feat(data): add SampleBlockService with wire lookups and reverse query"
```

---

## Task 10: 迁移 AddSampleBlock

**Files:**
- Create（生成）: `src/GlassFactory.BillTracker.Data/Migrations/*_AddSampleBlock.cs`

- [ ] **Step 1: 生成迁移**

Run:
```bash
BILLTRACKER_DATA_DIR=/tmp/bt-ef dotnet ef migrations add AddSampleBlock \
  --project src/GlassFactory.BillTracker.Data \
  --startup-project src/GlassFactory.BillTracker.Data
```
Expected: 生成迁移，`Up` 内 `CreateTable("SampleBlocks")`（含 `WireId` 外键 → Wires、`Model` 唯一索引、`WireId` 索引）+ 给 `OrderItems` `AddColumn("SampleBlockModel")`。

> 失败时手写：参照现有 `Migrations/20260302141206_AddOrderItemModelHoleFeeAmount.cs`（`AddColumn`）与 `20260301120123_InitialCreate.cs`（`CreateTable` + `ForeignKey` + `CreateIndex`），并同步更新 `BillTrackerDbContextModelSnapshot.cs`。

- [ ] **Step 2: 构建 + 全量测试**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug && dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug`
Expected: Build succeeded；全部测试 PASS。

- [ ] **Step 3: 提交**

```bash
git add src/GlassFactory.BillTracker.Data/Migrations/
git commit -m "feat(data): add EF migration for SampleBlocks and OrderItem.SampleBlockModel"
```

---

## Task 11: 样块管理窗口 + 主界面入口

**Files:**
- Create: `src/GlassFactory.BillTracker.App/ViewModels/SampleBlockManagementViewModel.cs`
- Create: `src/GlassFactory.BillTracker.App/Views/SampleBlockManagementWindow.xaml`
- Create: `src/GlassFactory.BillTracker.App/Views/SampleBlockManagementWindow.xaml.cs`
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/GlassFactory.BillTracker.App/Views/MainWindow.xaml`
- Modify: `src/GlassFactory.BillTracker.App/Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `ISampleBlockService`、`IWireService` (Tasks 5, 9)
- Produces: `MainWindowViewModel.OpenSampleBlockManagementCommand`；`MainWindowViewModel` 构造新增 `ISampleBlockService` 参数。

- [ ] **Step 1: 建 SampleBlockManagementViewModel**

Create `src/GlassFactory.BillTracker.App/ViewModels/SampleBlockManagementViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using GlassFactory.BillTracker.App.Commands;
using GlassFactory.BillTracker.App.ViewModels.Base;
using GlassFactory.BillTracker.Data.Services;
using GlassFactory.BillTracker.Domain.Entities;

namespace GlassFactory.BillTracker.App.ViewModels;

public sealed class SampleBlockManagementViewModel : ObservableObject
{
    private readonly ISampleBlockService _sampleBlockService;
    private readonly IWireService _wireService;

    private Guid _editingId;
    private string _model = string.Empty;
    private Wire? _selectedWire;
    private decimal _price;
    private string? _note;
    private string? _searchKeyword;
    private SampleBlock? _selectedSampleBlock;

    public ObservableCollection<SampleBlock> SampleBlocks { get; } = new();
    public ObservableCollection<Wire> Wires { get; } = new();

    public SampleBlockManagementViewModel(ISampleBlockService sampleBlockService, IWireService wireService)
    {
        _sampleBlockService = sampleBlockService;
        _wireService = wireService;

        NewCommand = new RelayCommand(ResetForm);
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        DeleteCommand = new RelayCommand(() => _ = DeleteAsync());
        SearchCommand = new RelayCommand(() => _ = LoadSampleBlocksAsync());

        _ = InitAsync();
    }

    public string? SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }

    public SampleBlock? SelectedSampleBlock
    {
        get => _selectedSampleBlock;
        set
        {
            if (SetProperty(ref _selectedSampleBlock, value) && value is not null)
            {
                LoadForm(value);
            }
        }
    }

    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    public Wire? SelectedWire
    {
        get => _selectedWire;
        set => SetProperty(ref _selectedWire, value);
    }

    public decimal Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }

    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand SearchCommand { get; }

    private async Task InitAsync()
    {
        await LoadWiresAsync();
        await LoadSampleBlocksAsync();
    }

    private async Task LoadWiresAsync()
    {
        try
        {
            var wires = await _wireService.GetWiresAsync();
            Wires.Clear();
            foreach (var w in wires)
            {
                Wires.Add(w);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载丝列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task LoadSampleBlocksAsync()
    {
        try
        {
            var items = await _sampleBlockService.GetSampleBlocksAsync(SearchKeyword);
            SampleBlocks.Clear();
            foreach (var sb in items)
            {
                SampleBlocks.Add(sb);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载样块列表失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadForm(SampleBlock sb)
    {
        _editingId = sb.Id;
        Model = sb.Model;
        SelectedWire = Wires.FirstOrDefault(x => x.Id == sb.WireId);
        Price = sb.Price;
        Note = sb.Note;
    }

    private void ResetForm()
    {
        _editingId = Guid.Empty;
        Model = string.Empty;
        SelectedWire = null;
        Price = 0m;
        Note = null;
        SelectedSampleBlock = null;
    }

    private async Task SaveAsync()
    {
        if (SelectedWire is null)
        {
            MessageBox.Show("请为样块选择丝。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var saved = await _sampleBlockService.SaveAsync(new SampleBlock
            {
                Id = _editingId,
                Model = Model,
                WireId = SelectedWire.Id,
                Price = Price,
                Note = Note
            });
            await LoadSampleBlocksAsync();
            SelectedSampleBlock = SampleBlocks.FirstOrDefault(x => x.Id == saved.Id);
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

        if (MessageBox.Show($"确认删除样块“{Model}”吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _sampleBlockService.DeleteAsync(_editingId);
            ResetForm();
            await LoadSampleBlocksAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
```

- [ ] **Step 2: 建窗口 XAML**

Create `src/GlassFactory.BillTracker.App/Views/SampleBlockManagementWindow.xaml`:

```xml
<Window x:Class="GlassFactory.BillTracker.App.Views.SampleBlockManagementWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="样块管理"
        Width="860"
        Height="560"
        WindowStartupLocation="CenterOwner">
    <DockPanel Margin="10">
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,0,0,8">
            <TextBox Width="220" Text="{Binding SearchKeyword, UpdateSourceTrigger=PropertyChanged}" />
            <Button Content="搜索" Command="{Binding SearchCommand}" Margin="6,0,0,0" Width="70" />
            <Button Content="新增" Command="{Binding NewCommand}" Margin="16,0,0,0" Width="70" />
        </StackPanel>

        <Border DockPanel.Dock="Right" BorderBrush="#DDD" BorderThickness="1" Padding="10" Margin="8,0,0,0" Width="300">
            <StackPanel>
                <TextBlock Text="型号 *" Margin="0,0,0,2" />
                <TextBox Text="{Binding Model, UpdateSourceTrigger=PropertyChanged}" MaxLength="100" Margin="0,0,0,8" />
                <TextBlock Text="丝 *" Margin="0,0,0,2" />
                <ComboBox ItemsSource="{Binding Wires}" SelectedItem="{Binding SelectedWire}" DisplayMemberPath="Model" Margin="0,0,0,8" />
                <TextBlock Text="价格" Margin="0,0,0,2" />
                <TextBox Text="{Binding Price}" Margin="0,0,0,8" />
                <TextBlock Text="备注" Margin="0,0,0,2" />
                <TextBox Text="{Binding Note, UpdateSourceTrigger=PropertyChanged}" AcceptsReturn="True" Height="80" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto" Margin="0,0,0,12" />
                <StackPanel Orientation="Horizontal">
                    <Button Content="保存" Command="{Binding SaveCommand}" Width="90" Margin="0,0,8,0" />
                    <Button Content="删除" Command="{Binding DeleteCommand}" Width="90" />
                </StackPanel>
            </StackPanel>
        </Border>

        <DataGrid ItemsSource="{Binding SampleBlocks}"
                  SelectedItem="{Binding SelectedSampleBlock}"
                  AutoGenerateColumns="False"
                  IsReadOnly="True"
                  CanUserAddRows="False"
                  SelectionMode="Single">
            <DataGrid.Columns>
                <DataGridTextColumn Header="型号" Binding="{Binding Model}" Width="150" />
                <DataGridTextColumn Header="丝" Binding="{Binding Wire.Model}" Width="150" />
                <DataGridTextColumn Header="价格" Binding="{Binding Price, StringFormat=F2}" Width="90" />
                <DataGridTextColumn Header="备注" Binding="{Binding Note}" Width="*" />
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
```

- [ ] **Step 3: 建窗口 code-behind**

Create `src/GlassFactory.BillTracker.App/Views/SampleBlockManagementWindow.xaml.cs`:

```csharp
using System.Windows;

namespace GlassFactory.BillTracker.App.Views;

public partial class SampleBlockManagementWindow : Window
{
    public SampleBlockManagementWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: MainWindowViewModel 加样块服务与命令**

字段区加 `private readonly ISampleBlockService _sampleBlockService;`。构造函数签名末尾加参数 `ISampleBlockService sampleBlockService`，函数体加 `_sampleBlockService = sampleBlockService;`。命令属性区加 `public RelayCommand OpenSampleBlockManagementCommand { get; }`，构造函数内加：

```csharp
        OpenSampleBlockManagementCommand = new RelayCommand(OpenSampleBlockManagement);
```

加方法：

```csharp
    private void OpenSampleBlockManagement()
    {
        var window = new SampleBlockManagementWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = new SampleBlockManagementViewModel(_sampleBlockService, _wireService)
        };
        window.ShowDialog();
    }
```

- [ ] **Step 5: MainWindow.xaml 加按钮**

在"丝管理"按钮后加：

```xml
                <Button Content="样块管理" Command="{Binding OpenSampleBlockManagementCommand}" Margin="2" />
```

- [ ] **Step 6: MainWindow.xaml.cs 装配样块服务**

在 `var wireService = ...;` 之后加：

```csharp
        var sampleBlockService = new SampleBlockService(AppRuntimeContext.DbPath);
```

把 `MainWindowViewModel(...)` 调用改为末尾追加 `sampleBlockService`：

```csharp
        _viewModel = new MainWindowViewModel(customerService, orderService, exportService, fileDialogService, printService, wireService, sampleBlockService);
```

- [ ] **Step 7: 编译验证**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug`
Expected: Build succeeded。

- [ ] **Step 8: 提交**

```bash
git add src/GlassFactory.BillTracker.App/ViewModels/SampleBlockManagementViewModel.cs src/GlassFactory.BillTracker.App/Views/SampleBlockManagementWindow.xaml src/GlassFactory.BillTracker.App/Views/SampleBlockManagementWindow.xaml.cs src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs src/GlassFactory.BillTracker.App/Views/MainWindow.xaml src/GlassFactory.BillTracker.App/Views/MainWindow.xaml.cs
git commit -m "feat(app): add sample block management window and main-window entry"
```

---

## Task 12: 丝窗口反查——某种丝涉及的样块

**Files:**
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/WireManagementViewModel.cs`
- Modify: `src/GlassFactory.BillTracker.App/Views/WireManagementWindow.xaml`
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs`（`OpenWireManagement` 传入样块服务）

**Interfaces:**
- Consumes: `ISampleBlockService.GetByWireIdAsync` (Task 9)
- Produces: `WireManagementViewModel` 构造改为 `(IWireService, ISampleBlockService)`；暴露 `ObservableCollection<SampleBlock> RelatedSampleBlocks`。

- [ ] **Step 1: VM 注入样块服务 + 反查集合**

修改 `WireManagementViewModel`：
- 构造签名改为 `public WireManagementViewModel(IWireService wireService, ISampleBlockService sampleBlockService)`，字段加 `private readonly ISampleBlockService _sampleBlockService;` 并在构造体赋值。
- 加 `public ObservableCollection<SampleBlock> RelatedSampleBlocks { get; } = new();`。
- `SelectedWire` setter 内，`LoadForm(value);` 之后加 `_ = LoadRelatedAsync(value.Id);`。选中为 null 时清空：在 setter 内 `value is not null` 分支之外，添加 else 清空逻辑。改写 setter：

```csharp
    public Wire? SelectedWire
    {
        get => _selectedWire;
        set
        {
            if (SetProperty(ref _selectedWire, value))
            {
                if (value is not null)
                {
                    LoadForm(value);
                    _ = LoadRelatedAsync(value.Id);
                }
                else
                {
                    RelatedSampleBlocks.Clear();
                }
            }
        }
    }
```

- 加方法：

```csharp
    private async Task LoadRelatedAsync(Guid wireId)
    {
        try
        {
            var related = await _sampleBlockService.GetByWireIdAsync(wireId);
            RelatedSampleBlocks.Clear();
            foreach (var sb in related)
            {
                RelatedSampleBlocks.Add(sb);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载关联样块失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
```

（需 `using GlassFactory.BillTracker.Data.Services;` 已存在；`Guid`/`Exception` 为全局引用。）

- [ ] **Step 2: XAML 加反查列表**

在 `WireManagementWindow.xaml` 右侧表单 `Border` 内、按钮 `StackPanel` 之后（`</StackPanel>` 前的最外层 StackPanel 里）追加：

```xml
                <TextBlock Text="该丝涉及的样块" FontWeight="Bold" Margin="0,14,0,4" />
                <ListBox ItemsSource="{Binding RelatedSampleBlocks}" DisplayMemberPath="Model" Height="140" />
```

- [ ] **Step 3: MainWindowViewModel 传样块服务给丝窗口**

把 `OpenWireManagement` 改为：

```csharp
    private void OpenWireManagement()
    {
        var window = new WireManagementWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = new WireManagementViewModel(_wireService, _sampleBlockService)
        };
        window.ShowDialog();
    }
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug`
Expected: Build succeeded。

- [ ] **Step 5: 提交**

```bash
git add src/GlassFactory.BillTracker.App/ViewModels/WireManagementViewModel.cs src/GlassFactory.BillTracker.App/Views/WireManagementWindow.xaml src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs
git commit -m "feat(app): show related sample blocks for selected wire"
```

---

## Task 13: 订单接入——样块型号选择器 + 自动带出丝/价格

**Files:**
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/Rows/OrderItemRowViewModel.cs`
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/OrderEditViewModel.cs`
- Modify: `src/GlassFactory.BillTracker.App/Views/OrderEditWindow.xaml`
- Modify: `src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs`（`OpenOrderDialogAsync` 加载样块并传入）

**Interfaces:**
- Consumes: `ISampleBlockService.GetSampleBlocksAsync` (Task 9)
- Produces:
  - `OrderItemRowViewModel` 构造新增可选参 `Func<string, (string WireModel, decimal Price)?>? sampleBlockResolver`；新增 `SampleBlockModel { get; set; }`。
  - `OrderEditViewModel` 构造新增参 `IReadOnlyList<SampleBlock> sampleBlocks`；暴露 `IReadOnlyList<string> SampleBlockModels`。

- [ ] **Step 1: 行 VM 加 SampleBlockModel + resolver**

在 `OrderItemRowViewModel`：
- 字段区加 `private string? _sampleBlockModel;` 和 `private readonly Func<string, (string WireModel, decimal Price)?>? _sampleBlockResolver;`。
- 构造函数改为：

```csharp
    public OrderItemRowViewModel(
        Action? recalculateCallback = null,
        Func<string, (string WireModel, decimal Price)?>? sampleBlockResolver = null)
    {
        _recalculateCallback = recalculateCallback;
        _sampleBlockResolver = sampleBlockResolver;
    }
```

- 在 `Model` 属性之后加：

```csharp
    public string? SampleBlockModel
    {
        get => _sampleBlockModel;
        set
        {
            var normalized = (value ?? string.Empty).Trim();
            if (SetProperty(ref _sampleBlockModel, normalized))
            {
                var resolved = _sampleBlockResolver?.Invoke(normalized);
                if (resolved is { } r)
                {
                    WireType = r.WireModel;
                    WireUnitPrice = r.Price;
                }
            }
        }
    }
```

- `ToEntity()` 内 `Model = ...` 之后加 `SampleBlockModel = string.IsNullOrWhiteSpace(SampleBlockModel) ? null : SampleBlockModel.Trim(),`。
- `FromEntity(...)` 与 `CloneForCopy(...)` 各新增 resolver 形参并透传给 `new OrderItemRowViewModel(recalculateCallback, sampleBlockResolver)`。**在对象初始化器中把 `SampleBlockModel = item.SampleBlockModel` 放在 `WireType`/`WireUnitPrice` 之前**，以保证已存快照覆盖 resolver 的自动带出：

```csharp
    public static OrderItemRowViewModel FromEntity(
        OrderItem item,
        Action? recalculateCallback = null,
        Func<string, (string WireModel, decimal Price)?>? sampleBlockResolver = null)
    {
        var row = new OrderItemRowViewModel(recalculateCallback, sampleBlockResolver)
        {
            Id = item.Id,
            GlassLengthMm = item.GlassLengthMm,
            GlassWidthMm = item.GlassWidthMm,
            Quantity = item.Quantity,
            GlassUnitPricePerM2 = item.GlassUnitPricePerM2,
            Model = item.Model,
            SampleBlockModel = item.SampleBlockModel, // 先设置（可能触发 resolver）
            WireType = item.WireType,                 // 再用已存快照覆盖
            WireUnitPrice = item.WireUnitPrice,
            HoleFee = item.HoleFee,
            OtherFee = item.OtherFee,
            Note = item.Note
        };

        row.Recalculate();
        return row;
    }
```

`CloneForCopy` 同法：新增 resolver 形参，初始化器包含 `SampleBlockModel`（在 `WireType`/`WireUnitPrice` 之前），其余字段照旧（`Id = Guid.Empty`）。

- [ ] **Step 2: OrderEditViewModel 接收样块列表 + 提供 resolver**

- 构造函数签名改为：`public OrderEditViewModel(IReadOnlyList<Customer> customers, IReadOnlyList<SampleBlock> sampleBlocks, string orderNo, Order? existing = null)`。
- 字段加 `private readonly IReadOnlyList<SampleBlock> _sampleBlocks;`，构造体开头 `_sampleBlocks = sampleBlocks;`。
- 暴露列表属性：`public IReadOnlyList<string> SampleBlockModels { get; }`，构造体内赋值 `SampleBlockModels = sampleBlocks.Select(x => x.Model).ToList();`（放在 `_sampleBlocks = ...` 之后）。
- 加私有 resolver 方法：

```csharp
    private (string WireModel, decimal Price)? ResolveSampleBlock(string model)
    {
        var normalized = (model ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var match = _sampleBlocks.FirstOrDefault(x => string.Equals(x.Model, normalized, StringComparison.Ordinal));
        if (match is null)
        {
            return null;
        }

        return (match.Wire?.Model ?? string.Empty, match.Price);
    }
```

- 所有创建行 VM 的位置传入 resolver：
  - 构造体加载既有明细：`Items.Add(OrderItemRowViewModel.FromEntity(item, RecalculateTotal, ResolveSampleBlock));`
  - 空订单占位：`Items.Add(new OrderItemRowViewModel(RecalculateTotal, ResolveSampleBlock));`（构造体末尾与 `DeleteItemCore` 内补位处）
  - `AddItem()`：`Items.Add(new OrderItemRowViewModel(RecalculateTotal, ResolveSampleBlock) { Quantity = 1, WireType = "默认丝" });`
  - `CopySelectedItem()`：`rowToCopy.CloneForCopy(RecalculateTotal, ResolveSampleBlock)`
  - `DeleteItemCore()` 内 `Items.Add(new OrderItemRowViewModel(RecalculateTotal, ResolveSampleBlock));`

（`SampleBlock` 需 `using GlassFactory.BillTracker.Domain.Entities;`——已存在。）

- [ ] **Step 3: OrderEditWindow.xaml 加样块型号/丝/价格列 + 加宽**

- 窗口宽度：`Width="1100"` 改为 `Width="1300"`。
- 明细 `DataGrid` 列：在"型号"列之后插入可编辑样块型号列，并在其后加两只读列：

```xml
                    <DataGridTemplateColumn Header="样块型号" Width="120" MinWidth="110">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <ComboBox IsEditable="True"
                                          Text="{Binding SampleBlockModel, UpdateSourceTrigger=LostFocus}"
                                          ItemsSource="{Binding DataContext.SampleBlockModels, RelativeSource={RelativeSource AncestorType=Window}}" />
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                    <DataGridTextColumn Header="丝" Binding="{Binding WireType}" IsReadOnly="True" Width="100" MinWidth="80" />
                    <DataGridTextColumn Header="价格(参考)" Binding="{Binding WireUnitPrice, StringFormat=F0}" IsReadOnly="True" Width="80" MinWidth="70" ElementStyle="{StaticResource RightAlignedDisplayCellStyle}" />
```

- 备注列已是 `Width="*"`，会自动收缩，无需改动。

- [ ] **Step 4: MainWindowViewModel 加载样块并传入订单 VM**

在 `OpenOrderDialogAsync` 内，`var customerEntities = await _customerService.GetCustomersAsync();` 之后加：

```csharp
            var sampleBlocks = await _sampleBlockService.GetSampleBlocksAsync();
```

把 `var vm = new OrderEditViewModel(customerEntities, orderNo, existingOrder);` 改为：

```csharp
            var vm = new OrderEditViewModel(customerEntities, sampleBlocks, orderNo, existingOrder);
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug`
Expected: Build succeeded。

- [ ] **Step 6: 提交**

```bash
git add src/GlassFactory.BillTracker.App/ViewModels/Rows/OrderItemRowViewModel.cs src/GlassFactory.BillTracker.App/ViewModels/OrderEditViewModel.cs src/GlassFactory.BillTracker.App/Views/OrderEditWindow.xaml src/GlassFactory.BillTracker.App/ViewModels/MainWindowViewModel.cs
git commit -m "feat(app): pick sample block in order editor with auto-filled wire and price"
```

---

## Task 14: 订单保存持久化 SampleBlockModel

**Files:**
- Modify: `src/GlassFactory.BillTracker.App/Services/OrderService.cs`

**Interfaces:**
- Consumes: `OrderItem.SampleBlockModel` (Task 8)
- 说明：`OrderService` 在 App 层（不可被现有测试框架单测）；持久化正确性已由 Task 8 的 `OrderItem_ShouldPersistSampleBlockSnapshot_WithoutAffectingAmount`（DbContext 级）覆盖。本任务用编译验证。

- [ ] **Step 1: 新建分支写入 SampleBlockModel**

在 `OrderService.SaveAsync` 的**新建**分支（`orderModel.Id == Guid.Empty`）构造 `new OrderItem { ... }` 处，`Model = item.Model,` 之后加：

```csharp
                    SampleBlockModel = string.IsNullOrWhiteSpace(item.SampleBlockModel) ? null : item.SampleBlockModel.Trim(),
```

- [ ] **Step 2: 更新分支——新增明细写入 SampleBlockModel**

在**更新**分支中构造 `new OrderItem { ... }`（新增明细）处，同样在 `Model = item.Model,` 之后加同一行：

```csharp
                    SampleBlockModel = string.IsNullOrWhiteSpace(item.SampleBlockModel) ? null : item.SampleBlockModel.Trim(),
```

- [ ] **Step 3: 变更检测与赋值——ApplyIncomingItemIfChanged**

在 `ApplyIncomingItemIfChanged` 的 `changed` 判断里加入 SampleBlockModel 比较（在 `!string.Equals(trackedItem.Model, incomingItem.Model, StringComparison.Ordinal) ||` 附近追加一行）：

```csharp
            !string.Equals(trackedItem.SampleBlockModel ?? string.Empty, incomingItem.SampleBlockModel ?? string.Empty, StringComparison.Ordinal) ||
```

并在实际赋值段（`trackedItem.Model = incomingItem.Model;` 之后）加：

```csharp
        trackedItem.SampleBlockModel = string.IsNullOrWhiteSpace(incomingItem.SampleBlockModel) ? null : incomingItem.SampleBlockModel.Trim();
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug`
Expected: Build succeeded。

- [ ] **Step 5: 提交**

```bash
git add src/GlassFactory.BillTracker.App/Services/OrderService.cs
git commit -m "feat(app): persist OrderItem.SampleBlockModel on save"
```

---

## Task 15: 全量回归 + 文档更新

**Files:**
- Modify: `README.md`（新增子系统与列的简述）
- Modify: `CLAUDE.md`（若存在，补充新实体/服务位置）

- [ ] **Step 1: 全量构建 + 测试**

Run: `dotnet build GlassFactory.BillTracker.sln -c Debug && dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug`
Expected: Build succeeded；全部测试 PASS。

- [ ] **Step 2: 更新 README.md**

在功能清单补充：订单明细"平方(㎡)"列与"总计"行（总数量/总方数）；"丝管理"（型号/厂家/价格/备注）；"样块管理"（型号/丝/价格/备注）；订单明细可选样块型号自动带出丝与参考价（不计入金额）。

- [ ] **Step 3: 更新 CLAUDE.md**

补充：`Wire`/`SampleBlock` 实体位于 Domain；`WireService`/`SampleBlockService` 位于 **Data 层**（`(string dbPath)` 构造，可被测试项目单测，与 `ExportService` 一致）；订单明细 `SampleBlockModel` 为快照字符串，`WireType`/`WireUnitPrice` 复用为样块推导快照，均不计入金额。

- [ ] **Step 4: 提交**

```bash
git add README.md CLAUDE.md
git commit -m "docs: document wire/sample-block subsystems and order columns"
```

---

## 手动验收清单（Windows，人工执行）

WPF 需在 Windows 运行；以下为交付前人工验收：
1. 启动应用，工具栏出现"丝管理""样块管理"按钮。
2. 丝管理：新增/编辑/删除丝；删除被样块引用的丝被拦截并提示。
3. 样块管理：新增样块（必选丝）/编辑/删除；型号重复被拦截。
4. 丝管理选中某丝，右侧"该丝涉及的样块"列出正确样块。
5. 订单编辑：明细"型号"列保留；"数量"与"单价"间出现"平方(㎡)"两位小数；底部"总计"行显示总数量与总方数。
6. 订单明细"样块型号"下拉选择后，"丝""价格(参考)"自动填入；金额不随价格变化。
7. 保存订单再打开，样块型号/丝/参考价快照正确回显；随后修改样块主数据不影响历史订单。
8. `dotnet test` 全绿。
