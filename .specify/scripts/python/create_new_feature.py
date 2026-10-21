# 创建 Spec Kit 功能目录及需求规范。
# 项目：SCP-cv
# 文件：create_new_feature.py
# 作者：Qintsg
# 日期：2026-10-21
# /// script
# dependencies = ["PyYAML==6.0.3"]
# ///

from __future__ import annotations

import datetime
import json
import re
import shlex
import sys
from dataclasses import dataclass
from pathlib import Path

from common import (
    TemplateResolutionError,
    get_repo_root,
    persist_feature_json,
    resolve_template_content,
)


def _json_line(payload: object) -> str:
    """:param payload: 需要输出的结果对象。
    :returns: 带换行符的 JSON 文本。
    """
    return json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n"


_STOP_WORDS = frozenset(
    """
    i a an the to for of in on at by with from is are was were be been being
    have has had do does did will would should could can may might must shall
    this that these those my your our their want need add get set
    """.split()
)

_MAX_BRANCH_LENGTH = 244
_MAX_FEATURE_NUMBER = 2**63 - 1


def _int64_from_digits(value: str) -> int | None:
    """:param value: 无符号十进制数字文本。
    :returns: 有效整数；超过功能编号范围时返回 None。
    """
    normalized = value.lstrip("0") or "0"
    maximum = str(_MAX_FEATURE_NUMBER)
    if len(normalized) > len(maximum) or (
        len(normalized) == len(maximum) and normalized > maximum
    ):
        return None
    return int(normalized, 10)


def _persistence_assignments(
    branch_name: str, feature_dir: str, *, powershell: bool
) -> tuple[str, str]:
    """:param branch_name: 当前功能标识。
    :param feature_dir: 当前功能目录。
    :param powershell: 是否输出 PowerShell 环境变量命令。
    :returns: 功能标识及目录的环境变量设置命令。
    """
    if powershell:
        quoted_branch = "'" + branch_name.replace("'", "''") + "'"
        quoted_dir = "'" + feature_dir.replace("'", "''") + "'"
        return (
            f"$env:SPECIFY_FEATURE = {quoted_branch}",
            f"$env:SPECIFY_FEATURE_DIRECTORY = {quoted_dir}",
        )
    return (
        f"export SPECIFY_FEATURE={shlex.quote(branch_name)}",
        f"export SPECIFY_FEATURE_DIRECTORY={shlex.quote(feature_dir)}",
    )


def _usage(argv0: str) -> str:
    """:param argv0: 当前脚本名称。
    :returns: 命令行参数格式。
    """
    return (
        f"Usage: {argv0} [--json] [--dry-run] [--allow-existing-branch] "
        "[--short-name <name>] [--number N] [--timestamp] <feature_description>"
    )


def _help_text(argv0: str) -> str:
    """:param argv0: 当前脚本名称。
    :returns: 完整命令行使用说明。
    """
    return f"""{_usage(argv0)}

Options:
  --json              Output in JSON format
  --dry-run           Compute feature name and paths without creating directories or files
  --allow-existing-branch  Reuse an existing feature directory if it already exists
  --short-name <name> Provide a custom short name (2-4 words) for the feature
  --number N          Prefer a feature number (auto-corrected if its specs prefix exists)
  --timestamp         Use timestamp prefix (YYYYMMDD-HHMMSS) instead of sequential numbering
  --help, -h          Show this help message

Examples:
  {argv0} 'Add user authentication system' --short-name 'user-auth'
  {argv0} 'Implement OAuth2 integration for API' --number 5
  {argv0} --timestamp --short-name 'user-auth' 'Add user authentication'
"""


@dataclass(frozen=True)
class Args:
    json_mode: bool = False
    dry_run: bool = False
    allow_existing: bool = False
    short_name: str = ""
    branch_number: str = ""
    use_timestamp: bool = False
    description: str = ""


def _parse_args(argv: list[str], argv0: str) -> Args:
    """:param argv: 当前命令行参数。
    :param argv0: 当前脚本名称。
    :returns: 通过必要选项检查的功能创建参数。
    """
    json_mode = False
    dry_run = False
    allow_existing = False
    short_name = ""
    branch_number = ""
    use_timestamp = False
    rest: list[str] = []

    i = 0
    while i < len(argv):
        arg = argv[i]
        if arg == "--json":
            json_mode = True
        elif arg == "--dry-run":
            dry_run = True
        elif arg == "--allow-existing-branch":
            allow_existing = True
        elif arg in {"--short-name", "--number"}:
            if i + 1 >= len(argv) or argv[i + 1].startswith("--"):
                print(f"Error: {arg} requires a value", file=sys.stderr)
                raise SystemExit(1)
            i += 1
            if arg == "--short-name":
                short_name = argv[i]
            else:
                branch_number = argv[i]
        elif arg == "--timestamp":
            use_timestamp = True
        elif arg in {"--help", "-h"}:
            sys.stdout.write(_help_text(argv0))
            raise SystemExit(0)
        else:
            rest.append(arg)
        i += 1

    description = " ".join(rest).strip()
    if not description:
        if rest:
            print(
                "Error: Feature description cannot be empty or contain only whitespace",
                file=sys.stderr,
            )
        else:
            print(_usage(argv0), file=sys.stderr)
        raise SystemExit(1)

    return Args(
        json_mode=json_mode,
        dry_run=dry_run,
        allow_existing=allow_existing,
        short_name=short_name,
        branch_number=branch_number,
        use_timestamp=use_timestamp,
        description=description,
    )


