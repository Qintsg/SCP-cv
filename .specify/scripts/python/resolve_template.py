# 读取项目配置指定的 Spec Kit 模板内容。
# 项目：SCP-cv
# 文件：resolve_template.py
# 作者：Qintsg
# 日期：2026-10-21
# /// script
# dependencies = ["PyYAML==6.0.3"]
# ///

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from common import (
    TemplateResolutionError,
    get_repo_root,
    resolve_template_content,
)


def main(argv: list[str] | None = None) -> int:
    """:param argv: 命令行参数；省略时读取进程参数。
    :returns: 指定模板读取成功时返回零。
    """
    parser = argparse.ArgumentParser()
    parser.add_argument("template_name")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args(argv)

    repo_root = get_repo_root(Path(__file__))
    try:
        content = resolve_template_content(args.template_name, repo_root)
    except TemplateResolutionError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    if content is None:
        print(
            f"ERROR: Could not resolve required {args.template_name} from the "
            f"template override stack for {repo_root}",
            file=sys.stderr,
        )
        return 1

    if args.json:
        print(
            json.dumps(
                {
                    "TEMPLATE_NAME": args.template_name,
                    "TEMPLATE_CONTENT": content,
                },
                ensure_ascii=False,
                separators=(",", ":"),
            )
        )
    else:
        sys.stdout.write(content)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
