#!/usr/bin/env python
# -*- coding: UTF-8 -*-
"""
播控浏览器 QA 的封闭 HTTP/SSE 夹具；不调用任何真实主机或设备。
@Project : SCP-cv
@File : playback_state_fixture.py
@Author : Qintsg
@Date : 2026-09-29
"""

import copy
from typing import Any
from urllib.parse import urlsplit

from playwright.sync_api import BrowserContext, Page, Route

EVENT_SOURCE_SHIM = r"""
// QA 专用 EventSource：只投递协议数据，不建立网络或模拟原生播放器。
window.__qaEventSources = [];
class QaEventSource {
  static CONNECTING = 0; static OPEN = 1; static CLOSED = 2;
  constructor(url) {
    this.url = String(url); this.readyState = 1; this.listeners = new Map();
    window.__qaEventSources.push(this);
    queueMicrotask(() => this.onopen?.(new Event('open')));
  }
  addEventListener(type, callback) {
    const handlers = this.listeners.get(type) || [];
    handlers.push(callback); this.listeners.set(type, handlers);
  }
  close() { this.readyState = 2; }
  emit(type, payload) {
    if (this.readyState !== 1) return;
    const event = new MessageEvent(type, { data: JSON.stringify(payload) });
    for (const callback of this.listeners.get(type) || []) callback(event);
  }
}
window.EventSource = QaEventSource;
"""


def source(
    source_id: int, kind: str, name: str, state: str = ""
) -> dict[str, Any]:
    """生成与 REST 合同同形的非生产媒体记录。

    :param source_id: 夹具主键。
    :param kind: 媒体类型。
    :param name: 唯一显示名称。
    :param state: PPT 准备状态。
    :returns: 媒体源字段。
    """
    return {
        "id": source_id,
        "source_type": kind,
        "name": name,
        "uri": (
            f"srt://127.0.0.1:9/?streamid=read:qa{source_id}"
            if kind == "srt_stream"
            else (
                f"rtsp://127.0.0.1:9/qa/{source_id}"
                if "stream" in kind
                else f"media/QA/{name}"
            )
        ),
        "is_available": not state or state == "ready",
        "stream_identifier": "",
        "folder_id": None,
        "original_filename": name,
        "file_size": 1024,
        "mime_type": "application/octet-stream",
        "is_temporary": False,
        "expires_at": None,
        "metadata": (
            {"stream_status": "unverified"} if "stream" in kind else {}
        ),
        "preheat_enabled": False,
        "keep_alive": False,
        "playback_mode": "slide_images" if kind == "ppt" else "",
        "preparation_state": state,
        "page_count": 9 if state == "ready" else 0,
        "preview_url": "/api/qa-fixture/slides/1/" if state == "ready" else "",
        "thumbnail_url": (
            "/api/qa-fixture/slides/1/" if state == "ready" else ""
        ),
        "preview_kind": "image" if state == "ready" else "icon",
        "preview_label": name,
        "created_at": "2026-09-29T00:00:00Z",
    }


