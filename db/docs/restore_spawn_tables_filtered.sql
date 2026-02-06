SET FOREIGN_KEY_CHECKS=0;
USE l1jdb;

-- Build NPC mapping (jpdbbackup -> l1jdb) by name_id + gfx_id
DROP TEMPORARY TABLE IF EXISTS tmp_npc_map;
CREATE TEMPORARY TABLE tmp_npc_map (
  jp_id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  l1_id INT(10) UNSIGNED NOT NULL,
  KEY (l1_id)
);

INSERT INTO tmp_npc_map (jp_id, l1_id)
SELECT j.id, MIN(l.id)
FROM jpdbbackup.npcs j
JOIN l1jdb.npcs l
  ON j.name_id = l.name_id
 AND j.gfx_id = l.gfx_id
GROUP BY j.id;

-- spawn_boss_mobs: keep only 182-mapped NPCs and 182 map_ids
TRUNCATE TABLE l1jdb.spawn_boss_mobs;
INSERT INTO l1jdb.spawn_boss_mobs (
  id, npc_id, note, group_id, cycle_type, count,
  loc_x, loc_y, random_x, random_y,
  loc_x1, loc_y1, loc_x2, loc_y2,
  heading, map_id, respawn_screen, movement_distance, rest, spawn_type, percentage
)
SELECT
  b.id,
  m.l1_id,
  b.note,
  b.group_id,
  b.cycle_type,
  b.count,
  b.loc_x, b.loc_y, b.random_x, b.random_y,
  b.loc_x1, b.loc_y1, b.loc_x2, b.loc_y2,
  b.heading, b.map_id, b.respawn_screen, b.movement_distance, b.rest, b.spawn_type, b.percentage
FROM jpdbbackup.spawn_boss_mobs b
JOIN tmp_npc_map m ON b.npc_id = m.jp_id
JOIN l1jdb.map_ids mi ON b.map_id = mi.id;

-- spawn_doors: keep only 182 map_ids
TRUNCATE TABLE l1jdb.spawn_doors;
INSERT INTO l1jdb.spawn_doors (
  id, map_id, note, gfx_id, loc_x, loc_y, hp, npc_id, is_open
)
SELECT
  d.id, d.map_id, d.note, d.gfx_id, d.loc_x, d.loc_y, d.hp, d.npc_id, d.is_open
FROM jpdbbackup.spawn_doors d
JOIN l1jdb.map_ids mi ON d.map_id = mi.id;

-- spawn_lights: keep only 182-mapped NPCs and 182 map_ids
TRUNCATE TABLE l1jdb.spawn_lights;
INSERT INTO l1jdb.spawn_lights (
  id, npc_id, loc_x, loc_y, map_id
)
SELECT
  l.id, m.l1_id, l.loc_x, l.loc_y, l.map_id
FROM jpdbbackup.spawn_lights l
JOIN tmp_npc_map m ON l.npc_id = m.jp_id
JOIN l1jdb.map_ids mi ON l.map_id = mi.id;

-- spawn_ub_mobs: keep only ub_id present (182-filtered) and 182-mapped NPCs
TRUNCATE TABLE l1jdb.spawn_ub_mobs;
INSERT INTO l1jdb.spawn_ub_mobs (
  id, ub_id, pattern, group_id, npc_id, note,
  count, spawn_delay, seal_count
)
SELECT
  s.id, s.ub_id, s.pattern, s.group_id, m.l1_id, s.note,
  s.count, s.spawn_delay, s.seal_count
FROM jpdbbackup.spawn_ub_mobs s
JOIN l1jdb.ubs u ON s.ub_id = u.id
JOIN tmp_npc_map m ON s.npc_id = m.jp_id;

SET FOREIGN_KEY_CHECKS=1;
