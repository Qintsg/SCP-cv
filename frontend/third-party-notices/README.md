# 直播预览新增依赖声明

018网页构建使用以下开源组件，原许可证随仓库保留：

- mpegts.js 1.8.2，Apache-2.0，[官方源码](https://github.com/xqq/mpegts.js)，许可证见[mpegts.js-LICENSE.txt](mpegts.js-LICENSE.txt)。
- es6-promise 4.2.8，MIT，mpegts.js运行依赖，许可证见[es6-promise-LICENSE.txt](es6-promise-LICENSE.txt)。
- events 3.3.0，MIT，mpegts.js运行依赖，许可证见[events-LICENSE.txt](events-LICENSE.txt)。

许可证文本来自对应版本的实际依赖包，保留原版权声明。依赖通过npm安装。

022的Electron媒体容器处理使用`flvee0.0.1`、`@mediafish/flv0.2.2`及其运行依赖。版权和完整许可见[FLV依赖许可](flv-preview-LICENSE.txt)，目录包包含这些依赖及许可。服务器使用`LiveStreamingServerNet0.33.1`，其许可随.NET发布包保存在`third-party-notices/LiveStreamingServerNet-LICENSE.txt`。

018服务器FFmpeg和MediaMTX是独立程序，不加入网页观看端安装。020 Windows Electron另随包提供自包含helper和FFmpeg，用户无需自行安装；Web不受影响。本轮只准备本机development-only资产，不提交媒体二进制或公开发行安装器。

Native资产清单保存精确FFmpeg摘要、HTTPS来源和`-version`构建配置，包内保留FFmpeg-LICENSE.txt。当前验证构建含GPL组件，不能称为纯LGPL；公开分发前必须提供匹配二进制的完整对应源码及第三方材料并完成匹配审计，不能用单个LICENSE或“非商业”代替。准备脚本缺少--source-archive时拒绝发行模式；打包前检查完整性，`--require-distributable`还拒绝development-only资产。见[020构建说明](../../docs/input-routing-preview-020.md#构建与发行)。
