SET FOREIGN_KEY_CHECKS=0;
USE l1jdb;

TRUNCATE TABLE l1jdb.mob_skills;
TRUNCATE TABLE l1jdb.mob_groups;
TRUNCATE TABLE l1jdb.npc_actions;
TRUNCATE TABLE l1jdb.npc_chats;
TRUNCATE TABLE l1jdb.map_ids;
TRUNCATE TABLE l1jdb.map_timers;

DROP TABLE IF EXISTS l1jdb.npc_map_loose;
CREATE TABLE l1jdb.npc_map_loose (
  jp_id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  l1_id INT(10) UNSIGNED NOT NULL,
  KEY (l1_id)
);

INSERT INTO l1jdb.npc_map_loose (jp_id, l1_id)
SELECT j.id, MIN(l.id)
FROM jpdbbackup.npcs j
JOIN l1jdb.npcs l
  ON j.name_id = l.name_id
 AND j.gfx_id = l.gfx_id
GROUP BY j.id;

INSERT IGNORE INTO l1jdb.npc_map_loose (jp_id, l1_id)
SELECT j.id, MIN(l.id)
FROM jpdbbackup.npcs j
JOIN l1jdb.npcs l
  ON j.name_id = l.name_id
LEFT JOIN l1jdb.npc_map_loose m
  ON m.jp_id = j.id
WHERE m.jp_id IS NULL
GROUP BY j.id;

INSERT INTO l1jdb.npc_actions (npc_id, note, normal_action, chaotic_action, teleport_url, teleport_urla)
SELECT
  m.l1_id,
  MAX(a.note),
  MAX(a.normal_action),
  MAX(a.chaotic_action),
  MAX(a.teleport_url),
  MAX(a.teleport_urla)
FROM jpdbbackup.npc_actions a
JOIN l1jdb.npc_map_loose m ON a.npc_id = m.jp_id
GROUP BY m.l1_id;

INSERT INTO l1jdb.npc_chats (
  npc_id, note, chat_timing, start_delay_time, chat_id1, chat_id2, chat_id3, chat_id4, chat_id5,
  chat_interval, is_shout, is_world_chat, is_repeat, repeat_interval, game_time
)
SELECT
  m.l1_id,
  MAX(c.note),
  c.chat_timing,
  MAX(c.start_delay_time),
  MAX(c.chat_id1),
  MAX(c.chat_id2),
  MAX(c.chat_id3),
  MAX(c.chat_id4),
  MAX(c.chat_id5),
  MAX(c.chat_interval),
  MAX(c.is_shout),
  MAX(c.is_world_chat),
  MAX(c.is_repeat),
  MAX(c.repeat_interval),
  MAX(c.game_time)
FROM jpdbbackup.npc_chats c
JOIN l1jdb.npc_map_loose m ON c.npc_id = m.jp_id
GROUP BY m.l1_id, c.chat_timing;

INSERT INTO l1jdb.mob_skills (
  npc_id, note, act_no, type, tri_rnd, tri_hp, tri_companion_hp, tri_range, tri_count,
  change_target, `range`, area_width, area_height, leverage, skill_id, gfx_id, act_id,
  summon_id, summon_min, summon_max, poly_id, chat_id
)
SELECT
  m.l1_id,
  MAX(s.note),
  s.act_no,
  MAX(s.type),
  MAX(s.tri_rnd),
  MAX(s.tri_hp),
  MAX(s.tri_companion_hp),
  MAX(s.tri_range),
  MAX(s.tri_count),
  MAX(s.change_target),
  MAX(s.`range`),
  MAX(s.area_width),
  MAX(s.area_height),
  MAX(s.leverage),
  MAX(s.skill_id),
  MAX(s.gfx_id),
  MAX(s.act_id),
  MAX(s.summon_id),
  MAX(s.summon_min),
  MAX(s.summon_max),
  MAX(s.poly_id),
  MAX(s.chat_id)
FROM jpdbbackup.mob_skills s
JOIN l1jdb.npc_map_loose m ON s.npc_id = m.jp_id
GROUP BY m.l1_id, s.act_no;

