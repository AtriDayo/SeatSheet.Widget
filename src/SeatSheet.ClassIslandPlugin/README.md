# SeatSheet 点名插件（试用版）

面向 **ClassIsland 2.1.0.1**。通过官方本机 IPC 接收已经抽取的结果，用原生提醒展示姓名，以及可选的班级和座位；音效、朗读及效果遵循 ClassIsland 设置。

**这一版没有随机抽取功能。** 名单、权重、缺席排除和点名设置将由桌面软件负责。

## 试用

1. 在 ClassIsland 插件管理中安装 `SeatSheet.ClassIslandPlugin.cipx`，按提示重启。
2. 打开 ClassIsland 应用设置中的 **SeatSheet 点名** 页面，可调整 **通知显示时长**（2–30 秒，默认 5 秒，自动保存）。
3. 点击 **测试提醒**，会显示虚构的“示例同学 / 示例班级 / 第2排第3列”，姓名只朗读一次。
4. 如果只显示“已交给 ClassIsland”而没有提醒，请检查原生提醒开关、主界面提醒设置以及“SeatSheet 点名”提醒提供方设置。

测试按钮在插件内，不需要额外发送工具；每次点击代表一条新的测试消息。

## 构建与打包

需要 .NET 8 SDK；无需额外 `dotnet workload`。在 SeatSheet.Widget 仓库根目录执行：

```powershell
dotnet build src/SeatSheet.ClassIslandPlugin -c Release -p:PackagePlugin=true
```

输出到 `artifacts/plugin/SeatSheet.ClassIslandPlugin.cipx`。它是带 `.cipx` 扩展名的 ZIP，包含插件 DLL、协议 DLL、依赖描述、清单、图标和本 README；宿主 DLL、运行时、桌面 EXE 和本机配置不进入包。

更多协议、验证和排错说明见仓库的 `docs/CLASSISLAND-PLUGIN.md`。现有 stable workflow 仍只打包桌面 EXE。
