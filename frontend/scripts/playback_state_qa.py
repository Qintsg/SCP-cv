#!/usr/bin/env python
# -*- coding: UTF-8 -*-
"""
封闭真实浏览器播控矩阵：两窗状态、离线门禁、PPT 状态及实验提示。
仅使用本机自有 Vite 和受控 HTTP/SSE；不能证明实体播出或真实 Office。
@Project : SCP-cv
@File : playback_state_qa.py
@Author : Qintsg
@Date : 2026-09-29
"""

import argparse
import json
import os
import shutil
import socket
import subprocess
from contextlib import contextmanager
from pathlib import Path
from typing import Any, Iterator

from playwright.sync_api import Browser, expect, sync_playwright

from playback_state_fixture import ControlledBackend
from playback_qa.tools import (
    SOURCE_ITEM_GEOMETRY,
    close_notifications as close_notifications,
    navigate,
    record_check,
    stable_screenshot,
)
from playback_qa.cases import (
    inspect_ppt,
    inspect_source_rows,
    inspect_existing_prepare,
)
from playback_qa.edge_cases import (
    inspect_offline_controls,
    inspect_prepare_scope,
    inspect_preparation_availability,
)


@contextmanager
def isolated_vite(output: Path) -> Iterator[str]:
    """启动自有、隐藏、回环且 strictPort 的 Vite，并精确结束该进程。

    :param output: 忽略目录内的日志路径。
    :returns: 仅本机测试 origin。
    :raises RuntimeError: 依赖缺失、Vite 启动失败或未及时退出。
    """
    frontend = Path(__file__).resolve().parents[1]
    node = shutil.which("node")
    vite = frontend / "node_modules/vite/bin/vite.js"
    if not node or not vite.is_file():
        raise RuntimeError("已有 Node/Vite 不可用；请按仓库 pnpm 约定安装依赖")
    with socket.socket() as reserved:
        reserved.bind(("127.0.0.1", 0))
        port = reserved.getsockname()[1]
    origin = f"http://127.0.0.1:{port}"
    environment = {
        **os.environ,
        "VITE_BACKEND_TARGET": "http://127.0.0.1:9",
        "NO_COLOR": "1",
    }
    with (output / "vite.log").open("wb") as log:
        # 并行 QA 的依赖优化缓存也必须隔离，避免其它测试触发重优化/刷新。
        vite_entry = (vite.parents[1] / "dist/node/index.js").as_uri()
        configuration = {
            "mode": "web",
            "cacheDir": str(output / "vite-cache"),
            "server": {
                "host": "127.0.0.1",
                "port": port,
                "strictPort": True,
            },
        }
        startup = (
            f"import {{ createServer }} from {json.dumps(vite_entry)};"
            f"const server = await createServer({json.dumps(configuration)});"
            "await server.listen(); server.printUrls();"
        )
        process = subprocess.Popen(
            [node, "--input-type=module", "-e", startup],
            cwd=frontend,
            env=environment,
            stdout=log,
            stderr=subprocess.STDOUT,
            creationflags=(
                subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
            ),
        )
        try:
            # 就绪轮询由独立 Chromium 等待 HTTP 条件，不依赖任意固定睡眠。
            with sync_playwright() as playwright:
                readiness_browser = playwright.chromium.launch(headless=True)
                readiness = readiness_browser.new_page()
                try:
                    readiness.wait_for_function(
                        "async origin => { try { await fetch(origin, "
                        "{mode: 'no-cors'}); return true; } "
                        "catch { return false; } }",
                        arg=origin,
                        polling=250,
                        timeout=30000,
                    )
                    if process.poll() is not None:
                        raise RuntimeError(
                            f"自有 Vite 提前退出 {process.returncode}"
                        )
                finally:
                    readiness_browser.close()
            yield origin
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait(timeout=15)
            with socket.socket() as check:
                if check.connect_ex(("127.0.0.1", port)) == 0:
                    raise RuntimeError(f"测试 Vite 退出后端口 {port} 仍被占用")


