#!/usr/bin/env python
# -*- coding: UTF-8 -*-
'''
以真实后端验证中文目录、文件移动、下载与桌面/手机页面同步。
@Project : SCP-cv
@File : media_folder_qa.py
@Author : Qintsg
@Date : 2026-09-28
'''

import argparse
import hashlib
import json
import time
from pathlib import Path

from playwright.sync_api import Page, expect, sync_playwright


def create_folder(page: Page, name: str, empty_check: bool = False) -> int:
    """在当前目录创建本轮测试文件夹。

    :param page: 已认证的页面。
    :param name: 唯一测试名称。
    :param empty_check: 是否先检查空名称门禁。
    :returns: 新目录主键。
    """
    page.get_by_role("button", name="新建文件夹", exact=True).click()
    modal = page.locator(".n-modal")
    if empty_check:
        modal.get_by_role("button", name="创建", exact=True).click()
        expect(modal.get_by_role("alert")).to_have_text("请输入文件夹名称")
    modal.get_by_role("textbox", name="文件夹名称").fill(name)
    with page.expect_response(lambda r: r.url.endswith("/api/folders/") and r.request.method == "POST") as response:
        modal.get_by_role("button", name="创建", exact=True).click()
    assert response.value.status == 201, response.value.text()
    modal.wait_for(state="hidden")
    expect(page.get_by_role("button", name=name, exact=True)).to_be_visible()
    return response.value.json()["folder"]["id"]


def upload(page: Page, path: Path, filename: str, name: str) -> dict:
    """通过上传抽屉提交真实文件，返回后端登记记录。

    :param page: 已认证页面。
    :param path: 本机只读夹具。
    :param filename: 上传后的原件名称。
    :param name: 唯一显示名称。
    :returns: 后端源记录。
    """
    page.get_by_role("button", name="添加源", exact=True).first.click()
    drawer = page.locator(".n-drawer")
    drawer.locator('input[type="file"]').set_input_files({"name": filename, "mimeType": "application/octet-stream", "buffer": path.read_bytes()})
    drawer.get_by_placeholder("例如：早会演示文稿").fill(name)
    drawer.get_by_role("switch").click()
    with page.expect_response(lambda r: r.url.endswith("/api/sources/upload/") and r.request.method == "POST", timeout=60000) as response:
        drawer.get_by_role("button", name="上传并保存", exact=True).click()
    assert response.value.status == 201, response.value.text()
    drawer.wait_for(state="hidden")
    expect(page.get_by_role("button", name=f"{name} 的操作菜单")).to_be_visible()
    return response.value.json()["source"]


def download(page: Page, name: str, expected_digest: str, output: Path) -> None:
    """从行菜单下载，核对原件字节而不是只核对 HTTP 成功。

    :param page: 已认证页面。
    :param name: 唯一源名称。
    :param expected_digest: 原件 SHA256。
    :param output: 下载证据路径。
    :returns: None
    """
    page.get_by_role("button", name=f"{name} 的操作菜单").click()
    with page.expect_download() as event:
        page.get_by_text("下载", exact=True).last.click()
    event.value.save_as(output)
    assert event.value.suggested_filename == "中文同名图卡.png"
    assert hashlib.sha256(output.read_bytes()).hexdigest() == expected_digest


def folder_menu(page: Page, name: str) -> None:
    """打开已渲染的目标目录卡片菜单。

    :param page: 页面。
    :param name: 目录名称。
    :returns: None
    """
    card = page.locator(".sources-view__folder-card").filter(has=page.get_by_role("button", name=name, exact=True))
    card.get_by_role("button", name="编辑", exact=True).click()


def choose_submenu(page: Page, parent: str, destination: str) -> None:
    """等待级联菜单进入稳定帧再选择，不强制点击动画中的元素。

    :param page: 页面。
    :param parent: 父菜单文本。
    :param destination: 子菜单文本。
    :returns: None
    """
    page.wait_for_function("document.getAnimations().every(animation => animation.playState !== 'running')")
    page.get_by_text(parent, exact=True).last.hover()
    target = page.get_by_text(destination, exact=True).last
    target.wait_for()
    page.wait_for_function("document.getAnimations().every(animation => animation.playState !== 'running')")
    target.click()