class ControlledBackend:
    """拒绝未知请求，精确许可受控重开，提供可追溯状态更新。"""

    def __init__(self, origin: str) -> None:
        """初始化只属于此浏览器上下文的夹具。

        :param origin: 自有本机 Vite origin。
        :returns: None
        """
        self.origin = origin
        self.sources = [
            source(101, "rtsp_stream", "QA 窗口一直播"),
            source(102, "srt_stream", "QA 窗口二直播"),
            source(103, "video", "QA 短视频.mp4"),
            *[
                source(201 + index, "ppt", f"QA 文稿 {state}.pptx", state)
                for index, state in enumerate(
                    (
                        "queued",
                        "running",
                        "failed",
                        "uncertain",
                        "missing",
                        "ready",
                    )
                )
            ],
        ]
        self.sequence = 0
        self.sessions = [self.make_session(1, 101), self.make_session(2, 102)]
        self.requests: list[dict[str, Any]] = []
        self.unexpected: list[dict[str, Any]] = []
        self.allowed_open: tuple[int, int] | None = None
        self.allowed_prepare: int | None = None
        self.held_prepares: dict[int, Route] = {}
        self.expected_http_errors: list[dict[str, Any]] = []

    def make_session(
        self,
        window: int,
        source_id: int,
        state: str = "loading",
        online: bool = True,
        error: str = "",
        mode: str = "",
    ) -> dict[str, Any]:
        """按真实 DTO 构造递增时间的会话，防止较旧 SSE 帧覆盖。

        :param window: 窗口 1/2。
        :param source_id: 夹具媒体主键。
        :param state: 播放状态。
        :param online: 是否存在有效 Worker 心跳。
        :param error: 显式适配器原因。
        :param mode: 当前文稿模式。
        :returns: 会话字段。
        """
        self.sequence += 1
        item = next(item for item in self.sources if item["id"] == source_id)
        stamp = (
            f"2026-09-29T00:{self.sequence // 60:02}:{self.sequence % 60:02}Z"
        )
        return {
            "window_id": window,
            "session_id": window,
            "source_id": source_id,
            "source_name": item["name"],
            "source_type": item["source_type"],
            "source_type_label": (
                "演示文稿" if item["source_type"] == "ppt" else "QA 媒体"
            ),
            "source_uri": item["uri"],
            "playback_mode": mode,
            "playback_state": state,
            "playback_state_label": {
                "loading": "加载中",
                "error": "异常",
                "playing": "播放中",
                "paused": "已暂停",
            }.get(state, state),
            "error_message": error,
            "display_mode": "single",
            "display_mode_label": "大屏输出",
            "target_display_label": f"QA DISPLAY{window + 1}",
            "current_slide": 3 if mode else 0,
            "total_slides": 9 if mode else 0,
            "position_ms": 1000 if source_id == 103 else 0,
            "duration_ms": 4000 if source_id == 103 else 0,
            "pending_command": "open" if state == "loading" else "",
            "player_online": online,
            "player_last_seen_at": stamp,
            "last_updated_at": stamp,
            "volume": 50,
            "is_muted": False,
            "loop_enabled": False,
        }

    def install(self, context: BrowserContext) -> None:
        """在加载应用前封闭所有 HTTP 外联及模拟 SSE 源。

        :param context: 独立 Chromium 上下文。
        :returns: None
        """
        context.add_init_script(EVENT_SOURCE_SHIM)
        context.route("**/*", self.handle)

    def handle(self, route: Route) -> None:
        """只放行本机静态模块，所有 API 都受精确白名单控制。

        :param route: 浏览器拦截请求。
        :returns: None
        """
        request = route.request
        parsed = urlsplit(request.url)
        if f"{parsed.scheme}://{parsed.netloc}" != self.origin:
            self.reject(route, "非本机 origin")
            return
        if not parsed.path.startswith("/api/"):
            if parsed.path.startswith(
                ("/events", "/media", "/admin", "/static")
            ):
                self.reject(route, "真实后端代理路径")
            else:
                route.continue_()
            return
        record = {
            "method": request.method,
            "path": parsed.path,
            "body": request.post_data_json if request.post_data else None,
        }
        self.requests.append(record)
        if request.method != "GET":
            if (
                self.allowed_prepare is not None
                and request.method == "POST"
                and parsed.path
                == f"/api/sources/{self.allowed_prepare}/prepare/"
            ):
                source_id = self.allowed_prepare
                self.allowed_prepare = None
                self.held_prepares[source_id] = route
                return
            allowed = self.allowed_open
            if (
                allowed
                and request.method == "POST"
                and parsed.path == f"/api/playback/{allowed[0]}/open/"
            ):
                if record["body"] != {
                    "source_id": allowed[1],
                    "autoplay": True,
                }:
                    self.reject(route, "重开请求体不匹配")
                    return
                self.allowed_open = None
                self.sessions[allowed[0] - 1] = self.make_session(*allowed)
                route.fulfill(
                    json={"success": True, "sessions": self.sessions}
                )
                return
            self.reject(route, "未预期 mutation")
            return
        user = {
            "id": 1,
            "username": "qa-fixture",
            "is_staff": True,
            "is_superuser": True,
        }
        payloads = {
            "/api/auth/csrf/": {"csrfToken": "qa-no-real-credential"},
            "/api/auth/status/": {"authenticated": True, "user": user},
            "/api/runtime/": {
                "runtime": {
                    "big_screen_mode": "double",
                    "volume_level": 50,
                    "muted_windows": [],
                }
            },
            "/api/volume/": {
                "volume": {
                    "level": 50,
                    "muted": False,
                    "system_synced": False,
                    "backend": "qa_fixture",
                }
            },
            "/api/sessions/": {"sessions": self.sessions},
            "/api/sources/": {"sources": self.sources},
            "/api/folders/": {"folders": []},
            "/api/scenarios/": {"scenarios": []},
            "/api/devices/": {"devices": []},
            "/api/displays/": {"targets": []},
            "/api/background-audio/": {
                "background_audio": {
                    "state": {
                        "source_id": None,
                        "source_name": "",
                        "playback_state": "idle",
                        "volume": 50,
                        "position_ms": 0,
                        "duration_ms": 0,
                        "is_muted": False,
                        "loop_enabled": False,
                        "pending_command": "",
                        "error_message": "",
                    },
                    "playlist": [],
                }
            },
            "/api/settings/powerpoint/": {
                "settings": {
                    "experimental_enabled": False,
                    "available": True,
                    "detail": "QA 仅展示；不会启动 Office",
                }
            },
            "/api/video-wall/layout/": {
                "layout": {
                    "draft": None,
                    "draft_revision": 0,
                    "active_preset": "double",
                }
            },
        }
        if parsed.path.endswith("/ppt-resources/"):
            source_id = int(parsed.path.split("/")[3])
            if source_id != 206:
                self.reject(route, "非 ready 文稿资源请求")
                return
            route.fulfill(
                json={
                    "success": True,
                    "resources": [
                        {
                            "id": page,
                            "source_id": 206,
                            "page_index": page,
                            "slide_image": f"/api/qa-fixture/slides/{page}/",
                            "next_slide_image": "",
                            "speaker_notes": "",
                            "has_media": False,
                            "media_items": [],
                            "created_at": "",
                        }
                        for page in range(1, 10)
                    ],
                }
            )
        elif parsed.path.startswith("/api/qa-fixture/slides/"):
            number = parsed.path.split("/")[-2]
            route.fulfill(
                content_type="image/svg+xml",
                body=(
                    '<svg xmlns="http://www.w3.org/2000/svg" '
                    'width="1920" height="1080">'
                    '<rect width="1920" height="1080" fill="#075985"/>'
                    '<text x="180" y="540" fill="white" '
                    f'font-size="160">QA page {number}</text></svg>'
                ),
            )
        elif parsed.path in payloads:
            route.fulfill(json={"success": True, **payloads[parsed.path]})
        else:
            self.reject(route, "未知 API GET")

    def reject(self, route: Route, reason: str) -> None:
        """中止请求并记录失败，绝不退回真实网络。

        :param route: 请求路由。
        :param reason: 门禁原因。
        :returns: None
        """
        self.unexpected.append(
            {
                "method": route.request.method,
                "url": route.request.url,
                "reason": reason,
            }
        )
        route.abort("blockedbyclient")

    def emit(
        self, page: Page, window: int, source_id: int, **changes: Any
    ) -> None:
        """通过与产品相同事件名注入状态，不直接访问 Vue/Pinia。

        :param page: 已建立仿真 SSE 连接的页面。
        :param window: 目标窗口。
        :param source_id: 夹具媒体。
        :param changes: 会话状态字段。
        :returns: None
        """
        self.sessions[window - 1] = self.make_session(
            window, source_id, **changes
        )
        page.evaluate(
            "payload => window.__qaEventSources"
            ".filter(s => s.readyState === 1)"
            ".forEach(s => s.emit('playback_state', payload))",
            {"sessions": copy.deepcopy(self.sessions)},
        )

    def complete_prepare(self, source_id: int, state: str = "queued") -> None:
        """返回公开 prepare 的受控当前状态，不声明真实新作业或 Office 行为。

        :param source_id: 已挂起的夹具文稿主键。
        :param state: 当前后端状态；可以是已存在的 ready/running。
        :returns: None
        :raises AssertionError: 没有相应已接收请求。
        """
        route = self.held_prepares.pop(source_id, None)
        assert route is not None
        item = next(item for item in self.sources if item["id"] == source_id)
        item["preparation_state"] = state
        item["is_available"] = state == "ready"
        item["page_count"] = 9 if state == "ready" else 0
        if state == "ready":
            item["preview_kind"] = "image"
            item["preview_url"] = item["thumbnail_url"] = (
                "/api/qa-fixture/slides/1/"
            )
        route.fulfill(json={"success": True, "source": copy.deepcopy(item)})

    @property
    def held_prepare(self) -> Route | None:
        """兼容旧入口读取第一个挂起请求。

        :returns: 当前挂起请求或空值。
        """
        return next(iter(self.held_prepares.values()), None)

    def fail_prepare(self, source_id: int, detail: str) -> None:
        """按真实错误合同释放指定旧请求，记录预期 HTTP 400。

        :param source_id: 指定的挂起源。
        :param detail: 明确的错误原因。
        :returns: None
        """
        route = self.held_prepares.pop(source_id, None)
        assert route is not None
        self.expected_http_errors.append(
            {"path": f"/api/sources/{source_id}/prepare/", "status": 400}
        )
        route.fulfill(
            status=400,
            json={"success": False, "code": "media_error", "detail": detail},
        )

    def abort_pending_prepares(self) -> None:
        """清理本上下文的全部受控挂起请求，不遗留回调。

        :returns: None
        """
        for route in self.held_prepares.values():
            route.abort()
        self.held_prepares.clear()
