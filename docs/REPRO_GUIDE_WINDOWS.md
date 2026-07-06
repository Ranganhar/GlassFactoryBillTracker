# Windows 复现与诊断采集指南

## 1. 前置准备
在 Windows 10/11 PowerShell 中，先发布应用：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_windows.ps1
```

确认发布产物存在：
- `dist/win-x64/GlassFactory.BillTracker.App.exe`

## 2. 一键复现+收集（推荐）

### 2.1 默认命令
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\repro_collect.ps1
```

### 2.2 指定 DataDir
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\repro_collect.ps1 -Runtime win-x64 -DataDir D:\BillTrackerRepro
```

### 2.3 指定输出目录
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\repro_collect.ps1 -OutDir .\artifacts\repro\manual_case_01
```

## 3. 手动复现步骤（脚本会提示）
1. 脚本启动应用后，进入主界面。
2. 按 `Ctrl+N`（或点击“新建订单”）。
3. 如果出现卡死/秒退，等待窗口退出；若卡死不退出，请在任务管理器结束进程。
4. 回到 PowerShell，按回车继续采集。
5. 脚本会自动执行 `--force-crash` 故障注入并采集 crash.log。

## 4. 输出物在哪里
脚本结束后会生成 zip：
- `artifacts/repro/repro_bundle_yyyyMMdd_HHmmss.zip`

请将该 zip 发给开发者。

## 5. 如果只想单独收集日志（不启动程序）

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\collect_logs.ps1 -DataDir D:\BillTrackerRepro
```

## 6. 收到诊断包后的第二轮修复流程
开发者将按以下流程处理：
1. 从 `crash.log` 提取异常类型、消息、堆栈，定位到文件/行号。
2. 若是死锁：定位同步等待点并改为 `async/await`。
3. 若是栈溢出：定位递归触发链并加 guard。
4. 若是 binding/converter：补 `null-safe` 与 fallback。
5. 提交最小补丁，并回归：
   - `dotnet test`
   - `DbSmokeTest`
   - Windows 手动 `Ctrl+N` 验证

## 7. 程序卡死时如何继续
- 如果程序卡死不退出，打开任务管理器结束 `GlassFactory.BillTracker.App.exe`。
- 回到 PowerShell 按回车，脚本仍可继续完成日志与事件采集。
