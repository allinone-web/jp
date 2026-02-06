SET FOREIGN_KEY_CHECKS=0;
USE l1jdb;

TRUNCATE TABLE l1jdb.pet_types;
TRUNCATE TABLE l1jdb.pet_items;

DROP TEMPORARY TABLE IF EXISTS tmp_pet_npc_map;
CREATE TEMPORARY TABLE tmp_pet_npc_map (
  jp_id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  l1_id INT(10) UNSIGNED NOT NULL,
  KEY (l1_id)
);

INSERT INTO tmp_pet_npc_map (jp_id, l1_id)
SELECT j.id, MIN(l.id)
FROM jpdbbackup.npcs j
JOIN l1jdb.npcs l
  ON j.name_id = l.name_id
 AND j.gfx_id = l.gfx_id
GROUP BY j.id;

DROP TEMPORARY TABLE IF EXISTS tmp_pet_npc_map_transform;
CREATE TEMPORARY TABLE tmp_pet_npc_map_transform LIKE tmp_pet_npc_map;
INSERT INTO tmp_pet_npc_map_transform SELECT * FROM tmp_pet_npc_map;

DROP TEMPORARY TABLE IF EXISTS tmp_item_map;
CREATE TEMPORARY TABLE tmp_item_map (
  jp_item_id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  l1_item_id INT(10) UNSIGNED NOT NULL,
  KEY (l1_item_id)
);

DROP TEMPORARY TABLE IF EXISTS tmp_jp_items;
CREATE TEMPORARY TABLE tmp_jp_items (
  id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  identified_name_id VARCHAR(100) NOT NULL,
  KEY (identified_name_id)
);

INSERT INTO tmp_jp_items (id, identified_name_id)
SELECT id, identified_name_id FROM jpdbbackup.etc_items;
INSERT INTO tmp_jp_items (id, identified_name_id)
SELECT id, identified_name_id FROM jpdbbackup.weapons;
INSERT INTO tmp_jp_items (id, identified_name_id)
SELECT id, identified_name_id FROM jpdbbackup.armors;

DROP TEMPORARY TABLE IF EXISTS tmp_l1_items;
CREATE TEMPORARY TABLE tmp_l1_items (
  id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  identified_name_id VARCHAR(100) NOT NULL,
  KEY (identified_name_id)
);

INSERT INTO tmp_l1_items (id, identified_name_id)
SELECT id, identified_name_id FROM l1jdb.etc_items;
INSERT INTO tmp_l1_items (id, identified_name_id)
SELECT id, identified_name_id FROM l1jdb.weapons;
INSERT INTO tmp_l1_items (id, identified_name_id)
SELECT id, identified_name_id FROM l1jdb.armors;

INSERT INTO tmp_item_map (jp_item_id, l1_item_id)
SELECT jp.id, MIN(l.id)
FROM tmp_jp_items jp
JOIN tmp_l1_items l ON jp.identified_name_id = l.identified_name_id
GROUP BY jp.id;

DROP TEMPORARY TABLE IF EXISTS tmp_item_map_tame;
CREATE TEMPORARY TABLE tmp_item_map_tame LIKE tmp_item_map;
INSERT INTO tmp_item_map_tame SELECT * FROM tmp_item_map;

DROP TEMPORARY TABLE IF EXISTS tmp_item_map_transform;
CREATE TEMPORARY TABLE tmp_item_map_transform LIKE tmp_item_map;
INSERT INTO tmp_item_map_transform SELECT * FROM tmp_item_map;

INSERT INTO l1jdb.pet_types (
  npc_id, note, tame_item_id, min_hpup, max_hpup, min_mpup, max_mpup,
  transform_item_id, transform_npc_id,
  message_id1, message_id2, message_id3, message_id4, message_id5,
  defy_message_id, use_equipment
)
SELECT
  n.l1_id,
  p.note,
  COALESCE(t.l1_item_id, 0),
  p.min_hpup,
  p.max_hpup,
  p.min_mpup,
  p.max_mpup,
  COALESCE(t2.l1_item_id, 0),
  COALESCE(n2.l1_id, 0),
  p.message_id1,
  p.message_id2,
  p.message_id3,
  p.message_id4,
  p.message_id5,
  p.defy_message_id,
  p.use_equipment
FROM jpdbbackup.pet_types p
JOIN tmp_pet_npc_map n ON p.npc_id = n.jp_id
LEFT JOIN tmp_item_map_tame t ON p.tame_item_id = t.jp_item_id
LEFT JOIN tmp_item_map_transform t2 ON p.transform_item_id = t2.jp_item_id
LEFT JOIN tmp_pet_npc_map_transform n2 ON p.transform_npc_id = n2.jp_id;

INSERT INTO l1jdb.pet_items (
  item_id, note, hit_modifier, dmg_modifier, ac, str, con, dex, `int`, wis,
  hp, mp, sp, mr, use_type
)
SELECT
  m.l1_item_id,
  p.note,
  p.hit_modifier,
  p.dmg_modifier,
  p.ac,
  p.str,
  p.con,
  p.dex,
  p.`int`,
  p.wis,
  p.hp,
  p.mp,
  p.sp,
  p.mr,
  p.use_type
FROM jpdbbackup.pet_items p
JOIN tmp_item_map m ON p.item_id = m.jp_item_id;

SET FOREIGN_KEY_CHECKS=1;
