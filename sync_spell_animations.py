#!/usr/bin/env python3
"""Sync exported spell animations into the mod assets.

Usage:
    python sync_spell_animations.py
    python sync_spell_animations.py "C:/Users/PC/Documents/VintageStoryModels/Exports/spellsandrunesAnims.json"

The script expects the export json to contain an "animations" array. Animations
are grouped by their code prefix, e.g. "fire_blast" goes into fire.json.
"""

from __future__ import annotations

import argparse
import json
from copy import deepcopy
from pathlib import Path


ROOT = Path(__file__).resolve().parent
DEFAULT_EXPORT = (
    Path.home()
    / "Documents"
    / "VintageStoryModels"
    / "Exports"
    / "spellsandrunesAnims.json"
)

ANIMATIONS_DIR = ROOT / "assets" / "spellsandrunes" / "animations"
PLAYER_PATCH = ROOT / "assets" / "spellsandrunes" / "patches" / "player-shape-animations.json"
SHAPE_FILE = "game:shapes/entity/humanoid/seraph-faceless.json"
SHAPE_ANIMATION_PATCH_PATH = "/animations/-"

# Exported animations sometimes come out with generic Hold values. The existing
# spell animation setup uses Stop for one-shots and Repeat for held/channel
# animations.
ANIMATION_END_OVERRIDES = {
    "air_wind_vortex": "Repeat",
    "fire_cook_in_hand": "Repeat",
    "fire_flamethrower": "Repeat",
    "fire_jet_flight": "Repeat",
}


def load_json(path: Path) -> object:
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def save_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def normalize_animation(animation: dict) -> dict:
    normalized = deepcopy(animation)
    code = normalized.get("code")
    normalized["onAnimationEnd"] = ANIMATION_END_OVERRIDES.get(code, "Stop")
    return normalized


def element_for(animation: dict) -> str | None:
    code = animation.get("code")
    if not isinstance(code, str) or "_" not in code:
        return None
    return code.split("_", 1)[0].lower()


def upsert_animations(existing: list[dict], exported: list[dict]) -> list[dict]:
    by_code = {
        anim.get("code"): index
        for index, anim in enumerate(existing)
        if isinstance(anim, dict) and isinstance(anim.get("code"), str)
    }
    result = list(existing)

    for animation in exported:
        code = animation.get("code")
        if not isinstance(code, str):
            continue

        normalized = normalize_animation(animation)
        if code in by_code:
            result[by_code[code]] = normalized
        else:
            by_code[code] = len(result)
            result.append(normalized)

    return result


def sync_animation_files(exported_animations: list[dict], dry_run: bool) -> dict[str, list[dict]]:
    grouped: dict[str, list[dict]] = {}
    for animation in exported_animations:
        element = element_for(animation)
        if element is None:
            print(f"Skipping animation without element prefix: {animation.get('code')!r}")
            continue
        grouped.setdefault(element, []).append(animation)

    synced: dict[str, list[dict]] = {}
    for element, animations in sorted(grouped.items()):
        path = ANIMATIONS_DIR / f"{element}.json"
        if path.exists():
            data = load_json(path)
            if not isinstance(data, dict):
                raise ValueError(f"{path} must contain a json object")
            existing = data.get("animations", [])
            if not isinstance(existing, list):
                raise ValueError(f"{path} must contain an animations array")
        else:
            data = {"animations": []}
            existing = []

        merged = upsert_animations(existing, animations)
        data["animations"] = merged
        synced[element] = merged

        if dry_run:
            print(f"Would sync {len(animations)} exported animations into {path}")
        else:
            save_json(path, data)
            print(f"Synced {len(animations)} exported animations into {path}")

    return synced


def regenerate_player_patch(synced: dict[str, list[dict]], dry_run: bool) -> None:
    patches = []
    for element in sorted(synced):
        for animation in synced[element]:
            patches.append(
                {
                    "file": SHAPE_FILE,
                    "op": "add",
                    "path": SHAPE_ANIMATION_PATCH_PATH,
                    "value": normalize_animation(animation),
                }
            )

    if dry_run:
        print(f"Would write {len(patches)} seraph shape animation patches to {PLAYER_PATCH}")
    else:
        save_json(PLAYER_PATCH, patches)
        print(f"Wrote {len(patches)} seraph shape animation patches to {PLAYER_PATCH}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Sync Spells & Runes animation exports.")
    parser.add_argument(
        "export",
        nargs="?",
        type=Path,
        default=DEFAULT_EXPORT,
        help=f"Path to exported spellsandrunesAnims.json. Default: {DEFAULT_EXPORT}",
    )
    parser.add_argument("--dry-run", action="store_true", help="Print actions without writing files.")
    args = parser.parse_args()

    export_path = args.export.expanduser().resolve()
    data = load_json(export_path)
    if not isinstance(data, dict) or not isinstance(data.get("animations"), list):
        raise ValueError(f"{export_path} must contain an animations array")

    exported_animations = [anim for anim in data["animations"] if isinstance(anim, dict)]
    synced = sync_animation_files(exported_animations, args.dry_run)
    regenerate_player_patch(synced, args.dry_run)


if __name__ == "__main__":
    main()