INSERT INTO l1jdb.mob_groups (
  id, note, remove_group_if_leader_die,
  leader_id,
  minion1_id, minion1_count,
  minion2_id, minion2_count,
  minion3_id, minion3_count,
  minion4_id, minion4_count,
  minion5_id, minion5_count,
  minion6_id, minion6_count,
  minion7_id, minion7_count
)
SELECT
  g.id,
  g.note,
  g.remove_group_if_leader_die,
  COALESCE(m0.l1_id, 0),
  COALESCE(m1.l1_id, 0), g.minion1_count,
  COALESCE(m2.l1_id, 0), g.minion2_count,
  COALESCE(m3.l1_id, 0), g.minion3_count,
  COALESCE(m4.l1_id, 0), g.minion4_count,
  COALESCE(m5.l1_id, 0), g.minion5_count,
  COALESCE(m6.l1_id, 0), g.minion6_count,
  COALESCE(m7.l1_id, 0), g.minion7_count
FROM jpdbbackup.mob_groups g
LEFT JOIN l1jdb.npc_map_loose m0 ON g.leader_id = m0.jp_id
LEFT JOIN l1jdb.npc_map_loose m1 ON g.minion1_id = m1.jp_id
LEFT JOIN l1jdb.npc_map_loose m2 ON g.minion2_id = m2.jp_id
LEFT JOIN l1jdb.npc_map_loose m3 ON g.minion3_id = m3.jp_id
LEFT JOIN l1jdb.npc_map_loose m4 ON g.minion4_id = m4.jp_id
LEFT JOIN l1jdb.npc_map_loose m5 ON g.minion5_id = m5.jp_id
LEFT JOIN l1jdb.npc_map_loose m6 ON g.minion6_id = m6.jp_id
LEFT JOIN l1jdb.npc_map_loose m7 ON g.minion7_id = m7.jp_id
WHERE (g.leader_id = 0 OR m0.l1_id IS NOT NULL)
  AND (g.minion1_id = 0 OR m1.l1_id IS NOT NULL)
  AND (g.minion2_id = 0 OR m2.l1_id IS NOT NULL)
  AND (g.minion3_id = 0 OR m3.l1_id IS NOT NULL)
  AND (g.minion4_id = 0 OR m4.l1_id IS NOT NULL)
  AND (g.minion5_id = 0 OR m5.l1_id IS NOT NULL)
  AND (g.minion6_id = 0 OR m6.l1_id IS NOT NULL)
  AND (g.minion7_id = 0 OR m7.l1_id IS NOT NULL);

DROP TEMPORARY TABLE IF EXISTS tmp_map_ids;
CREATE TEMPORARY TABLE tmp_map_ids (
  map_id INT(10) UNSIGNED NOT NULL PRIMARY KEY
);

INSERT IGNORE INTO tmp_map_ids (map_id)
SELECT DISTINCT map_id FROM l1jdb.spawn_mobs;
INSERT IGNORE INTO tmp_map_ids (map_id)
SELECT DISTINCT map_id FROM l1jdb.spawn_npcs;
INSERT IGNORE INTO tmp_map_ids (map_id)
SELECT DISTINCT map FROM l1jdb.npc_teleport;
INSERT IGNORE INTO tmp_map_ids (map_id)
SELECT DISTINCT check_map FROM l1jdb.npc_teleport;
INSERT IGNORE INTO tmp_map_ids (map_id)
SELECT DISTINCT src_map_id FROM l1jdb.dungeons;
INSERT IGNORE INTO tmp_map_ids (map_id)
SELECT DISTINCT new_map_id FROM l1jdb.dungeons;
INSERT IGNORE INTO tmp_map_ids (map_id)
SELECT DISTINCT map_id FROM l1jdb.restart_locations;
INSERT IGNORE INTO tmp_map_ids (map_id)
SELECT DISTINCT area_map_id FROM l1jdb.return_locations;
INSERT IGNORE INTO tmp_map_ids (map_id)
SELECT DISTINCT getback_map_id FROM l1jdb.return_locations;

INSERT INTO l1jdb.map_ids (
  id, name, start_x, end_x, start_y, end_y, monster_amount, drop_rate, unique_rate,
  underwater, markable, teleportable, escapable, resurrection, painwand, penalty,
  take_pets, recall_pets, usable_item, usable_skill
)
SELECT
  m.id, m.name, m.start_x, m.end_x, m.start_y, m.end_y, m.monster_amount, m.drop_rate, m.unique_rate,
  m.underwater, m.markable, m.teleportable, m.escapable, m.resurrection, m.painwand, m.penalty,
  m.take_pets, m.recall_pets, m.usable_item, m.usable_skill
FROM jpdbbackup.map_ids m
JOIN tmp_map_ids t ON t.map_id = m.id;

DROP TABLE IF EXISTS l1jdb.npc_map_loose;
SET FOREIGN_KEY_CHECKS=1;
