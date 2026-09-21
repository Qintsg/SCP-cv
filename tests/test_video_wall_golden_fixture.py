#!/user/bin/env python
# -*- coding: UTF-8 -*-
'''
视频墙控制包黄金样本与旧实现的一致性测试。

样本由 `tools/generate_video_wall_golden.py` 从旧实现的实际运行结果导出，
.NET 侧 `VideoWallGoldenPacketsTests` 拿它逐包校对迁移结果。本文件守在样本这一头：
样本一旦与 `scp_cv/services/video_wall.py` 脱节，.NET 侧的比对就变成拿旧结果对新代码，
失去意义。
@Project : SCP-cv
@File : test_video_wall_golden_fixture.py
@Author : Qintsg
@Date : 2026-09-21
'''
from __future__ import annotations

import json
from pathlib import Path
from typing import Any, cast

from scp_cv.services.video_wall import VideoWallMode, build_sequence

_GOLDEN_PATH = (
    Path(__file__).resolve().parents[1]
    / "runtime-dotnet"
    / "tests"
    / "ScpCv.Infrastructure.Tests"
    / "Fixtures"
    / "video-wall-packets.json"
)

# 运行态大屏模式 → 旧实现的视频墙物理布局模式
_RUNTIME_MODES = {
    "single": VideoWallMode.FULLSCREEN_WS21,
    "double": VideoWallMode.SPLIT_WS21_WS22,
}


def _read_golden() -> dict[str, Any]:
    """
    读取黄金样本。
    :return: 黄金样本字典
    """
    return cast(dict[str, Any], json.loads(_GOLDEN_PATH.read_text(encoding="utf-8")))


def _legacy_packets(wall_mode: str) -> list[dict[str, Any]]:
    """
    把旧实现的下发序列转成与样本同构的结构。
    :param wall_mode: 旧实现的视频墙物理布局模式
    :return: 发送项列表（phase / ip / port / packet）
    """
    return [
        {
            "phase": str(item["phase"]),
            "ip": str(item["ip"]),
            "port": int(str(item["port"])),
            "packet": cast(bytes, item["packet"]).hex(),
        }
        for item in build_sequence(wall_mode)
    ]


def test_golden_fixture_is_readable() -> None:
    """样本文件必须存在且能被解析——生成脚本没跑过时给出明确提示。"""
    assert _GOLDEN_PATH.is_file(), (
        f"黄金样本缺失：{_GOLDEN_PATH}，请运行 uv run python tools/generate_video_wall_golden.py"
    )


def test_golden_fixture_covers_both_runtime_modes() -> None:
    """样本必须覆盖运行态的两种大屏模式，多出或缺少模式都说明生成脚本与运行态脱节。"""
    golden = _read_golden()

    assert set(golden["modes"]) == set(_RUNTIME_MODES)


def test_golden_fixture_matches_legacy_build_sequence() -> None:
    """样本必须等于旧实现当前的下发序列，逐包逐字节。"""
    golden = _read_golden()

    for runtime_mode, wall_mode in _RUNTIME_MODES.items():
        assert golden["modes"][runtime_mode] == _legacy_packets(wall_mode), (
            f"{runtime_mode} 模式的黄金样本与旧实现不一致："
            f"请重跑 tools/generate_video_wall_golden.py 并同步 .NET 侧实现"
        )
