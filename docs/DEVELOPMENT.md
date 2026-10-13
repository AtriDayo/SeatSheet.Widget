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

展开与还原使用实时布局，文字与按钮保持尺寸，座位表保持等比缩放。过渡只调整固定宿主内的面板位置、大小和圆角，不使用拉伸截图；移除原先的 30 帧上限，由 WPF 渲染时钟驱动；座位表使用 RenderTransform 和独立滚动范围，动画帧不强制 UpdateLayout，减少每帧对座位文字与卡片的重复布局。“展开和还原动画”默认关闭，旧配置缺少该选项时也保持关闭，直接切换大小。可在“常规 → 面板显示 → 视觉效果”手动开启，并遵循 Windows 界面动画开关。实际流畅度应在目标旧电脑上试用确认。

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

现有 `build-stable.yml` 仍只由 `stable` 的推送触发，或手动选择 `stable` 运行，只上传桌面 Actions 产物。日常改动在 `main`，经审查后同步 `stable`。产品标签触发的预发布草稿流程独立，见下文。

插件已加入解决方案，但桌面发布脚本和 workflow 仍仅发布桌面项目。插件单独编译、打包：

```powershell
dotnet run --project tests/SeatSheet.ClassIslandPlugin.Checks -c Release
dotnet build src/SeatSheet.ClassIslandPlugin -c Release -p:PackagePlugin=true
```

产物为 `artifacts/plugin/SeatSheet.ClassIslandPlugin.cipx`。无需额外 .NET 工作负载，插件用 `net8.0` / Avalonia。不要对整个解决方案执行单 EXE 发布，也不要将插件混入桌面发布目录。安装后可从 ClassIsland“SeatSheet 点名”设置页点击“测试提醒”。详见 [插件说明](CLASSISLAND-PLUGIN.md)。

## 开发辅助脚本

桌面随机点名的设置、通信与验证见 [点名说明](ROLLCALL.md)。设置窗口的“随机点名”标签页集中管理名单、权重、缺席状态和显示目标。

`dev/start-backend.ps1` 和 `dev/start-frontend.mjs` 是依赖相邻 SeatSheet 网站项目的本地联调工具，并非运行桌面软件的必要条件。图标资源位于 `src/SeatSheet.Widget/Assets`，`dev/convert-icon.py` 用 Pillow 转换应用图标尺寸。

## 版本与预发布草稿

桌面版本由 `src/SeatSheet.Widget/SeatSheet.Widget.csproj` 的 `Version` 决定，当前开发版本为 `0.1.0-beta.2`。“设置 → 关于”读取程序集版本，Windows 文件属性中的产品版本保留预发布后缀，数字文件版本为 `0.1.0.0`。

插件版本由其项目中的 `Version` 决定，保持 `0.1.5`；`manifest.yml` 的 ClassIsland 数字版本为 `0.1.5.0`。版本必须递增，不为统一桌面与插件编号而降低插件版本。协议 v1 独立维护。

发布前可先在本地校验，不创建标签或 Release：

```powershell
pwsh -File dev/check-release-version.ps1 -Product widget -Tag widget-v0.1.0-beta.2
pwsh -File dev/check-release-version.ps1 -Product plugin -Tag plugin-v0.1.5
```

`release-product.yml` 在推送 `widget-v*` 或 `plugin-v*` 标签时运行：

1. 要求标签提交已经包含在 `stable` 中，且标签版本与相应项目版本一致；插件还校验 manifest。
2. 运行核心及官方 IPC 检查，只打包选定产品，桌面和插件输出目录保持分离。
3. 校验产物版本后创建 **Draft + Pre-release**，上传该产品附件并附 SHA256。不自动公开发布，不覆盖已有 Release，不将其设为 Latest。
4. 维护者试用附件、补充本产品更新说明后手动发布。旧标签 `plugin-v0.1.5` 已发布，不能再次用于新版本。

标签只应在用户明确要求发布后创建和推送。本流程不会因为提交 main 或同步 stable 而创建 Release。

