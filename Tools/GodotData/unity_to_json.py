"""Turns the Unity game's ScriptableObject data (Assets/Data/*.asset, Unity YAML) into JSON for
the 3D versions, so numbers live in one place and every version plays by the same rules.

    python Tools/GodotData/unity_to_json.py

Writes Godot/data/<category>.json and Unreal/Content/Data/<category>.json (the same files), each
{asset id: fields}. What changes on the way:
- Unity bookkeeping (m_ObjectHideFlags, m_Script, ...) is dropped; m_Name becomes "_name".
- [SerializeReference] blocks (an ability's effects, ...) are inlined with their class in "$type".
- A reference to another data asset becomes {"$ref": "<category>/<asset name>"}; to anything else
  (sprites, prefabs, audio) {"$asset": "<path under Assets>"}; an empty reference becomes null.
Enums stay as the numbers Unity stored; the Godot side keeps the matching enum tables.
"""
import json
import os
import re
import sys

import yaml

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")
DATA = os.path.join(ASSETS, "Data")
OUTS = [os.path.join(ROOT, "Godot", "data"), os.path.join(ROOT, "Unreal", "Content", "Data")]

# category -> folder or single asset under Assets/Data
CATEGORIES = {
    "abilities": "Abilities",
    "items": "Items",
    "classes": "Classes",
    "races": "Races",
    "quests": "Quests",
    "combat": "Combat.asset",
    "progression": "Progression.asset",
}

GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})", re.M)
# Unity writes small int arrays (enum lists) as hex: 4 little-endian bytes per number, then "i".
INT_ARRAY_RE = re.compile(r"^(?:[0-9a-f]{8})+i$")


def guid_map():
    """guid -> path relative to Assets, from every .meta file."""
    out = {}
    for base, _, files in os.walk(ASSETS):
        for f in files:
            if not f.endswith(".meta"):
                continue
            with open(os.path.join(base, f), encoding="utf-8", errors="replace") as fh:
                m = GUID_RE.search(fh.read(400))
            if m:
                rel = os.path.relpath(os.path.join(base, f[:-5]), ASSETS).replace("\\", "/")
                out[m.group(1)] = rel
    return out


def load_unity_yaml(path):
    with open(path, encoding="utf-8") as fh:
        text = fh.read()
    text = "\n".join(l for l in text.splitlines() if not l.startswith("%"))
    text = re.sub(r"^--- !u!\d+ &-?\d+.*$", "---", text, flags=re.M)
    docs = [d for d in yaml.safe_load_all(text) if d]
    for d in docs:
        if "MonoBehaviour" in d:
            return d["MonoBehaviour"]
    return None


def category_of(rel_path):
    """Assets-relative path of a data asset -> 'category/name', or None."""
    if not rel_path.startswith("Data/") or not rel_path.endswith(".asset"):
        return None
    inner = rel_path[len("Data/"):-len(".asset")]
    for cat, where in CATEGORIES.items():
        if where.endswith(".asset"):
            if inner == where[:-6]:
                return cat
        elif inner.startswith(where + "/"):
            return cat + "/" + os.path.basename(inner)
    return None


class Converter:
    def __init__(self):
        self.guids = guid_map()

    def ref(self, v):
        file_id = v.get("fileID", 0)
        if not file_id:
            return None
        guid = v.get("guid")
        if not guid:
            return {"$local": file_id}
        rel = self.guids.get(guid)
        if rel is None:
            return {"$missing": guid}
        cat = category_of(rel)
        if cat:
            return {"$ref": cat}
        return {"$asset": rel}

    def walk(self, v, refs):
        if isinstance(v, dict):
            if set(v.keys()) == {"rid"}:
                rid = v["rid"]
                if rid in refs:
                    return self.walk(refs[rid], refs)
                return None
            if "fileID" in v and set(v.keys()) <= {"fileID", "guid", "type"}:
                return self.ref(v)
            return {k: self.walk(x, refs) for k, x in v.items()}
        if isinstance(v, list):
            return [self.walk(x, refs) for x in v]
        if isinstance(v, str) and INT_ARRAY_RE.match(v):
            return [int.from_bytes(bytes.fromhex(v[i:i + 8]), "little", signed=True) for i in range(0, len(v) - 1, 8)]
        return v

    def convert(self, mono):
        refs = {}
        block = mono.get("references") or {}
        for r in block.get("RefIds") or []:
            t = r.get("type") or {}
            data = dict(r.get("data") or {})
            data["$type"] = t.get("class")
            refs[r["rid"]] = data
        out = {"_name": mono.get("m_Name")}
        for k, v in mono.items():
            if k.startswith("m_") or k == "references":
                continue
            out[k] = v
        return self.walk(out, refs)


def main():
    conv = Converter()
    for out in OUTS:
        os.makedirs(out, exist_ok=True)
    for cat, where in CATEGORIES.items():
        path = os.path.join(DATA, where)
        if where.endswith(".asset"):
            mono = load_unity_yaml(path)
            result = conv.convert(mono)
        else:
            result = {}
            for f in sorted(os.listdir(path)):
                if not f.endswith(".asset"):
                    continue
                mono = load_unity_yaml(os.path.join(path, f))
                if mono is None:
                    continue
                result[f[:-6]] = conv.convert(mono)
        count = len(result) if not where.endswith(".asset") else 1
        for out in OUTS:
            dst = os.path.join(out, cat + ".json")
            with open(dst, "w", encoding="utf-8", newline="\n") as fh:
                json.dump(result, fh, ensure_ascii=False, indent=1, sort_keys=False)
                fh.write("\n")
            print(f"{cat}: {count} -> {os.path.relpath(dst, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
