# 182 -> JP Database Merge Summary

This document captures the migration plan, actual executed rules, and current state of the 182 data merged into the JP schema. It is intended for follow-up work by another AI or engineer.

## Goal
- Keep JP server code and JP database schema.
- Replace JP data with 182-era data where possible.
- If 182 has no equivalent table, use `jpdbbackup` and filter to 182-era entities.

## Databases
- Source 182: `182`
- Target JP schema: `l1jdb`
- Backup JP data: `jpdbbackup` (original JP data)

## Core Mapping Rules
- NPC mapping: `jpdbbackup.npcs` -> `l1jdb.npcs` using `name_id + gfx_id` (strict match). If not matched, rows are dropped.
- Item mapping: `jpdbbackup.*items` -> `l1jdb` using `identified_name_id` (strict match). If not matched, rows are dropped.
- Map filtering: only keep rows whose `map_id` exists in `l1jdb.map_ids` (which already reflects 182 maps).
- Coordinate filtering (dungeons): coordinates must fall within map bounds from `l1jdb.map_ids`.

## Major Fixes Applied
- NPC lawful/int swap fix after conversion:
  - `lawful` and `int` were swapped for non-monsters, corrected by SQL.

## Tables Migrated and Rules

### Converted from 182 -> JP schema (primary)
- `npcs`, `etc_items`, `weapons`, `armors`, `skills`, etc. via `convert_182_to_l1jdb.sql`.

### From jpdbbackup with 182 filtering
- `mob_skills`, `npc_actions`, `npc_chats`, `mob_groups` via name_id/gfx mapping.
- `map_ids` imported only for maps referenced by 182 data.
- `ub_managers` via NPC mapping.
- `traps` via NPC mapping + map filter; keeps only if `monster_npc_id` can map.
- `ubs`, `ub_times` filtered by `ub_id` that appears in `l1jdb.ub_managers` (182 NPC only).
- `resolvents` filtered by item mapping (identified_name_id -> l1jdb item id).
- `spawn_boss_mobs` filtered by NPC mapping + map filter.
- `spawn_lights` filtered by NPC mapping + map filter.
- `spawn_ub_mobs` filtered by `ub_id` (182 UB only) + NPC mapping.
- `spawn_doors` filtered by map filter.
- `door_gfxs` filtered by gfx used in `l1jdb.spawn_doors`.
- `drop_rates` filtered by item mapping.
- `dungeons` filtered by 182 map ids AND coordinate bounds.

### From 182 auxiliary tables
- `spr_actions` from `182.sprite_frame` (frame_rate = 24) and added `name` column.
- `armor_sets` from `182.items_setoption` + `182.items.set_item_uid` mapped to l1jdb items.

## Added/Modified Columns
- `l1jdb.spr_actions`: added `name` (from `182.sprite_frame.name`; fallback by npc gfx_id).

## Current Known Limitations
- `pet_items` is empty because JP pet item name_ids do not exist in 182 item tables.
- `pet_types` exists for dog/wolf type NPCs only (8 rows).
- Any JP-only NPCs without 182 mapping were dropped from support tables.

## Scripts Executed (and Purpose)
- `jp/db/docs/convert_182_to_l1jdb.sql`
  - Full 182 -> JP schema conversion (core data tables).
- `jp/db/docs/restore_jp_support_tables_from_backup.sql`
  - Restore JP-only support tables: `mob_skills`, `npc_actions`, `npc_chats`, `mob_groups`, `map_ids`, `map_timers`.
- `jp/db/docs/restore_pet_types_from_backup.sql`
  - Restore `pet_types`/`pet_items` from jpdbbackup with item/NPC mapping.
- `jp/db/docs/restore_182_support_tables_2.sql`
  - `spr_actions`, `armor_sets`, plus `ub_managers`, `towns`, `traps`.
- `jp/db/docs/restore_ub_and_spr_actions_name.sql`
  - `ubs`, `ub_times`, and add/seed `spr_actions.name`.
- `jp/db/docs/restore_ub_times_ubs_filtered_and_resolvents.sql`
  - Filtered `ubs/ub_times` by 182 NPC mapping; restore `resolvents`.
- `jp/db/docs/restore_spawn_tables_filtered.sql`
  - Filtered `spawn_boss_mobs`, `spawn_doors`, `spawn_lights`, `spawn_ub_mobs`.
- `jp/db/docs/restore_drop_rates_door_gfxs_dungeons_filtered.sql`
  - Filtered `drop_rates`, `door_gfxs`, and `dungeons`.

## Current Counts (last known)
- `spr_actions`: 281
- `armor_sets`: 5
- `ub_managers`: 5
- `ubs`: 5
- `ub_times`: 38
- `traps`: 45
- `spawn_boss_mobs`: 24
- `spawn_doors`: 552
- `spawn_lights`: 169
- `spawn_ub_mobs`: 412
- `door_gfxs`: 125
- `drop_rates`: 48
- `dungeons`: 868
- `resolvents`: 185
- `pet_types`: 8
- `pet_items`: 0

## Inns Tables (What They Are)
- `inns`: innkeeper room definitions and occupancy.
  - Key columns: `npc_id` (innkeeper NPC), `room_number`, `key_id`, `lodger_id`, `hall`, `due_time`.
- `inn_keys`: active key items for inn rooms.
  - Key columns: `item_obj_id`, `id` (key id), `npc_id`, `hall`, `due_time`.

## Remaining TODO Suggestions
- Decide if JP-only pet items should be added into 182 item tables (to enable `pet_items`).
- Validate UB areas against 182 map/npc usage (optional stricter filtering).
- Re-run any table-specific corrections if manual QA finds mismatches (use targeted UPDATE SQL).

