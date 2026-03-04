# 夹丝玻璃加工厂离线管理系统

## 1. 项目简介
本项目是一个面向夹丝玻璃加工厂的离线桌面系统，用于客户、订单、账单与导出管理。

核心目标：
- 在本地稳定运行（SQLite 单文件）
- 提供订单明细级金额计算（mm -> ㎡）
- 提供可组合筛选与快速查询
- 提供 Excel/JSON 导出
- 提供 Windows 一键发布与验收链路

## 2. 功能清单
- 客户管理
  - 新增、编辑、删除客户
  - 删除存在订单的客户会被阻止（Restrict）
  - 左侧客户列表点击后自动过滤订单
- 订单管理
  - 新增、编辑、删除订单
  - 支持多条订单明细
  - 支持订单状态：未收款、部分收款、已收款
  - 支持单张附件上传（并预留多附件接口）
- 筛选与搜索
  - 客户、日期范围、金额范围、支付方式、订单状态、关键词
  - 输入防抖（300ms）、回车立即查询
  - 筛选条件条（可逐项移除）
  - 结果统计：条目数与合计金额
- 导出
  - Excel 导出（Orders + OrderItems 两个 Sheet）
  - JSON 导出（Orders）
- 数据目录机制
  - 首次启动选择 data_dir
  - 路径持久化到 %APPDATA%
  - 自动创建 billtracker.db / attachments / exports / logs

## 3. 技术栈与架构
- 技术栈
  - .NET 8
  - WPF（Windows Desktop）
  - SQLite
  - Entity Framework Core
  - ClosedXML
  - Serilog
  - xUnit
- 架构分层
  - App：UI、ViewModel、交互服务
  - Domain：实体、枚举、金额计算规则
  - Data：DbContext、EF 配置、迁移、导出服务
  - Infrastructure：数据目录服务、附件服务、日志相关基础能力

## 4. 目录结构

```text
.
├─ src/
│  ├─ GlassFactory.BillTracker.App/
│  ├─ GlassFactory.BillTracker.App.Win7/
│  ├─ GlassFactory.BillTracker.Domain/
│  ├─ GlassFactory.BillTracker.Data/
│  └─ GlassFactory.BillTracker.Infrastructure/
├─ tests/
│  └─ GlassFactory.BillTracker.Tests/
├─ tools/
│  └─ GlassFactory.BillTracker.DbSmokeTest/
├─ scripts/
│  ├─ build_windows.ps1
│  ├─ build_windows7.ps1
│  ├─ publish_smoke_check.ps1
│  ├─ collect_logs.ps1
│  └─ repro_collect.ps1
├─ dist/                  # Windows 发布后产物目录
├─ artifacts/             # 中间发布目录
├─ GlassFactory.BillTracker.sln
├─ Directory.Packages.props
└─ README.md
```

## 5. 金额计算规则（MVP 固定规则）
- 输入单位
  - 长宽：mm
  - 玻璃单价：元/㎡
- 公式
  - 面积(㎡) = (LengthMm / 1000m) * (WidthMm / 1000m)
  - 玻璃费用 = AreaM2 * Quantity * GlassUnitPricePerM2
  - 行金额 = 玻璃费用 + WireUnitPrice + OtherFee
  - 订单总额 = Sum(LineAmount)
- 精度
  - 全程 decimal
  - 统一 Math.Round(x, 4, MidpointRounding.AwayFromZero)

示例：
- 明细 A 行金额 = 223.3819
- 明细 B 行金额 = 362.5551
- 订单总额 = 585.9370

## 6. 数据存储说明
### 6.1 data_dir 选择流程
1. 首次启动弹窗选择 data_dir（建议 D/E 盘）
2. 若选择 C 盘会提示风险，但允许确认
3. 选择结果保存后，下次启动自动复用

### 6.2 设置文件位置
- %APPDATA%/GlassFactoryBillTracker/settings.json

### 6.3 data_dir 目录结构
- billtracker.db
- attachments/
- exports/
- logs/（app.log）

