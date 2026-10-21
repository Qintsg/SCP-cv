# 提供 Spec Kit 功能目录、配置及模板读取工具。
# 项目：SCP-cv
# 文件：common.py
# 作者：Qintsg
# 日期：2026-10-21
# /// script
# dependencies = ["PyYAML==6.0.3"]
# ///

from __future__ import annotations

import json
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path

import yaml


def _trim_trailing_separators(value: Path) -> str:
    """:param value: 待规范化路径。
    :returns: 保留根目录并移除末尾分隔符的路径文本。
    """
    text = str(value)
    while len(text) > 1 and text.endswith((os.sep, "/")):
        text = text[:-1]
    return text


def find_specify_root(start_dir: Path | None = None) -> Path | None:
    """:param start_dir: 向上查找的起始目录；省略时使用当前目录。
    :returns: 包含 .specify 的项目目录；未找到时返回 None。
    """
    current = (start_dir or Path.cwd()).resolve()
    while True:
        if (current / ".specify").is_dir():
            return current
        parent = current.parent
        if parent == current:
            return None
        current = parent


def resolve_specify_init_dir() -> Path:
    """:returns: SPECIFY_INIT_DIR 指定且通过验证的项目目录。
    """
    raw = os.environ.get("SPECIFY_INIT_DIR", "")
    candidate = Path(raw)
    if not candidate.is_absolute():
        candidate = Path.cwd() / candidate
    try:
        init_root = candidate.resolve(strict=True)
    except OSError:
        print(
            f"ERROR: SPECIFY_INIT_DIR does not point to an existing directory: {raw}",
            file=sys.stderr,
        )
        raise SystemExit(1)
    if not init_root.is_dir():
        print(
            f"ERROR: SPECIFY_INIT_DIR does not point to an existing directory: {raw}",
            file=sys.stderr,
        )
        raise SystemExit(1)
    if not (init_root / ".specify").is_dir():
        print(
            "ERROR: SPECIFY_INIT_DIR is not a Spec Kit project "
            f"(no .specify/ directory): {init_root}",
            file=sys.stderr,
        )
        raise SystemExit(1)
    return init_root


def get_repo_root(script_file: Path | None = None) -> Path:
    """:param script_file: 当前工具文件，用于定位所属项目。
    :returns: 环境变量或实际目录指定的项目根目录。
    """
    if os.environ.get("SPECIFY_INIT_DIR"):
        return resolve_specify_init_dir()

    specify_root = find_specify_root()
    if specify_root is not None:
        return specify_root

    if script_file is not None:
        script_root = find_specify_root(script_file.resolve().parent)
        if script_root is not None:
            return script_root

        # 安装后的工具位于 .specify/scripts/python/<script>.py。
        return script_file.resolve().parents[3]
    return Path.cwd().resolve()


def get_current_branch() -> str:
    """:returns: 环境变量中明确指定的功能标识。
    """
    return os.environ.get("SPECIFY_FEATURE", "")


def _read_json_mapping(path: Path) -> dict[str, object]:
    """读取必须使用对象根节点的现有 JSON 配置。

    :param path: 明确存在的配置文件。
    :returns: 通过根节点类型检查的配置对象。
    :raises TemplateResolutionError: 配置路径或根节点类型无效。
    """
    if not path.is_file():
        raise TemplateResolutionError(f"配置路径必须是常规文件：{path}")
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise TemplateResolutionError(f"配置根节点必须是对象：{path}")
    return value


def read_feature_json_feature_directory(repo_root: Path) -> str:
    """读取当前功能指针并拒绝畸形配置。

    :param repo_root: Spec Kit 项目目录。
    :returns: 当前功能目录；可选指针文件不存在时返回空字符串。
    :raises TemplateResolutionError: 已有指针内容不符合配置要求。
    """
    feature_json = repo_root / ".specify" / "feature.json"
    if not os.path.lexists(feature_json):
        return ""
    value = _read_json_mapping(feature_json).get("feature_directory")
    if not isinstance(value, str) or not value.strip():
        raise TemplateResolutionError(f"feature_directory 必须是非空字符串：{feature_json}")
    return value


def _json_dump(data: dict[str, str]) -> str:
    """:param data: 待保存的配置对象。
    :returns: 带换行符的 JSON 文本。
    """
    return json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n"


