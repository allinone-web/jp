SET FOREIGN_KEY_CHECKS=0;
USE l1jdb;

-- spr_actions from 182.sprite_frame
TRUNCATE TABLE l1jdb.spr_actions;
INSERT INTO l1jdb.spr_actions (spr_id, act_id, frame_count, frame_rate)
SELECT gfx, action, frame, 24
FROM `182`.sprite_frame;

-- armor_sets from 182.items_setoption + 182.items (set_item_uid)
TRUNCATE TABLE l1jdb.armor_sets;

DROP TEMPORARY TABLE IF EXISTS tmp_182_items;
CREATE TEMPORARY TABLE tmp_182_items (
  item_id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  nameid VARCHAR(40) NOT NULL,
  set_item_uid INT(10) UNSIGNED NOT NULL,
  KEY (nameid),
  KEY (set_item_uid)
);

INSERT INTO tmp_182_items (item_id, nameid, set_item_uid)
SELECT item_id, nameid, set_item_uid
FROM `182`.items
WHERE set_item_uid <> 0;

DROP TEMPORARY TABLE IF EXISTS tmp_l1_items;
CREATE TEMPORARY TABLE tmp_l1_items (
  id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  identified_name_id VARCHAR(255) NOT NULL,
  KEY (identified_name_id)
);

INSERT INTO tmp_l1_items (id, identified_name_id)
SELECT id, identified_name_id FROM l1jdb.etc_items;
INSERT INTO tmp_l1_items (id, identified_name_id)
SELECT id, identified_name_id FROM l1jdb.weapons;
INSERT INTO tmp_l1_items (id, identified_name_id)
SELECT id, identified_name_id FROM l1jdb.armors;

DROP TEMPORARY TABLE IF EXISTS tmp_itemid_map;
CREATE TEMPORARY TABLE tmp_itemid_map (
  set_item_uid INT(10) UNSIGNED NOT NULL,
  l1_item_id INT(10) UNSIGNED NOT NULL,
  KEY (set_item_uid),
  KEY (l1_item_id)
);

INSERT INTO tmp_itemid_map (set_item_uid, l1_item_id)
SELECT i.set_item_uid, MIN(l.id)
FROM tmp_182_items i
JOIN tmp_l1_items l ON i.nameid = l.identified_name_id
GROUP BY i.set_item_uid, i.item_id;

DROP TEMPORARY TABLE IF EXISTS tmp_set_items;
CREATE TEMPORARY TABLE tmp_set_items (
  set_item_uid INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  sets VARCHAR(255) NOT NULL,
  item_count INT(10) UNSIGNED NOT NULL
);

INSERT INTO tmp_set_items (set_item_uid, sets, item_count)
SELECT set_item_uid,
       GROUP_CONCAT(l1_item_id ORDER BY l1_item_id SEPARATOR ',') AS sets,
       COUNT(*) AS item_count
FROM tmp_itemid_map
GROUP BY set_item_uid;

INSERT INTO l1jdb.armor_sets (
  id, note, sets, poly_id, ac, hp, mp, hpr, mpr,
  str, dex, con, wis, cha, `int`, sp, mr,
  damage_reduction, weight_reduction,
  hit_modifier, dmg_modifier, bow_hit_modifier, bow_dmg_modifier,
  defense_water, defense_wind, defense_fire, defense_earth,
  resist_stun, resist_stone, resist_sleep, resist_freeze, resist_hold, resist_blind,
  is_haste, exp_bonus, potion_recovery_rate
)
SELECT
  s.uid,
  s.name,
  si.sets,
  s.polymorph,
  s.add_ac,
  s.add_hp,
  s.add_mp,
  s.tic_hp,
  s.tic_mp,
  s.add_str,
  s.add_dex,
  s.add_con,
  s.add_wis,
  s.add_cha,
  s.add_int,
  0,
  s.add_mr,
  0,
  0,
  0,
  0,
  0,
  0,
  s.wateress,
  s.windress,
  s.fireress,
  s.earthress,
  0, 0, 0, 0, 0, 0,
  0, 0, 0
FROM `182`.items_setoption s
JOIN tmp_set_items si ON s.uid = si.set_item_uid;

-- ub_managers, towns, traps (182 DB has no tables; sourced from jpdbbackup)
TRUNCATE TABLE l1jdb.ub_managers;
TRUNCATE TABLE l1jdb.towns;
TRUNCATE TABLE l1jdb.traps;

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

INSERT INTO l1jdb.ub_managers (ub_id, npc_id, note)
SELECT u.ub_id, m.l1_id, u.note
FROM jpdbbackup.ub_managers u
JOIN tmp_npc_map m ON u.npc_id = m.jp_id;

INSERT INTO l1jdb.towns (
  id, name, leader_id, tax_rate, tax_rate_reserved,
  sales_money, sales_money_yesterday, town_tax, town_fix_tax
)
SELECT
  id, name, leader_id, tax_rate, tax_rate_reserved,
  sales_money, sales_money_yesterday, town_tax, town_fix_tax
FROM jpdbbackup.towns;

INSERT INTO l1jdb.traps (
  id, note, type, gfx_id, is_detectionable, base, dice, dice_count,
  poison_type, poison_delay, poison_time, poison_damage,
  monster_npc_id, monster_count,
  teleport_x, teleport_y, teleport_map_id,
  skill_id, skill_time_seconds, switch_id
)
SELECT
  t.id,
  t.note,
  t.type,
  t.gfx_id,
  t.is_detectionable,
  t.base,
  t.dice,
  t.dice_count,
  t.poison_type,
  t.poison_delay,
  t.poison_time,
  t.poison_damage,
  CASE
    WHEN t.monster_npc_id = 0 THEN 0
    ELSE m.l1_id
  END AS monster_npc_id,
  t.monster_count,
  t.teleport_x,
  t.teleport_y,
  t.teleport_map_id,
  t.skill_id,
  t.skill_time_seconds,
  t.switch_id
FROM jpdbbackup.traps t
LEFT JOIN tmp_npc_map m ON t.monster_npc_id = m.jp_id
WHERE t.monster_npc_id = 0 OR m.l1_id IS NOT NULL;

SET FOREIGN_KEY_CHECKS=1;
