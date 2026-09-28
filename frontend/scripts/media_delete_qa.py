#!/usr/bin/env python
# -*- coding: UTF-8 -*-
'''
在真实工作站后端验证媒体删除失败反馈、列表同步和桌面/手机布局。
@Project : SCP-cv
@File : media_delete_qa.py
@Author : Qintsg
@Date : 2026-09-28
'''

import argparse
import json
from pathlib import Path

from playwright.sync_api import Page, sync_playwright


def attempt_locked_delete(page: Page, source_id: int, name: str) -> None:
    """尝试删除已由工作站夹具锁定的 QA 源，核对错误与刷新。

    :param page: 已认证的页面。
    :param source_id: 本轮 QA 源主键。
    :param name: 页面显示名称。
    :returns: None
    :raises AssertionError: 删除误报成功、没有刷新或源从列表消失。
    """
    page.get_by_role("button", name=f"{name} 的操作菜单").click()
    page.get_by_text("删除源", exact=True).last.click()
    page.get_by_text("原件被占用或只读时删除会被拒绝。", exact=False).wait_for()
    with page.expect_response(
        lambda response: response.url.endswith("/api/sources/")
        and response.request.method == "GET"
    ) as refreshed:
        with page.expect_response(
            lambda response: response.url.endswith(f"/api/sources/{source_id}/")
            and response.request.method == "DELETE"
        ) as deleted:
            page.locator(".n-modal").get_by_role("button", name="删除源", exact=True).click()
    assert deleted.value.status == 400, deleted.value.text()
    assert refreshed.value.status == 200, refreshed.value.text()
    assert any(item["id"] == source_id for item in refreshed.value.json()["sources"])
    page.get_by_text("媒体原件被占用或没有删除权限，删除未生效", exact=False).last.wait_for()
    page.locator(".n-modal").wait_for(state="hidden")
    page.get_by_role("button", name=f"{name} 的操作菜单").wait_for()


def main() -> None:
    """仅对显式指定且名称带 QA 前缀的已锁定源执行页面失败回归。

    :returns: None
    :raises AssertionError: 页面错误、横向溢出或删除失败合同不匹配。
    """
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", required=True)
    parser.add_argument("--cookies", type=Path, required=True)
    parser.add_argument("--source-id", type=int, required=True)
    parser.add_argument("--source-name", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    assert args.source_id > 0 and args.source_name.startswith("qa-delete-")
    cookies = json.loads(args.cookies.read_text(encoding="utf-8-sig"))
    args.output.mkdir(parents=True, exist_ok=True)
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True)
        context = browser.new_context(viewport={"width": 1440, "height": 900})
        context.add_cookies(cookies)
        page = context.new_page()
        errors: list[str] = []
        page.on("pageerror", lambda error: errors.append(str(error)))
        page.goto(args.base.rstrip("/") + "/sources")
        page.wait_for_load_state("networkidle")
        assert "/login" not in page.url
        attempt_locked_delete(page, args.source_id, args.source_name)
        page.evaluate("window.scrollTo(0, 0)")
        page.screenshot(path=str(args.output / "delete-locked-desktop.png"), full_page=True)
        page.set_viewport_size({"width": 390, "height": 844})
        attempt_locked_delete(page, args.source_id, args.source_name)
        assert page.locator(".n-notification").count() == 1
        assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
        page.evaluate("window.scrollTo(0, 0)")
        page.screenshot(path=str(args.output / "delete-locked-mobile.png"), full_page=True)
        page.screenshot(path=str(args.output / "delete-locked-mobile-viewport.png"))
        assert not errors, errors
        context.close()
        browser.close()
    print("WORKSTATION_DELETE_QA_PASSED desktop/mobile=2 responses=400 refresh=200")


if __name__ == "__main__":
    main()
