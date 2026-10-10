# SeatSheet 图标

使用内置 imagegen 工具生成，透明 PNG 原稿为 `app-icon-source.png`（1254×1254），保留原始透明通道。`app-icon.png` 为 512×512，供 WPF 使用；`app.ico` 包含 16、20、24、32、40、48、64、96、128、256 像素版本，供 EXE 与托盘使用。

`dev/convert-icon.py` 使用 Pillow 的 Lanczos 重采样转换分辨率，并验证 ICO 的全部帧及透明通道。源图仅做尺寸转换，未重绘。

生成提示词：

> Use case: logo-brand. Create one polished Windows desktop application icon for SeatSheet, a classroom seating chart quick-access widget. Square composition, genuinely transparent background outside the icon. A single cobalt-blue rounded-square tile with a very subtle blue gradient, containing a crisp white top-down classroom seat layout: six large rounded rectangular seats in two rows of three, with a short white teacher desk bar above. Simple bold geometry, high contrast, clear silhouette legible at 16px, restrained modern Windows / WinUI visual style. Tile fills about 86 percent of canvas, centered, uniform padding. Front-facing orthographic, minimal subtle depth, no perspective, no busy textures, no tiny details. No letters, words, numbers, watermark, surrounding scene or mockup. Output a high-resolution square PNG with real alpha transparency.
