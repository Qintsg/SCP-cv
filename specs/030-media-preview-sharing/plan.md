# Implementation Plan: 媒体预览、网页刷新与持续共享

**Branch**: `main` | **Date**: 2026-10-30 | **Spec**: [spec.md](spec.md)

## Summary

网页增加真实REFRESH命令；共享发布者由应用按窗口持有；媒体名称打开按类型呈现的预览面板。

## Technical Context

**Language/Version**: TypeScript、Vue3、C#/.NET10。

**Primary Dependencies**: Pinia、Ant Design Vue、WebView2、现有预览池及文稿制品接口。

**Storage**: 现有SQLite与持久命令队列，无新增字段。

**Testing**: 真实回环HTTP/流服务、正式PlayerRuntimeHost、真实WebRTC和只读无头浏览器。

**Target Platform**: Windows播放器及Web控制端。

**Project Type**: 播放器与Web控制台。

**Performance Goals**: 同源预览共享解码，不随控制视图切换重建共享。

**Constraints**: 本机验证，不连接D4；源码及客户端按031授权发布。

**Scale/Scope**: 两个播放窗口，全部现有媒体类型。

## Constitution Check

- [x] I：实际导航成功才完成刷新；退出释放共享及预览。
- [x] II：规范、研究、合同、任务和验证相互关联。
- [x] III：真实文件、网络、原生部件与浏览器分层验证。
- [x] IV：Web复用REST和持久队列，不直接操作原生对象。
- [x] V：复用既有转换、直播池及身份边界；大文件新增能力使用独立模块。

## Project Structure

- `frontend/src/features/screen-share/`：共享所有者和离页停止入口。
- `frontend/src/features/sources/`：详情预览及文稿资源加载。
- `frontend/src/features/display/PlaybackControl.vue`：网页刷新动作。
- `runtime-dotnet/src/ScpCv.PlayerWorker/Playback/`：实际WebView刷新。
- `runtime-dotnet/src/ScpCv.Infrastructure/Playback/`：来源门禁与REFRESH入队。
- `specs/030-media-preview-sharing/`：设计和验证证据。

## Implementation

REFRESH限定Web来源，禁用当前WebView网络缓存并绕过Service Worker，等待本次导航完成。共享由应用持有，页面仅观察；暂时disconnected提供有界恢复，永久失败停止。媒体预览用原生图片、音视频、受限iframe、文稿页图及ClientStreamPreview；关闭卸载内容，手机允许明确打开预览。文稿未准备可明确准备并有界读取。文件预览增加音频支持。

## Complexity Tracking

无宪章例外。
