# 检查 Spec Kit 功能目录、任务及模板的前置条件。
# 项目：SCP-cv
# 文件：check_prerequisites.py
# 作者：Qintsg
# 日期：2026-10-21
# /// script
# dependencies = ["PyYAML==6.0.3"]
# ///

from __future__ import annotations

import json
import sys
from dataclasses import dataclass
from pathlib import Path

from common import (
    FeaturePaths,
    TemplateResolutionError,
    format_speckit_command,
    get_feature_paths,
    resolve_template_content,
)


def _json_line(payload: object) -> str:
    """:param payload: 需要输出的结果对象。
    :returns: 带换行符的 UTF-8 JSON 文本。
    """
    return json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n"


HELP_TEXT = """Usage: check_prerequisites.py [OPTIONS]

Consolidated prerequisite checking for Spec-Driven Development workflow.

OPTIONS:
  --json              Output in JSON format
  --require-tasks     Require tasks.md to exist (for implementation phase)
  --include-tasks     Include tasks.md in AVAILABLE_DOCS list
  --paths-only        Only output path variables (no prerequisite validation)
  --template NAME     Include composed template content in JSON output
  --help, -h          Show this help message

EXAMPLES:
  # 检查设计阶段前置条件（需要 plan.md）
  uv run .specify/scripts/python/check_prerequisites.py --json

  # 检查实现阶段前置条件（需要 plan.md 和 tasks.md）
  uv run .specify/scripts/python/check_prerequisites.py --json --require-tasks --include-tasks

  # 只读取当前功能路径
  uv run .specify/scripts/python/check_prerequisites.py --paths-only

"""


@dataclass(frozen=True)
class Args:
    json_mode: bool = False
    require_tasks: bool = False
    include_tasks: bool = False
    paths_only: bool = False
    template_name: str | None = None


def _parse_args(argv: list[str]) -> Args:
    """:param argv: 当前命令行参数。
    :returns: 通过选项检查的前置条件配置。
    """
    json_mode = False
    require_tasks = False
    include_tasks = False
    paths_only = False
    template_name = None

    index = 0
    while index < len(argv):
        arg = argv[index]
        if arg == "--json":
            json_mode = True
        elif arg == "--require-tasks":
            require_tasks = True
        elif arg == "--include-tasks":
            include_tasks = True
        elif arg == "--paths-only":
            paths_only = True
        elif arg == "--template":
            index += 1
            if index >= len(argv):
                print(
                    "ERROR: --template requires a template name",
                    file=sys.stderr,
                )
                raise SystemExit(1)
            template_name = argv[index]
        elif arg in {"--help", "-h"}:
            sys.stdout.write(HELP_TEXT)
            raise SystemExit(0)
        else:
            print(
                f"ERROR: Unknown option '{arg}'. Use --help for usage information.",
                file=sys.stderr,
            )
            raise SystemExit(1)
        index += 1

    return Args(
        json_mode=json_mode,
        require_tasks=require_tasks,
        include_tasks=include_tasks,
        paths_only=paths_only,
        template_name=template_name,
    )


def _dir_has_entries(path: Path) -> bool:
    """:param path: 需要检查的目录。
    :returns: 目录存在并包含文件时返回 True。
    """
    try:
        return path.is_dir() and any(path.iterdir())
    except OSError:
        return False


def _available_docs(paths: FeaturePaths, include_tasks: bool) -> list[str]:
    """:param paths: 当前功能的文件路径。
    :param include_tasks: 是否包含任务清单。
    :returns: 当前实际存在的文档名称。
    """
    docs: list[str] = []
    if paths.research.is_file():
        docs.append("research.md")
    if paths.data_model.is_file():
        docs.append("data-model.md")
    if _dir_has_entries(paths.contracts_dir):
        docs.append("contracts/")
    if paths.quickstart.is_file():
        docs.append("quickstart.md")
    if include_tasks and paths.tasks.is_file():
        docs.append("tasks.md")
    return docs


def _print_paths_only(paths: FeaturePaths, json_mode: bool) -> None:
    """:param paths: 当前功能的文件路径。
    :param json_mode: 是否输出 JSON 对象。
    :returns: 输出当前路径信息。
    """
    if json_mode:
        sys.stdout.write(
            _json_line(
                {
                    "REPO_ROOT": str(paths.repo_root),
                    "BRANCH": paths.current_branch,
                    "FEATURE_DIR": str(paths.feature_dir),
                    "FEATURE_SPEC": str(paths.feature_spec),
                    "IMPL_PLAN": str(paths.impl_plan),
                    "TASKS": str(paths.tasks),
                }
            )
        )
        return

    print(f"REPO_ROOT: {paths.repo_root}")
    print(f"BRANCH: {paths.current_branch}")
    print(f"FEATURE_DIR: {paths.feature_dir}")
    print(f"FEATURE_SPEC: {paths.feature_spec}")
    print(f"IMPL_PLAN: {paths.impl_plan}")
    print(f"TASKS: {paths.tasks}")


