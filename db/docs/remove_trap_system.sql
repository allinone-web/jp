-- Remove any spawns that reference trap-type NPCs
DELETE FROM spawn_npcs WHERE npc_id IN (SELECT id FROM npcs WHERE impl = 'L1Trap');
DELETE FROM spawn_mobs WHERE npc_id IN (SELECT id FROM npcs WHERE impl = 'L1Trap');
DELETE FROM spawn_boss_mobs WHERE npc_id IN (SELECT id FROM npcs WHERE impl = 'L1Trap');

-- Remove trap-type NPC templates
DELETE FROM npcs WHERE impl = 'L1Trap';

-- Remove trap placement and definitions
DROP TABLE IF EXISTS spawn_traps;
DROP TABLE IF EXISTS traps;