def run_viewport(
    browser: Browser, origin: str, output: Path, mobile: bool
) -> dict[str, Any]:
    """在独立上下文完成单视口矩阵并保存请求及失败诊断。

    :param browser: Headless Chromium。
    :param origin: 仅本机服务器。
    :param output: 忽略证据目录。
    :param mobile: 390px 触屏或 1440px 桌面。
    :returns: 报告，不把部分通过提升为整体验收。
    """
    output.mkdir(parents=True, exist_ok=True)
    context = browser.new_context(
        viewport={
            "width": 390 if mobile else 1440,
            "height": 844 if mobile else 900,
        },
        is_mobile=mobile,
        has_touch=mobile,
        reduced_motion="reduce",
    )
    fixture = ControlledBackend(origin)
    fixture.install(context)
    page = context.new_page()
    report: dict[str, Any] = {
        "checks": [],
        "console_errors": [],
        "page_errors": [],
        "exception": None,
    }
    page.on(
        "console",
        lambda message: (
            report["console_errors"].append(
                {"text": message.text, "location": message.location}
            )
            if message.type == "error"
            else None
        ),
    )
    page.on(
        "pageerror", lambda error: report["page_errors"].append(str(error))
    )
    try:
        for window, param in ((1, "big-left"), (2, "big-right")):
            navigate(page, origin, f"/display/{param}", mobile)
            if not mobile:
                rows = page.locator(".source-picker__item").evaluate_all(
                    SOURCE_ITEM_GEOMETRY
                )
                record_check(
                    report,
                    f"window{window}_source_picker_badges_not_clipped",
                    all(
                        item["statusBottom"] <= item["rowBottom"] + 1
                        for item in rows
                    ),
                    rows,
                )
            control = page.locator(".playback-control")
            expect(
                control.locator(".playback-control__heading").get_by_text(
                    "加载中", exact=True
                )
            ).to_be_visible()
            record_check(
                report,
                f"window{window}_loading_not_labeled_live",
                control.get_by_text("直播中", exact=True).count() == 0,
            )
            expect(
                page.get_by_text("命令已受理，等待实际结果", exact=True)
            ).to_be_visible()
            stable_screenshot(page, output / f"window{window}-loading.png")
            error = (
                f"QA_RTSP_404 窗口 {window}：发布路径不存在；受理不等于出画"
            )
            fixture.emit(
                page, window, 100 + window, state="error", error=error
            )
            expect(
                control.locator(".playback-control__heading").get_by_text(
                    "异常", exact=True
                )
            ).to_be_visible()
            expect(control.get_by_text(error, exact=True)).to_be_visible(
                timeout=7000
            )
            live_tag = control.get_by_text("直播中", exact=True)
            record_check(
                report,
                f"window{window}_error_not_labeled_live",
                live_tag.count() == 0,
                {
                    "error": error,
                    "live_label_visible": live_tag.count() > 0
                    and live_tag.is_visible(),
                },
            )
            if not mobile:
                record_check(
                    report,
                    f"window{window}_error_source_picker_not_onair",
                    page.locator(".source-picker")
                    .get_by_text("正在播出", exact=True)
                    .count()
                    == 0,
                )
            stable_screenshot(page, output / f"window{window}-error.png")
            fixture.allowed_open = (window, 100 + window)
            try:
                with page.expect_response(
                    lambda response: response.url.endswith(
                        f"/api/playback/{window}/open/"
                    )
                    and response.request.method == "POST",
                    timeout=4000,
                ):
                    reopen = control.get_by_role(
                        "button", name="再次打开源", exact=True
                    )
                    (
                        reopen.tap(timeout=3500)
                        if mobile
                        else reopen.click(timeout=3500)
                    )
                expect(
                    control.locator(".playback-control__heading").get_by_text(
                        "加载中", exact=True
                    )
                ).to_be_visible()
                record_check(
                    report, f"window{window}_explicit_reopen_action", True
                )
            except Exception as reopen_error:
                record_check(
                    report,
                    f"window{window}_explicit_reopen_action",
                    False,
                    str(reopen_error),
                )
                fixture.allowed_open = None
                stable_screenshot(
                    page, output / f"window{window}-reopen-blocked.png"
                )
                if mobile:
                    page.get_by_text("播放控制", exact=True).click()
            fixture.emit(page, window, 100 + window, state="playing")
            expect(control.get_by_text("播放中", exact=True)).to_be_visible()
            expect(control.get_by_text(error, exact=True)).to_be_hidden()
            proof = stable_screenshot(
                page, output / f"window{window}-recovered.png"
            )
            record_check(
                report,
                f"window{window}_loading_error_reason_explicit_reopen_recovery",
                True,
                proof,
            )
            record_check(
                report,
                f"window{window}_no_horizontal_overflow",
                proof["scrollWidth"] <= proof["viewport"],
            )
        navigate(page, origin, "/display/big-left", mobile)
        inspect_offline_controls(page, fixture, report, output, mobile)
        inspect_source_rows(page, fixture, report, origin, output, mobile)
        inspect_ppt(page, fixture, report, origin, output, mobile)
        inspect_existing_prepare(page, fixture, report, origin, output, mobile)
        inspect_prepare_scope(page, fixture, report, origin, output, mobile)
        inspect_preparation_availability(
            page, fixture, report, origin, output, mobile
        )
    except Exception as error:
        report["exception"] = str(error)
        page.screenshot(path=str(output / "failure.png"), full_page=True)
        (output / "failure-dom.html").write_text(
            page.content(), encoding="utf-8"
        )
    finally:
        fixture.abort_pending_prepares()
        report["requests"] = fixture.requests
        report["unexpected_requests"] = fixture.unexpected
        expected_errors = [
            error
            for error in report["console_errors"]
            if any(
                error["location"].get("url", "").endswith(item["path"])
                and error["text"].startswith("Failed to load resource:")
                and "status of 400" in error["text"]
                for item in fixture.expected_http_errors
            )
        ]
        report["expected_console_errors"] = expected_errors
        report["console_errors"] = [
            error
            for error in report["console_errors"]
            if error not in expected_errors
        ]
        report["expected_http_errors"] = fixture.expected_http_errors
        report["sse_urls"] = page.evaluate(
            "(window.__qaEventSources || []).map(source => source.url)"
        )
        context.close()
    record_check(
        report, "zero_unexpected_requests", not report["unexpected_requests"]
    )
    record_check(report, "zero_console_errors", not report["console_errors"])
    record_check(report, "zero_page_errors", not report["page_errors"])
    return report


