#!/usr/bin/env python
# -*- coding: UTF-8 -*-
'''
直播源添加抽屉的真实浏览器合同回归。
@Project : SCP-cv
@File : stream_source_qa.py
@Author : Qintsg
@Date : 2026-09-28
'''

import os
import re
from pathlib import Path

from playwright.sync_api import sync_playwright


def main() -> None:
    """以登录操作员身份从页面创建 RTSP 源并核对 REST 结果。

    :returns: None
    :raises AssertionError: 页面未提供直播源入口或登记失败
    """
    password = os.environ["QA_LOCAL_PASSWORD"]
    base = "http://127.0.0.1:5174"
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True)
        page = browser.new_page(viewport={"width": 1440, "height": 900})
        errors: list[str] = []
        page.on("pageerror", lambda error: errors.append(str(error)))
        page.goto(base + "/login")
        page.wait_for_load_state("networkidle")
        page.get_by_placeholder("请输入用户名").fill("qa-local")
        page.get_by_placeholder("请输入密码").fill(password)
        page.get_by_role("button", name="登录").click()
        page.wait_for_url(re.compile(r"/dashboard$"))
        page.goto(base + "/sources")
        page.wait_for_load_state("networkidle")
        page.get_by_role("button", name="添加源", exact=True).first.click()
        page.locator(".n-tabs-tab").filter(has_text="直播流").click()
        page.screenshot(path=str(Path.cwd() / ".validation/qa-big-screen-20260927/stream-recon.png"), full_page=True)
        page.get_by_label("流地址").fill("rtsp://192.0.2.10:8554/live")
        page.get_by_label("显示名称").fill("QA 直播摄像头")
        with page.expect_response(lambda response: response.url.endswith("/api/sources/streams/")) as created:
            page.get_by_role("button", name="添加直播源").click()
        assert created.value.status == 201, created.value.text()
        assert created.value.json()["source"]["source_type"] == "rtsp_stream"
        source_id = created.value.json()["source"]["id"]
        page.locator(".n-drawer").wait_for(state="hidden")
        page.get_by_text("直播 · 待验证", exact=True).wait_for()
        page.get_by_role("button", name="QA 直播摄像头 的操作菜单").click()
        page.get_by_text("编辑", exact=True).last.click()
        page.get_by_label("流地址").fill("rtsp://192.0.2.11:8554/next")
        with page.expect_response(lambda response: f"/api/sources/{source_id}/" in response.url
                                  and response.request.method == "PATCH") as updated:
            page.get_by_role("button", name="保存修改").click()
        assert updated.value.status == 200, updated.value.text()
        assert updated.value.json()["source"]["uri"] == "rtsp://192.0.2.11:8554/next"
        page.locator(".n-drawer").wait_for(state="hidden")
        for protocol, url, name, expected in [
            ("SRT · 低延迟推流", "srt://192.0.2.11:8890/live", "QA SRT", "srt_stream"),
            ("自定义 IP 媒体流", "http://192.0.2.12:8080/live.ts", "QA IP 流", "custom_stream"),
        ]:
            page.get_by_role("button", name="添加源", exact=True).first.click()
            page.locator(".n-tabs-tab").filter(has_text="直播流").click()
            page.locator(".n-drawer .n-select").click()
            page.get_by_text(protocol, exact=True).last.click()
            page.get_by_label("流地址").fill(url)
            page.get_by_label("显示名称").fill(name)
            with page.expect_response(lambda response: response.url.endswith("/api/sources/streams/")) as added:
                page.get_by_role("button", name="添加直播源").click()
            assert added.value.status == 201, added.value.text()
            assert added.value.json()["source"]["source_type"] == expected
            page.locator(".n-drawer").wait_for(state="hidden")
        page.get_by_role("button", name="添加源", exact=True).first.click()
        page.locator(".n-tabs-tab").filter(has_text="直播流").click()
        page.get_by_label("流地址").fill("http://192.0.2.20/not-rtsp")
        with page.expect_response(lambda response: response.url.endswith("/api/sources/streams/")) as rejected:
            page.get_by_role("button", name="添加直播源").click()
        assert rejected.value.status == 400
        page.screenshot(path=str(Path.cwd() / ".validation/qa-big-screen-20260927/stream-invalid.png"), full_page=True)
        page.get_by_text("直播源地址必须是无内嵌凭据的 rtsp:// 主机地址。", exact=False).wait_for(timeout=3000)
        page.get_by_role("button", name="取消").last.click()
        page.set_viewport_size({"width": 390, "height": 844})
        assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
        page.screenshot(path=str(Path.cwd() / ".validation/qa-big-screen-20260927/stream-mobile.png"), full_page=True)
        assert not errors, errors
        print("BROWSER_STREAM_SOURCE_PASS", flush=True)
        browser.close()


if __name__ == "__main__":
    main()