def persist_feature_json(repo_root: Path, feature_dir_value: str) -> None:
    """:param repo_root: 当前项目目录。
    :param feature_dir_value: 当前功能目录。
    :returns: 保存发生变化的本机功能指针。
    """
    # 保存项目内的相对路径，保留 specs 符号连接的项目语义。
    value = feature_dir_value
    relative = Path(value)
    if relative.is_absolute():
        try:
            value = relative.relative_to(repo_root).as_posix()
        except ValueError:
            value = str(relative)

    current = read_feature_json_feature_directory(repo_root)
    if current == value:
        return

    specify_dir = repo_root / ".specify"
    specify_dir.mkdir(parents=True, exist_ok=True)
    (specify_dir / "feature.json").write_bytes(
        _json_dump({"feature_directory": value}).encode("utf-8")
    )


@dataclass(frozen=True)
class FeaturePaths:
    repo_root: Path
    current_branch: str
    feature_dir: Path
    feature_spec: Path
    impl_plan: Path
    tasks: Path
    research: Path
    data_model: Path
    quickstart: Path
    contracts_dir: Path


def get_feature_paths(
    *, no_persist: bool = False, script_file: Path | None = None
) -> FeaturePaths:
    """:param no_persist: 是否保持当前功能指针不变。
    :param script_file: 当前工具文件，用于定位所属项目。
    :returns: 当前功能全部设计文件的路径。
    """
    repo_root = get_repo_root(script_file)
    current_branch = get_current_branch()

    feature_dir_raw = os.environ.get("SPECIFY_FEATURE_DIRECTORY", "")
    if feature_dir_raw:
        feature_dir = Path(feature_dir_raw)
        if not feature_dir.is_absolute():
            feature_dir = repo_root / feature_dir
        if not no_persist:
            persist_feature_json(repo_root, feature_dir_raw)
    elif (repo_root / ".specify" / "feature.json").is_file():
        stored = read_feature_json_feature_directory(repo_root)
        if not stored:
            print(
                "ERROR: Feature directory not found. Set SPECIFY_FEATURE_DIRECTORY "
                "or ensure .specify/feature.json contains feature_directory.",
                file=sys.stderr,
            )
            raise SystemExit(1)
        feature_dir = Path(stored)
        if not feature_dir.is_absolute():
            feature_dir = repo_root / feature_dir
    else:
        print(
            "ERROR: Feature directory not found. Set SPECIFY_FEATURE_DIRECTORY "
            "or run the specify command to create .specify/feature.json.",
            file=sys.stderr,
        )
        raise SystemExit(1)

    if not current_branch:
        current_branch = Path(_trim_trailing_separators(feature_dir)).name

    return FeaturePaths(
        repo_root=repo_root,
        current_branch=current_branch,
        feature_dir=feature_dir,
        feature_spec=feature_dir / "spec.md",
        impl_plan=feature_dir / "plan.md",
        tasks=feature_dir / "tasks.md",
        research=feature_dir / "research.md",
        data_model=feature_dir / "data-model.md",
        quickstart=feature_dir / "quickstart.md",
        contracts_dir=feature_dir / "contracts",
    )


_SAFE_COMPONENT_PATTERN = re.compile(r"[a-z0-9-]+")


def _is_safe_component(value: object) -> bool:
    """:param value: 待检查的目录或模板标识。
    :returns: 标识仅包含受支持字符时返回 True。
    """
    return (
        isinstance(value, str)
        and _SAFE_COMPONENT_PATTERN.fullmatch(value) is not None
    )


def _normalize_priority(value: object) -> int:
    """检查显式配置的模板优先级。

    :param value: 配置中的优先级。
    :returns: 大于零的整数优先级。
    :raises TemplateResolutionError: 优先级类型或范围无效。
    """
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        raise TemplateResolutionError("配置 priority 必须是大于零的整数")
    return value


