#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
確認 sprite_offsets 中主體、Shadow、Clothes 的錨點是否定義在同一邏輯點（例如都是腳底）。

用法:
  python3 check_sprite_anchor_align.py [sprite_offsets路徑] [動作ID]

範例:
  python3 check_sprite_anchor_align.py
  python3 check_sprite_anchor_align.py ../sprite_offsets-138_update.txt 10

約定:
  - (dx, dy) = 錨點在紋理內的像素位置（左上角為 0,0，y 向下為正）。
  - 若錨點都在「腳底」，則各層的 dy 會接近各自紋理的 h（底部）；即 dy/h 接近 1。
  - 若錨點都在「紋理水平中心」，則 dx 接近 w/2。
  - 本腳本只讀取 txt 的 (dx, dy)，不讀取 w,h（需從紋理取得）；輸出 dy 供比對，遊戲內可開 DEBUG_ANCHOR_ALIGN 看 (dx,dy,w,h) 與 dy/h。
"""

import os
import re
import sys

def load_offsets(path):
    """解析 sprite_offsets：key = (gfx_id, action_id), value = [(frame_idx, dx, dy), ...]"""
    data = {}
    if not os.path.exists(path):
        return data
    current_key = None
    with open(path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith('ANCHOR'):
                continue
            if line.startswith('#'):
                parts = line[1:].split('-')
                if len(parts) >= 2:
                    try:
                        gfx_id = int(parts[0])
                        action_id = int(parts[1])
                        current_key = (gfx_id, action_id)
                        if current_key not in data:
                            data[current_key] = []
                    except ValueError:
                        current_key = None
                continue
            if line.startswith('FRAME') and current_key is not None:
                m_idx = re.search(r'FRAME\s+(\d+)', line)
                m_dx = re.search(r'dx=(-?\d+)', line)
                m_dy = re.search(r'dy=(-?\d+)', line)
                if m_idx and m_dx and m_dy:
                    data[current_key].append((
                        int(m_idx.group(1)),
                        int(m_dx.group(1)),
                        int(m_dy.group(1))
                    ))
    return data

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    default_path = os.path.join(script_dir, '..', 'sprite_offsets-138_update.txt')
    path = sys.argv[1] if len(sys.argv) > 1 else default_path
    action_id = int(sys.argv[2]) if len(sys.argv) > 2 else 10

    # 常見：240=主體, 241=Shadow, 242=Clothes（依 list.spr / 專案設定）
    gfx_ids = [(240, 'Body'), (241, 'Shadow'), (242, 'Clothes')]

    data = load_offsets(path)
    if not data:
        print(f'無法讀取或無資料: {path}')
        return

    print(f'讀取: {path}')
    print(f'比對 動作 ID = {action_id}，FRAME 0 的 (dx, dy)')
    print('若錨點皆在「腳底」，各層 dy 應接近各自紋理高度 h（即遊戲內 dy/h ≈ 1）。')
    print('本表僅 (dx, dy)；w,h 需從紋理取得，遊戲內開 DEBUG_ANCHOR_ALIGN 可看 (dx,dy,w,h) 與 dy/h。')
    print('-' * 72)

    for gfx_id, label in gfx_ids:
        key = (gfx_id, action_id)
        if key not in data or not data[key]:
            print(f'  {label} (GfxId={gfx_id}): 無此 action {action_id}')
            continue
        frames = sorted(data[key], key=lambda x: x[0])
        # 只取 FRAME 0
        f0 = [t for t in frames if t[0] == 0]
        if f0:
            _, dx, dy = f0[0]
            print(f'  {label} (GfxId={gfx_id}): FRAME 0  dx={dx}  dy={dy}')
        else:
            print(f'  {label} (GfxId={gfx_id}): 無 FRAME 0，首幀 {frames[0][0]}  dx={frames[0][1]}  dy={frames[0][2]}')

    print('-' * 72)
    print('說明: 各層紋理尺寸 (w,h) 不同，故 (dx,dy) 絕對值不必相同；同一邏輯點（腳底）時 dy 相對各自 h 應接近（dy/h≈1）。')

if __name__ == '__main__':
    main()
