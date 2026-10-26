#!/usr/bin/env python3
"""生成 base 内容包的语言文件 client/lang/<code>.json。

输入：
  scripts/dev/lang/ui.json                    界面文字主表 { key: { "zh-Hans": "...", "en": "..." } }
  mods/base/assets/original/stringtable_cn.ini 原版字符串表（简体）
输出（mods/base 与 data/client 各一份，data/ 存在才写）：
  zh-Hans.json  界面文字（简体）
  zh-Hant.json  界面文字 + 原版字符串表整表（由简体经 OpenCC s2twp 转换，需人工校对）
  en.json       界面文字（英文）；游戏内容暂回退到简体

依赖：pip install opencc-python-reimplemented
用法：python3 scripts/dev/gen-lang.py
"""
import json
import pathlib
import sys

try:
    from opencc import OpenCC
except ImportError:
    sys.exit("缺少依赖：pip install opencc-python-reimplemented")

root = pathlib.Path(__file__).resolve().parents[2]
ui = json.loads((root / "scripts/dev/lang/ui.json").read_text(encoding="utf-8"))
ini = root / "mods/base/assets/original/stringtable_cn.ini"
s2t = OpenCC("s2twp")


def legacy_strings():
    table = {}
    for line in ini.read_text(encoding="utf-8").splitlines():
        sep = line.find("=")
        if sep > 0 and not line.lstrip().startswith(";"):
            table[line[:sep].strip()] = line[sep + 1:].strip()
    return table


def write(code, strings):
    body = json.dumps({"schema": 1, "strings": strings}, ensure_ascii=False, indent=1) + "\n"
    for base in (root / "mods/base/assets/client", root / "data/client"):
        if base.parent.exists():
            (base / "lang").mkdir(parents=True, exist_ok=True)
            (base / "lang" / f"{code}.json").write_text(body, encoding="utf-8")
    print(f"{code}: {len(strings)} strings")


hans = {k: v["zh-Hans"] for k, v in ui.items()}
write("zh-Hans", hans)
write("en", {k: v["en"] for k, v in ui.items() if v.get("en")})
hant = {k: s2t.convert(v) for k, v in legacy_strings().items()}
hant.update({k: s2t.convert(v) for k, v in hans.items()})
write("zh-Hant", hant)