## 7. Windows 运行与发布
### 7.0 双版本发布策略
- Win10/11 版本（现有）
  - Runtime: .NET 8 self-contained
  - Script: scripts/build_windows.ps1
  - Dist: dist/win-x64 or dist/win-arm64
- Win7 x64 版本（新增）
  - Runtime: .NET Framework 4.8 WPF
  - Script: scripts/build_windows7.ps1
  - Dist: dist/win7-x64

### 7.1 一键发布
在仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_windows.ps1
```

可选参数：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_windows.ps1 -Runtime win-arm64
powershell -ExecutionPolicy Bypass -File .\scripts\build_windows.ps1 -Configuration Release
```

脚本会自动执行：
- SDK 版本检查（要求 .NET 8+）
- restore / build / test
- publish（单文件 + self-contained + 不裁剪）
- 整理产物到 dist/<runtime>/
- 输出 exe 路径与文件大小

### 7.2 固定产物路径
- dist/win-x64/GlassFactory.BillTracker.App.exe
- dist/win-arm64/GlassFactory.BillTracker.App.exe
- dist/win7-x64/GlassFactory.BillTracker.App.Win7.exe

### 7.3 Win7 x64 构建
Run on Windows 7 x64 shell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_windows7.ps1
```

Win7 prerequisites:
- .NET Framework 4.8 Developer Pack
- Visual Studio Build Tools with MSBuild and .NET desktop workload

If prerequisites are missing, script prints an explicit install instruction.

### 7.3.1 Windows 7 note
- The script always prints a startup header with timestamp, PowerShell version, OS version, and current directory.
- The script prints step markers for every stage and never waits for interactive input.
- The script prints full failure diagnostics: exception type, message, inner exception, stack trace, and failing script line/command.
- The script resolves script paths with MyInvocation for PowerShell 2.0 compatibility.
- The main application target net8.0-windows requires Windows 10/11 for supported build and runtime.
- If dotnet is missing on Windows 7, the script exits with code 2 and prints a clean action plan.
- If dotnet is missing, the script exits with code 2 and prints installation guidance.
- If the detected target framework is unsupported on Windows 7 (for example net8.0-windows), the script exits with code 3 and prints clear next steps:
  - Build on Windows 10/11 with scripts/build_windows.ps1 (recommended)
  - Retarget to a Windows 7 compatible framework (for example net48) and adjust dependencies
- On success, the script prints the final executable path in dist/win7-x64.

### 7.4 首次运行注意事项
- 首次启动会要求选择 data_dir
- 建议使用 D:\BillTrackerData 或 E:\BillTrackerData
- 不建议长期放在系统盘根目录

### 7.5 发布后快速自检

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish_smoke_check.ps1
```

默认行为（idempotent）：
- 脚本每次运行都会自动生成唯一临时 DataDir（位于 `%TEMP%`）
- 可重复执行，不会因为历史 smoke 数据导致 `Orders.OrderNo` 唯一约束冲突
- 脚本会在输出中打印本次实际使用的 DataDir