同仓库中的 Latest 是共享的，且不适用于预发布版本。README 顶部与 `ProductInfo.cs` 的下载入口使用明确的产品 Release 链接，发布后再更新到已公开且附件完整的版本。本次桌面入口配套更新为 beta.2；公开 Release 前必须确认对应附件已完整上传。

首次使用时网站地址为空，自动打开设置并引导连接。不预填本地开发地址，也不自动请求网络；可关闭或选择“稍后配置”，外观仍可离线保存。已有合法地址与缓存继续保留。测试连接不写配置，更换地址保存时必须验证成功，配置读取错误仍回到默认值。


点名悬浮窗与“课间禁用点名”均在随机点名标签页启用，默认关闭。悬浮窗沿用 PanelWindow 的统一抽取与重发流程，不复制名单或抽取引擎；位置保存在本地，关闭只暂时隐藏，可从托盘重新显示，取消设置不会改变开关。

课间限制直接读取 ClassIsland 2.1.0.1 官方 `IPublicLessonsService`，无需更新插件。源码核对依据：
- [官方 2.1.0.1 IPC 契约](https://github.com/ClassIsland/ClassIsland/blob/2.1.0.1/ClassIsland.Shared.IPC/Abstractions/Services/IPublicLessonsService.cs)
- [课程服务注册与状态更新](https://github.com/ClassIsland/ClassIsland/blob/2.1.0.1/ClassIsland/Services/LessonsService.cs)

只读取计时器、课表启用／加载／时间点确认标志与 CurrentState；不读取姓名、科目、整个课表，不调用任何 setter。官方属性代理是同步调用，在短生命周期专用线程中读取以免阻塞 WPF 或 IPC 线程池；一次查询预算 700ms，单属性超时 250ms，仅在启用限制且面板、悬浮窗或结果窗可见时每两秒检查状态，全部隐藏或关闭限制时停止计时器；点击前也检查。课间按钮禁用与操作中的忙碌状态合并，完成通知发送不会误恢复课间按钮。仅稳定且已确认的 `TimeState.Breaking` 拦截新抽取与重发；宿主未运行、停用课表、暂停计时器、未确认状态、查询超时均允许点名。放学状态不作为课间。

检查使用随机私有管道，模拟正式 2.1.0.1 课程契约。可单独只读探测日常 ClassIsland（仅输出是否课间，不发送提醒、不修改课程）：

```powershell
dotnet run --project tests/SeatSheet.ClassIslandPlugin.Checks -c Release -- --probe-classisland
```

`--expand-qa` 同时输出渲染等级、有效进度间隔的中位数；截图会影响测量，超过 100ms 的间隔被排除。这是开发诊断，不等同于目标电脑的稳定帧率或 GPU 性能承诺；实际高配体验仍需试用。


## 设置页视觉效果

“常规 → 面板显示 → 视觉效果”分别控制“展开和还原动画”和“设置标签页过渡”，两项独立保存、默认关闭。标签过渡可以在设置草稿中即时预览，关闭或取消设置不会修改已保存开关，写入失败时回滚。

标签切换只动画视口内的卡片：旧卡片按顺序向侧面位移 24 DIP 并淡出（115ms，间隔18ms），新卡片从上方14 DIP依次滑入并淡入（210ms，间隔28ms）。不拉伸、不模糊背景，不逐帧重新布局；仅边界切换时更新布局。临时位图缓存、透明度时钟和渲染变换在完成、关闭开关或关闭窗口时恢复。视口裁切限制过渡范围，标题和底部操作区固定。连续选择只进入最后请求的页面，保留同一组控件的草稿与滚动位置，并遵循 Windows 界面动画设置。

```powershell
dotnet run --project src/SeatSheet.Widget -- --tab-motion-qa
```

该检查使用虚构数据，验证卡片顺序、快速切换、窄窗口、草稿与滚动位置保留、中途关闭动画、窗口关闭与缓存清理，输出图片及 `tab-motion-result.json`。
