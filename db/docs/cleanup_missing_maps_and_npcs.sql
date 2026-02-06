-- Remove spawns that reference NPCs not in npcs table
DELETE s FROM spawn_npcs s LEFT JOIN npcs n ON s.npc_id = n.id WHERE n.id IS NULL;
DELETE s FROM spawn_mobs s LEFT JOIN npcs n ON s.npc_id = n.id WHERE n.id IS NULL;
DELETE s FROM spawn_boss_mobs s LEFT JOIN npcs n ON s.npc_id = n.id WHERE n.id IS NULL;

-- Remove rows that reference missing map_ids (DB map list)
DELETE FROM spawn_mobs WHERE map_id NOT IN (SELECT id FROM map_ids);
DELETE FROM spawn_npcs WHERE map_id NOT IN (SELECT id FROM map_ids);
DELETE FROM spawn_boss_mobs WHERE map_id NOT IN (SELECT id FROM map_ids);
DELETE FROM spawn_doors WHERE map_id NOT IN (SELECT id FROM map_ids);
DELETE FROM spawn_lights WHERE map_id NOT IN (SELECT id FROM map_ids);
DELETE FROM spawn_traps WHERE map_id NOT IN (SELECT id FROM map_ids);
DELETE FROM map_timers WHERE map_id NOT IN (SELECT id FROM map_ids);

DELETE FROM dungeons
 WHERE src_map_id NOT IN (SELECT id FROM map_ids)
    OR new_map_id NOT IN (SELECT id FROM map_ids);

DELETE FROM return_locations
 WHERE area_map_id NOT IN (SELECT id FROM map_ids)
    OR getback_map_id NOT IN (SELECT id FROM map_ids);

DELETE FROM random_dungeons
 WHERE src_map_id NOT IN (SELECT id FROM map_ids)
    OR (new_map_id1 <> 0 AND new_map_id1 NOT IN (SELECT id FROM map_ids))
    OR (new_map_id2 <> 0 AND new_map_id2 NOT IN (SELECT id FROM map_ids))
    OR (new_map_id3 <> 0 AND new_map_id3 NOT IN (SELECT id FROM map_ids))
    OR (new_map_id4 <> 0 AND new_map_id4 NOT IN (SELECT id FROM map_ids))
    OR (new_map_id5 <> 0 AND new_map_id5 NOT IN (SELECT id FROM map_ids));
