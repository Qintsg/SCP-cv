# 直播预览新增依赖声明

018网页构建使用以下开源组件，原许可证随仓库保留：

- mpegts.js 1.8.2，Apache-2.0，[官方源码](https://github.com/xqq/mpegts.js)，许可证见[mpegts.js-LICENSE.txt](mpegts.js-LICENSE.txt)。
- es6-promise 4.2.8，MIT，mpegts.js运行依赖，许可证见[es6-promise-LICENSE.txt](es6-promise-LICENSE.txt)。
- events 3.3.0，MIT，mpegts.js运行依赖，许可证见[events-LICENSE.txt](events-LICENSE.txt)。

许可证文本直接取自本次pnpm安装的对应包，未改写版权声明。均不要求购买商业播放器授权。

服务器FFmpeg和MediaMTX是独立程序，不加入观看端安装。若后续分发服务器二进制，需要随所选构建保留其许可证及对应分发材料；本轮没有新增提交或部署FFmpeg二进制。
