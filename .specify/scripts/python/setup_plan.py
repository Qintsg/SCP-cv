# 准备当前 Spec Kit 功能的实现计划。
# 项目：SCP-cv
# 文件：setup_plan.py
# 作者：Qintsg
# 日期：2026-10-21
# /// script
# dependencies = ["PyYAML==6.0.3"]
# ///

from __future__ import annotations

import json
import sys
from pathlib import Path

from common import (
    TemplateResolutionError,
    get_feature_paths,
    resolve_template_content,
)


def _json_line(payload: object) -> str:
    """:param payload: 需要输出的结果对象。
    :returns: 带换行符的 JSON 文本。
    """
    return json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n"


def _help_text(argv0: str) -> str:
    """:param argv0: 当前脚本名称。
    :returns: 命令行使用说明。
    """
    return f"""Usage: {argv0} [--json]
  --json    Output results in JSON format
  --help    Show this help message
"""


def main(argv: list[str] | None = None) -> int:
    """校验参数并准备当前功能的实现计划。

    :param argv: 命令行参数；省略时使用当前进程参数。
    :returns: 成功返回零，参数或必需模板无效时返回非零状态。
    """
    args = list(argv if argv is not None else sys.argv[1:])
    json_mode = False
    for arg in args:
        if arg == "--json":
            json_mode = True
        elif arg in {"--help", "-h"}:
            sys.stdout.write(_help_text(sys.argv[0]))
            return 0
        else:
            print(f"ERROR: Unknown option '{arg}'", file=sys.stderr)
            return 1

    try:
        paths = get_feature_paths(script_file=Path(__file__))
    except SystemExit as exc:
        if exc.code == 0:
            return 0
        print("ERROR: Failed to resolve feature paths", file=sys.stderr)
        return int(exc.code) if isinstance(exc.code, int) else 1

    # JSON 模式的状态信息写入 stderr，保持 stdout 仅包含结果对象。
    status_stream = sys.stderr if json_mode else sys.stdout
    if paths.impl_plan.is_file():
        print(
            f"Plan already exists at {paths.impl_plan}, skipping template copy",
            file=status_stream,
        )
    else:
        try:
            template_content = resolve_template_content("plan-template", paths.repo_root)
        except TemplateResolutionError as exc:
            print(f"Error: {exc}", file=sys.stderr)
            return 1
        if template_content is None:
            print("ERROR: Required plan-template was not found", file=sys.stderr)
            return 1
        paths.feature_dir.mkdir(parents=True, exist_ok=True)
        paths.impl_plan.write_bytes(template_content.encode("utf-8"))
        print(f"Copied plan template to {paths.impl_plan}", file=status_stream)

    if json_mode:
        sys.stdout.write(
            _json_line(
                {
                    "FEATURE_SPEC": str(paths.feature_spec),
                    "IMPL_PLAN": str(paths.impl_plan),
                    "SPECS_DIR": str(paths.feature_dir),
                    "BRANCH": paths.current_branch,
                }
            )
        )
    else:
        print(f"FEATURE_SPEC: {paths.feature_spec}")
        print(f"IMPL_PLAN: {paths.impl_plan}")
        print(f"SPECS_DIR: {paths.feature_dir}")
        print(f"BRANCH: {paths.current_branch}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
