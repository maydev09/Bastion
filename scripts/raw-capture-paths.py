#!/usr/bin/env python3
"""Prints "<module>\t<path>" for every Modules.<module>.RawCaptures.Path in the app's configuration.

Usage: raw-capture-paths.py <content root> <toml file>...
The files are merged in the given order, later ones win, as ConfigurationSources.cs does; missing
files are skipped. A relative path is resolved against the content root, as the app does.
Environment variable overrides are not seen.
"""
import os
import sys
import tomllib


def merge(target: dict, source: dict) -> None:
    for key, value in source.items():
        if isinstance(value, dict) and isinstance(target.get(key), dict):
            merge(target[key], value)
        else:
            target[key] = value


root, *files = sys.argv[1:]
config: dict = {}
for file in files:
    if os.path.exists(file):
        with open(file, "rb") as f:
            merge(config, tomllib.load(f))

for module, section in config.get("Modules", {}).items():
    path = section.get("RawCaptures", {}).get("Path")
    if path:
        print(f"{module}\t{os.path.normpath(os.path.join(root, path))}")
