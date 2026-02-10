#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
讀取校準匯出檔（遊戲內按 F8 匯出的 calibration_anchor.json），
依「腳底對齊」公式計算每朝向的 Shadow/Clothes 微調，輸出 C# 程式碼。

用法:
  1. 遊戲內：旋轉角色至朝向 0,1,...,7，各按一次 F8（匯出至 user://calibration_anchor.json）
  2. 將該檔複製到專案目錄，或指定路徑
  3. 執行: python3 calibration_to_tweak.py [路徑]
  4. 將輸出的 C# switch 貼回 GameEntity.Visuals.cs 的 GetShadowClothesHeadingTweak

公式說明:
  腳底在紋理內 = 底部中點 (w/2, h)。錨點到腳底向量 = (w/2 - dx, h - dy)。
  令 body 腳底為參考，則 shadow 的 Offset 應使 shadow 腳底 = body 腳底：
  shadow_offset_desired = (body_w/2 - body_dx, body_h - body_dy) - (shadow_w/2 - shadow_dx, shadow_h - shadow_dy)。
  當前腳底對齊公式已給出 base offset，故 tweak = desired - current（只算與 base 的差）。
  化簡得：shadowTweak.x = body_w/2 - body_dx - shadow_w/2 + shadow_dx，
          shadowTweak.y = (body_h - body_dy) - (shadow_h - shadow_dy) - (shadow_h/2 - shadow_dy) = body_h - body_dy - shadow_h + shadow_dy - shadow_h/2 + shadow_dy = body_h - body_dy - 3*shadow_h/2 + 2*shadow_dy。
  但當前 ApplyLayerFeetAlignToBody 已做 Y 方向腳底對齊，故只需補 X 與可能的殘差。
  本腳本直接算「使底部中點對齊」的完整 offset，再減去當前公式給出的 offset，得到 tweak。
"""

import json
import os
import sys

def load_calibration(path):
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    if isinstance(data, list):
        # 每筆 { "heading": 0, "body": {...}, "shadow": {...}, "clothes": {...} }，取最後一筆 per heading
        by_head = {}
        for entry in data:
            h = entry.get("heading", 0)
            by_head[h] = entry
        return by_head
    if isinstance(data, dict):
        return {k: v for k, v in data.items() if isinstance(v, dict) and "body" in v}
    return {}

def vec(d):
    dx = d.get("dx", 0)
    dy = d.get("dy", 0)
    w = d.get("w", 1)
    h = d.get("h", 1)
    return dx, dy, w, h

def bottom_center_offset(dx, dy, w, h):
    """錨點到腳底（紋理底部中點）的向量。(w/2, h) - (dx, dy) = (w/2-dx, h-dy)"""
    return (w / 2 - dx, h - dy)

def current_shadow_offset(sdx, sdy, sw, sh, body_dy, body_h):
    """當前 ApplyLayerFeetAlignToBody 給 shadow 的 offset（相對 BodyOffset）"""
    ax = sw / 2 - sdx
    ay = sh / 2 - sdy
    body_anchor_to_feet = body_h - body_dy
    layer_anchor_to_feet = sh - sdy
    feet_align_y = body_anchor_to_feet - layer_anchor_to_feet
    return (ax, ay + feet_align_y)

def desired_shadow_offset(bdx, bdy, bw, bh, sdx, sdy, sw, sh):
    """使 shadow 腳底與 body 腳底重合時，shadow 的 offset（相對 BodyOffset）"""
    bx, by = bottom_center_offset(bdx, bdy, bw, bh)
    sx, sy = bottom_center_offset(sdx, sdy, sw, sh)
    return (bx - sx, by - sy)

def tweak_shadow(entry):
    body = entry.get("body")
    shadow = entry.get("shadow")
    if not body or not shadow:
        return None, None
    bdx, bdy, bw, bh = vec(body)
    sdx, sdy, sw, sh = vec(shadow)
    desired = desired_shadow_offset(bdx, bdy, bw, bh, sdx, sdy, sw, sh)
    current = current_shadow_offset(sdx, sdy, sw, sh, bdy, bh)
    return (desired[0] - current[0], desired[1] - current[1])

def current_clothes_offset(cdx, cdy, cw, ch, body_dy, body_h):
    ax = cw / 2 - cdx
    ay = ch / 2 - cdy
    body_anchor_to_feet = body_h - body_dy
    layer_anchor_to_feet = ch - cdy
    feet_align_y = body_anchor_to_feet - layer_anchor_to_feet
    return (ax, ay + feet_align_y)

def desired_clothes_offset(bdx, bdy, bw, bh, cdx, cdy, cw, ch):
    bx, by = bottom_center_offset(bdx, bdy, bw, bh)
    cx, cy = bottom_center_offset(cdx, cdy, cw, ch)
    return (bx - cx, by - cy)

def tweak_clothes(entry):
    body = entry.get("body")
    clothes = entry.get("clothes")
    if not body or not clothes:
        return None, None
    bdx, bdy, bw, bh = vec(body)
    cdx, cdy, cw, ch = vec(clothes)
    desired = desired_clothes_offset(bdx, bdy, bw, bh, cdx, cdy, cw, ch)
    current = current_clothes_offset(cdx, cdy, cw, ch, bdy, bh)
    return (desired[0] - current[0], desired[1] - current[1])

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    default_path = os.path.join(script_dir, "..", "calibration_anchor.json")
    path = sys.argv[1] if len(sys.argv) > 1 else default_path
    if not os.path.exists(path):
        print("用法: python3 calibration_to_tweak.py [calibration_anchor.json 路徑]")
        print("請先於遊戲內旋轉角色至朝向 0~7，各按一次 F8，再將 user://calibration_anchor.json 複製到專案。")
        return
    by_head = load_calibration(path)
    if not by_head:
        print("無法解析或無有效資料。")
        return
    # 輸出 0~7，缺的用 (0,0)
    headings = list(range(8))
    print("// ----- 貼回 GameEntity.Visuals.cs GetShadowClothesHeadingTweak 的 switch 內 -----")
    print("\t\tprivate void GetShadowClothesHeadingTweak(out Vector2 shadowTweak, out Vector2 clothesTweak)")
    print("\t\t{")
    print("\t\t\tshadowTweak = Vector2.Zero;")
    print("\t\t\tclothesTweak = Vector2.Zero;")
    print("\t\t\tswitch (Heading)")
    print("\t\t\t{")
    for h in headings:
        entry = by_head.get(str(h)) or by_head.get(h)
        if entry:
            st = tweak_shadow(entry)
            ct = tweak_clothes(entry)
            sx = st[0] if st[0] is not None else 0
            sy = st[1] if st[1] is not None else 0
            cx = ct[0] if ct[0] is not None else 0
            cy = ct[1] if ct[1] is not None else 0
        else:
            sx = sy = cx = cy = 0
        print(f"\t\t\t\tcase {h}:")
        print(f"\t\t\t\t\tshadowTweak = new Vector2({sx:.1f}f, {sy:.1f}f);")
        print(f"\t\t\t\t\tclothesTweak = new Vector2({cx:.1f}f, {cy:.1f}f);")
        print("\t\t\t\t\tbreak;")
    print("\t\t\t}")
    print("\t\t}")
    print("// ----- 以上 -----")

if __name__ == "__main__":
    main()
