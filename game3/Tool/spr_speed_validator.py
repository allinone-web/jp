#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
list.spr 與 SprDataTable.cs 速度數據驗證工具

從 list.spr 提取角色的 walk、攻擊速度，轉換為 ms，並與 SprDataTable.cs 對比
自動檢測所有在 sprite_frame 表中的角色（變身卷軸角色）
"""

import re
import os
from datetime import datetime
from collections import defaultdict
from typing import Dict, List, Tuple, Optional

# 動作類型分類
class ActionCategory:
    WALK = "行走 (Walk)"
    MELEE_ATTACK = "近戰攻擊 (Melee Attack)"
    RANGED_ATTACK = "遠程攻擊 (Ranged Attack - Bow)"
    LONG_RANGE_MELEE = "長距離近戰 (Long Range Melee - Spear/Staff)"
    MAGIC_RANGED = "魔法遠程 (Magic Ranged)"

# 動作 ID 到類型的映射
ACTION_ID_TO_CATEGORY = {
    # Walk
    0: ActionCategory.WALK,
    4: ActionCategory.WALK,
    11: ActionCategory.WALK,
    20: ActionCategory.WALK,
    24: ActionCategory.WALK,
    40: ActionCategory.WALK,
    
    # Melee Attack
    1: ActionCategory.MELEE_ATTACK,
    5: ActionCategory.MELEE_ATTACK,
    12: ActionCategory.MELEE_ATTACK,
    30: ActionCategory.MELEE_ATTACK,
    
    # Ranged Attack
    21: ActionCategory.RANGED_ATTACK,
    
    # Long Range Melee
    41: ActionCategory.LONG_RANGE_MELEE,
    
    # Magic Ranged
    18: ActionCategory.MAGIC_RANGED,
    19: ActionCategory.MAGIC_RANGED,
}


def load_sprite_frame_from_sql(sql_path: str) -> Tuple[Dict[int, str], Dict[int, Dict[int, int]]]:
    """從 SQL 文件中提取 sprite_frame 表的數據"""
    character_names = {}
    spr_data = {}
    
    if not os.path.exists(sql_path):
        print(f"警告: 找不到 SQL 文件: {sql_path}")
        return character_names, spr_data
    
    in_sprite_frame = False
    with open(sql_path, 'r', encoding='utf-8') as f:
        for line in f:
            if 'INSERT INTO `sprite_frame`' in line:
                in_sprite_frame = True
                continue
            
            if in_sprite_frame:
                if line.strip().startswith(';'):
                    break
                
                # 解析: ('王子',0,0,'walk',640),
                match = re.match(r"\s*\('([^']+)',(\d+),(\d+),'[^']+',(\d+)\)", line)
                if match:
                    name = match.group(1)
                    gfx_id = int(match.group(2))
                    action_id = int(match.group(3))
                    frame = int(match.group(4))
                    
                    if gfx_id not in character_names:
                        character_names[gfx_id] = name
                    
                    if gfx_id not in spr_data:
                        spr_data[gfx_id] = {}
                    spr_data[gfx_id][action_id] = frame
    
    print(f"從 SQL 文件載入 {len(character_names)} 個角色")
    return character_names, spr_data


def load_spr_data_table_from_cs(cs_path: str) -> Dict[int, Dict[int, int]]:
    """從 SprDataTable.cs 中提取數據"""
    spr_data = {}
    
    if not os.path.exists(cs_path):
        print(f"警告: 找不到 C# 文件: {cs_path}")
        return spr_data
    
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()
        
        # 查找 rawData 列表
        match = re.search(r'var rawData = new List<\(int gfx, int action, int frame\)>\s*\{([^}]+)\}', content, re.DOTALL)
        if match:
            data_block = match.group(1)
            # 解析所有 (gfx, action, frame) 元組
            pattern = r'\((\d+),\s*(\d+),\s*(\d+)\)'
            for match in re.finditer(pattern, data_block):
                gfx_id = int(match.group(1))
                action_id = int(match.group(2))
                frame = int(match.group(3))
                
                if gfx_id not in spr_data:
                    spr_data[gfx_id] = {}
                spr_data[gfx_id][action_id] = frame
    
    print(f"從 SprDataTable.cs 載入 {len(spr_data)} 個角色的數據")
    return spr_data


def parse_list_spr(file_path: str, target_gfx_ids: set = None) -> Dict[int, Dict]:
    """從 list.spr 解析角色速度數據"""
    result = {}
    
    if not os.path.exists(file_path):
        print(f"錯誤: 找不到文件: {file_path}")
        return result
    
    current_char = None
    
    with open(file_path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            
            # 解析角色定義行: #0	208	prince
            header_match = re.match(r'^#(\d+)\s+(\d+)\s+(.+)$', line)
            if header_match:
                gfx_id = int(header_match.group(1))
                # 如果指定了目標 gfx_ids，只解析這些角色
                if target_gfx_ids is None or gfx_id in target_gfx_ids:
                    current_char = {
                        'gfx_id': gfx_id,
                        'name': header_match.group(3).strip(),
                        'actions': {}
                    }
                    result[gfx_id] = current_char
                else:
                    current_char = None
                continue
            
            if current_char is None:
                continue
            
            # 解析動作行: 0.walk(1 4,24.0:4 24.1:4[300 24.2:4 24.3:4)
            action_match = re.match(r'(\d+)\.([a-zA-Z0-9_\s]+)\(([^)]+)\)', line)
            if not action_match:
                continue
            
            action_id = int(action_match.group(1))
            action_name = action_match.group(2).strip()
            content = action_match.group(3).strip()
            
            if action_id not in ACTION_ID_TO_CATEGORY:
                continue
            
            # 解析幀數據
            parts = content.split(',')
            if len(parts) < 2:
                continue
            
            # 解析幀 tokens: 24.0:4 24.1:4[300 24.2:4 24.3:4
            frame_tokens = parts[1].split()
            frame_durations = []
            total_ms = 0
            
            for token in frame_tokens:
                # 移除音效標記 [300, <97 等
                clean_token = token
                clean_token = re.sub(r'\[(\d+)', '', clean_token)
                if '<' in clean_token:
                    clean_token = clean_token[:clean_token.index('<')]
                clean_token = clean_token.replace('!', '').replace('>', '').strip()
                
                if not clean_token:
                    continue
                
                # 解析 A.B:C 格式（支持負數 A，如 -1.0:4）
                frame_match = re.match(r'(-?\d+)\.(\d+):(\d+)', clean_token)
                if frame_match:
                    duration_unit = int(frame_match.group(3))
                    frame_ms = duration_unit * 40  # DurationUnit * 40ms
                    frame_durations.append(frame_ms)
                    total_ms += frame_ms
            
            if frame_durations:
                current_char['actions'][action_id] = {
                    'action_id': action_id,
                    'action_name': action_name,
                    'category': ACTION_ID_TO_CATEGORY[action_id],
                    'total_ms': total_ms,
                    'frame_durations': frame_durations
                }
    
    return result


def generate_comparison_report(list_spr_data: Dict, spr_data_table_data: Dict, character_names: Dict):
    """生成對比報告"""
    report = []
    report.append("=" * 80)
    report.append("list.spr 與 SprDataTable.cs 速度數據對比報告")
    report.append("=" * 80)
    report.append("")
    report.append(f"生成時間: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    report.append("")
    report.append("說明:")
    report.append("  - list.spr: 從 list.spr 文件解析的動作總時長（所有幀 DurationUnit * 40ms 的總和）")
    report.append("  - SprDataTable: 從 SprDataTable.cs 或 SQL 文件中定義的間隔時間（毫秒）")
    report.append("  - 差異: 兩者的差值（毫秒）")
    report.append("  - ⚠️ 標記: 特別關注的角色（如 gfx=240 死亡騎士）")
    report.append("")
    
    # 特別標記 gfx=240
    special_gfx_ids = {240}
    
    for gfx_id in sorted(list_spr_data.keys()):
        char_data = list_spr_data[gfx_id]
        char_name = character_names.get(gfx_id, char_data['name'])
        special_mark = " ⚠️" if gfx_id in special_gfx_ids else ""
        
        report.append(f"## GfxId {gfx_id}: {char_name}{special_mark}")
        report.append("-" * 80)
        
        # 按類型分組
        actions_by_category = defaultdict(list)
        for action_id, action_data in char_data['actions'].items():
            actions_by_category[action_data['category']].append(action_data)
        
        for category in sorted(actions_by_category.keys()):
            report.append("")
            report.append(f"### {category}")
            report.append("")
            
            for action in sorted(actions_by_category[category], key=lambda x: x['action_id']):
                spr_value = spr_data_table_data.get(gfx_id, {}).get(action['action_id'], None)
                
                report.append(f"  Action {action['action_id']}.{action['action_name']}:")
                frame_str = "+".join(str(f) for f in action['frame_durations'])
                report.append(f"    list.spr:     {action['total_ms']}ms (幀: {frame_str}ms = {action['total_ms']}ms)")
                
                if spr_value is not None:
                    diff = action['total_ms'] - spr_value
                    status = "✓ 一致" if diff == 0 else f"✗ 不一致 (差異: {diff}ms)"
                    report.append(f"    SprDataTable: {spr_value}ms {status}")
                    if diff != 0:
                        diff_desc = f"list.spr 比 SprDataTable 多 {diff}ms" if diff > 0 else f"list.spr 比 SprDataTable 少 {abs(diff)}ms"
                        report.append(f"    說明: {diff_desc}")
                else:
                    report.append(f"    SprDataTable: [缺失]")
                report.append("")
        
        report.append("")
    
    # 統計摘要
    report.append("=" * 80)
    report.append("統計摘要")
    report.append("=" * 80)
    report.append("")
    
    total_actions = 0
    matched_actions = 0
    mismatched_actions = 0
    missing_actions = 0
    mismatch_details = []
    special_mismatches = []  # gfx=240 等特殊角色的不一致
    
    for gfx_id in sorted(list_spr_data.keys()):
        char_data = list_spr_data[gfx_id]
        for action_id, action_data in char_data['actions'].items():
            total_actions += 1
            spr_value = spr_data_table_data.get(gfx_id, {}).get(action_id, None)
            
            if spr_value is not None:
                if action_data['total_ms'] == spr_value:
                    matched_actions += 1
                else:
                    mismatched_actions += 1
                    diff = action_data['total_ms'] - spr_value
                    detail = (
                        f"GfxId {gfx_id} ({character_names.get(gfx_id, char_data['name'])}) "
                        f"Action {action_id}.{action_data['action_name']}: "
                        f"list.spr={action_data['total_ms']}ms, SprDataTable={spr_value}ms, 差異={diff}ms"
                    )
                    mismatch_details.append(detail)
                    if gfx_id in special_gfx_ids:
                        special_mismatches.append(detail)
            else:
                missing_actions += 1
    
    report.append(f"總動作數: {total_actions}")
    report.append(f"一致: {matched_actions} ({matched_actions * 100.0 / total_actions:.1f}%)")
    report.append(f"不一致: {mismatched_actions} ({mismatched_actions * 100.0 / total_actions:.1f}%)")
    report.append(f"缺失: {missing_actions} ({missing_actions * 100.0 / total_actions:.1f}%)")
    report.append("")
    
    if special_mismatches:
        report.append("=" * 80)
        report.append("⚠️ 特殊角色不一致詳情 (gfx=240 等)")
        report.append("=" * 80)
        for detail in special_mismatches:
            report.append(f"  {detail}")
        report.append("")
    
    if mismatch_details:
        report.append("不一致詳情:")
        report.append("-" * 80)
        for detail in mismatch_details:
            report.append(f"  {detail}")
        report.append("")
    
    # 保存報告
    report_text = "\n".join(report)
    report_path = "Tool/spr_speed_comparison.txt"
    os.makedirs(os.path.dirname(report_path), exist_ok=True)
    with open(report_path, 'w', encoding='utf-8') as f:
        f.write(report_text)
    
    # 同時輸出到控制台
    print(report_text)


def main():
    print("=" * 80)
    print("list.spr 與 SprDataTable.cs 速度數據驗證工具")
    print("=" * 80)
    print()
    
    # 1. 從 SQL 文件載入 sprite_frame 數據
    sql_path = "server/datebase_182_2026-01-21.sql"
    print("正在從 SQL 文件載入 sprite_frame 數據...")
    character_names, sql_spr_data = load_sprite_frame_from_sql(sql_path)
    print()
    
    # 2. 從 SprDataTable.cs 載入數據
    cs_path = "Client/Data/SprDataTable.cs"
    print("正在從 SprDataTable.cs 載入數據...")
    cs_spr_data = load_spr_data_table_from_cs(cs_path)
    print()
    
    # 3. 合併數據（優先使用 SQL 數據，因為它是服務器的真實數據）
    spr_data_table_data = sql_spr_data.copy()
    for gfx_id, actions in cs_spr_data.items():
        if gfx_id not in spr_data_table_data:
            spr_data_table_data[gfx_id] = {}
        spr_data_table_data[gfx_id].update(actions)
    
    # 4. 從 list.spr 提取數據（只提取在 sprite_frame 表中的角色）
    list_spr_path = "Assets/list.spr"
    if not os.path.exists(list_spr_path):
        print(f"錯誤: 找不到文件: {list_spr_path}")
        print("請確保在項目根目錄運行此工具")
        return
    
    target_gfx_ids = set(character_names.keys())
    print(f"正在解析 list.spr（目標角色數: {len(target_gfx_ids)}）...")
    list_spr_data = parse_list_spr(list_spr_path, target_gfx_ids)
    print(f"解析完成，找到 {len(list_spr_data)} 個角色")
    print()
    
    # 5. 生成對比報告
    print("正在生成對比報告...")
    generate_comparison_report(list_spr_data, spr_data_table_data, character_names)
    
    print()
    print("驗證完成！報告已保存到 Tool/spr_speed_comparison.txt")


if __name__ == "__main__":
    main()
