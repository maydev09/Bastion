#!/usr/bin/env python3
"""Prints "<module>\t<path>" for every Modules.<module>.RawCaptures.Path in the app's configuration.

Usage: raw-capture-paths.py <content root> <toml file>...
The files are merged in the given order, later ones win, as ConfigurationSources.cs does; missing
files are skipped. Keys ignore case, as in .NET configuration; a module keeps the spelling of its
first appearance. A relative path is resolved against the content root, as the app does.
Environment variable overrides are not seen.
"""
import os
import sys
import tomllib


def key_in(table: dict, key: str) -> str:
    return next((k for k in table if k.lower() == key.lower()), key)


def get(table: dict, key: str):
    return table.get(key_in(table, key))


def merge(target: dict, source: dict) -> None:
    for key, value in source.items():
        existing = key_in(target, key)
        if isinstance(value, dict) and isinstance(target.get(existing), dict):
            merge(target[existing], value)
        else:
            target[existing] = value


root, *files = sys.argv[1:]
config: dict = {}
for file in files:
    if os.path.exists(file):
        with open(file, "rb") as f:
            merge(config, tomllib.load(f))

for module, section in (get(config, "Modules") or {}).items():
    path = get(get(section, "RawCaptures") or {}, "Path")
    if path:
        print(f"{module}\t{os.path.normpath(os.path.join(root, path))}")
