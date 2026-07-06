# 设计：平方列/总计行 + 丝管理 + 样块管理子系统

日期：2026-07-05
状态：已批准（待写实现计划）

## 1. 背景与目标

夹丝玻璃加工厂离线管理系统当前有客户、订单、账单、导出功能。本次新增：

1. 订单明细每行显示**平方(㎡)**，插入在「单价」列之前。
2. 订单明细底部新增**总计行**，显示总数量、总方数。
3. 新增**丝管理**子系统（型号/厂家/价格/备注）。
4. 新增**样块管理**子系统（型号/丝/价格/备注）。
5. 订单明细可选样块型号，自动带出对应的丝与价格（仅参考）。

### 已确认的关键决策（来自需求澄清）

- 订单接入方式：订单明细**选一个样块型号** → 自动推导对应的丝 → 自动填样块系统中定义的**价格**。一次选择驱动全部。
- 样块↔丝基数：一个样块**恰好一种丝**（丝→样块为一对多，供反查）。
- 现有「型号」列：**保留不变**，另加新列承载样块型号。
- 入口与实现：主界面按钮 + 独立窗口，**分阶段**实现。
- 价格来源：**样块新增价格字段**，订单取样块价；丝自身价格/厂家仅作参考展示。
- 金额影响：带入的价格**仅参考展示，不计入订单金额**（行金额/总额公式不变）。

## 2. 总体架构

沿用现有分层与既有模式，不引入新框架：

- **无 DI 容器**：服务在 `MainWindow` 构造函数手工 `new`；数据库路径经静态 `AppRuntimeContext` 提供。
- **DbContext-per-op**：每个服务方法用 `AppRuntimeContext.CreateDbContext()` 开短生命周期上下文，读用 `AsNoTracking`，写包事务，失败 catch `DbUpdateException` → 友好中文异常。
- **EF 配置**：每实体一个 `IEntityTypeConfiguration<T>`，`ApplyConfigurationsFromAssembly` 自动发现。
- **迁移**：启动时 `Database.Migrate()` 自动应用。新迁移经 `DesignTimeDbContextFactory`（读 `BILLTRACKER_DATA_DIR` 环境变量）生成。
- **管理窗口**：参考 `CustomerEditWindow`/`CustomerService` 模式，各子系统一个独立窗口，主界面工具栏按钮打开。

### 关键取舍

- **快照复用**：`OrderItem` 已有休眠字段 `WireType`(丝型号) 与 `WireUnitPrice`(丝单价)，两者当前不在金额公式中。复用它们存储"所选样块推导出的丝型号 + 样块价格"快照。仅新增一个字符串列 `SampleBlockModel`。
  - 备选：全部新增字段。否决理由：多一个 decimal 价格列迁移，且与现有休眠字段语义重叠，无收益。
- **订单存快照字符串、不建外键**：订单明细以文本保存样块型号/丝型号/价格快照，不 FK 到 `SampleBlock`。好处：样块/丝主数据后续改动或删除不影响历史订单；样块删除不被订单阻塞。唯一的真实外键是 `SampleBlock.WireId → Wire`（`Restrict`）。

## 3. 数据模型

### 3.1 新实体 `Wire`（丝）

`src/GlassFactory.BillTracker.Domain/Entities/Wire.cs`

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | Guid | 主键，默认 `Guid.NewGuid()` |
| Model | string | 型号，必填 |
| Manufacturer | string? | 厂家 |
| Price | decimal | 价格（参考） |
| Note | string? | 备注 |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| SampleBlocks | `ICollection<SampleBlock>` | 反向导航 |

`WireConfiguration`：表 `Wires`；`Model` 必填 `HasMaxLength(100)` + **唯一索引**；`Manufacturer` `HasMaxLength(200)`；`Price` `HasPrecision(18,4)`；`Note` `HasMaxLength(2000)`；`CreatedAt`/`UpdatedAt` 必填；`HasMany(SampleBlocks).WithOne(Wire).HasForeignKey(WireId).OnDelete(Restrict)`。

### 3.2 新实体 `SampleBlock`（样块）

`src/GlassFactory.BillTracker.Domain/Entities/SampleBlock.cs`

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | Guid | 主键 |
| Model | string | 型号，必填 |
| WireId | Guid | 外键→Wire，恰好一个 |
| Wire | Wire | 导航 |
| Price | decimal | 价格（新增；订单取此价） |
| Note | string? | 备注 |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

`SampleBlockConfiguration`：表 `SampleBlocks`；`Model` 必填 `HasMaxLength(100)` + **唯一索引**；`Price` `HasPrecision(18,4)`；`Note` `HasMaxLength(2000)`；`HasIndex(WireId)`；关系已由 `WireConfiguration` 配置（保持单侧配置避免重复）。