def _sorted_preset_ids(presets_dir: Path) -> list[str]:
    """按照已验证的配置读取预设顺序。

    :param presets_dir: 可选的预设目录。
    :returns: 启用预设的标识列表。
    :raises TemplateResolutionError: 已有配置的结构、标识或优先级无效。
    """
    registry = presets_dir / ".registry"
    if os.path.lexists(registry):
        presets = _read_json_mapping(registry).get("presets", {})
        if not isinstance(presets, dict):
            raise TemplateResolutionError(f"presets 必须是对象：{registry}")
        ranked: list[tuple[int, str]] = []
        for preset_id, metadata in presets.items():
            if not _is_safe_component(preset_id) or not isinstance(metadata, dict):
                raise TemplateResolutionError(f"预设标识或配置对象无效：{registry}")
            enabled = metadata.get("enabled", True)
            if not isinstance(enabled, bool):
                raise TemplateResolutionError(f"enabled 必须是布尔值：{registry}")
            priority = _normalize_priority(metadata.get("priority", 10))
            if enabled:
                ranked.append((priority, preset_id))
        return [preset_id for _, preset_id in sorted(ranked)]
    if not os.path.lexists(presets_dir):
        return []
    return sorted(
        path.name
        for path in presets_dir.iterdir()
        if path.is_dir() and _is_safe_component(path.name)
    )


def _sorted_extension_ids(extensions_dir: Path) -> list[str]:
    """按照注册信息和目录读取启用扩展。

    :param extensions_dir: 可选的扩展目录。
    :returns: 按照优先级排序的扩展标识。
    :raises TemplateResolutionError: 已有扩展配置无效。
    """
    registry = extensions_dir / ".registry"
    registered_ids: set[str] = set()
    extensions: dict[object, object] = {}
    if os.path.lexists(registry):
        if not registry.is_file():
            raise TemplateResolutionError(
                f"Invalid extension registry {registry}: not a regular file"
            )
        data = _read_json_mapping(registry)
        raw_extensions = data.get("extensions", {})
        if not isinstance(raw_extensions, dict):
            raise TemplateResolutionError(
                f"Invalid extension registry {registry}: "
                "'extensions' must be a mapping"
            )
        extensions = raw_extensions
        registered_ids = {
            ext_id for ext_id in extensions if isinstance(ext_id, str)
        }

    ranked: list[tuple[int, str]] = []
    for ext_id, metadata in extensions.items():
        if not _is_safe_component(ext_id) or not isinstance(metadata, dict):
            raise TemplateResolutionError(f"扩展标识或配置对象无效：{registry}")
        enabled = metadata.get("enabled", True)
        if not isinstance(enabled, bool):
            raise TemplateResolutionError(f"enabled 必须是布尔值：{registry}")
        priority = _normalize_priority(metadata.get("priority", 10))
        if enabled:
            ranked.append((priority, ext_id))

    if os.path.lexists(extensions_dir):
        ranked.extend(
            (10, path.name)
            for path in extensions_dir.iterdir()
            if (
                path.is_dir()
                and _is_safe_component(path.name)
                and path.name not in registered_ids
            )
        )
    return [ext_id for _, ext_id in sorted(ranked)]


def _conventional_template(
    base_dir: Path, template_name: str
) -> Path | None:
    """:param base_dir: 模板所属目录。
    :param template_name: 模板名称。
    :returns: 实际存在的常规模板路径；未找到时返回 None。
    """
    for candidate in (
        base_dir / "templates" / f"{template_name}.md",
        base_dir / f"{template_name}.md",
    ):
        if candidate.is_file():
            return candidate
    return None


def resolve_template(template_name: str, repo_root: Path) -> Path | None:
    """按照项目覆盖、预设、扩展和内置模板的顺序查找文件。

    :param template_name: 模板名称。
    :param repo_root: 当前项目目录。
    :returns: 最高优先级的现有模板路径；未找到时返回 None。
    """
    if not _is_safe_component(template_name):
        return None

    base = repo_root / ".specify" / "templates"

    override = base / "overrides" / f"{template_name}.md"
    if override.is_file():
        return override

    presets_dir = repo_root / ".specify" / "presets"
    if presets_dir.is_dir():
        for preset_id in _sorted_preset_ids(presets_dir):
            candidate = _conventional_template(
                presets_dir / preset_id, template_name
            )
            if candidate is not None:
                return candidate

    ext_dir = repo_root / ".specify" / "extensions"
    if ext_dir.is_dir():
        for extension_id in _sorted_extension_ids(ext_dir):
            ext = ext_dir / extension_id
            candidate = _conventional_template(ext, template_name)
            if candidate is not None:
                return candidate

    core = base / f"{template_name}.md"
    if core.is_file():
        return core
    return None


