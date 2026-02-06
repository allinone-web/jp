SET FOREIGN_KEY_CHECKS=0;
USE l1jdb;

-- ub_times / ubs: keep only ub_id that exist in l1jdb.ub_managers (182-era mapped NPCs)
TRUNCATE TABLE l1jdb.ub_times;
TRUNCATE TABLE l1jdb.ubs;

INSERT INTO l1jdb.ubs (
  id, name, map_id, area_x1, area_y1, area_x2, area_y2,
  min_level, max_level, max_player,
  enter_royal, enter_knight, enter_wizard, enter_elf, enter_darkelf,
  enter_dragonknight, enter_illusionist,
  enter_male, enter_female, use_pot, hpr_bonus, mpr_bonus
)
SELECT
  u.id, u.name, u.map_id, u.area_x1, u.area_y1, u.area_x2, u.area_y2,
  u.min_level, u.max_level, u.max_player,
  u.enter_royal, u.enter_knight, u.enter_wizard, u.enter_elf, u.enter_darkelf,
  u.enter_dragonknight, u.enter_illusionist,
  u.enter_male, u.enter_female, u.use_pot, u.hpr_bonus, u.mpr_bonus
FROM jpdbbackup.ubs u
WHERE u.id IN (SELECT DISTINCT ub_id FROM l1jdb.ub_managers);

INSERT INTO l1jdb.ub_times (ub_id, ub_time)
SELECT t.ub_id, t.ub_time
FROM jpdbbackup.ub_times t
JOIN l1jdb.ubs u ON t.ub_id = u.id;

-- resolvents: keep only items that exist in 182 (mapped to l1jdb)
TRUNCATE TABLE l1jdb.resolvents;

DROP TEMPORARY TABLE IF EXISTS tmp_jp_items;
CREATE TEMPORARY TABLE tmp_jp_items (
  id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  identified_name_id VARCHAR(255) NOT NULL,
  KEY (identified_name_id)
);

INSERT IGNORE INTO tmp_jp_items (id, identified_name_id)
SELECT id, identified_name_id FROM jpdbbackup.etc_items;
INSERT IGNORE INTO tmp_jp_items (id, identified_name_id)
SELECT id, identified_name_id FROM jpdbbackup.weapons;
INSERT IGNORE INTO tmp_jp_items (id, identified_name_id)
SELECT id, identified_name_id FROM jpdbbackup.armors;

DROP TEMPORARY TABLE IF EXISTS tmp_l1_items;
CREATE TEMPORARY TABLE tmp_l1_items (
  id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  identified_name_id VARCHAR(255) NOT NULL,
  KEY (identified_name_id)
);

INSERT IGNORE INTO tmp_l1_items (id, identified_name_id)
SELECT id, identified_name_id FROM l1jdb.etc_items;
INSERT IGNORE INTO tmp_l1_items (id, identified_name_id)
SELECT id, identified_name_id FROM l1jdb.weapons;
INSERT IGNORE INTO tmp_l1_items (id, identified_name_id)
SELECT id, identified_name_id FROM l1jdb.armors;

DROP TEMPORARY TABLE IF EXISTS tmp_item_map;
CREATE TEMPORARY TABLE tmp_item_map (
  jp_item_id INT(10) UNSIGNED NOT NULL PRIMARY KEY,
  l1_item_id INT(10) UNSIGNED NOT NULL,
  KEY (l1_item_id)
);

INSERT INTO tmp_item_map (jp_item_id, l1_item_id)
SELECT jp.id, MIN(l.id)
FROM tmp_jp_items jp
JOIN tmp_l1_items l ON jp.identified_name_id = l.identified_name_id
GROUP BY jp.id;

INSERT INTO l1jdb.resolvents (item_id, note, crystal_count)
SELECT m.l1_item_id, MAX(r.note), MAX(r.crystal_count)
FROM jpdbbackup.resolvents r
JOIN tmp_item_map m ON r.item_id = m.jp_item_id
GROUP BY m.l1_item_id;

SET FOREIGN_KEY_CHECKS=1;