### 3.3 `OrderItem` 改造

- 保留现有 `Model`(型号, ≤13 字符) 语义与列不变。
- **新增** `SampleBlockModel` (string?) — 所选样块型号快照。`OrderItemConfiguration` 加 `HasMaxLength(100)`。
- **复用** `WireType` (现有 string) ← 所选样块推导出的丝型号快照（当前默认 "默认丝"，改为选样块后写入其丝型号；未选时保留原值/空）。
- **复用** `WireUnitPrice` (现有 decimal) ← 所选样块的价格快照（参考，不计金额）。

### 3.4 `BillTrackerDbContext`

新增 `DbSet<Wire> Wires`、`DbSet<SampleBlock> SampleBlocks`。

## 4. 阶段 1 — 订单：平方列 + 总计行

不依赖新实体，先行落地。

### 4.1 平方(㎡) 列

- `OrderItemRowViewModel` 新增只读属性 `AreaM2`：`Math.Round(OrderAmountCalculator.CalculateAreaM2(GlassLengthMm, GlassWidthMm), 2, MidpointRounding.AwayFromZero)`。在 `GlassLengthMm`/`GlassWidthMm` 变更（现有 `Recalculate()` 路径）时通知刷新。
- 领域 `OrderAmountCalculator` 不改（`CalculateAreaM2` 已存在）。
- `OrderEditWindow.xaml`：在「数量」与「单价(元/㎡)」之间插入只读列 `Header="平方(㎡)"`，`Binding=AreaM2, StringFormat=F2`，右对齐，`IsReadOnly=True`。

### 4.2 总计行

- `OrderEditViewModel` 新增只读属性 `TotalQuantity` (int) = `Σ Items.Quantity`；`TotalAreaM2` (decimal) = `Round(Σ CalculateAreaM2, 2, AwayFromZero)`。在现有 `RecalculateTotal()` 内一并计算并通知。
- `OrderEditWindow.xaml`：明细表下方新增一行（Grid 行），最左显示「总计」，其后显示 `总数量：{TotalQuantity}`、`总方数：{TotalAreaM2:F2}`。现有右侧「合计：{TotalAmount:F0}」保留。
- 说明：每行显示的面积为逐行 Round 到 2 位；总方数为原始面积求和后 Round 到 2 位，二者极端情况下可能有 0.01 差异，属预期。

## 5. 阶段 2 — 丝管理系统

### 5.1 数据层

- `Wire` 实体 + `WireConfiguration` + DbSet。
- 迁移：`AddWire`（建 `Wires` 表）。

### 5.2 服务 `IWireService` / `WireService`

置于 App 层 `Services/`（与 `CustomerService` 同层，沿用 DbContext-per-op）：

- `Task<List<Wire>> GetWiresAsync(string? keyword = null, CancellationToken)` — 关键词匹配型号/厂家。
- `Task<Wire?> GetByIdAsync(Guid, CancellationToken)`
- `Task<Wire?> GetByModelAsync(string model, CancellationToken)` — 供样块/订单查询提取厂家/价格/备注。
- `Task<Wire> SaveAsync(Wire, CancellationToken)` — 校验型号非空、型号唯一（排除自身）；新增/更新分支同 `CustomerService`。
- `Task DeleteAsync(Guid, CancellationToken)` — 被样块引用时 `DbUpdateException` → "该丝已被样块引用，无法删除。"

### 5.3 UI

- `WireManagementViewModel`：`ObservableCollection<Wire> Wires`；搜索关键词；表单字段 Model/Manufacturer/Price/Note；命令 新增/保存/删除/刷新；选中行载入表单。
- `WireManagementWindow.xaml`/`.cs`：左侧 DataGrid 列表 + 右侧表单 + 按钮。
- 主界面 `MainWindow.xaml` 工具栏加「丝管理」按钮 → `MainWindowViewModel` 加命令打开 `WireManagementWindow`（`ShowDialog`）。窗口构造时手工 `new WireService()`（沿用手工装配）。

## 6. 阶段 3 — 样块管理系统 + 接入订单

### 6.1 数据层

- `SampleBlock` 实体 + `SampleBlockConfiguration` + DbSet。
- 迁移：`AddSampleBlockAndOrderItemSampleBlockModel`（建 `SampleBlocks` 表 **且** 给 `OrderItems` 加 `SampleBlockModel` 列，可空）。

### 6.2 服务 `ISampleBlockService` / `SampleBlockService`

