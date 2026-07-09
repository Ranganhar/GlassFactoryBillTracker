# 设计：丝/样块子系统改造 + 订单去集成 + 打印合计

日期：2026-07-06
状态：已批准（待写实现计划）

## 1. 背景与目标

在上一版（2026-07-05 spec）里，丝与样块互相绑定、样块接入订单。经验证后需求变更：把丝、样块改成**两个独立主数据表**，各带**多图附件 + 筛选**；订单不再引用样块；打印底部增加数量/方数合计。

### 已确认的关键决策

- 附件：每条记录可传**多张图片**，独立附件表（仿 `OrderAttachment`），文件存 `attachments/wires/<WireId>/`、`attachments/sampleblocks/<SampleBlockId>/`；管理窗口显示缩略图，双击放大（复用现有 `ImageViewWindow` + `PathToImageSourceConverter`）。
- 样块"客户"字段：**自由文本**（不引用 Customer 表）。
- 进货时间 / 订单时间：**仅日期（年月日）**，存 `DateTime?`（时间部分为 00:00），`DatePicker` 选择。
- 丝/样块表：**可自由改结构**，被删列的旧值可弃（这两表无需保留的数据）。
- 列表"附件"列：显示**张数**（如 "3张"）；缩略图在选中后的表单区展示。

## 2. 总体架构

沿用现有分层与模式：无 DI 容器、手工装配、DbContext-per-op、`IEntityTypeConfiguration<T>` 自动发现、启动 `Database.Migrate()`、服务在 Data 层用构造参数（不走静态定位器）。

- 附件文件读写需要 `dataDir`，因此 `WireService`/`SampleBlockService` 构造从 `(string dbPath)` 改为 `(string dbPath, string dataDir)`（与 `AttachmentService(dataDir, dbPath)` 同时持有二者一致）。App 侧用 `new WireService(AppRuntimeContext.DbPath, AppRuntimeContext.DataDir)` 装配。
- 附件缩略图/查看器复用订单编辑器既有 UI：`PathToImageSourceConverter`、`ImageViewWindow`、`WrapPanel` 缩略图条，行 VM 仿 `OrderAttachmentRowViewModel`（`AbsolutePath`/`DisplayName`/`SourcePath`/`RelativePath`）。

## 3. A — 订单去集成（回退订单侧）

- `Views/OrderEditWindow.xaml`：删除 `样块型号`(DataGridTemplateColumn) / `丝` / `价格(参考)` 三列。保留列序：型号/长/宽/数量/**平方(㎡)**/单价/打孔费/其他费用/金额/备注，及底部"总计"行（总数量/总方数/合计）。窗口 `Width` 1300 → 1100。
- `ViewModels/Rows/OrderItemRowViewModel.cs`：移除 `SampleBlockModel` 属性、`_sampleBlockResolver` 字段、构造第二参数，及 `FromEntity`/`CloneForCopy`/`ToEntity` 中的 SampleBlockModel 处理。保留 `AreaM2`。`WireType`/`WireUnitPrice` 恢复为存储但不显示（`AddItem` 仍设 `WireType="默认丝"` 以满足 `IsRequired`）。
- `ViewModels/OrderEditViewModel.cs`：构造签名去掉 `IReadOnlyList<SampleBlock> sampleBlocks`；删除 `SampleBlockModels`、`ResolveSampleBlock`；所有行 VM 创建点不再传 resolver。
- `ViewModels/MainWindowViewModel.cs`：`OpenOrderDialogAsync` 不再 `GetSampleBlocksAsync()`，`OrderEditViewModel` 构造回到 `(customerEntities, orderNo, existingOrder)`。
- `App/Services/OrderService.cs`：`SaveAsync` 新建/更新分支及 `ApplyIncomingItemIfChanged` 移除 `SampleBlockModel` 读写。
- `Domain/Entities/OrderItem.cs`：删除 `SampleBlockModel` 属性。`Data/.../OrderItemConfiguration.cs`：删除 `SampleBlockModel` 的 `HasMaxLength` 配置（保留两条 `HasIndex`）。
- 迁移 `RemoveOrderItemSampleBlockModel`：`DropColumn OrderItems.SampleBlockModel`（现全 NULL，无损失）。

## 4. B — 丝管理改造

### 4.1 实体

`Domain/Entities/Wire.cs`

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | Guid | 主键 |
| Model | string | 型号，必填，唯一索引 |
| Price | decimal | 价格 |
| PurchaseDate | DateTime? | 进货时间（仅日期） |
| Note | string? | 备注 |
| CreatedAt/UpdatedAt | DateTime | |
| Attachments | `ICollection<WireAttachment>` | 导航 |

