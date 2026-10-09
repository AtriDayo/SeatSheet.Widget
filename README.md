# SeatSheet.Widget

让座位表随手可见。SeatSheet.Widget 是面向 Windows 10 / 11 的桌面座位表工具，通过桌面右侧的“座位”按钮，快速查看 SeatSheet 网站中的班级座位安排。

## 功能

- **一键查看**：点击右侧“座位”按钮弹出面板，再次点击即可收起；按钮可以上下拖动。
- **灵活显示**：支持适应窗口、放大缩小，以及展开到整个桌面工作区。
- **连接网站**：填写 SeatSheet 网站地址，测试连接成功后保存。
- **离线查看**：网络不可用时，显示最近一次成功获取的座位表。
- **快速管理**：从面板打开网站的座位管理页面。
- **托盘入口**：通过系统托盘查看座位表、打开设置、隐藏按钮或退出程序。

## 获取与运行

项目目前为预览版。每次更新 `stable` 分支后，会自动构建 Windows x64 单 EXE，并保存在 [GitHub Actions](https://github.com/AtriDayo/SeatSheet.Widget/actions/workflows/build-stable.yml) 的产物中，不创建 GitHub Release。

下载步骤：登录 GitHub，打开最近一次成功的 `stable` 构建，在页面底部 **Artifacts** 中下载 `SeatSheet.Widget-win-x64-编号`。解压 ZIP 后运行 `SeatSheet.Widget.exe` 即可，无需另外安装 .NET。产物保留 30 天；这里提供的是程序文件，不是安装器。

如果你已经拿到完整的程序文件夹，打开其中的 `SeatSheet.Widget.exe` 即可。请保留同目录下的其他文件；当前普通编译版本需要安装 **.NET 8 桌面运行时**，自带运行时的发布版本则不需要另外安装 .NET。

本工具需要连接已有的 SeatSheet 网站，不包含网站服务端。

## 首次使用

1. 启动程序，在桌面右侧找到“座位”按钮。
2. 右键按钮或托盘图标，选择“设置”。
3. 填写平时查看座位表的网站地址，例如 `https://seats.atridayo.com`。首次运行预填的是本地开发地址，请换成你的实际网站地址。
4. 点击“测试连接”，确认显示正确的班级名称。
5. 按需调整面板宽度，点击“连接并保存”。

之后点击“座位”按钮即可查看。每次打开面板时会自动刷新，也可以点击“刷新”手动获取最新数据。

## 日常操作

| 操作 | 用途 |
| --- | --- |
| 点击“座位”按钮 | 打开或收起面板 |
| 上下拖动按钮 | 调整按钮位置 |
| “适应” | 让座位表适应当前面板大小 |
| “−” / “＋” | 缩小或放大座位表 |
| “展开” / “还原” | 切换大视图与浮动面板 |
| “收起”或面板内按 `Esc` | 收起面板 |
| “管理座位” | 在浏览器中打开网站管理页面 |
| 托盘菜单中的“退出” | 完全退出程序 |

面板中只查看座位表；修改座位安排仍在网站中进行。

## 设置与缓存

设置与离线缓存默认保存在当前 Windows 用户的目录中：

```text
%LOCALAPPDATA%\SeatSheet.Widget\
```

- `settings.json`：网站地址、面板宽度和按钮位置。
- `cache.json`：最近一次成功获取的座位表。

可将上述路径粘贴到资源管理器地址栏打开。配置保存在程序文件夹之外，替换程序文件进行升级时通常可以继续使用原有设置。缓存中包含座位表内容。

## 当前限制

- 按钮和面板定位在主屏幕，暂不支持选择其他显示器。
- 暂不支持开机启动、暗色主题或全屏应用自动隐藏。
- 当前使用浅色实色背景，尚未提供毛玻璃或 Mica 效果。
- Windows 10 与较旧电脑的显示和动画表现仍需实际设备验证。

## 从源码运行

开发需要 **.NET 8 SDK**，技术栈为 C# / WPF。

```powershell
git clone https://github.com/AtriDayo/SeatSheet.Widget.git
cd SeatSheet.Widget
dotnet run -- --open
```

生成与 Actions 一致的自带运行时 Windows x64 单 EXE：

```powershell
dotnet publish SeatSheet.Widget.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o artifacts\win-x64
```

产物为 `artifacts\win-x64\SeatSheet.Widget.exe`。自动构建仅在 `stable` 分支更新时触发，也可以在 Actions 页面选择 `stable` 手动运行。