class TemplateResolutionError(RuntimeError):
    """配置内容或模板组合无效。"""


# 按照 PresetManifest 合同检查模板声明的类型及组合策略。
_VALID_TEMPLATE_TYPES = ("template", "command", "script")
_VALID_TEMPLATE_STRATEGIES = ("replace", "prepend", "append", "wrap")
_VALID_SCRIPT_STRATEGIES = ("replace", "wrap")


def _validate_manifest_template_entry(entry: object) -> None:
    """:param entry: 清单中的模板声明。
    :returns: 声明符合类型、路径字段及组合策略要求时正常返回。
    :raises ValueError: 模板声明类型或策略无效。
    """
    if not isinstance(entry, dict):
        raise ValueError("manifest template entries must be mappings")
    if "type" not in entry or "name" not in entry or "file" not in entry:
        raise ValueError("manifest template entry missing type, name, or file")
    for field in ("type", "name", "file"):
        if not isinstance(entry[field], str):
            raise ValueError(f"manifest template {field} must be a string")
    if entry["type"] not in _VALID_TEMPLATE_TYPES:
        raise ValueError(f"invalid manifest template type '{entry['type']}'")
    strategy = entry.get("strategy", "replace")
    if not isinstance(strategy, str):
        raise ValueError("manifest template strategy must be a string")
    strategy = strategy.lower()
    if strategy not in _VALID_TEMPLATE_STRATEGIES:
        raise ValueError(f"invalid manifest template strategy '{strategy}'")
    if entry["type"] == "script" and strategy not in _VALID_SCRIPT_STRATEGIES:
        raise ValueError(
            f"invalid manifest script strategy '{strategy}'"
        )


def _preset_template_layer(
    preset_dir: Path, template_name: str
) -> tuple[Path, str] | None:
    """读取预设清单明确指定的模板。

    :param preset_dir: 当前预设目录。
    :param template_name: 需要读取的模板名称。
    :returns: 模板路径和组合策略；没有对应模板声明时返回 None。
    :raises TemplateResolutionError: 现有清单或模板路径无效。
    """
    manifest_path = preset_dir / "preset.yml"
    conventional = _conventional_template(preset_dir, template_name)

    if os.path.lexists(manifest_path):
        if not manifest_path.is_file():
            raise TemplateResolutionError(f"预设清单必须是常规文件：{manifest_path}")
        try:
            manifest = yaml.safe_load(manifest_path.read_text(encoding="utf-8"))
            if not isinstance(manifest, dict):
                raise ValueError("manifest root must be a mapping")
            if "provides" not in manifest:
                raise ValueError("manifest missing provides section")
            provides = manifest["provides"]
            if not isinstance(provides, dict):
                raise ValueError("manifest provides must be a mapping")
            if "templates" not in provides:
                raise ValueError("manifest provides missing templates")
            templates = provides["templates"]
            if not isinstance(templates, list):
                raise ValueError("manifest templates must be a list")
            if not templates:
                raise ValueError("manifest must provide at least one template")
            for entry in templates:
                _validate_manifest_template_entry(entry)
            for entry in templates:
                if (
                    entry.get("name") != template_name
                    or entry.get("type", "template") != "template"
                ):
                    continue
                file_value = entry.get("file", "")
                strategy = entry.get("strategy", "replace")
                relative = Path(file_value)
                if (
                    not file_value.strip()
                    or relative.is_absolute()
                    or ".." in relative.parts
                ):
                    raise ValueError("manifest template file must be a non-empty relative path")
                candidate = preset_dir / relative
                if not candidate.is_file():
                    raise ValueError(f"manifest template file does not exist: {candidate}")
                return candidate, strategy.lower()
        except (OSError, UnicodeError, ValueError, yaml.YAMLError) as exc:
            raise TemplateResolutionError(
                f"Failed to parse preset manifest {manifest_path}: {exc}"
            ) from exc

    return (conventional, "replace") if conventional is not None else None


