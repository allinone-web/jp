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

-- Preview counts (optional)
SELECT COUNT(*) AS map_ids_not_in_182
  FROM l1jdb.map_ids
 WHERE id NOT IN (SELECT id FROM l1jdb.map_ids_182);

-- Remove rows referencing non-182 map ids
DELETE FROM l1jdb.spawn_mobs
 WHERE map_id NOT IN (SELECT id FROM l1jdb.map_ids_182);

DELETE FROM l1jdb.spawn_npcs
 WHERE map_id NOT IN (SELECT id FROM l1jdb.map_ids_182);

DELETE FROM l1jdb.spawn_boss_mobs
 WHERE map_id NOT IN (SELECT id FROM l1jdb.map_ids_182);

DELETE FROM l1jdb.spawn_doors
 WHERE map_id NOT IN (SELECT id FROM l1jdb.map_ids_182);

DELETE FROM l1jdb.spawn_lights
 WHERE map_id NOT IN (SELECT id FROM l1jdb.map_ids_182);

DELETE FROM l1jdb.map_timers
 WHERE map_id NOT IN (SELECT id FROM l1jdb.map_ids_182);

DELETE FROM l1jdb.dungeons
 WHERE src_map_id NOT IN (SELECT id FROM l1jdb.map_ids_182)
    OR new_map_id NOT IN (SELECT id FROM l1jdb.map_ids_182);

DELETE FROM l1jdb.return_locations
 WHERE area_map_id NOT IN (SELECT id FROM l1jdb.map_ids_182)
    OR (getback_map_id NOT IN (SELECT id FROM l1jdb.map_ids_182) AND getback_map_id <> 0);

DELETE FROM l1jdb.restart_locations
 WHERE map_id NOT IN (SELECT id FROM l1jdb.map_ids_182);

DELETE FROM l1jdb.random_dungeons
 WHERE src_map_id NOT IN (SELECT id FROM l1jdb.map_ids_182)
    OR (new_map_id1 <> 0 AND new_map_id1 NOT IN (SELECT id FROM l1jdb.map_ids_182))
    OR (new_map_id2 <> 0 AND new_map_id2 NOT IN (SELECT id FROM l1jdb.map_ids_182))
    OR (new_map_id3 <> 0 AND new_map_id3 NOT IN (SELECT id FROM l1jdb.map_ids_182))
    OR (new_map_id4 <> 0 AND new_map_id4 NOT IN (SELECT id FROM l1jdb.map_ids_182))
    OR (new_map_id5 <> 0 AND new_map_id5 NOT IN (SELECT id FROM l1jdb.map_ids_182));

-- Remove non-182 map_ids
DELETE FROM l1jdb.map_ids
 WHERE id NOT IN (SELECT id FROM l1jdb.map_ids_182);

DROP TABLE IF EXISTS l1jdb.map_ids_182;