可选：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish_smoke_check.ps1 -Runtime win-arm64 -DataDir D:\BillTrackerSmoke
```

说明：如果显式传入 `-DataDir`，则使用该固定目录。

## 8. 使用指南（最短路径）
1. 启动应用并选择 data_dir
2. 左侧新增客户
3. 点击新建订单，添加明细并保存
4. 在主界面按条件筛选订单
5. 点击导出 Excel，生成报表

## 9. 导出说明
### 9.1 Excel（.xlsx）
- 文件名默认：GlassFactoryBillTracker_Orders_yyyyMMdd_HHmmss.xlsx
- 默认目录：data_dir/exports
- 可另存为任意路径

Sheet: Orders（字段顺序）
1. 订单号 OrderNo
2. 日期时间 DateTime
3. 客户 CustomerName
4. 支付方式 PaymentMethod
5. 订单状态 OrderStatus
6. 总金额 TotalAmount
7. 备注 Note
8. 附件 AttachmentPath

Sheet: OrderItems（字段顺序）
1. 订单号 OrderNo
2. 长(mm) GlassLengthMm
3. 宽(mm) GlassWidthMm
4. 数量 Quantity
5. 玻璃单价(元/㎡) GlassUnitPricePerM2
6. 面积(㎡) AreaM2
7. 玻璃费用 GlassCost
8. 丝织品类型 WireType
9. 丝织品单价 WireUnitPrice
10. 其他费用 OtherFee
11. 行金额 LineAmount
12. 备注 Note

格式规则：
- 表头加粗
- 冻结首行
- 自动筛选
- 金额列 0.0000
- 日期列 yyyy-MM-dd HH:mm:ss
- Orders 底部有合计行

### 9.2 JSON（Orders）
- 文件名默认：GlassFactoryBillTracker_Orders_yyyyMMdd_HHmmss.json
- 导出范围与当前筛选一致

示例片段：

```json
[
  {
    "OrderNo": "20260302-0001",
    "DateTime": "2026-03-02 10:30:00",
    "CustomerName": "测试客户",
    "PaymentMethod": "微信",
    "OrderStatus": "部分收款",
    "TotalAmount": 212.2468,
    "Note": "样例",
    "AttachmentPath": "attachments/20260302-0001/a.png"
  }
]
```

## 10. 测试与质量保证
### 10.1 运行测试

```bash
dotnet test tests/GlassFactory.BillTracker.Tests/GlassFactory.BillTracker.Tests.csproj -c Debug
```

### 10.2 核心测试用例
- OrderAmountCalculatorTests
  - 验证 mm -> ㎡、4 位小数、AwayFromZero
  - 验证多明细 LineAmount 与 TotalAmount
- OrderStatusFilterTests
  - 使用临时 SQLite 文件写入三种状态订单
  - 使用筛选服务校验状态过滤结果
- ExportServiceTests
  - 验证 Excel 文件存在、Sheet 数量
  - 验证 Orders/OrderItems 表头
  - 验证 Orders 合计行与金额

### 10.3 额外回归

```bash
dotnet run --project tools/GlassFactory.BillTracker.DbSmokeTest/GlassFactory.BillTracker.DbSmokeTest.csproj -- <temp_data_dir>
```

预期包含：SMOKE_TEST_PASS

## 11. 常见问题排查
- PowerShell 脚本无法执行
  - 使用 ExecutionPolicy Bypass 参数执行脚本
- Win7 script fails with missing MSBuild
  - Install Visual Studio Build Tools and .NET desktop build workload
- Win7 script fails with missing .NET Framework 4.8
  - Install .NET Framework 4.8 Developer Pack
- SmartScreen 拦截 EXE
  - 点击“更多信息 -> 仍要运行”
- 无法写入 data_dir
  - 更换到有权限目录（如 D:\BillTrackerData）
- SQLite 锁文件或占用
  - 关闭重复进程后重试
- Excel 导出失败（文件被占用）
  - 关闭已打开的目标 xlsx
- 迁移/启动异常
  - 检查 data_dir 路径、日志文件 logs/app.log、数据库权限

## 12. 未来扩展规划
- 多附件 UI 与批量上传（当前已预留 IAttachmentService 与 OrderAttachments）
- 大数据量分页与虚拟滚动
- 增加 PaidAmount 并自动推导订单状态
- Excel 导入（.xlsx）
- 审计日志、操作人、多角色权限

## 13. 发布验收清单（最终交付）
1. 执行 scripts/build_windows.ps1 成功
2. dist/win-x64/GlassFactory.BillTracker.App.exe 存在
3. 执行 scripts/publish_smoke_check.ps1 输出 SMOKE_TEST_PASS
4. 运行 exe 后能选择 data_dir 并自动创建目录结构
5. 新建客户与订单成功
6. Excel 导出成功并包含 Orders/OrderItems
7. dotnet test 全绿