def _clean_branch_name(name: str) -> str:
    """:param name: 功能名称文本。
    :returns: 由小写字母、数字和连接符组成的名称。
    """
    cleaned = re.sub(r"[^a-z0-9]", "-", name.lower())
    cleaned = re.sub(r"-+", "-", cleaned)
    return cleaned.strip("-")


def _generate_branch_name(description: str) -> str:
    """:param description: 用户提供的功能描述。
    :returns: 从有效关键词生成的功能名称。
    """
    clean = re.sub(r"[^a-z0-9]", " ", description.lower())
    meaningful: list[str] = []
    for word in clean.split():
        if word in _STOP_WORDS:
            continue
        if len(word) >= 3:
            meaningful.append(word)
        # 原文中的大写缩写保留为有效关键词。
        elif re.search(
            rf"(?<![0-9A-Za-z_]){re.escape(word.upper())}(?![0-9A-Za-z_])",
            description,
        ):
            meaningful.append(word)

    if meaningful:
        max_words = 4 if len(meaningful) == 4 else 3
        return "-".join(meaningful[:max_words])

    cleaned = _clean_branch_name(description)
    return "-".join([part for part in cleaned.split("-") if part][:3])


def _get_highest_from_specs(specs_dir: Path) -> int:
    """:param specs_dir: 当前功能规范根目录。
    :returns: 已有顺序编号中的最大有效值。
    """
    highest = 0
    if not specs_dir.is_dir():
        return highest
    for entry in specs_dir.iterdir():
        if not entry.is_dir():
            continue
        name = entry.name
        # 读取至少三位数字的顺序编号，忽略时间戳目录。
        if re.match(r"^[0-9]{3,}-", name) and not re.match(
            r"^[0-9]{8}-[0-9]{6}-", name
        ):
            number = _int64_from_digits(re.match(r"^[0-9]+", name).group())
            if number is not None:
                highest = max(highest, number)
    return highest


def _fit_branch_name(feature_num: str, branch_suffix: str) -> str:
    """:param feature_num: 功能编号。
    :param branch_suffix: 功能名称部分。
    :returns: 长度符合限制的完整功能标识。
    """
    branch_name = f"{feature_num}-{branch_suffix}"
    if len(branch_name) <= _MAX_BRANCH_LENGTH:
        return branch_name

    max_suffix_length = _MAX_BRANCH_LENGTH - (len(feature_num) + 1)
    truncated_suffix = re.sub(r"-$", "", branch_suffix[:max_suffix_length])
    return f"{feature_num}-{truncated_suffix}"


def _spec_prefix_exists(specs_dir: Path, feature_num: str) -> bool:
    """:param specs_dir: 当前功能规范根目录。
    :param feature_num: 待检查编号。
    :returns: 编号已经用于功能目录时返回 True。
    """
    if not specs_dir.exists():
        return False
    return any(
        entry.is_dir() and entry.name.startswith(f"{feature_num}-")
        for entry in specs_dir.iterdir()
    )


def _has_spec_prefix_conflict(
    specs_dir: Path,
    feature_num: str,
    requested_dir: Path,
    *,
    allow_existing: bool,
) -> bool:
    """:param specs_dir: 当前功能规范根目录。
    :param feature_num: 待使用编号。
    :param requested_dir: 当前请求的完整功能目录。
    :param allow_existing: 是否允许复用同一现有功能目录。
    :returns: 编号存在冲突时返回 True。
    """
    if allow_existing and requested_dir.is_dir():
        return False

    return _spec_prefix_exists(specs_dir, feature_num)