去掉 `Manufacturer`。**保留** `SampleBlocks` 导航——丝↔样块解绑统一放在 C 阶段做（本阶段模型仍与样块相连，保持一致）。

`Domain/Entities/WireAttachment.cs`：`Id, WireId(Guid), Wire(nav), RelativePath(string), CreatedAt(DateTime)`。

### 4.2 EF 配置

- `WireConfiguration`：`Model` required maxlen100 + unique；`Price` precision(18,4)；`PurchaseDate` 无特殊；`Note` maxlen2000；`HasMany(Attachments).WithOne(Wire).HasForeignKey(WireId).OnDelete(Cascade)`。本阶段**保留**原 `HasMany(SampleBlocks)` 关系（C 阶段再移除）。
- `WireAttachmentConfiguration`：表 `WireAttachments`；`RelativePath` required maxlen1024；`HasIndex(WireId)`。

### 4.3 服务 `IWireService`/`WireService`（Data 层，`(dbPath, dataDir)`）

- `GetWiresAsync(WireFilter filter)` → `List<Wire>`，`Include(Attachments)`；筛选：`Model.Contains`、`Price >= Min`/`<= Max`、`PurchaseDate >= From`/`<= To`、`Note.Contains`。按 Model 排序。
- `GetByIdAsync(Guid)` → 含附件。
- `SaveAsync(Wire)`：Model 非空+唯一（排除自身）；新建/更新分支。
- `DeleteAsync(Guid)`：删记录（附件行级联）+ 删 `attachments/wires/<id>/` 目录。
- `AddAttachmentAsync(Guid wireId, string sourcePath)`：复制到 `attachments/wires/<wireId>/`（重名加时间戳），写 `WireAttachment` 行，返回相对路径。
- `RemoveAttachmentAsync(Guid attachmentId)`：删文件 + 删行。

`WireFilter`（**Data 层** DTO，供服务与测试直接引用）：`string? Model; decimal? PriceMin; decimal? PriceMax; DateTime? PurchaseFrom; DateTime? PurchaseTo; string? Note;`。

### 4.4 窗口 `WireManagementWindow` + `WireManagementViewModel`

- 顶部筛选条：型号(TextBox)、价格 min/max(TextBox)、进货时间 起/止(DatePicker)、备注(TextBox)、查询/清除。
- 列表 DataGrid：型号、价格、进货时间(`yyyy-MM-dd`)、备注、附件(张数)。
- 右侧表单：型号、价格、进货时间(DatePicker)、备注(TextBox)；缩略图条(`WrapPanel`+`ListBox`，双击 `ImageViewWindow` 放大)、添加图片/移除选中图片；新增/保存/删除。
- 附件在编辑态暂存（新增源路径列表 + 待删附件 Id 列表），保存记录后由 VM 调 `AddAttachmentAsync`/`RemoveAttachmentAsync` 落盘。图片选择用现有 `Microsoft.Win32.OpenFileDialog`（图片过滤，多选），同订单编辑器。

### 4.5 迁移 `ReworkWire`

`Wires`：`DropColumn Manufacturer`；`AddColumn PurchaseDate(TEXT null)`。`CreateTable WireAttachments`（FK→Wires，Cascade，`WireId` 索引）。

## 5. C — 样块管理改造（与丝解绑）

### 5.1 实体

`Domain/Entities/SampleBlock.cs`

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | Guid | 主键 |
| Model | string | 型号，必填，唯一索引 |
| Customer | string? | 客户（自由文本） |
| OrderTime | DateTime? | 订单时间（仅日期） |
| Note | string? | 备注 |
| CreatedAt/UpdatedAt | DateTime | |
| Attachments | `ICollection<SampleBlockAttachment>` | 导航 |

去掉 `WireId`、`Wire`、`Price`。

`Domain/Entities/SampleBlockAttachment.cs`：`Id, SampleBlockId, SampleBlock(nav), RelativePath, CreatedAt`。

### 5.2 EF 配置

- `SampleBlockConfiguration`：`Model` required maxlen100 + unique；`Customer` maxlen200；`Note` maxlen2000；`OrderTime` 无特殊；`HasMany(Attachments).WithOne(SampleBlock).HasForeignKey(SampleBlockId).OnDelete(Cascade)`。移除 `WireId` 索引与关系。
- `SampleBlockAttachmentConfiguration`：表 `SampleBlockAttachments`；`RelativePath` required maxlen1024；`HasIndex(SampleBlockId)`。
- `WireConfiguration`：本阶段移除 `HasMany(SampleBlocks)` 关系（丝与样块解绑）。

