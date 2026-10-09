#!/usr/bin/env python3
"""Checks that every file and folder under Assets/ was committed with its .meta file.

Unity gives every asset a .meta file holding its ID (GUID) and import settings.
If an asset is committed without its .meta, every other computer gives it a new
ID, and scenes and prefabs that use it break ("Missing script", pink sprites).

- Missing .meta file: error (fails the GitHub check).
- .meta file whose asset is missing: warning. Usually an empty folder (Git
  doesn't store empty folders) or a file that was deleted without its .meta.

Usage: python3 tools/check_meta.py [project folder]   (default: current folder)
"""
import os
import sys


def ignored_by_unity(name):
    """Unity skips these names entirely, and they get no .meta file."""
    lower = name.lower()
    return (
        name.startswith(".")
        or name.endswith("~")
        or lower == "cvs"
        or lower.endswith(".tmp")
    )


def check(project_root):
    assets = os.path.join(project_root, "Assets")
    if not os.path.isdir(assets):
        print("No Assets folder yet, nothing to check.")
        return 0

    missing_meta = []
    orphan_meta = []

    for folder, subfolders, files in os.walk(assets):
        subfolders[:] = sorted(d for d in subfolders if not ignored_by_unity(d))
        names = set(subfolders) | {f for f in files if not ignored_by_unity(f)}

        for name in sorted(names):
            if name.endswith(".meta"):
                if name[: -len(".meta")] not in names:
                    orphan_meta.append(os.path.join(folder, name))
            elif name + ".meta" not in names:
                missing_meta.append(os.path.join(folder, name))

    def show(path):
        return os.path.relpath(path, project_root).replace(os.sep, "/")

    for path in orphan_meta:
        print(f"::warning file={show(path)}::{show(path)} has no matching asset "
              "(an empty folder, or a deleted file whose .meta was left behind).")
    for path in missing_meta:
        print(f"::error file={show(path)}::{show(path)} was committed without its .meta file. "
              "Open the project in Unity, then commit the .meta file Unity creates next to it.")

    if missing_meta:
        print(f"{len(missing_meta)} asset(s) are missing their .meta file.")
        return 1
    print(f"All assets have .meta files ({len(orphan_meta)} warning(s)).")
    return 0


if __name__ == "__main__":
    sys.exit(check(sys.argv[1] if len(sys.argv) > 1 else "."))
