-- Build temporary 182 map-id set from 182-era tables
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
SELECT COUNT(*) AS jpdbbackup_total FROM jpdbbackup.return_locations;
SELECT COUNT(*) AS jpdbbackup_182_only
  FROM jpdbbackup.return_locations r
 WHERE r.area_map_id IN (SELECT id FROM l1jdb.map_ids_182)
   AND r.getback_map_id IN (SELECT id FROM l1jdb.map_ids_182);

-- Replace l1jdb.return_locations with 182-only rows from jpdbbackup
DELETE FROM l1jdb.return_locations;
INSERT INTO l1jdb.return_locations
SELECT r.*
  FROM jpdbbackup.return_locations r
 WHERE r.area_map_id IN (SELECT id FROM l1jdb.map_ids_182)
   AND r.getback_map_id IN (SELECT id FROM l1jdb.map_ids_182);

DROP TABLE IF EXISTS l1jdb.map_ids_182;
