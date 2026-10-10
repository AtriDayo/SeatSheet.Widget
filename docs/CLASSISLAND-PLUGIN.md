# ClassIsland 点名插件

## 试用步骤

开发机需要 **.NET 8 SDK**。无须安装 .NET MAUI、Web、UWP 等工作负载，也不需要构建 ClassIsland 本体。使用 Visual Studio 编辑时，可选“.NET 桌面开发”工作负载；命令行构建不依赖 Visual Studio。插件使用 Avalonia / `net8.0`，桌面软件继续使用 WPF / `net8.0-windows`。

在仓库根目录执行：

```powershell
dotnet build src/SeatSheet.ClassIslandPlugin -c Release -p:PackagePlugin=true
```

拿到 `artifacts/plugin/SeatSheet.ClassIslandPlugin.cipx` 后，在 ClassIsland 插件管理中从本地安装并重启，再打开应用设置里的“SeatSheet 点名”，点击“测试提醒”。无需独立打包脚本或测试发送工具。

`.cipx` 本质是 ZIP；`manifest.yml` 位于包根目录，指定入口 DLL。打包目标只收集插件、协议 DLL、插件 `.deps.json`、清单、图标和 README。插件借用宿主已有依赖，不包含 ClassIsland DLL、.NET 运行时或桌面 EXE。

不要向 ClassIsland 的版本程序目录复制、覆盖 DLL。通过插件管理安装，宿主会将插件放入其应用数据目录的 `Plugins/seatsheet.widget.rollcall`。卸载也通过插件管理完成。

## 本版范围

- 接收已确定的单人点名结果，展示姓名和可选的班级、座位。
- 独立的原生提醒提供方，姓名朗读一次，班级、座位只显示；原生卡片设置页支持 2–30 秒显示时长，默认 5 秒，自动保存。
- 插件设置页提供虚构数据的“测试提醒”按钮；此按钮直接验证接收与展示逻辑。
- 真实官方命名管道收发由自动检查验证，使用随机私有管道和假的提醒出口，不连接日常 ClassIsland。
- 桌面端已接入随机点名、权重、缺席排除与三种显示目标，参见 [桌面点名说明](ROLLCALL.md)。插件继续只负责结果通知。

固定使用 `ClassIsland.PluginSdk 2.1.0.1`，其对应官方提交为 `15273f82c9d2d55929df83b5fb806e68ee4547c0`。宿主使用 `dotnetCampus.Ipc 2.0.0-alpha410`。插件项目提交 NuGet 锁文件以固定实际依赖，更新宿主兼容目标时需重新验证；不使用浮动 SDK 版本。

## 协议 v1

侧栏实际 `FAPathIcon` 控件限制为 16×16 逻辑像素并居中，禁止宿主主题将其放大到整个图标槽。讲台和六个座位之间的间距也收紧；仅限制本插件的图标。

图标源文件为 `src/SeatSheet.ClassIslandPlugin/Assets/seatsheet-outline.svg`，讲台使用圆角胶囊轮廓，六个座椅使用圆弧靠背和圆头座面。SVG 嵌入插件 DLL，其路径只解析一次并缓存，不增加 SVG 渲染依赖；前景色继承宿主主题。

设置页区分“接收服务”和“客户端连接状态”。前者仅反映接收端初始化与停用状态；后者根据官方 IPC 路由的握手或成功结果维护 30 秒活跃期限，使用单调时钟，过期显示未连接。桌面端选择外部通知时每 10 秒发送一次 `hello`；这不是操作系统管道句柄状态，也不是客户端身份认证。内部测试不更新连接状态。仅设置页可见时每秒刷新文字，关闭后停止刷新。

侧栏图标使用与桌面标志相同的讲台和六个座位构图，以主题色线条显示。ClassIsland 2.1.0.1 的设置页元数据只支持字体图标，插件通过 Avalonia 导航项 Tag 事件替换自己的图标，不修改宿主文件或其他插件导航项。

显示时长保存在插件配置目录的 `notification.json`，只包含秒数。带班级或座位时，前 1–2 秒显示姓名提示，其余时间显示结果正文；只有姓名时全部用于姓名提示。修改只影响下一条通知。滑块停止调整 350 毫秒后保存，测试和离开页面时也会保存；保存失败继续使用原值。

其他插件的“持久化二级提醒”，从截图说明看应指姓名强调提示之后继续显示结果正文，并非将姓名保存到磁盘。本插件不提供此开关。名单、权重、缺席排除和显示目标由桌面软件管理。

使用 ClassIsland 官方 `IIpcService.JsonRoutedProvider`，管道名由官方 `IpcClient.PipeName` 提供：`ClassIsland.IPC.v2.Server`。桌面软件使用 `ClassIsland.Shared.IPC 2.1.0.1` 建立连接，再通过 JSON 路由客户端请求以下路由：

