# SeatSheet.Widget Demo

Windows 10 / 11 桌面座位表小工具，C# + WPF + .NET 8，无额外 NuGet UI 依赖。

## 运行

```powershell
dotnet build
dotnet run -- --open
```

或双击 `bin\Debug\net8.0-windows\SeatSheet.Widget.exe`。右侧灰色“座位”标签点击打开 / 收起、上下拖动调整位置；尺寸为 30×72 DIP，文字竖排，平时略微淡化、悬停变清晰。右键或托盘菜单可设置、隐藏按钮和退出。

首次默认连接 `http://localhost:5173`。在设置里填写网站地址，可接受 `/api`、`/api/seat-plan` 或 `/config` 后缀；仅在实际验证成功后保存。通过 GET `/api/seat-plan` 读取原项目的数据，不执行 PUT 或管理操作。

面板提供刷新、适应、放大缩小、展开到工作区及管理网页入口。程序不会自动设置开机启动。设置和按来源匹配的座位表缓存保存在 `%LOCALAPPDATA%\SeatSheet.Widget`；网络失败时保留最近成功的数据。

面板默认以浮动卡片形式垂直居中，占工作区高度的 80% 且不超过 720 DIP，右侧为按钮留出空间。圆角按工作区短边的 1.8% 计算，限制在 12–24 DIP，随系统 DPI 缩放。展开模式才接近整个工作区。目前背景为实色；透明窗口仅用于圆角和阴影，尚未启用 Mica / Acrylic。

这是 Demo：主屏右侧定位，浅色 WinUI 风格；尚未加入多屏选择、全屏应用自动隐藏、开机启动、暗色主题和安装器。需要在学校实际 Windows 10 上验证。

动画固定原生窗口，只让卡片滑动约 28 DIP、轻微缩放并淡入。弹出 300ms / 收回 170ms，使用不同的三次贝塞尔缓动；快速点击可从当前位置反向衔接。动画期间临时缓存卡片，结束后恢复文字渲染。刷新在弹出结束后开始，数据未变化时不重建座位格；阴影不再使用整块面板的模糊特效。

## QA

```powershell
dotnet run -- --smoke
dotnet run -- --smoke --compact
dotnet run --project tests/Widget.Checks.csproj
dotnet run -- --motion-qa
```

Smoke 模式读取本地 API，打开真实 WPF 面板，在输出目录 `qa` 保存渲染截图和加载结果后自动退出，不写用户设置或缓存。

## 发布

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o artifacts\win-x64
```

发布需要恢复对应运行时包，最终用户不需安装 SDK。将整个输出文件夹一起分发。本 Demo 当前使用 .NET 8，正式维护应安排升级到长期支持版本。

## 本次联调环境

复用现有 `seatsheet-audit-20260929` PostgreSQL 容器（localhost:55432），使用原 SeatSheet 后端运行在 localhost:3000。没有迁移、种子写入或覆盖现有班级数据。

前端运行在 localhost:5173，通过临时 Vite 配置将 `/api` 代理到 localhost:3000；没有修改原项目配置。可用 `node dev/start-frontend.mjs` 启动此前端（要求 sibling `D:\Program\SeatSheet` 的依赖已安装）。开发服务需要保持运行；日常使用请在设置里换成实际部署的网站地址。

后端启动脚本为 `pwsh -File dev/start-backend.ps1`，从现有数据库容器读取凭据到当前进程，不把数据库密码写入文件。仅本地管理网页的演示密码为 `widget-local-demo`；Widget 本身不会读取或保存管理密码。请勿在联调数据库中随意保存名单。