def main() -> int:
    """执行两种视口，报告任何可复现缺口并清理自有服务。

    :returns: 全部判据通过为 0，否则为 1。
    """
    parser = argparse.ArgumentParser(
        description="封闭本机播控/PPT 浏览器矩阵，无实体副作用"
    )
    parser.add_argument(
        "--output",
        type=Path,
        required=True,
        help=".validation 下本轮唯一证据目录",
    )
    args = parser.parse_args()
    output = args.output.resolve()
    validation = Path(__file__).resolve().parents[2] / ".validation"
    if not output.is_relative_to(validation):
        raise ValueError("QA 输出必须在当前仓库 .validation 忽略目录内")
    output.mkdir(parents=True, exist_ok=True)
    with isolated_vite(output) as origin, sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True)
        try:
            reports = {
                name: run_viewport(browser, origin, output / name, mobile)
                for name, mobile in (
                    ("desktop-1440", False),
                    ("mobile-390", True),
                )
            }
        finally:
            browser.close()
    success = all(
        not report["exception"]
        and all(check["passed"] for check in report["checks"])
        for report in reports.values()
    )
    summary = {
        "success": success,
        "scope": (
            "真实 Vue 浏览器 + 封闭 HTTP/EventSource 协议夹具；"
            "无 D4/原生媒体验证"
        ),
        "reports": reports,
    }
    (output / "summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(
        json.dumps(
            {
                "success": success,
                "output": str(output),
                "failed": {
                    name: [
                        check["name"]
                        for check in report["checks"]
                        if not check["passed"]
                    ]
                    for name, report in reports.items()
                },
                "exceptions": {
                    name: report["exception"]
                    for name, report in reports.items()
                },
            },
            ensure_ascii=False,
        )
    )
    return 0 if success else 1


if __name__ == "__main__":
    raise SystemExit(main())