| 路由 | 请求 | 回应 |
| --- | --- | --- |
| `seatsheet.widget/rollcall/v1/hello` | 无参数 | `protocolVersion`、`pluginVersion`、`receiverInstanceId`、`ready` |
| `seatsheet.widget/rollcall/v1/notify` | `RollCallMessage` | `RollCallReceipt` |

协议 DTO 位于 `src/SeatSheet.RollCall.Protocol`，JSON 字段使用 camelCase。例子全部是虚构数据：

```json
{
  "protocolVersion": 1,
  "messageId": "7c54a277-e6c4-4aa7-a534-a8f721caf246",
  "type": "rollCallResult",
  "createdAtUtc": "2026-10-10T02:00:00Z",
  "expiresAtUtc": "2026-10-10T02:00:30Z",
  "student": {
    "name": "示例同学",
    "className": "示例班级",
    "seat": { "row": 2, "column": 3, "label": "第2排第3列" }
  }
}
```

- `messageId` 是非空 GUID（带连字符），在抽取成功时生成一次；通知重试不能重新抽取或改动内容、时间和 ID。
- `name` 必填，最多 64 个 UTF-16 字符；班级与座位标签最多 128 个。文本不允许控制字符。
- 班级与座位可省略；座位坐标必须成对提供且从 **1** 开始，范围 1–30；也可只提供标签。现有桌面坐标从 0 开始，后续发送时须转换。
- 时间使用 UTC，建议有效期 30 秒，最长 60 秒；允许创建时间比接收端快 5 秒，双方依赖本机系统时钟。
- 验证后的规范 JSON 不超过 8 KiB。这是业务层限制，不是替换官方 IPC 的底层帧大小限制。
- 不发送名单、权重、缺席名单、学号、网站地址或凭据。

回执包含原消息 ID、接收端实例 ID、`status` 与 `code`：

| status | 意义 |
| --- | --- |
| `accepted` | 已调用 ClassIsland 原生提醒入口；不保证已显示或朗读 |
| `duplicate` | ID 已预留，本次不再提醒；前一次结果可能不确定 |
| `expired` | 消息已过期，没有新增提醒 |
| `invalid` | 字段无效，或同 ID 携带了不同内容 |
| `unsupportedVersion` | 协议版本不支持 |
| `busy` | 待处理请求或去重记录达到上限 |
| `internalError` | 未就绪、存储失败或提醒提交结果不确定 |

`hello.ready = false` 时不要发送。管道连接失败意味着通信不可用；管道可连接但本插件路由无响应，可能是插件未安装、禁用或启动异常。不能仅凭超时认定 ClassIsland 没有运行。

桌面发送端每次请求建立短连接，握手与回执共用约 3 秒总超时；选择外部通知时每 10 秒握手一次。当前不自动重试，用户可在结果窗口手动重发同一条消息。超时可能已经提交，重发不更改抽取结果、消息 ID 或有效期。`accepted`、`duplicate` 表示结果已提交或已保留，不保证实际播放；`invalid`、`expired` 等业务拒绝不应盲目重试。接收端没有随机抽取接口。

## 去重、性能与隐私

并发接收串行处理，最多 8 个待处理请求，最多 1024 条去重记录；达到上限返回 `busy`，不驱逐仍有效的记录。只有收到消息时才清理过期记录，不后台轮询。

宿主分配的插件配置目录内保存 `dedup.json`，只包含消息 ID、SHA-256 指纹和保留期限，**不包含姓名、班级或座位**。保留期限是消息过期时间加 5 分钟。记录先原子写入磁盘，再提交提醒，覆盖重启后的重复消息。磁盘写入失败时停用接收；去重文件损坏时不自动覆盖，防止丢失记录后重播。停用状态请检查目录权限、磁盘和日志；如需清理损坏文件，请先退出 ClassIsland，并等待旧消息全部过期。

采用优先避免重复的语义：记录完成到提醒提交之间崩溃，可能漏掉一次通知；没有保证“恰好显示一次”。提醒提交异常时也保留记录，重试不会再次播放。姓名结果应始终保留在桌面软件中，“抽取失败”和“发送失败”必须分别处理。

插件不改动请求级的音效、语音或特效设置，不启动自己的音频引擎。ClassIsland 关闭提醒、主界面没有提醒消费者或提供方被禁用时，受理不等于显示。插件自身不记录学生文本，但 ClassIsland 自身的诊断日志可能记录提醒内容。

## 检查

```powershell
dotnet run --project tests/SeatSheet.ClassIslandPlugin.Checks -c Release
dotnet run --project tests/SeatSheet.Widget.Checks -c Release
```

插件检查覆盖协议校验、并发重复、ID 冲突、跨重启去重、磁盘故障、提醒提交异常、排队过期、容量上限、纯 ID 文件记录及真实官方命名管道往返。所有数据均为合成数据。

安装后的手动验证：点击“测试提醒”，检查中文姓名、班级、座位；关闭/开启原生语音与音效，确认遵循设置；关闭提醒时不应绕过宿主开关。1920×1080、i5-2430M 的实际性能仍须在学校设备上验证。
