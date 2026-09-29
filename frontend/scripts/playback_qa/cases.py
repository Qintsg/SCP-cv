#!/usr/bin/env python
# -*- coding: UTF-8 -*-
"""
媒体准备与源状态的独立浏览器用例；只使用封闭 HTTP/SSE 夹具。
@Project : SCP-cv
@File : cases.py
@Author : Qintsg
@Date : 2026-09-29
"""

import copy
import re
from pathlib import Path
from typing import Any

from playwright.sync_api import Page, expect

from playback_state_fixture import ControlledBackend
from .tools import (
    SOURCE_CARD_XPATH,
    close_notifications,
    navigate,
    record_check,
    stable_screenshot,
)


def inspect_ppt(
    page: Page,
    fixture: ControlledBackend,
    report: dict[str, Any],
    origin: str,
    output: Path,
    mobile: bool,
) -> None:
    """验证转换状态、默认页图及实验提示，绝不写设置或执行转换。

    :param page: 页面。
    :param fixture: 受控 API。
    :param report: 当前视口报告。
    :param origin: 本机 origin。
    :param output: 稳定截图目录。
    :param mobile: 移动模式。
    :returns: None
    """
    navigate(page, origin, "/sources")
    expected_labels = {
        "queued": "正在准备页图",
        "running": "正在准备页图",
        "failed": "页图转换失败",
        "uncertain": "转换状态待确认",
        "missing": "页图尚未准备",
    }
    status_labels = {
        "queued": "排队等待转换",
        "running": "正在转换页图",
        "failed": "页图转换失败",
        "uncertain": "转换结果待人工确认",
        "missing": "页图尚未准备",
    }
    for state, label in expected_labels.items():
        # 用真实搜索进入各文稿用例，避免其它行的滚动位置影响独立 drawer 验收。
        page.get_by_role("textbox", name="搜索源名称或 URL", exact=True).fill(
            f"QA 文稿 {state}.pptx"
        )
        menu = page.get_by_role(
            "button", name=f"QA 文稿 {state}.pptx 的操作菜单", exact=True
        )
        container = menu.locator(
            "xpath=ancestor::tr" if not mobile else SOURCE_CARD_XPATH
        )
        expect(container.get_by_text(label, exact=True)).to_be_visible()
        menu.tap() if mobile else menu.click()
        edit = page.get_by_text("编辑", exact=True).last
        edit.tap() if mobile else edit.click()
        drawer = page.locator(".n-drawer")
        expect(
            drawer.get_by_role("alert").filter(
                has_text=(
                    f"逐页图片状态：{status_labels[state]}；已准备 0 页。"
                    "原始 PPT 文件仍保留。"
                )
            )
        ).to_be_visible()
        retries = drawer.get_by_role("button", name=re.compile("重试转换$"))
        record_check(
            report,
            f"ppt_{state}_retry_visibility",
            retries.count() == (1 if state in ("failed", "missing") else 0),
            {"state": state, "retry_count": retries.count()},
        )
        stable_screenshot(page, output / f"ppt-{state}.png")
        retry_executed = retries.count() > 0
        if retry_executed:
            source_id = next(
                item["id"]
                for item in fixture.sources
                if item["preparation_state"] == state
            )
            fixture.allowed_prepare = source_id
            with page.expect_request(
                lambda request: request.url.endswith(
                    f"/api/sources/{source_id}/prepare/"
                )
            ):
                retries.tap() if mobile else retries.click()
            expect(retries).to_be_disabled()
            page.keyboard.press("Enter")
            page.keyboard.press("Enter")
            prepare_requests = [
                request
                for request in fixture.requests
                if request["path"] == f"/api/sources/{source_id}/prepare/"
            ]
            record_check(
                report,
                f"ppt_{state}_pending_single_prepare",
                len(prepare_requests) == 1,
                prepare_requests,
            )
            fixture.complete_prepare(source_id)
        else:
            drawer.get_by_role("button", name="取消", exact=True).click()
        drawer.wait_for(state="hidden")
        if retry_executed:
            expect(
                container.get_by_text("正在准备页图", exact=True)
            ).to_be_visible()
            record_check(
                report, f"ppt_{state}_prepare_return_only_queued", True
            )
        close_notifications(page)
        record_check(report, f"ppt_{state}_status_visible", True)
    # 同一夹具原件只改受控协议状态，验证未知新码不被误译或盲重试。
    unknown = next(item for item in fixture.sources if item["id"] == 205)
    unknown["preparation_state"] = "qa_future_state"
    navigate(page, origin, "/sources")
    page.get_by_role("textbox", name="搜索源名称或 URL", exact=True).fill(
        unknown["name"]
    )
    menu = page.get_by_role(
        "button", name=f"{unknown['name']} 的操作菜单", exact=True
    )
    menu.tap() if mobile else menu.click()
    edit = page.get_by_text("编辑", exact=True).last
    edit.tap() if mobile else edit.click()
    drawer = page.locator(".n-drawer")
    expect(
        drawer.get_by_role("alert").filter(
            has_text=(
                "逐页图片状态：qa_future_state；已准备 0 页。"
                "原始 PPT 文件仍保留。"
            )
        )
    ).to_be_visible()
    expect(
        drawer.get_by_role("button", name=re.compile("重试转换$"))
    ).to_have_count(0)
    stable_screenshot(page, output / "ppt-unknown-diagnostic.png")
    record_check(report, "ppt_unknown_raw_diagnostic_no_retry", True)
    drawer.get_by_role("button", name="取消", exact=True).click()
    drawer.wait_for(state="hidden")
    unknown["preparation_state"] = "queued"
    fixture.emit(page, 1, 206, state="playing", mode="slide_images")
    navigate(page, origin, "/display/big-left", mobile)
    expect(page.get_by_text("逐页图片 · 默认", exact=True)).to_be_visible()
    expect(page.get_by_text("3 / 9", exact=True)).to_be_visible()
    proof = stable_screenshot(page, output / "ppt-default-page-images.png")
    record_check(
        report,
        "ppt_default_preview_decoded",
        any(image["width"] == 1920 for image in proof["images"]),
        proof,
    )
    fixture.emit(page, 1, 206, state="playing", mode="powerpoint")
    expect(
        page.get_by_text("PowerPoint 原生放映 · 实验性", exact=True)
    ).to_be_visible()
    stable_screenshot(page, output / "ppt-experimental-session.png")
    navigate(page, origin, "/settings")
    page.get_by_text("开发", exact=True).click()
    expect(
        page.get_by_text("PowerPoint 原生放映（实验性）", exact=True)
    ).to_be_visible()
    expect(
        page.get_by_text(
            "默认播放使用上传时生成的逐页图片，不会启动 PowerPoint 放映。"
            "启用原生模式可能与现场 Office 实例冲突，仅影响下次打开的文稿。",
            exact=True,
        )
    ).to_be_visible()
    expect(
        page.get_by_text("QA 仅展示；不会启动 Office", exact=True)
    ).to_be_visible()
    proof = stable_screenshot(page, output / "ppt-experimental-settings.png")
    record_check(report, "experimental_settings_warning_no_write", True, proof)