def main() -> None:
    """仅修改新建 QA 夹具，失败保留证据，成功清理自己的源与目录。

    :returns: None
    :raises AssertionError: 页面/接口/字节或布局不符合合同。
    """
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", required=True)
    parser.add_argument("--cookies", type=Path, required=True)
    parser.add_argument("--image", type=Path, required=True)
    parser.add_argument("--ppt", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    suffix = str(time.time_ns())[-10:]
    parent_name = f"QA-006-UI-资料-{suffix}"
    renamed = f"QA-006-UI-已整理-{suffix}"
    target_name = f"QA-006-UI-目标-{suffix}"
    names = [f"QA-006-UI-图卡-{suffix}-{i}" for i in (1, 2)]
    digest = hashlib.sha256(args.image.read_bytes()).hexdigest()
    source_ids: list[int] = []
    folder_ids: list[int] = []
    summary: dict = {"sources": source_ids, "folders": folder_ids, "checks": []}
    (args.output / "fixture.json").write_text(json.dumps(summary), encoding="utf-8")
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True)
        context = browser.new_context(viewport={"width": 1440, "height": 900}, accept_downloads=True)
        context.add_cookies(json.loads(args.cookies.read_text(encoding="utf-8-sig")))
        page = context.new_page()
        errors: list[str] = []
        writes: list[str] = []
        page.on("pageerror", lambda error: errors.append(str(error)))
        page.on("request", lambda request: writes.append(request.url.split("/api/", 1)[1]) if request.method == "PATCH" and "/api/" in request.url else None)
        page.on("response", lambda response: print("FOLDER_HTTP", response.request.method, response.status, flush=True) if "/api/folders/" in response.url else None)
        page.on("requestfailed", lambda request: print("FOLDER_REQUEST_FAILED", request.method, request.failure, flush=True) if "/api/folders/" in request.url else None)
        try:
            page.goto(args.base.rstrip("/") + "/sources")
            page.wait_for_load_state("networkidle")
            assert "/login" not in page.url
            folder_ids.append(create_folder(page, parent_name, empty_check=True))
            folder_ids.append(create_folder(page, target_name))
            page.get_by_role("button", name=parent_name, exact=True).click()
            child = create_folder(page, "PPT文件")
            folder_ids.append(child)
            page.get_by_role("button", name="PPT文件", exact=True).click()
            for name in names:
                source = upload(page, args.image, "中文同名图卡.png", name)
                source_ids.append(source["id"])
                assert source["folder_id"] == child
                assert parent_name in source["uri"] and "PPT文件" in source["uri"]
            assert source["uri"].endswith("中文同名图卡 (2).png")
            download(page, names[0], digest, args.output / "desktop-download.png")
            summary["checks"].append("same-name upload and desktop download")
            page.get_by_role("button", name="新建文件夹", exact=True).click()
            page.locator(".n-modal").get_by_role("textbox", name="文件夹名称").fill("子目录")
            with page.expect_response(lambda r: r.url.endswith("/api/folders/") and r.request.method == "POST") as created:
                page.locator(".n-modal").get_by_role("button", name="创建", exact=True).click()
            grandchild = created.value.json()["folder"]["id"]
            folder_ids.append(grandchild)
            page.locator(".n-modal").wait_for(state="hidden")
            page.get_by_role("button", name="新建文件夹", exact=True).click()
            page.locator(".n-modal").get_by_role("textbox", name="文件夹名称").fill("子目录")
            with page.expect_response(lambda r: r.url.endswith("/api/folders/") and r.request.method == "POST") as duplicate:
                page.locator(".n-modal").get_by_role("button", name="创建", exact=True).click()
            assert duplicate.value.status == 400
            expect(page.locator(".n-modal")).to_be_visible()
            page.locator(".n-modal").get_by_role("button", name="取消", exact=True).click()
            page.locator(".n-modal").wait_for(state="hidden")
            summary["checks"].append("empty and duplicate folder rejection")
            page.get_by_role("button", name=f"{names[0]} 的操作菜单").click()
            with page.expect_response(lambda r: r.url.endswith(f"/api/sources/{source_ids[0]}/move/")) as moved:
                choose_submenu(page, "移动到…", "移动到根目录")
            assert moved.value.status == 200
            expect(page.get_by_role("button", name=f"{names[0]} 的操作菜单")).to_have_count(0)
            page.get_by_role("button", name="媒体根目录", exact=True).click()
            expect(page.get_by_role("button", name=f"{names[0]} 的操作菜单")).to_be_visible()
            folder_menu(page, parent_name)
            page.once("dialog", lambda dialog: dialog.accept(renamed))
            with page.expect_response(lambda r: r.url.endswith(f"/api/folders/{folder_ids[0]}/") and r.request.method == "PATCH") as rename:
                page.get_by_text("重命名", exact=True).last.click()
            assert rename.value.status == 200
            expect(page.get_by_role("button", name=renamed, exact=True)).to_be_visible()
            page.get_by_role("button", name=renamed, exact=True).click()
            folder_menu(page, "PPT文件")
            with page.expect_response(lambda r: r.url.endswith(f"/api/folders/{child}/") and r.request.method == "PATCH") as folder_move:
                choose_submenu(page, "移动文件夹到…", target_name)
            assert folder_move.value.status == 200
            expect(page.get_by_role("button", name="PPT文件", exact=True)).to_have_count(0)
            page.get_by_role("button", name="媒体根目录", exact=True).click()
            page.get_by_role("button", name=target_name, exact=True).click()
            page.get_by_role("button", name="PPT文件", exact=True).click()
            expect(page.get_by_role("navigation", name="文件夹路径")).to_contain_text(target_name)
            download(page, names[1], digest, args.output / "moved-download.png")
            summary["checks"].append("source move, parent rename and subtree move refresh")
            page.screenshot(path=str(args.output / "folders-desktop.png"), full_page=True)
            page.set_viewport_size({"width": 390, "height": 844})
            download(page, names[1], digest, args.output / "mobile-download.png")
            assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
            page.evaluate("window.scrollTo(0, 0)")
            page.screenshot(path=str(args.output / "folders-mobile.png"), full_page=True)
            page.screenshot(path=str(args.output / "folders-mobile-viewport.png"))
            summary["checks"].append("mobile download and no horizontal overflow")
            if args.ppt:
                ppt_name = f"QA-006-UI-PPT-{suffix}"
                ppt = upload(page, args.ppt, "中文文稿.pptx", ppt_name)
                source_ids.append(ppt["id"])
                assert ppt["preparation_state"] == "queued"
                page.get_by_text("正在准备页图", exact=True).last.wait_for()
                page.get_by_role("button", name=f"{ppt_name} 的操作菜单").click()
                page.get_by_text("编辑", exact=True).last.click()
                expect(page.locator(".n-drawer")).to_contain_text("queued")
                expect(page.locator(".n-drawer")).to_contain_text("原始 PPT 文件仍保留")
                page.screenshot(path=str(args.output / "ppt-queued-mobile.png"))
                page.locator(".n-drawer").get_by_role("button", name="取消", exact=True).click()
                page.locator(".n-drawer").wait_for(state="hidden")
                summary["checks"].append("PPT upload queued and retained-original feedback")
            assert not errors, errors
            for path in (f"sources/{source_ids[0]}/move/", f"folders/{folder_ids[0]}/", f"folders/{child}/"):
                assert writes.count(path) == 1, f"UI write must dispatch exactly once: {path} count={writes.count(path)}"
            summary["checks"].append("each UI move/rename dispatched exactly once")
            csrf = context.request.get(args.base.rstrip("/") + "/api/auth/csrf/").json()["csrfToken"]
            headers = {"X-CSRFToken": csrf, "Origin": args.base.rstrip("/")}
            for source_id in source_ids:
                response = context.request.delete(args.base.rstrip("/") + f"/api/sources/{source_id}/", headers=headers)
                assert response.status == 200, f"QA source cleanup failed {source_id}: {response.status}"
            for folder_id in reversed(folder_ids):
                response = context.request.delete(args.base.rstrip("/") + f"/api/folders/{folder_id}/", headers=headers)
                assert response.status == 200, f"QA folder cleanup failed {folder_id}: {response.status}"
            summary["checks"].append("owned fixtures cleaned")
            summary["passed"] = True
        finally:
            if not summary.get("passed"):
                page.screenshot(path=str(args.output / "failed-page.png"), full_page=True)
                summary["failure_page"] = page.locator("body").inner_text()
            (args.output / "fixture.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
            context.close()
            browser.close()
    print("WORKSTATION_FOLDER_QA_PASSED", json.dumps(summary["checks"], ensure_ascii=False))


if __name__ == "__main__":
    main()
