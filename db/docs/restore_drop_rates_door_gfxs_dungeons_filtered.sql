SET FOREIGN_KEY_CHECKS=0;
USE l1jdb;

-- drop_rates: keep only items that exist in 182 (mapped to l1jdb)
TRUNCATE TABLE l1jdb.drop_rates;

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

INSERT INTO l1jdb.drop_rates (item_id, note, drop_rate, drop_amount, unique_rate)
SELECT m.l1_item_id, MAX(r.note), MAX(r.drop_rate), MAX(r.drop_amount), MAX(r.unique_rate)
FROM jpdbbackup.drop_rates r
JOIN tmp_item_map m ON r.item_id = m.jp_item_id
GROUP BY m.l1_item_id;

-- door_gfxs: keep only gfx used by 182-filtered spawn_doors
TRUNCATE TABLE l1jdb.door_gfxs;
INSERT INTO l1jdb.door_gfxs (id, note, direction, left_edge_offset, right_edge_offset)
SELECT id, note, direction, left_edge_offset, right_edge_offset
FROM jpdbbackup.door_gfxs
WHERE id IN (SELECT DISTINCT gfx_id FROM l1jdb.spawn_doors);

-- dungeons: keep only 182 map_ids
TRUNCATE TABLE l1jdb.dungeons;
INSERT INTO l1jdb.dungeons (
  src_x, src_y, src_map_id, new_x, new_y, new_map_id, new_heading, note
)
SELECT
  d.src_x, d.src_y, d.src_map_id, d.new_x, d.new_y, d.new_map_id, d.new_heading, d.note
FROM jpdbbackup.dungeons d
JOIN l1jdb.map_ids ms ON d.src_map_id = ms.id
JOIN l1jdb.map_ids mn ON d.new_map_id = mn.id
WHERE d.src_x BETWEEN ms.start_x AND ms.end_x
  AND d.src_y BETWEEN ms.start_y AND ms.end_y
  AND d.new_x BETWEEN mn.start_x AND mn.end_x
  AND d.new_y BETWEEN mn.start_y AND mn.end_y;

SET FOREIGN_KEY_CHECKS=1;