- `Task<List<SampleBlock>> GetSampleBlocksAsync(string? keyword = null, CancellationToken)` — `Include(Wire)`。
- `Task<SampleBlock?> GetByIdAsync(Guid, CancellationToken)` — `Include(Wire)`。
- `Task<SampleBlock?> GetByModelAsync(string model, CancellationToken)` — `Include(Wire)`，供订单自动带出。
- `Task<List<SampleBlock>> GetByWireIdAsync(Guid wireId, CancellationToken)` — 供丝窗口反查"某种丝涉及的样块"。
- `Task<SampleBlock> SaveAsync(SampleBlock, CancellationToken)` — 校验型号非空+唯一、`WireId` 必选且存在。
- `Task DeleteAsync(Guid, CancellationToken)` — 无订单外键，可直接删。

### 6.3 样块管理 UI

- `SampleBlockManagementViewModel`：`ObservableCollection<SampleBlock>`；可选丝列表（`WireService.GetWiresAsync`）；表单 型号 / 丝(下拉) / 价格 / 备注；增改删刷新。
- `SampleBlockManagementWindow.xaml`/`.cs`：列表 + 表单（丝用 `ComboBox` 绑定丝列表，`DisplayMemberPath=Model`）。
- 主界面工具栏加「样块管理」按钮 → 打开窗口。

### 6.4 丝窗口反查（满足需求 5(2)）

- `WireManagementWindow` 选中丝时，下方只读列出该丝涉及的样块（`SampleBlockService.GetByWireIdAsync`）。为此 `WireManagementViewModel` 需引用 `ISampleBlockService`（窗口构造时 `new`）。

### 6.5 订单接入（满足需求 4(2)/5(2)）

- `MainWindowViewModel` 在打开订单编辑前加载样块列表（`SampleBlockService.GetSampleBlocksAsync`，含丝），传入 `OrderEditViewModel`（同现有 `customers` 列表模式）。为此 `MainWindow` 构造需 `new SampleBlockService()` 并传入 `MainWindowViewModel`。
- `OrderEditViewModel`：持有样块列表；提供解析方法 `ResolveSampleBlock(string model)` → 命中则返回 (丝型号, 样块价格)。将解析回调传给行 VM（类似现有 `recalculateCallback`），避免行 VM 直接依赖服务。
- `OrderItemRowViewModel`：新增 `SampleBlockModel` 属性；setter 内调用解析回调，命中则写 `WireType` = 丝型号、`WireUnitPrice` = 样块价格；未命中保留输入文本、不带出、不报错。`ToEntity`/`FromEntity`/`CloneForCopy` 同步 `SampleBlockModel`。
- `OrderEditWindow.xaml` 明细表：
  - 新增可编辑列「样块型号」（`ComboBox`，`IsEditable=True`，autocomplete，ItemsSource=样块型号列表；置于「型号」列之后）。
  - 新增只读列「丝」(绑定 `WireType`)。
  - 新增只读列「价格(参考)」(绑定 `WireUnitPrice`, `F0`)。
  - 窗口宽度 1100 → **约 1300**，压缩「备注」列以容纳新列。
- 保存：样块型号可选；已有 `OrderService.SaveAsync` 已写 `WireType`/`WireUnitPrice`，新增 `SampleBlockModel` 的读写（新建与更新两分支 + `ApplyIncomingItemIfChanged` 变更检测）。

## 7. 范围外（本轮不做）

- Excel 导出 / 打印账单 中的样块、丝、平方/总计新列——暂不改动（现有导出的「面积㎡」若存在则保持）。可作后续阶段。
- 样块价计入订单金额——已确认仅参考、不计入。
- 丝/样块的拼音搜索、批量导入、软删除、审计——不在本轮。

## 8. 测试计划

现有测试项目 `tests/GlassFactory.BillTracker.Tests`（xUnit + 临时 SQLite 文件）。新增：

- **阶段 1**：`OrderAmountCalculator`/行 VM 面积计算：mm→㎡、Round 2 位、AwayFromZero；总数量/总方数聚合（含逐行 Round 与总额 Round 的差异用例）。
- **阶段 2**：`WireService` 保存型号唯一校验、按型号查、被样块引用删除受阻。
- **阶段 3**：`SampleBlockService` 保存（丝必选/型号唯一）、按丝反查、按型号查带出丝与价格；订单保存后 `SampleBlockModel`/`WireType`/`WireUnitPrice` 快照正确写入且 `Amount` 不受价格影响（回归：金额公式不变）。

## 9. 实现顺序

1. 阶段 1（订单平方列 + 总计行）——独立、可先交付。
2. 阶段 2（丝管理）——实体/服务/窗口/入口。
3. 阶段 3（样块管理 + 订单接入 + 丝窗口反查）。

每阶段结束跑 `dotnet test` 与 `dotnet build`（App 目标 `net8.0-windows`，`EnableWindowsTargeting=true` 可在非 Windows 构建，但 WPF 运行需 Windows）。
