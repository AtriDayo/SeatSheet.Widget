# 开发与构建

所有命令均在仓库根目录运行，开发需要 .NET 8 SDK。

```text
SeatSheet.Widget.sln                     解决方案
src/SeatSheet.Widget/                    桌面项目与资源
src/SeatSheet.ClassIslandPlugin/         ClassIsland 2.1.0.1 点名接收插件
src/SeatSheet.RollCall.Protocol/         共享点名消息协议
tests/SeatSheet.Widget.Checks/           核心检查
tests/SeatSheet.ClassIslandPlugin.Checks/ 插件协议、去重与官方 IPC 检查
dev/                                    开发、发布与资源转换脚本
.github/workflows/                      自动构建
artifacts/widget/win-x64/                桌面发布输出（不提交）
artifacts/plugin/                       独立插件包（不提交）
```

## 编译、运行与检查

```powershell
dotnet build SeatSheet.Widget.sln -c Release
dotnet run --project src/SeatSheet.Widget -- --settings
dotnet run --project tests/SeatSheet.Widget.Checks -c Release
```

常规编译文件位于各项目自己的 `bin/` 和 `obj/` 中。Release 编译输出为 `src/SeatSheet.Widget/bin/Release/net8.0-windows/`，它与可分发的单 EXE 发布目录不同。

实际 WPF 界面检查会自动退出，不写用户配置；图像和结果放在编译输出目录的 `qa/` 中。

```powershell
dotnet run --project src/SeatSheet.Widget -- --appearance-qa
dotnet run --project src/SeatSheet.Widget -- --motion-qa
dotnet run --project src/SeatSheet.Widget -- --expand-qa --compact
```

## 发布桌面程序

```powershell
pwsh -File dev/publish-widget.ps1
```

本地与 Actions 使用同一脚本，目标固定为桌面项目，产物固定为 `artifacts/widget/win-x64/SeatSheet.Widget.exe`：Windows x64、自带运行时、单文件，不裁剪 WPF 程序集。

自动 workflow 仅由 `stable` 的推送触发，或手动选择 `stable` 运行，只上传 Actions 产物，不创建 Release。日常改动默认提交到 `main`，需要生成稳定版产物时再将经过确认的改动同步到 `stable`。

插件已加入解决方案，但桌面发布脚本和 workflow 仍仅发布桌面项目。插件单独编译、打包：

```powershell
dotnet run --project tests/SeatSheet.ClassIslandPlugin.Checks -c Release
dotnet build src/SeatSheet.ClassIslandPlugin -c Release -p:PackagePlugin=true
```

产物为 `artifacts/plugin/SeatSheet.ClassIslandPlugin.cipx`。无需额外 .NET 工作负载，插件用 `net8.0` / Avalonia。不要对整个解决方案执行单 EXE 发布，也不要将插件混入桌面发布目录。安装后可从 ClassIsland“SeatSheet 点名”设置页点击“测试提醒”。详见 [插件说明](CLASSISLAND-PLUGIN.md)。

## 开发辅助脚本

`dev/start-backend.ps1` 和 `dev/start-frontend.mjs` 是依赖相邻 SeatSheet 网站项目的本地联调工具，并非运行桌面软件的必要条件。图标资源位于 `src/SeatSheet.Widget/Assets`，`dev/convert-icon.py` 用 Pillow 转换应用图标尺寸。
