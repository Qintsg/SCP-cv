#!/usr/bin/env python
# -*- coding: UTF-8 -*-
"""
真实渲染等待、截图与报告工具；不建立后端或设备连接。
@Project : SCP-cv
@File : tools.py
@Author : Qintsg
@Date : 2026-09-29
"""

from pathlib import Path
from typing import Any

from playwright.sync_api import Page, expect

SOURCE_CARD_XPATH = (
    "xpath=ancestor::div[contains(concat(' ', "
    "normalize-space(@class), ' '), ' n-card ')][1]"
)
SOURCE_ITEM_GEOMETRY = """items => items.map(item => ({
  name: item.querySelector('.source-picker__name').textContent,
  rowBottom: item.getBoundingClientRect().bottom,
  statusBottom: item.querySelector('.source-picker__sub')
    .getBoundingClientRect().bottom
}))"""


def stable_screenshot(page: Page, path: Path) -> dict[str, Any]:
    """等候字体、有限动画及可见图片解码后保存稳定帧和溢出证据。

    :param page: 真实渲染页面。
    :param path: 截图目标。
    :returns: 文档宽度及解码完成图像清单。
    """
    page.evaluate("() => document.fonts.ready")
    page.wait_for_function(
        "document.getAnimations().every(a => a.playState !== 'running' || "
        "a.effect?.getTiming().iterations === Infinity)"
    )
    page.wait_for_function(
        "Array.from(document.images).filter(i => "
        "i.getBoundingClientRect().width > 0)"
        ".every(i => i.complete && i.naturalWidth > 0)"
    )
    page.evaluate(
        "() => Promise.all(Array.from(document.images)"
        ".filter(i => i.getBoundingClientRect().width > 0)"
        ".map(i => i.decode()))"
    )
    proof = page.evaluate("""() => ({
          viewport: innerWidth,
          scrollWidth: document.documentElement.scrollWidth,
          images: Array.from(document.images)
            .filter(i => i.getBoundingClientRect().width > 0)
            .map(i => ({url: i.getAttribute('src'), complete: i.complete,
              width: i.naturalWidth, height: i.naturalHeight}))
        })""")
    # 交互用例只捕获真实视口，完整长图留给单独只读视觉检查。
    page.screenshot(path=str(path), full_page=False, animations="disabled")
    return proof


def navigate(page: Page, origin: str, path: str, mobile: bool = False) -> None:
    """等待应用初始化及仿真 SSE，再按真实渲染状态操作页面。

    :param page: 独立页面。
    :param origin: 自有 Vite。
    :param path: 目标路由。
    :param mobile: 是否切换至播放控制移动标签。
    :returns: None
    """
    page.goto(origin + path, wait_until="networkidle")
    page.wait_for_function(
        "window.__qaEventSources?.some(s => s.readyState === 1)"
    )
    if mobile and path.startswith("/display/"):
        page.get_by_text("播放控制", exact=True).tap()


def record_check(
    report: dict[str, Any], name: str, passed: bool, evidence: Any = None
) -> None:
    """记录可继续调查的独立检查，不隐藏已观察的产品缺陷。

    :param report: 当前视口报告。
    :param name: 检查名。
    :param passed: 实际判据。
    :param evidence: 受控状态/几何证据。
    :returns: None
    """
    report["checks"].append(
        {"name": name, "passed": passed, "evidence": evidence}
    )


def close_notifications(page: Page) -> None:
    """用实际关闭控件清除已验证的错误提示，再开始独立用例。

    :param page: 真实页面。
    :returns: None
    """
    notices = page.locator(".n-notification")
    while notices.count():
        count = notices.count()
        notices.last.locator(".n-base-close").click()
        expect(notices).to_have_count(count - 1)