def main(argv: list[str] | None = None) -> int:
    """:param argv: 命令行参数；省略时读取进程参数。
    :returns: 功能创建或只读计算成功时返回零。
    """
    argv0 = sys.argv[0]
    args = _parse_args(list(argv if argv is not None else sys.argv[1:]), argv0)

    repo_root = get_repo_root(Path(__file__))
    specs_dir = repo_root / "specs"
    if not args.dry_run:
        specs_dir.mkdir(parents=True, exist_ok=True)

    if args.short_name:
        branch_suffix = _clean_branch_name(args.short_name)
    else:
        branch_suffix = _generate_branch_name(args.description)

    branch_number = args.branch_number
    if args.use_timestamp and branch_number:
        print(
            "[specify] Warning: --number is ignored when --timestamp is used",
            file=sys.stderr,
        )
        branch_number = ""

    if args.use_timestamp:
        feature_num = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    else:
        if branch_number:
            # 顺序编号只接受无符号十进制数字。
            if not re.fullmatch(r"[0-9]+", branch_number):
                print(
                    "Error: --number must be an unsigned integer, "
                    f"got '{branch_number}'",
                    file=sys.stderr,
                )
                return 1
            number = _int64_from_digits(branch_number)
            if number is None:
                print(
                    "Error: --number must be between 0 and "
                    f"{_MAX_FEATURE_NUMBER}, got '{branch_number}'",
                    file=sys.stderr,
                )
                return 1
        else:
            number = _get_highest_from_specs(specs_dir) + 1
        if number > _MAX_FEATURE_NUMBER:
            rejected_number = branch_number or str(number)
            number_label = "--number" if branch_number else "feature number"
            print(
                f"Error: {number_label} must be between 0 and "
                f"{_MAX_FEATURE_NUMBER}, got '{rejected_number}'",
                file=sys.stderr,
            )
            return 1
        feature_num = f"{number:03d}"

        # 明确指定的编号发生冲突时，选择下一个可用编号。
        if branch_number:
            requested_branch_name = _fit_branch_name(feature_num, branch_suffix)
            requested_dir = specs_dir / requested_branch_name
            spec_conflict = _has_spec_prefix_conflict(
                specs_dir,
                feature_num,
                requested_dir,
                allow_existing=args.allow_existing,
            )
            if spec_conflict:
                requested_num = feature_num
                number = _get_highest_from_specs(specs_dir)
                while True:
                    number += 1
                    if number > _MAX_FEATURE_NUMBER:
                        print(
                            f"Error: feature number must be between 0 and "
                            f"{_MAX_FEATURE_NUMBER}, got '{number}'",
                            file=sys.stderr,
                        )
                        return 1
                    feature_num = f"{number:03d}"
                    if not _spec_prefix_exists(specs_dir, feature_num):
                        break
                print(
                    f"[specify] Warning: --number {requested_num} conflicts with "
                    f"an existing spec directory; using {feature_num} instead",
                    file=sys.stderr,
                )

    max_suffix_length = _MAX_BRANCH_LENGTH - (len(feature_num) + 1)
    if max_suffix_length <= 0:
        print("Error: feature number is too long for a branch name", file=sys.stderr)
        return 1

    original_branch_name = f"{feature_num}-{branch_suffix}"
    branch_name = _fit_branch_name(feature_num, branch_suffix)

    # GitHub 的分支名称长度上限为 244 字节。
    if branch_name != original_branch_name:
        print(
            "[specify] Warning: Branch name exceeded GitHub's 244-byte limit",
            file=sys.stderr,
        )
        print(
            f"[specify] Original: {original_branch_name} "
            f"({len(original_branch_name)} bytes)",
            file=sys.stderr,
        )
        print(
            f"[specify] Truncated to: {branch_name} ({len(branch_name)} bytes)",
            file=sys.stderr,
        )

    feature_dir = specs_dir / branch_name
    spec_file = feature_dir / "spec.md"

    if not args.dry_run:
        if feature_dir.is_dir() and not args.allow_existing:
            if args.use_timestamp:
                print(
                    f"Error: Feature directory '{feature_dir}' already exists. "
                    "Rerun to get a new timestamp or use a different --short-name.",
                    file=sys.stderr,
                )
            else:
                print(
                    f"Error: Feature directory '{feature_dir}' already exists. "
                    "Please use a different feature name or specify a different "
                    "number with --number.",
                    file=sys.stderr,
                )
            return 1

        template_content = None
        needs_spec = not spec_file.is_file()
        if needs_spec:
            try:
                template_content = resolve_template_content(
                    "spec-template", repo_root
                )
            except TemplateResolutionError as exc:
                print(f"Error: {exc}", file=sys.stderr)
                return 1
            if template_content is None:
                print("ERROR: Required spec-template was not found", file=sys.stderr)
                return 1

        feature_dir.mkdir(parents=True, exist_ok=True)

        if needs_spec:
            spec_file.write_bytes(template_content.encode("utf-8"))

        # 保存当前功能目录，供后续工具读取。
        persist_feature_json(repo_root, f"specs/{branch_name}")

        # 显示当前终端中设置功能变量的命令。
        feature_assignment, directory_assignment = _persistence_assignments(
            branch_name,
            str(feature_dir),
            powershell=sys.platform == "win32",
        )
        print(f"# To persist: {feature_assignment}", file=sys.stderr)
        print(f"#              {directory_assignment}", file=sys.stderr)

    if args.json_mode:
        payload: dict[str, object] = {
            "BRANCH_NAME": branch_name,
            "SPEC_FILE": str(spec_file),
            "FEATURE_NUM": feature_num,
        }
        if args.dry_run:
            payload["DRY_RUN"] = True
        sys.stdout.write(_json_line(payload))
    else:
        print(f"BRANCH_NAME: {branch_name}")
        print(f"SPEC_FILE: {spec_file}")
        print(f"FEATURE_NUM: {feature_num}")
        if not args.dry_run:
            print(f"# To persist in your shell: {feature_assignment}")
            print(f"#                           {directory_assignment}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