def _status_marker(ok: bool) -> str:
    """:param ok: 当前检查是否通过。
    :returns: 当前终端编码能够表示的检查标记。
    """
    glyph = "✓" if ok else "✗"
    try:
        glyph.encode(getattr(sys.stdout, "encoding", None) or "utf-8")
    except (LookupError, UnicodeEncodeError):
        return "[OK]" if ok else "[FAIL]"
    return glyph


def _check_file(path: Path, description: str) -> None:
    """:param path: 待检查的文件。
    :param description: 输出的文件说明。
    :returns: 输出文件存在状态。
    """
    print(f"  {_status_marker(path.is_file())} {description}")


def _check_dir(path: Path, description: str) -> None:
    """:param path: 待检查的目录。
    :param description: 输出的目录说明。
    :returns: 输出目录是否包含文件。
    """
    print(f"  {_status_marker(_dir_has_entries(path))} {description}")


def _print_text_results(paths: FeaturePaths, include_tasks: bool) -> None:
    """:param paths: 当前功能的文件路径。
    :param include_tasks: 是否包含任务清单。
    :returns: 输出可读的当前文档状态。
    """
    print(f"FEATURE_DIR:{paths.feature_dir}")
    print("AVAILABLE_DOCS:")
    _check_file(paths.research, "research.md")
    _check_file(paths.data_model, "data-model.md")
    _check_dir(paths.contracts_dir, "contracts/")
    _check_file(paths.quickstart, "quickstart.md")
    if include_tasks:
        _check_file(paths.tasks, "tasks.md")


def main(argv: list[str] | None = None) -> int:
    """:param argv: 命令行参数；省略时读取进程参数。
    :returns: 所有必需条件满足时返回零。
    """
    args = _parse_args(list(argv if argv is not None else sys.argv[1:]))

    try:
        paths = get_feature_paths(
            no_persist=args.paths_only,
            script_file=Path(__file__),
        )
    except SystemExit as exc:
        if exc.code == 0:
            return 0
        print("ERROR: Failed to resolve feature paths", file=sys.stderr)
        return int(exc.code) if isinstance(exc.code, int) else 1

    if args.paths_only:
        _print_paths_only(paths, args.json_mode)
        return 0

    if not paths.feature_dir.is_dir():
        print(f"ERROR: Feature directory not found: {paths.feature_dir}", file=sys.stderr)
        print(
            f"Run {format_speckit_command('specify', paths.repo_root)} first to create the feature structure.",
            file=sys.stderr,
        )
        return 1

    if not paths.impl_plan.is_file():
        print(f"ERROR: plan.md not found in {paths.feature_dir}", file=sys.stderr)
        print(
            f"Run {format_speckit_command('plan', paths.repo_root)} first to create the implementation plan.",
            file=sys.stderr,
        )
        return 1

    if args.require_tasks and not paths.tasks.is_file():
        print(f"ERROR: tasks.md not found in {paths.feature_dir}", file=sys.stderr)
        print(
            f"Run {format_speckit_command('tasks', paths.repo_root)} first to create the task list.",
            file=sys.stderr,
        )
        return 1

    docs = _available_docs(paths, args.include_tasks)
    template_content = None
    if args.template_name:
        try:
            template_content = resolve_template_content(
                args.template_name, paths.repo_root
            )
        except TemplateResolutionError as exc:
            print(f"ERROR: {exc}", file=sys.stderr)
            return 1
        if template_content is None:
            print(
                f"ERROR: Could not resolve required {args.template_name} from "
                f"the template override stack for {paths.repo_root}",
                file=sys.stderr,
            )
            return 1

    if args.json_mode:
        payload: dict[str, object] = {
            "FEATURE_DIR": str(paths.feature_dir),
            "AVAILABLE_DOCS": docs,
        }
        if args.template_name:
            payload["TEMPLATE_CONTENT"] = template_content
        sys.stdout.write(
            _json_line(payload)
        )
    else:
        _print_text_results(paths, args.include_tasks)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
