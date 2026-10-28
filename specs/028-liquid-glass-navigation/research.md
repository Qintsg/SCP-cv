# 调研：滑动玻璃导航

## 交互

**Decision**: 参考[Kyant AndroidLiquidGlass底栏](https://github.com/Kyant0/AndroidLiquidGlass/blob/kmp/app/src/commonMain/kotlin/com/kyant/backdrop/catalog/components/LiquidBottomTabs.kt)，采用连续镜片位置、按压放大、速度形变及松手吸附。
**Rationale**: 连续位置与最终选择分离，拖动期间不会切换业务页面。使用Motion 14.7.1（MIT）运行弹簧动画，结束后停止调度。
**Alternatives considered**: 单独的按钮背景无法表达连续滑动；采用一个移动镜片。

## 材质

**Decision**: 使用[simple-liquid-glass 5.4.0](https://github.com/lucaperullo/simple-liquid-glass)的MIT Web Component，以SVG backdrop-filter折射Android Chromium底下的真实内容。
**Rationale**: 浏览器入口独立于React；省略liquid与backdrop-selector属性，不运行持续波纹与页面捕获。SVG参数使用lens、lens-strength、lens-center、scale、displace、radius。
**Alternatives considered**: 全页图像捕获增加预览资源开销；本功能使用局部SVG滤镜。所用引擎自带针对非Chromium浏览器的材质支持。

## 验证边界

只读无头浏览器检查真实页面和截图；技术验证页调用真实运动控制接口、真实浏览器动画与导航记录。禁止模拟鼠标、键盘或触摸，也不把技术接口验证表述为实体触摸实测。
