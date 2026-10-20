# 017 Logo透明底处理记录

- 项目资产：`frontend/src/assets/sspu-ailab-transparent.png`。
- 原始资源：用户提供的`E:/Projects/Resources/Common/标志/SSPU AILAB1.png`；仓库原`frontend/src/assets/sspu-ailab.png`保留不改。
- 工具：内置image_gen，transparent_background=true，3轮编辑后采用第3轮。未用脚本扣白底；生成结果不是逐像素同一原图，已核对小尺寸字形、外部透明、内部白色及浅深主题。
- 深色呈现：CSS invert(1) hue-rotate(180deg)，不改变alpha；侧栏不再给图片加白色垫片。

最后一轮提示原文如下（编辑目标为第2轮PNG，约1600×976）：

> Precise cutout correction only, preserve all existing blue arch and letter shapes exactly. Make transparent ALL white below the horizontal baseline at the BOTTOM ENDS OF THE BLUE PILLARS (approximately y=577 in this 1600x976 image), EXCEPT the fully enclosed white triangular holes in both A letters and two enclosed white holes in B. Specifically remove the exterior white vertical strips between A and I, I and L, L and second A, second A and B; remove white below the arch left and right pillar ends. Keep the ENTIRE white interior rectangle ABOVE this baseline solid opaque white. The area under the blue arch baseline is only BLACK AILAB letters plus their ENCLOSED opaque white counters, with fully transparent gaps between letters and outside. No new text, no crop, no new objects, preserve proportions and exact AILAB SSPU# lettering.

生成源输出为Codex generated_images下`exec-66d0a5ec-619e-4a22-81b7-fa558bfe0811.png`，项目引用仓库内副本，不依赖该外部路径。
