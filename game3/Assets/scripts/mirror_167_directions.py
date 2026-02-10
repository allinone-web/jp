# -*- coding: utf-8 -*-
"""
將 167 的 4 個方向鏡像生成另外 4 個方向，並輸出座標偏移 txt。

方向對應：
  167-0 左上  → 167-4 右下 (水平+垂直鏡像)
  167-1 正上  → 167-5 下   (垂直鏡像)
  167-2 右上  → 167-6 左下 (水平鏡像)
  167-3 右    → 167-7 左   (水平鏡像)

用法：修改下方 INPUT_DIR / OUTPUT_DIR 後執行
  python3 mirror_167_directions.py
"""

import os
import re
from PIL import Image

# ================= 配置 =================
# 放 167-0-000.png, 167-1-000.png ... 的目錄（輸入與輸出可同目錄）
INPUT_DIR = "/Users/airtan/Documents/GitHub/game2/Assets/png138"
OUTPUT_DIR = INPUT_DIR  # 可改為其他目錄
# 新座標 txt 輸出路徑（可為 None 則只打印）
OUTPUT_TXT = None  # 例如 os.path.join(OUTPUT_DIR, "sprite_offsets_167_mirrored.txt")
# 未找到圖檔時，用此假設尺寸計算座標並仍輸出 txt（可複製）；有圖時會用實際尺寸
ASSUMED_W, ASSUMED_H = 24, 48
# ========================================

# 源方向 -> (目標方向, 是否水平鏡像, 是否垂直鏡像)
MIRROR_MAP = [
    (0, 4, True, True),   # 167-0 左上 -> 167-4 右下 (H+V)
    (1, 5, False, True),  # 167-1 正上 -> 167-5 下 (V)
    (2, 6, True, False),  # 167-2 右上 -> 167-6 左下 (H)
    (3, 7, True, False), # 167-3 右 -> 167-7 左 (H)
]

# 你提供的原始偏移（用於計算鏡像後的新 dx, dy；若腳本掃描到檔案會用實際圖檔尺寸覆蓋）
DEFAULT_FRAMES = {
    "167-0": [(3, -42), (3, -42), (2, -43), (2, -43)],
    "167-1": [(13, -44), (13, -44), (11, -46), (12, -46)],
    "167-2": [(1, -42), (1, -43), (-2, -43), (-3, -43)],
    "167-3": [(-5, -40), (-5, -41), (-11, -45), (-11, -44)],
}


def mirror_image(img, flip_h, flip_v):
    if flip_h and flip_v:
        return img.transpose(Image.Transpose.ROTATE_180)
    if flip_h:
        return img.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    if flip_v:
        return img.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    return img


def new_offset(dx, dy, w, h, flip_h, flip_v):
    """鏡像後新 dx, dy。錨點在紋理中為 (dx, dy) 從左上角；鏡像後該點在新紋理中的位置即新 offset。"""
    if flip_h and flip_v:
        return (w - 1 - dx, h - 1 - dy)
    if flip_h:
        return (w - 1 - dx, dy)
    if flip_v:
        return (dx, h - 1 - dy)
    return (dx, dy)


def collect_source_frames(src_dir, prefix):
    """掃描目錄，回傳 (frame_idx, 檔名) 列表，例如 [(0,'167-0-000.png'), (1,'167-0-001.png')]"""
    out = []
    # 檔名格式: 167-0-000.png -> prefix="167-0", 後面是 "000.png"（三位數+ .png）
    for name in sorted(os.listdir(src_dir)):
        if not name.startswith(prefix + "-") or not name.endswith(".png"):
            continue
        rest = name[len(prefix) + 1:]  # 167-0-000.png -> "000.png"
        if not rest.endswith(".png"):
            continue
        frame_str = rest[:-4]  # "000"
        if not frame_str.isdigit():
            continue
        frame_idx = int(frame_str)
        out.append((frame_idx, name))
    return sorted(out)


def run():
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    all_txt_lines = []

    for src_act, dst_act, flip_h, flip_v in MIRROR_MAP:
        src_prefix = f"167-{src_act}"
        dst_prefix = f"167-{dst_act}"
        frames = collect_source_frames(INPUT_DIR, src_prefix)
        if not frames:
            # 無圖時用預設偏移 + 假設尺寸仍輸出 txt，方便直接複製
            print(f"[略過] 未找到 {src_prefix}-*.png，已用假設尺寸 {ASSUMED_W}x{ASSUMED_H} 計算座標")
            default_list = DEFAULT_FRAMES.get(src_prefix, [(0, 0)] * 4)
            txt_lines = [
                f"#167-{dst_act}",
                f"ANCHOR = 167-{dst_act}-a.bmp",
            ]
            for fi, (dx, dy) in enumerate(default_list):
                ndx, ndy = new_offset(dx, dy, ASSUMED_W, ASSUMED_H, flip_h, flip_v)
                txt_lines.append(f"FRAME {fi} dx={ndx} dy={ndy} bmp=167-{dst_act}-{fi:03d}.png type=CHARACTER")
            txt_lines.append("")
            all_txt_lines.extend(txt_lines)
            continue

        txt_lines = [
            f"#167-{dst_act}",
            f"ANCHOR = 167-{dst_act}-a.bmp",
        ]
        for frame_idx, src_name in frames:
            src_path = os.path.join(INPUT_DIR, src_name)
            dst_name = f"167-{dst_act}-{frame_idx:03d}.png"
            dst_path = os.path.join(OUTPUT_DIR, dst_name)

            if not os.path.isfile(src_path):
                continue
            img = Image.open(src_path).convert("RGBA")
            w, h = img.size
            # 使用預設 dx,dy 或從同序號取
            default_list = DEFAULT_FRAMES.get(src_prefix, [(0, 0)] * (frame_idx + 1))
            dx, dy = default_list[frame_idx] if frame_idx < len(default_list) else (0, 0)

            mirrored = mirror_image(img, flip_h, flip_v)
            mirrored.save(dst_path, "PNG")
            ndx, ndy = new_offset(dx, dy, w, h, flip_h, flip_v)
            txt_lines.append(f"FRAME {frame_idx} dx={ndx} dy={ndy} bmp={dst_name} type=CHARACTER")
        txt_lines.append("")
        all_txt_lines.extend(txt_lines)
        print(f"已生成 167-{dst_act} (鏡像自 167-{src_act}) 共 {len(frames)} 幀")

    result_txt = "\n".join(all_txt_lines)
    if OUTPUT_TXT:
        with open(OUTPUT_TXT, "w", encoding="utf-8") as f:
            f.write(result_txt)
        print(f"座標已寫入: {OUTPUT_TXT}")
    else:
        print("\n======== 座標偏移數據（可複製到 sprite_offsets） ========\n")
        print(result_txt)


if __name__ == "__main__":
    run()