### 5.3 服务 `ISampleBlockService`/`SampleBlockService`（Data 层，`(dbPath, dataDir)`）

- `GetSampleBlocksAsync(SampleBlockFilter)` → `Include(Attachments)`；筛选：`Model.Contains`、`Customer.Contains`、`OrderTime >= From`/`<= To`、`Note.Contains`。
- `GetByIdAsync`、`SaveAsync`（Model 非空+唯一）、`DeleteAsync`（级联 + 删 `attachments/sampleblocks/<id>/`）、`AddAttachmentAsync`、`RemoveAttachmentAsync`（同丝服务模式）。
- 移除 `GetByModelAsync`、`GetByWireIdAsync`。

`SampleBlockFilter`（**Data 层** DTO）：`string? Model; string? Customer; DateTime? OrderFrom; DateTime? OrderTo; string? Note;`。

### 5.4 窗口 `SampleBlockManagementWindow` + VM

- 筛选条：型号、客户、订单时间 起/止、备注、查询/清除。
- 列表列：型号、客户、订单时间(`yyyy-MM-dd`)、备注、附件(张数)。
- 表单：型号、客户(TextBox)、订单时间(DatePicker)、备注；缩略图条 + 添加/移除；新增/保存/删除。
- 去掉丝下拉、去掉 `IWireService` 依赖。
- **丝窗口反查移除**：`WireManagementViewModel` 去掉 `ISampleBlockService`、`RelatedSampleBlocks`、`LoadRelatedAsync`；`WireManagementWindow.xaml` 去掉"该丝涉及的样块" ListBox；`MainWindowViewModel.OpenWireManagement` 只传丝服务。

### 5.5 迁移 `ReworkSampleBlock`

`SampleBlocks`：`DropColumn WireId`、`DropColumn Price`（连带丝 FK/索引）；`AddColumn Customer(TEXT null)`、`AddColumn OrderTime(TEXT null)`。`CreateTable SampleBlockAttachments`。

## 6. D — 打印合计

`App/Services/PrintService.cs`：点阵 `RenderDotMatrixTriplicate` 与 A4 `RenderA4` 的底部合计行，由 `合计：{金额}` 改为 `总数量：{数量}    总方数：{方数}    合计：{金额}`（同一右对齐底部行）。

- 每单：`总数量 = Σ item.Quantity`；`总方数 = Σ OrderAmountCalculator.CalculateAreaM2Rounded(item.GlassLengthMm, item.GlassWidthMm)`（单件面积求和，`F2`，与编辑器 `CalculateTotalAreaM2` 口径一致）。
- 新增私有 helper 构造该底部文本，`RenderDotMatrixTriplicate`（`BuildPageTable` 的 `totalText` 只在末页生成）与 `RenderA4`（`CreateBillCopyPanel` 的合计单元）各自替换。
- `OrderExportItemDto` 已含 `GlassLengthMm`/`GlassWidthMm`/`Quantity`，无需改导出 DTO。

## 7. 范围外

- Excel/JSON 导出的丝/样块新列——不动。
- 样块"客户"不做与 Customer 表的关联/校验（自由文本）。
- 丝/样块列表内**不**内嵌大缩略图（仅张数 + 选中表单区看图）。

## 8. 测试计划

- **Domain**：面积/合计 helper 已有测试；不变。
- **Data**：
  - `WireService`：筛选（型号含/价格区间/进货时间区间/备注含）、Model 唯一、删除级联附件、`AddAttachmentAsync` 落盘+行、`RemoveAttachmentAsync`（临时 dataDir）。
  - `SampleBlockService`：筛选（型号/客户/订单时间区间/备注）、唯一、附件增删。
  - 迁移链：应用到全新 DB；应用到 **bills 旧库副本**验证订单/客户/明细保留、掉列无副作用。
- **App/WPF**：`dotnet build` 编译验证（现有测试框架不覆盖 App/WPF）。
- **打印**：合计文本 helper 抽为可测纯函数（输入明细→输出 `总数量/总方数/合计` 文本），加 Domain/纯函数测试。

## 9. 实现顺序

1. A 订单去集成（含删列迁移）。
2. B 丝改造（实体/附件/服务/迁移/窗口/筛选）。
3. C 样块改造（实体/附件/服务/迁移/窗口/筛选 + 移除丝窗口反查）。
4. D 打印合计。

每阶段跑 `dotnet build` + `dotnet test`；迁移阶段额外在旧库副本上验证向后兼容。
