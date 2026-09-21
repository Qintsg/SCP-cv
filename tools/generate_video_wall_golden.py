#!/user/bin/env python
# -*- coding: UTF-8 -*-
'''
从旧 Python 实现导出视频墙控制包黄金样本，供 .NET 侧逐包比对。

样本内容来自 `scp_cv/services/video_wall.py` 的**实际运行结果**（不是从 .NET 实现或规范表格转抄），
因此 `runtime-dotnet/tests/ScpCv.Infrastructure.Tests/VideoWallGoldenPacketsTests.cs` 拿它比对时，
能发现迁移过程中任何逐节点、逐字节的走样——旧的单测只断言了单屏模式第一个映射包，
双屏模式 50 个映射包一个字节都没有断言。

用法（需要项目环境，因为旧实现依赖 Django 模型）：

```text
uv run python tools/generate_video_wall_golden.py           # 重新生成黄金样本
uv run python tools/generate_video_wall_golden.py --check   # 只校验样本与旧实现是否仍一致
```

改了视频墙协议时：先改 `scp_cv/services/video_wall.py`，再重跑本脚本，再跑 .NET 测试。
样本是生成产物，不要手工编辑。
@Project : SCP-cv
@File : generate_video_wall_golden.py
@Author : Qintsg
@Date : 2026-09-21
'''
from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from pathlib import Path
from typing import Any

_REPO_ROOT = Path(__file__).resolve().parents[1]

# 脚本位于 tools/ 下，直接执行时 sys.path[0] 是 tools/ 而不是仓库根，import scp_cv 会失败；
# 这里显式补上仓库根，使 `uv run python tools/generate_video_wall_golden.py` 能直接跑。
if str(_REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(_REPO_ROOT))

_GOLDEN_PATH = (
    _REPO_ROOT
    / "runtime-dotnet"
    / "tests"
    / "ScpCv.Infrastructure.Tests"
    / "Fixtures"
    / "video-wall-packets.json"
)
_LEGACY_SOURCE = _REPO_ROOT / "scp_cv" / "services" / "video_wall.py"

# 运行态大屏模式 → 旧实现的视频墙物理布局模式
_RUNTIME_MODES = {
    "single": "FULLSCREEN_WS21",
    "double": "SPLIT_WS21_WS22",
}


def build_golden() -> dict[str, Any]:
    """
    调用旧 Python 实现构造两种模式的完整下发序列。
    :return: 黄金样本字典（模式 → 发送项列表）
    :raises RuntimeError: 旧实现的模式常量缺失时
    """
    os.environ.setdefault("DJANGO_SETTINGS_MODULE", "scp_cv.settings")
    import django

    django.setup()

    from scp_cv.services.video_wall import VideoWallMode, build_sequence

    modes: dict[str, list[dict[str, Any]]] = {}
    for runtime_mode, wall_mode_name in _RUNTIME_MODES.items():
        if not hasattr(VideoWallMode, wall_mode_name):
            raise RuntimeError(
                f"旧实现缺少视频墙模式 {wall_mode_name}，请确认 scp_cv/services/video_wall.py 的 "
                f"VideoWallMode 是否被重命名"
            )
        wall_mode = getattr(VideoWallMode, wall_mode_name)
        modes[runtime_mode] = [
            {
                "phase": str(item["phase"]),
                "ip": str(item["ip"]),
                "port": int(str(item["port"])),
                "packet": bytes(item["packet"]).hex(),
            }
            for item in build_sequence(wall_mode)
        ]

    return {
        "note": "由旧 Python 实现生成的视频墙控制包黄金样本，请勿手工编辑；改动协议后重跑 tools/generate_video_wall_golden.py",
        "legacy_source": _LEGACY_SOURCE.relative_to(_REPO_ROOT).as_posix(),
        "legacy_source_sha256": hashlib.sha256(_LEGACY_SOURCE.read_bytes()).hexdigest(),
        "generator": "tools/generate_video_wall_golden.py",
        "modes": modes,
    }


def serialize(golden: dict[str, Any]) -> str:
    """
    序列化为稳定的 JSON 文本（LF 换行、缩进 2、键顺序固定）。
    :param golden: 黄金样本字典
    :return: 待写入文件的文本
    """
    return json.dumps(golden, ensure_ascii=False, indent=2, sort_keys=False) + "\n"


def main(argv: list[str] | None = None) -> int:
    """
    生成或校验黄金样本。
    :param argv: 命令行参数，缺省取 sys.argv
    :return: 进程退出码，0 表示成功
    """
    parser = argparse.ArgumentParser(description="导出视频墙控制包黄金样本")
    parser.add_argument(
        "--check",
        action="store_true",
        help="只校验现有样本与旧实现是否一致，不写文件",
    )
    args = parser.parse_args(argv)

    text = serialize(build_golden())
    if args.check:
        if not _GOLDEN_PATH.exists():
            print(f"黄金样本不存在：{_GOLDEN_PATH}", file=sys.stderr)
            return 1
        if _GOLDEN_PATH.read_text(encoding="utf-8") != text:
            print(
                f"黄金样本与旧实现不一致：{_GOLDEN_PATH}\n"
                f"若刚改过视频墙协议，请重跑本脚本（去掉 --check）后同步更新 .NET 侧实现。",
                file=sys.stderr,
            )
            return 1
        print(f"黄金样本与旧实现一致：{_GOLDEN_PATH}")
        return 0

    _GOLDEN_PATH.parent.mkdir(parents=True, exist_ok=True)
    _GOLDEN_PATH.write_text(text, encoding="utf-8", newline="\n")
    print(f"已写入黄金样本：{_GOLDEN_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
