SET FOREIGN_KEY_CHECKS=0;
USE l1jdb;

-- ub_times / ubs from jpdbbackup (182 DB does not have these tables)
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
  id, name, map_id, area_x1, area_y1, area_x2, area_y2,
  min_level, max_level, max_player,
  enter_royal, enter_knight, enter_wizard, enter_elf, enter_darkelf,
  enter_dragonknight, enter_illusionist,
  enter_male, enter_female, use_pot, hpr_bonus, mpr_bonus
FROM jpdbbackup.ubs;

INSERT INTO l1jdb.ub_times (ub_id, ub_time)
SELECT ub_id, ub_time
FROM jpdbbackup.ub_times;

-- spr_actions add name (from 182.sprite_frame)
ALTER TABLE l1jdb.spr_actions
ADD COLUMN name varchar(255) NULL AFTER act_id;

UPDATE l1jdb.spr_actions sa
JOIN `182`.sprite_frame sf
  ON sa.spr_id = sf.gfx
 AND sa.act_id = sf.action
SET sa.name = sf.name;

-- fallback name from NPCs (by gfx_id)
UPDATE l1jdb.spr_actions sa
JOIN (
  SELECT gfx_id, MIN(name) AS name
  FROM l1jdb.npcs
  GROUP BY gfx_id
) n ON sa.spr_id = n.gfx_id
SET sa.name = n.name
WHERE sa.name IS NULL OR sa.name = '';

SET FOREIGN_KEY_CHECKS=1;
