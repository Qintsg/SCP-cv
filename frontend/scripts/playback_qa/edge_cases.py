#!/usr/bin/env python
# -*- coding: UTF-8 -*-
"""
文稿准备的跨编辑周期及可用性边界，全部使用本机受控协议夹具。
@Project : SCP-cv
@File : edge_cases.py
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
    SOURCE_ITEM_GEOMETRY,
    close_notifications,
    navigate,
    record_check,
    stable_screenshot,
)


def open_edit(page: Page, name: str, mobile: bool) -> None:
    """通过真实搜索和行菜单打开指定源，不强制点击。

    :param page: 实际 Vue 页面。
    :param name: 精确源名称。
    :param mobile: 是否使用 tap。
    :returns: None
    """
    page.get_by_role("textbox", name="搜索源名称或 URL", exact=True).fill(name)
    menu = page.get_by_role("button", name=f"{name} 的操作菜单", exact=True)
    menu.tap() if mobile else menu.click()
    edit = page.get_by_text("编辑", exact=True).last
    edit.tap() if mobile else edit.click()
    expect(page.locator(".n-drawer")).to_be_visible()


def inspect_prepare_scope(
    page: Page,
    fixture: ControlledBackend,
    report: dict[str, Any],
    origin: str,
    output: Path,
    mobile: bool,
) -> None:
    """A 准备在途取消后进入新 scope，旧 success/error 不得影响新草稿。

    :param page: 真实页面。
    :param fixture: 受控后端。
    :param report: 回归报告。
    :param origin: 本机服务器。
    :param output: 截图目录。
    :param mobile: 移动触屏模式。
    :returns: None
    """
    originals = copy.deepcopy(fixture.sources)
    for result, new_id in (
        ("success", 205),
        ("error", 205),
        ("same_source", 203),
    ):
        for item in fixture.sources:
            if item["id"] in (203, 205):
                item.update(
                    {"preparation_state": "failed", "is_available": False}
                )
        old = next(item for item in fixture.sources if item["id"] == 203)
        new = next(item for item in fixture.sources if item["id"] == new_id)
        navigate(page, origin, "/sources")
        open_edit(page, old["name"], mobile)
        drawer = page.locator(".n-drawer")
        fixture.allowed_prepare = 203
        retry = drawer.get_by_role("button", name=re.compile("重试转换$"))
        with page.expect_request(
            lambda request: request.url.endswith("/api/sources/203/prepare/")
        ):
            retry.tap() if mobile else retry.click()
        expect(retry).to_be_disabled()
        drawer.get_by_role("button", name="取消", exact=True).click()
        drawer.wait_for(state="hidden")
        open_edit(page, new["name"], mobile)
        draft = f"QA 新编辑周期 {result} 草稿"
        field = drawer.get_by_placeholder("例如：早会演示文稿")
        field.fill(draft)
        new_retry = drawer.get_by_role("button", name=re.compile("重试转换$"))
        enabled = new_retry.is_enabled()
        record_check(
            report, f"prepare_scope_{result}_new_cycle_not_blocked", enabled
        )
        new_pending = False
        if enabled and new_id != 203:
            fixture.allowed_prepare = new_id
            with page.expect_request(
                lambda request: request.url.endswith(
                    f"/api/sources/{new_id}/prepare/"
                )
            ):
                new_retry.tap() if mobile else new_retry.click()
            expect(new_retry).to_be_disabled()
            new_pending = True
        with page.expect_response(
            lambda response: response.url.endswith("/api/sources/203/prepare/")
        ):
            if result == "error":
                fixture.fail_prepare(203, "QA_LATE_A_ERROR 旧编辑请求失败")
            else:
                fixture.complete_prepare(203, "ready")
        # 等待响应后 microtask/Vue 刷新，不在当前请求仍挂起时阻塞截图等待。
        page.evaluate(
            "() => new Promise(resolve => requestAnimationFrame("
            "() => requestAnimationFrame(resolve)))"
        )
        visible = drawer.is_visible()
        record_check(
            report, f"prepare_scope_{result}_new_drawer_preserved", visible
        )
        if visible:
            record_check(
                report,
                f"prepare_scope_{result}_new_draft_preserved",
                field.input_value() == draft,
            )
            record_check(
                report,
                f"prepare_scope_{result}_no_old_error",
                "QA_LATE_A_ERROR" not in drawer.inner_text(),
            )
            if new_pending:
                record_check(
                    report,
                    f"prepare_scope_{result}_new_pending_preserved",
                    new_retry.is_disabled(),
                )
        if new_pending:
            fixture.complete_prepare(new_id)
            drawer.wait_for(state="hidden")
        elif visible:
            drawer.get_by_role("button", name="取消", exact=True).click()
            drawer.wait_for(state="hidden")
        close_notifications(page)
        stable_screenshot(page, output / f"prepare-scope-{result}.png")
    fixture.sources[:] = originals


def inspect_preparation_availability(
    page: Page,
    fixture: ControlledBackend,
    report: dict[str, Any],
    origin: str,
    output: Path,
    mobile: bool,
) -> None:
    """PDF 不冒称页图缺失；就绪页图但源不可用时保留真实状态。

    :param page: 源页面。
    :param fixture: 受控来源。
    :param report: 判据报告。
    :param origin: 本机 origin。
    :param output: 截图目录。
    :param mobile: 手机卡片或桌面表格。
    :returns: None
    """
    item = next(item for item in fixture.sources if item["id"] == 205)
    original = copy.deepcopy(item)
    for mode, state in (("pdf", ""), ("slide_images", "ready")):
        item.update(
            {
                "playback_mode": mode,
                "preparation_state": state,
                "is_available": False,
            }
        )
        navigate(page, origin, "/sources")
        page.get_by_role("textbox", name="搜索源名称或 URL", exact=True).fill(
            item["name"]
        )
        row = page.locator(
            ".sources-view__cards" if mobile else ".sources-view__table"
        )
        text = row.inner_text()
        record_check(
            report,
            f"availability_{mode}_not_false_missing_images",
            "页图尚未准备" not in text,
            text,
        )
        if state == "ready":
            record_check(
                report,
                "availability_ready_preserves_actual_image_state",
                "页图已就绪" in text,
                text,
            )
        stable_screenshot(page, output / f"availability-{mode}.png")
    item.update(original)


def inspect_offline_controls(
    page: Page,
    fixture: ControlledBackend,
    report: dict[str, Any],
    output: Path,
    mobile: bool,
) -> None:
    """检查按钮、滑块、选源离线禁用及恢复后的请求零重放。

    :param page: 播控页面。
    :param fixture: 受控 API。
    :param report: 视口报告。
    :param output: 截图目录。
    :param mobile: 手机模式。
    :returns: None
    """
    fixture.emit(page, 1, 103, state="playing", online=False)
    expect(
        page.get_by_role("alert").get_by_text("播放器离线", exact=True)
    ).to_be_visible()
    controls = page.locator(".playback-control")
    expect(
        controls.get_by_role("button", name="暂停", exact=True)
    ).to_be_disabled()
    expect(
        controls.get_by_role("button", name="停止", exact=True)
    ).to_be_disabled()
    expect(
        controls.get_by_role("button", name="关闭显示", exact=True)
    ).to_be_disabled()
    switch_proof = [
        controls.get_by_role("switch", name=name, exact=True).evaluate(
            """element => ({
              name: element.getAttribute('aria-label'),
              class: element.className,
              tabindex: element.getAttribute('tabindex'),
              ariaDisabled: element.getAttribute('aria-disabled')
            })"""
        )
        for name in ("循环播放", "窗口静音")
    ]
    record_check(
        report,
        "offline_switches_component_disabled",
        all("n-switch--disabled" in item["class"] for item in switch_proof),
        switch_proof,
    )
    seek = controls.get_by_role("slider", name="播放进度", exact=True)
    seek_state = {
        "aria_disabled": seek.get_attribute("aria-disabled"),
        "tabindex": seek.get_attribute("tabindex"),
        "class": seek.get_attribute("class"),
        "enabled": seek.is_enabled(),
    }
    record_check(
        report, "offline_seek_disabled", not seek_state["enabled"], seek_state
    )
    before = len([r for r in fixture.requests if r["method"] != "GET"])
    if seek_state["enabled"]:
        seek.focus()
        seek.press("ArrowRight")
    record_check(report, "offline_transport_buttons_disabled", True)
    proof = stable_screenshot(page, output / "offline-video.png")
    record_check(
        report,
        "offline_video_no_horizontal_overflow",
        proof["scrollWidth"] <= proof["viewport"],
        proof,
    )
    if mobile:
        page.get_by_text("切换源", exact=True).first.tap()
    picker = page.locator(".source-picker").get_by_role(
        "button", name="QA 窗口一直播", exact=False
    )
    expect(picker).to_be_visible()
    picker_state = {
        "disabled": picker.is_disabled(),
        "text": picker.inner_text(),
    }
    record_check(
        report,
        "offline_source_picker_disabled",
        picker_state["disabled"],
        picker_state,
    )
    if not picker_state["disabled"]:
        picker.click()
        expect(
            page.get_by_text(
                "PlayerWorker 当前离线，已拒绝发送控制命令。", exact=True
            ).last
        ).to_be_visible()
    stable_screenshot(page, output / "offline-source-picker.png")
    for state, label in (
        ("queued", "正在准备页图"),
        ("running", "正在准备页图"),
        ("failed", "页图转换失败"),
        ("uncertain", "转换状态待确认"),
        ("missing", "页图尚未准备"),
    ):
        item = page.locator(".source-picker__item").filter(
            has=page.get_by_text(f"QA 文稿 {state}.pptx", exact=True)
        )
        expect(item.get_by_text(label, exact=True)).to_be_visible()
        record_check(
            report, f"source_picker_ppt_{state}_not_mislabeled_offline", True
        )
    rows = page.locator(".source-picker__item").evaluate_all(
        SOURCE_ITEM_GEOMETRY
    )
    record_check(
        report,
        "source_picker_nine_items_no_clipped_badges",
        len(rows) == 9
        and all(
            item["statusBottom"] <= item["rowBottom"] + 1 for item in rows
        ),
        rows,
    )
    close_notifications(page)
    fixture.emit(page, 1, 103, state="playing", online=True)
    expect(
        page.get_by_role("alert").get_by_text("播放器离线", exact=True)
    ).to_be_hidden()
    if mobile:
        page.get_by_text("播放控制", exact=True).tap()
    expect(
        controls.get_by_role("button", name="暂停", exact=True)
    ).to_be_enabled()
    stable_screenshot(page, output / "offline-recovered.png")
    after = len([r for r in fixture.requests if r["method"] != "GET"])
    record_check(
        report,
        "offline_attempt_and_recovery_no_mutation_or_replay",
        before == after,
        {"before": before, "after": after},
    )