def resolve_template_content(template_name: str, repo_root: Path) -> str | None:
    """:param template_name: 模板名称。
    :param repo_root: 当前项目目录。
    :returns: 按配置组合的模板内容；没有对应模板时返回 None。
    :raises TemplateResolutionError: 已有清单或组合要求无效。
    """
    if not _is_safe_component(template_name):
        return None

    layers: list[tuple[Path, str]] = []

    def compose_from_base() -> str:
        """:returns: 从替换模板开始组合的完整模板内容。
        """
        try:
            content = layers[-1][0].read_bytes().decode("utf-8")
            for path, strategy in reversed(layers[:-1]):
                layer_content = path.read_bytes().decode("utf-8")
                if strategy == "prepend":
                    content = f"{layer_content}\n\n{content}"
                elif strategy == "append":
                    content = f"{content}\n\n{layer_content}"
                elif strategy == "wrap":
                    placeholder = "{CORE_TEMPLATE}"
                    if placeholder not in layer_content:
                        raise TemplateResolutionError(
                            f"Wrap layer {path} is missing {placeholder}"
                        )
                    content = layer_content.replace(placeholder, content)
                else:
                    raise TemplateResolutionError(
                        f"Unknown template composition strategy '{strategy}' in {path}"
                    )
        except (OSError, UnicodeError) as exc:
            raise TemplateResolutionError(
                f"Failed to read template layer for '{template_name}': {exc}"
            ) from exc
        return content

    override = (
        repo_root
        / ".specify"
        / "templates"
        / "overrides"
        / f"{template_name}.md"
    )
    if override.is_file():
        layers.append((override, "replace"))
        return compose_from_base()

    presets_dir = repo_root / ".specify" / "presets"
    for preset_id in _sorted_preset_ids(presets_dir):
        layer = _preset_template_layer(presets_dir / preset_id, template_name)
        if layer is not None:
            layers.append(layer)
            if layer[1] == "replace":
                return compose_from_base()

    extensions_dir = repo_root / ".specify" / "extensions"
    for extension_id in _sorted_extension_ids(extensions_dir):
        extension_dir = extensions_dir / extension_id
        candidate = _conventional_template(extension_dir, template_name)
        if candidate is not None:
            layers.append((candidate, "replace"))
            return compose_from_base()

    core = repo_root / ".specify" / "templates" / f"{template_name}.md"
    if core.is_file():
        layers.append((core, "replace"))
        return compose_from_base()

    if not layers:
        return None

    raise TemplateResolutionError(
        f"Template '{template_name}' has composing layers but no replace base"
    )


def get_invoke_separator(repo_root: Path) -> str:
    """读取集成命令的调用分隔符。

    :param repo_root: Spec Kit 项目目录。
    :returns: 配置的分隔符；未配置时使用标准句点。
    :raises TemplateResolutionError: 已有集成配置类型或分隔符无效。
    """
    integration_json = repo_root / ".specify" / "integration.json"
    if not os.path.lexists(integration_json):
        return "."
    state = _read_json_mapping(integration_json)
    key = state.get("default_integration", state.get("integration", ""))
    if not isinstance(key, str):
        raise TemplateResolutionError(f"integration 必须是字符串：{integration_json}")
    settings = state.get("integration_settings", {})
    if not isinstance(settings, dict):
        raise TemplateResolutionError(f"integration_settings 必须是对象：{integration_json}")
    entry = settings.get(key, {})
    if not isinstance(entry, dict):
        raise TemplateResolutionError(f"所选集成配置必须是对象：{integration_json}")
    separator = entry.get("invoke_separator", ".")
    if not isinstance(separator, str) or separator not in {".", "-"}:
        raise TemplateResolutionError(f"invoke_separator 必须为 . 或 -：{integration_json}")
    return separator


def format_speckit_command(command_name: str, repo_root: Path) -> str:
    """:param command_name: Spec Kit 命令名称。
    :param repo_root: 当前项目目录。
    :returns: 使用当前集成分隔符的完整调用名称。
    """
    separator = get_invoke_separator(repo_root)
    name = command_name.lstrip("/")
    if name.startswith("speckit."):
        name = name[len("speckit.") :]
    elif name.startswith("speckit-"):
        name = name[len("speckit-") :]
    name = name.replace(".", separator)
    return f"$speckit{separator}{name}"
