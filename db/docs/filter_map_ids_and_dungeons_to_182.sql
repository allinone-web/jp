-- Build a temporary 182 map-id list from 182-era tables
DROP TABLE IF EXISTS l1jdb.map_ids_182;
CREATE TABLE l1jdb.map_ids_182 (
  id INT(10) UNSIGNED NOT NULL,
  PRIMARY KEY (id)
);

INSERT IGNORE INTO l1jdb.map_ids_182 (id)
SELECT locM FROM `182`.dungeon
UNION
SELECT gotoM FROM `182`.dungeon
UNION
SELECT mapid FROM `182`.getback_restart
UNION
SELECT locMap FROM `182`.npc_spawnlist
UNION
SELECT spawn_map FROM `182`.monster_spawnlist
UNION
SELECT map FROM `182`.npc_teleport;

-- Preview counts
SELECT COUNT(*) AS dungeons_not_in_182
  FROM l1jdb.dungeons
 WHERE src_map_id NOT IN (SELECT id FROM l1jdb.map_ids_182)
    OR new_map_id NOT IN (SELECT id FROM l1jdb.map_ids_182);

SELECT COUNT(*) AS map_ids_not_in_182
  FROM l1jdb.map_ids
 WHERE id NOT IN (SELECT id FROM l1jdb.map_ids_182);

-- Delete dungeons not in 182 maps
DELETE FROM l1jdb.dungeons
 WHERE src_map_id NOT IN (SELECT id FROM l1jdb.map_ids_182)
    OR new_map_id NOT IN (SELECT id FROM l1jdb.map_ids_182);

-- Delete map_ids not in 182
DELETE FROM l1jdb.map_ids
 WHERE id NOT IN (SELECT id FROM l1jdb.map_ids_182);

DROP TABLE IF EXISTS l1jdb.map_ids_182;