def inspect_source_rows(
    page: Page,
    fixture: ControlledBackend,
    report: dict[str, Any],
    origin: str,
    output: Path,
    mobile: bool,
) -> None:
    """核对行级播出标签，配置选中或历史状态不能冒充在线播出。

    :param page: 真实源管理页面。
    :param fixture: 受控状态。
    :param report: 判据报告。
    :param origin: 本机 origin。
    :param output: 截图目录。
    :param mobile: 是否使用卡片形态。
    :returns: None
    """
    navigate(page, origin, "/sources")
    page.get_by_role("textbox", name="搜索源名称或 URL", exact=True).fill(
        "QA 短视频.mp4"
    )
    menu = page.get_by_role(
        "button", name="QA 短视频.mp4 的操作菜单", exact=True
    )
    row = menu.locator(
        "xpath=ancestor::tr" if not mobile else SOURCE_CARD_XPATH
    )
    for name, state, online in (
        ("loading", "loading", True),
        ("error", "error", True),
        ("offline", "playing", False),
    ):
        fixture.emit(page, 1, 103, state=state, online=online)
        page.wait_for_function(
            "() => document.querySelector('.sources-view') !== null"
        )
        stable_screenshot(page, output / f"source-row-{name}.png")
        on_air = row.get_by_text("正在窗口 1 播出", exact=True)
        record_check(
            report,
            f"source_row_{name}_not_onair",
            on_air.count() == 0,
            {"row": row.inner_text()},
        )
    fixture.emit(page, 1, 103, state="playing", online=True)
    expect(row.get_by_text("正在窗口 1 播出", exact=True)).to_be_visible()
    record_check(report, "source_row_online_playing_onair", True)


def inspect_existing_prepare(
    page: Page,
    fixture: ControlledBackend,
    report: dict[str, Any],
    origin: str,
    output: Path,
    mobile: bool,
) -> None:
    """旧 failed 界面重试时，验证 ready/running 响应不冒称新建排队。

    :param page: 真实页面。
    :param fixture: 当前后端状态受控夹具。
    :param report: 判据结果。
    :param origin: 自有 Vite origin。
    :param output: 截图证据目录。
    :param mobile: 是否使用触屏 tap。
    :returns: None
    """
    item = next(item for item in fixture.sources if item["id"] == 203)
    original = copy.deepcopy(item)
    for state, label in (("ready", "页图已就绪"), ("running", "正在转换页图")):
        item.update(original)
        item.update(
            {
                "preparation_state": "failed",
                "is_available": False,
                "page_count": 0,
            }
        )
        navigate(page, origin, "/sources")
        page.get_by_role("textbox", name="搜索源名称或 URL", exact=True).fill(
            item["name"]
        )
        menu = page.get_by_role(
            "button", name=f"{item['name']} 的操作菜单", exact=True
        )
        menu.tap() if mobile else menu.click()
        edit = page.get_by_text("编辑", exact=True).last
        edit.tap() if mobile else edit.click()
        drawer = page.locator(".n-drawer")
        retry = drawer.get_by_role("button", name=re.compile("重试转换$"))
        fixture.allowed_prepare = 203
        before = len(
            [
                r
                for r in fixture.requests
                if r["path"] == "/api/sources/203/prepare/"
            ]
        )
        with page.expect_request(
            lambda request: request.url.endswith("/api/sources/203/prepare/")
        ):
            retry.tap() if mobile else retry.click()
        expect(retry).to_be_disabled()
        fixture.complete_prepare(203, state)
        drawer.wait_for(state="hidden")
        notices = page.locator(".n-notification")
        expect(notices).to_have_count(1)
        notification = notices.inner_text()
        after = len(
            [
                r
                for r in fixture.requests
                if r["path"] == "/api/sources/203/prepare/"
            ]
        )
        record_check(
            report,
            f"prepare_existing_{state}_not_claim_queued",
            "排队" not in notification,
            notification,
        )
        record_check(
            report,
            f"prepare_existing_{state}_notification_real_state",
            label in notification,
            notification,
        )
        record_check(
            report,
            f"prepare_existing_{state}_one_request",
            after == before + 1,
        )
        stable_screenshot(page, output / f"prepare-existing-{state}.png")
        close_notifications(page)
    item.update(original)
