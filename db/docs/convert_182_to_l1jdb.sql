SET FOREIGN_KEY_CHECKS=0;
USE l1jdb;

TRUNCATE TABLE l1jdb.accelerator_logs;
TRUNCATE TABLE l1jdb.accounts;
TRUNCATE TABLE l1jdb.armor_sets;
TRUNCATE TABLE l1jdb.armors;
TRUNCATE TABLE l1jdb.auction_houses;
TRUNCATE TABLE l1jdb.ban_ips;
TRUNCATE TABLE l1jdb.beginner_items;
TRUNCATE TABLE l1jdb.board_posts;
TRUNCATE TABLE l1jdb.castles;
TRUNCATE TABLE l1jdb.character_bookmarks;
TRUNCATE TABLE l1jdb.character_buddys;
TRUNCATE TABLE l1jdb.character_buffs;
TRUNCATE TABLE l1jdb.character_configs;
TRUNCATE TABLE l1jdb.character_quests;
TRUNCATE TABLE l1jdb.character_skills;
TRUNCATE TABLE l1jdb.characters;
TRUNCATE TABLE l1jdb.chat_logs;
TRUNCATE TABLE l1jdb.clan_applies;
TRUNCATE TABLE l1jdb.clan_recommends;
TRUNCATE TABLE l1jdb.clan_warehouse_histories;
TRUNCATE TABLE l1jdb.clans;
TRUNCATE TABLE l1jdb.commands;
TRUNCATE TABLE l1jdb.cooking_ingredients;
TRUNCATE TABLE l1jdb.cooking_recipes;
TRUNCATE TABLE l1jdb.door_gfxs;
TRUNCATE TABLE l1jdb.drop_items;
TRUNCATE TABLE l1jdb.drop_rates;
TRUNCATE TABLE l1jdb.dungeons;
TRUNCATE TABLE l1jdb.enchant_logs;
TRUNCATE TABLE l1jdb.etc_items;
TRUNCATE TABLE l1jdb.houses;
TRUNCATE TABLE l1jdb.inn_keys;
TRUNCATE TABLE l1jdb.inns;
TRUNCATE TABLE l1jdb.inventory_items;
TRUNCATE TABLE l1jdb.item_rates;
TRUNCATE TABLE l1jdb.magic_dolls;
TRUNCATE TABLE l1jdb.mails;
TRUNCATE TABLE l1jdb.map_ids;
TRUNCATE TABLE l1jdb.map_timers;
TRUNCATE TABLE l1jdb.mob_groups;
TRUNCATE TABLE l1jdb.mob_skills;
TRUNCATE TABLE l1jdb.npc_actions;
TRUNCATE TABLE l1jdb.npc_chats;
TRUNCATE TABLE l1jdb.npc_teleport;
TRUNCATE TABLE l1jdb.npcs;
TRUNCATE TABLE l1jdb.pet_items;
TRUNCATE TABLE l1jdb.pet_types;
TRUNCATE TABLE l1jdb.pets;
TRUNCATE TABLE l1jdb.polymorphs;
TRUNCATE TABLE l1jdb.race_tickets;
TRUNCATE TABLE l1jdb.random_dungeons;
TRUNCATE TABLE l1jdb.resolvents;
TRUNCATE TABLE l1jdb.restart_locations;
TRUNCATE TABLE l1jdb.return_locations;
TRUNCATE TABLE l1jdb.shops;
TRUNCATE TABLE l1jdb.shutdown_requests;
TRUNCATE TABLE l1jdb.skills;
TRUNCATE TABLE l1jdb.spawn_boss_mobs;
TRUNCATE TABLE l1jdb.spawn_doors;
TRUNCATE TABLE l1jdb.spawn_furnitures;
TRUNCATE TABLE l1jdb.spawn_lights;
TRUNCATE TABLE l1jdb.spawn_mobs;
TRUNCATE TABLE l1jdb.spawn_npcs;
TRUNCATE TABLE l1jdb.spawn_times;
TRUNCATE TABLE l1jdb.spawn_traps;
TRUNCATE TABLE l1jdb.spawn_ub_mobs;
TRUNCATE TABLE l1jdb.spr_actions;
TRUNCATE TABLE l1jdb.towns;
TRUNCATE TABLE l1jdb.traps;
TRUNCATE TABLE l1jdb.ub_managers;
TRUNCATE TABLE l1jdb.ub_times;
TRUNCATE TABLE l1jdb.ubs;
TRUNCATE TABLE l1jdb.weapon_skills;
TRUNCATE TABLE l1jdb.weapons;

SET @npc_collision_max := 1000;
SET @npc_offset := 100000;

INSERT INTO l1jdb.accounts (
  id, name, password, access_level, character_slot, last_activated_at, ip, host, is_active
)
SELECT
  uid,
  id,
  pw,
  status,
  0,
  NULLIF(logins_date, '0000-00-00 00:00:00'),
  last_ip,
  NULL,
  CASE WHEN status = 0 THEN 1 ELSE 0 END
FROM `182`.account;

DROP TEMPORARY TABLE IF EXISTS tmp_chars;
CREATE TEMPORARY TABLE tmp_chars AS
SELECT
  c.objID AS id,
  c.account_uid AS account_id,
  c.name,
  NULL AS birthday,
  c.level,
  c.level AS high_level,
  LEAST(c.exp, 2147483647) AS exp,
  c.maxHP AS max_hp,
  c.nowHP AS cur_hp,
  c.maxMP AS max_mp,
  c.nowMP AS cur_mp,
  c.ac,
  c.str,
  c.con,
  c.dex,
  c.cha,
  c.inter AS `int`,
  c.wis,
  0 AS status,
  c.class,
  c.sex,
  0 AS type,
  c.gfx_mode AS heading,
  c.locX AS loc_x,
  c.locY AS loc_y,
  c.locMAP AS map_id,
  c.food,
  c.lawful,
  c.title,
  c.clanID AS clan_id,
  c.clanNAME AS clan_name,
  0 AS clan_rank,
  0 AS bonus_status,
  0 AS elixir_status,
  c.elf_attr,
  c.pkcount AS pk_count,
  0 AS pk_count_for_elf,
  0 AS exp_res,
  0 AS partner_id,
  0 AS access_level,
  0 AS online_status,
  0 AS hometown_id,
  0 AS contribution,
  0 AS pay,
  0 AS hell_time,
  1 AS is_active,
  0 AS karma,
  NULL AS last_pk,
  NULL AS last_pk_for_elf,
  NULL AS delete_time,
  NULL AS rejoin_clan_time,
  c.str AS original_str,
  c.con AS original_con,
  c.dex AS original_dex,
  c.cha AS original_cha,
  c.inter AS original_int,
  c.wis AS original_wis,
  0 AS use_additional_warehouse,
  NULL AS logout_time
FROM `182`.characters c
JOIN (
  SELECT objID, MAX(exp) AS max_exp
  FROM `182`.characters
  GROUP BY objID
) m ON c.objID = m.objID AND c.exp = m.max_exp
GROUP BY c.objID;

INSERT INTO l1jdb.characters
SELECT * FROM tmp_chars;

INSERT INTO l1jdb.character_bookmarks (char_id, name, loc_x, loc_y, map_id)
SELECT
  char_id,
  location,
  loc_x,
  loc_y,
  loc_map
FROM `182`.characters_books;

INSERT IGNORE INTO l1jdb.character_skills (char_id, skill_id, skill_name, is_active, active_time_left)
SELECT
  char_id,
  skill_id,
  skill_name,
  0,
  0
FROM `182`.characters_skills;

INSERT IGNORE INTO l1jdb.character_buffs (char_id, skill_id, remaining_time, poly_id, attr_kind)
SELECT
  char_id,
  tid,
  ttime,
  0,
  0
FROM `182`.characters_buffs
WHERE type = 'skill';

INSERT IGNORE INTO l1jdb.character_quests (char_id, quest_id, quest_step)
SELECT
  char_id,
  quest_id,
  quest_step
FROM `182`.characters_quests;

SET @inv_id := 0;
INSERT INTO l1jdb.inventory_items (
  id, owner_id, location, item_id, item_count, is_equipped, enchant_level, is_identified,
  durability, charge_count, charge_time, expiration_time, last_used, is_sealed, is_protected,
  protect_item_id, attr_enchant_kind, attr_enchant_level, ac, str, con, dex, wis, cha, `int`,
  hp, hpr, mp, mpr, mr, sp, hit_modifier, dmg_modifier, bow_hit_modifier, bow_dmg_modifier,
  defense_earth, defense_water, defense_fire, defense_wind, resist_stun, resist_stone,
  resist_sleep, resist_freeze, resist_hold, resist_blind, exp_bonus, is_haste, can_be_dmg,
  is_unique, potion_recovery_rate
)
SELECT
  (@inv_id := @inv_id + 1) AS id,
  owner_id,
  location,
  item_id,
  item_count,
  is_equipped,
  enchant_level,
  is_identified,
  durability,
  charge_count,
  charge_time,
  NULL AS expiration_time,
  NULL AS last_used,
  0 AS is_sealed,
  0 AS is_protected,
  0 AS protect_item_id,
  0 AS attr_enchant_kind,
  0 AS attr_enchant_level,
  0 AS ac,
  0 AS str,
  0 AS con,
  0 AS dex,
  0 AS wis,
  0 AS cha,
  0 AS `int`,
  0 AS hp,
  0 AS hpr,
  0 AS mp,
  0 AS mpr,
  0 AS mr,
  0 AS sp,
  0 AS hit_modifier,
  0 AS dmg_modifier,
  0 AS bow_hit_modifier,
  0 AS bow_dmg_modifier,
  0 AS defense_earth,
  0 AS defense_water,
  0 AS defense_fire,
  0 AS defense_wind,
  0 AS resist_stun,
  0 AS resist_stone,
  0 AS resist_sleep,
  0 AS resist_freeze,
  0 AS resist_hold,
  0 AS resist_blind,
  0 AS exp_bonus,
  0 AS is_haste,
  1 AS can_be_dmg,
  0 AS is_unique,
  0 AS potion_recovery_rate
FROM (
  SELECT
    char_id AS owner_id,
    0 AS location,
    item_id,
    IFNULL(count, 0) AS item_count,
    IFNULL(equipped, 0) AS is_equipped,
    IFNULL(en, 0) AS enchant_level,
    IFNULL(definite, 0) AS is_identified,
    IFNULL(durability, 0) AS durability,
    IFNULL(have_count, 0) AS charge_count,
    IFNULL(time, 0) AS charge_time
  FROM `182`.characters_inventory
  WHERE char_id IS NOT NULL AND char_id <> 0

  UNION ALL

  SELECT
    account_uid AS owner_id,
    1 AS location,
    id AS item_id,
    IFNULL(count, 0) AS item_count,
    0 AS is_equipped,
    IFNULL(en, 0) AS enchant_level,
    IFNULL(definite, 0) AS is_identified,
    IFNULL(durability, 0) AS durability,
    IFNULL(have_count, 0) AS charge_count,
    IFNULL(time, 0) AS charge_time
  FROM `182`.warehouse

  UNION ALL

  SELECT
    account_uid AS owner_id,
    2 AS location,
    id AS item_id,
    IFNULL(count, 0) AS item_count,
    0 AS is_equipped,
    IFNULL(en, 0) AS enchant_level,
    IFNULL(definite, 0) AS is_identified,
    IFNULL(durability, 0) AS durability,
    IFNULL(have_count, 0) AS charge_count,
    IFNULL(time, 0) AS charge_time
  FROM `182`.warehouse_elf

  UNION ALL

  SELECT
    clan_id AS owner_id,
    3 AS location,
    id AS item_id,
    IFNULL(count, 0) AS item_count,
    0 AS is_equipped,
    IFNULL(en, 0) AS enchant_level,
    IFNULL(definite, 0) AS is_identified,
    IFNULL(durability, 0) AS durability,
    IFNULL(have_count, 0) AS charge_count,
    IFNULL(time, 0) AS charge_time
  FROM `182`.warehouse_clan
) t;

INSERT INTO l1jdb.pets (item_obj_id, id, name, npc_id, level, hp, mp, exp, lawful, food)
SELECT
  id,
  id,
  name,
  classId,
  level,
  nowHp,
  nowMp,
  exp,
  lawful,
  CAST(food AS UNSIGNED)
FROM `182`.characters_pet
WHERE del = 0;

INSERT INTO l1jdb.clans (id, name, leader_id, castle_id, house_id, created_at)
SELECT
  ClanId,
  ClanName,
  IFNULL((SELECT objID FROM `182`.characters c WHERE c.name = clan_list.lord LIMIT 1), 0),
  0,
  0,
  NOW()
FROM `182`.clan_list;

INSERT IGNORE INTO l1jdb.ban_ips (ip, host, mask)
SELECT ip, NULL, 32 FROM `182`.ban_list;

INSERT INTO l1jdb.board_posts (id, name, date, title, content)
SELECT id, name, days, subject, memo FROM `182`.board;

INSERT INTO l1jdb.mails (id, type, sender, receiver, date, read_status, inbox_id, subject, content)
SELECT
  uid,
  0,
  paperFrom,
  paperTo,
  STR_TO_DATE(CONCAT(paperYear, '-', paperMonth, '-', paperDate), '%Y-%m-%d'),
  0,
  0,
  paperSubject,
  paperMemo
FROM `182`.characters_letter;

INSERT INTO l1jdb.castles (id, name, war_time, tax_rate, public_money)
SELECT
  id,
  name,
  NULLIF(war_day, '0000-00-00 00:00:00'),
  tax,
  tax_total
FROM `182`.kingdom;

INSERT INTO l1jdb.dungeons (src_x, src_y, src_map_id, new_x, new_y, new_map_id, new_heading, note)
SELECT
  locX,
  locY,
  locM,
  MIN(gotoX),
  MIN(gotoY),
  MIN(gotoM),
  MIN(gotoH),
  ''
FROM `182`.dungeon
GROUP BY locX, locY, locM;

INSERT INTO l1jdb.restart_locations (area, loc_x, loc_y, map_id, note)
SELECT area, locx, locy, mapid, note FROM `182`.getback_restart;

INSERT INTO l1jdb.npc_teleport (action, npc_id, tele_num, check_lv_min, check_lv_max, check_map, x, y, map, aden)
SELECT
  action,
  CASE WHEN npc_id <= @npc_collision_max THEN npc_id + @npc_offset ELSE npc_id END,
  tele_num,
  check_lv_min,
  check_lv_max,
  check_map,
  x,
  y,
  map,
  aden
FROM `182`.npc_teleport;

INSERT INTO l1jdb.drop_items (npc_id, item_id, note, min, max, chance)
SELECT
  monid,
  itemid,
  MAX(name),
  MIN(count_min),
  MAX(count_max),
  MAX(chance)
FROM `182`.monster_item_drop
GROUP BY monid, itemid;

INSERT INTO l1jdb.spawn_mobs (
  npc_id, note, group_id, count, loc_x, loc_y, random_x, random_y, loc_x1, loc_y1, loc_x2, loc_y2,
  heading, min_respawn_delay, max_respawn_delay, map_id, respawn_screen, movement_distance, rest, near_spawn
)
SELECT
  monster,
  name,
  0,
  count,
  spawn_x,
  spawn_y,
  CASE WHEN random = 'true' THEN loc_size ELSE 0 END,
  CASE WHEN random = 'true' THEN loc_size ELSE 0 END,
  0,
  0,
  0,
  0,
  0,
  re_spawn,
  re_spawn,
  spawn_map,
  0,
  0,
  0,
  0
FROM `182`.monster_spawnlist;

INSERT INTO l1jdb.spawn_npcs (npc_id, note, count, loc_x, loc_y, random_x, random_y, heading, respawn_delay, map_id, movement_distance)
SELECT
  CASE WHEN npcID <= @npc_collision_max THEN npcID + @npc_offset ELSE npcID END,
  name,
  1,
  locX,
  locY,
  0,
  0,
  heading,
  respawn,
  locMap,
  0
FROM `182`.npc_spawnlist;

SET @shop_row := 0;
SET @shop_npc := -1;
INSERT INTO l1jdb.shops (npc_id, item_id, note, order_id, pack_count)
SELECT
  CASE WHEN npcid <= @npc_collision_max THEN npcid + @npc_offset ELSE npcid END,
  itemid,
  NULL,
  order_id,
  CASE WHEN itemcount > 0 THEN itemcount ELSE 1 END
FROM (
  SELECT
    npcid,
    itemid,
    itemcount,
    CASE
      WHEN @shop_npc = npcid THEN @shop_row := @shop_row + 1
      ELSE @shop_row := 0
    END AS order_id,
    @shop_npc := npcid AS _npc
  FROM `182`.npc_shop
  ORDER BY npcid, uid
) s;

INSERT INTO l1jdb.item_rates (item_id, note, selling_price, purchasing_price)
SELECT
  itemid,
  (SELECT i.name FROM `182`.items i WHERE i.item_id = npc_shop.itemid LIMIT 1),
  MIN(price),
  -1
FROM `182`.npc_shop
GROUP BY itemid;

INSERT INTO l1jdb.skills (
  id, name, skill_level, skill_number, consume_mp, consume_hp, consume_item_id, consume_amount,
  reuse_delay, buff_duration, target, target_to, damage_value, damage_dice, damage_dice_count,
  probability_value, probability_dice, probability_max, attr, type, lawful, ranged, area, through,
  skill_id, name_id, action_id, cast_gfx, cast_gfx2, sys_msg_id_happen, sys_msg_id_stop, sys_msg_id_fail,
  can_cast_with_invis, ignores_counter_magic, is_buff, impl
)
SELECT
  skill_id,
  name,
  skill_level,
  skill_no,
  mp_consume,
  hp_consume,
  item_consume,
  item_consume_count,
  reuse_delay,
  buff_duration,
  CASE
    WHEN type = 'buff' THEN 'buff'
    WHEN type = 'attack' THEN 'attack'
    ELSE ''
  END,
  0,
  min_dmg,
  CASE WHEN max_dmg > min_dmg THEN max_dmg - min_dmg ELSE 0 END,
  CASE WHEN max_dmg > min_dmg THEN 1 ELSE 0 END,
  0,
  0,
  -1,
  attr,
  CASE
    WHEN type = 'attack' THEN 64
    WHEN type = 'buff' THEN 2
    ELSE 128
  END,
  lawful_consume,
  `range`,
  0,
  0,
  id,
  name,
  0,
  cast_gfx,
  -1,
  0,
  0,
  0,
  0,
  0,
  CASE WHEN type = 'buff' THEN 1 ELSE 0 END,
  NULL
FROM `182`.skill_list;

INSERT INTO l1jdb.polymorphs (id, name, gfx_id, min_level, weapon_equip, armor_equip, can_use_skill, cause)
SELECT
  polyid,
  MAX(name),
  polyid,
  MIN(minlevel),
  MAX(CASE WHEN isWeapon = 1 THEN 2047 ELSE 0 END),
  MAX(
    (CASE WHEN isHelm = 1 THEN 1 ELSE 0 END)
    + (CASE WHEN isNecklace = 1 THEN 2 ELSE 0 END)
    + (CASE WHEN isEarring = 1 THEN 4 ELSE 0 END)
    + (CASE WHEN isT = 1 THEN 8 ELSE 0 END)
    + (CASE WHEN isArmor = 1 THEN 16 ELSE 0 END)
    + (CASE WHEN isCloak = 1 THEN 32 ELSE 0 END)
    + (CASE WHEN isBelt = 1 THEN 64 ELSE 0 END)
    + (CASE WHEN isShield = 1 THEN 128 ELSE 0 END)
    + (CASE WHEN isGlove = 1 THEN 256 ELSE 0 END)
    + (CASE WHEN isRing = 1 THEN 512 ELSE 0 END)
    + (CASE WHEN isBoots = 1 THEN 1024 ELSE 0 END)
  ),
  1,
  7
FROM `182`.polymorph
GROUP BY polyid;

INSERT INTO l1jdb.npcs (
  id, name, name_id, note, impl, gfx_id, level, hp, mp, ac, str, con, dex, wis, `int`, mr, exp, lawful,
  size, weak_attr, ranged, tamable, move_speed, atk_speed, alt_atk_speed, atk_magic_speed, sub_magic_speed,
  undead, poison_atk, paralysis_atk, agro, agro_sosc, agro_coi, family, agro_family, agro_gfx_id1, agro_gfx_id2,
  pickup_item, digest_item, brave_speed, hpr_interval, hpr, mpr_interval, mpr, teleport, random_level, random_hp,
  random_mp, random_ac, random_exp, random_lawful, damage_reduction, hard, doppel, enable_tu, enable_erase,
  bow_act_id, karma, transform_id, transform_gfx_id, light_size, amount_fixed, change_head, cant_resurrect,
  is_equality_drop, boss
)
SELECT
  uid,
  name,
  name_id,
  name,
  'L1Monster',
  gfx,
  level,
  hp,
  mp,
  ac,
  0,
  0,
  0,
  0,
  0,
  mr,
  exp,
  lawful,
  size,
  0,
  0,
  CASE WHEN tameable > 0 THEN 1 ELSE 0 END,
  0,
  0,
  0,
  0,
  0,
  undead,
  0,
  0,
  CASE WHEN agro > 0 THEN 1 ELSE 0 END,
  0,
  0,
  NULL,
  0,
  -1,
  -1,
  CASE WHEN item_pick > 0 THEN 1 ELSE 0 END,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0
FROM `182`.monster

UNION ALL

SELECT
  CASE WHEN npcid <= @npc_collision_max THEN npcid + @npc_offset ELSE npcid END,
  name,
  nameid,
  name,
  CASE
    WHEN type = 'Shop' THEN 'L1Merchant'
    WHEN type = 'PetShop' THEN 'L1Merchant'
    WHEN type = 'Dwarf' THEN 'L1Dwarf'
    WHEN type = 'Guard' THEN 'L1Guard'
    WHEN type = 'ElfGuard' THEN 'L1Guard'
    WHEN type = 'PatrolGuard' THEN 'L1Guard'
    WHEN type = 'Teleport' THEN 'L1Teleporter'
    WHEN type = 'Inn' THEN 'L1Housekeeper'
    WHEN type = 'Agit' THEN 'L1Housekeeper'
    WHEN type = 'Board' THEN 'L1Board'
    WHEN type = 'Sign' THEN 'L1Signboard'
    WHEN type = 'Door' THEN 'L1Door'
    WHEN type = 'CastleDoor' THEN 'L1Door'
    WHEN type = 'DoorBaphomet' THEN 'L1Door'
    WHEN type = 'TrapBaphomet' THEN 'L1Trap'
    WHEN type = 'CastleTop' THEN 'L1Tower'
    WHEN type = 'Quest' THEN 'L1Quest'
    ELSE 'L1Npc'
  END,
  gfxid,
  1,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  lawful,
  'small',
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  NULL,
  0,
  -1,
  -1,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  -1,
  0,
  0,
  light,
  0,
  0,
  0,
  0,
  0
FROM `182`.npc;

UPDATE l1jdb.npcs
SET lawful = `int`,
    `int` = 0
WHERE impl <> 'L1Monster'
  AND lawful = 0
  AND `int` <> 0;

INSERT INTO l1jdb.weapons (
  id, name, unidentified_name_id, identified_name_id, type, is_twohanded, material, weight,
  inv_gfx_id, grd_gfx_id, item_desc_id, dmg_small, dmg_large, `range`, safe_enchant,
  use_royal, use_knight, use_wizard, use_elf, use_darkelf, use_dragonknight, use_illusionist,
  hit_modifier, dmg_modifier, str, con, dex, `int`, wis, cha, hp, mp, hpr, mpr, sp, mr,
  is_haste, double_dmg_chance, weakness_exposure, magic_dmg_modifier, can_be_dmg,
  min_level, max_level, bless, tradable, deletable, charge_time, expiration_time
)
SELECT
  item_id,
  name,
  nameid,
  nameid,
  CASE
    WHEN type = 'sword' THEN 'sword'
    WHEN type = 'twohand' THEN 'twohandsword'
    WHEN type = 'dagger' THEN 'dagger'
    WHEN type = 'bow' THEN 'bow'
    WHEN type = 'arrow' THEN 'arrow'
    WHEN type = 'spear' THEN 'spear'
    WHEN type = 'axe' THEN 'blunt'
    WHEN type = 'wand' THEN 'staff'
    ELSE 'sword'
  END,
  CASE WHEN two_hand > 0 THEN 1 ELSE 0 END,
  material,
  weight,
  inv_gfx_id,
  grd_gfx_id,
  effect_id,
  dmg_small,
  dmg_large,
  CASE
    WHEN type = 'bow' THEN 8
    WHEN type = 'spear' THEN 2
    ELSE 1
  END,
  safe_enchant,
  CASE WHEN royal > 0 THEN 1 ELSE 0 END,
  CASE WHEN knight > 0 THEN 1 ELSE 0 END,
  CASE WHEN mage > 0 THEN 1 ELSE 0 END,
  CASE WHEN elf > 0 THEN 1 ELSE 0 END,
  0,
  0,
  0,
  add_hit,
  add_dmg,
  add_str,
  add_con,
  add_dex,
  add_int,
  add_wis,
  add_cha,
  add_hp,
  add_mp,
  add_hpr,
  add_mpr,
  add_sp,
  add_mr,
  CASE WHEN haste > 0 THEN 1 ELSE 0 END,
  0,
  0,
  0,
  CASE WHEN can_be_dmg > 0 THEN 1 ELSE 0 END,
  minlvl,
  maxlvl,
  0,
  CASE WHEN trade > 0 THEN 1 ELSE 0 END,
  CASE WHEN `drop` > 0 THEN 1 ELSE 0 END,
  continuous,
  NULL
FROM `182`.items
WHERE type IN ('sword','twohand','dagger','bow','arrow','spear','axe','wand');

INSERT INTO l1jdb.armors (
  id, name, unidentified_name_id, identified_name_id, type, material, grade, weight,
  inv_gfx_id, grd_gfx_id, item_desc_id, ac, safe_enchant, use_royal, use_knight, use_wizard,
  use_elf, use_darkelf, use_dragonknight, use_illusionist, str, con, dex, `int`, wis, cha,
  hp, mp, hpr, mpr, sp, min_level, max_level, mr, is_haste, damage_reduction,
  weight_reduction, hit_modifier, dmg_modifier, bow_hit_modifier, bow_dmg_modifier, bless,
  tradable, deletable, charge_time, expiration_time, defense_water, defense_wind, defense_fire,
  defense_earth, resist_stun, resist_stone, resist_sleep, resist_freeze, resist_hold,
  resist_blind, exp_bonus, potion_recovery_rate
)
SELECT
  item_id,
  name,
  nameid,
  nameid,
  CASE
    WHEN type = 'armor' THEN 'armor'
    WHEN type = 'helm' THEN 'helm'
    WHEN type = 'shield' THEN 'shield'
    WHEN type = 'cloak' THEN 'cloak'
    WHEN type = 'boots' THEN 'boots'
    WHEN type = 'glove' THEN 'glove'
    WHEN type = 'shirt' THEN 't_shirt'
    WHEN type = 'amulet' THEN 'amulet'
    WHEN type = 'belt' THEN 'belt'
    WHEN type = 'accessory' THEN 'ring'
    ELSE 'armor'
  END,
  material,
  -1,
  weight,
  inv_gfx_id,
  grd_gfx_id,
  effect_id,
  add_ac,
  safe_enchant,
  CASE WHEN royal > 0 THEN 1 ELSE 0 END,
  CASE WHEN knight > 0 THEN 1 ELSE 0 END,
  CASE WHEN mage > 0 THEN 1 ELSE 0 END,
  CASE WHEN elf > 0 THEN 1 ELSE 0 END,
  0,
  0,
  0,
  add_str,
  add_con,
  add_dex,
  add_int,
  add_wis,
  add_cha,
  add_hp,
  add_mp,
  add_hpr,
  add_mpr,
  add_sp,
  minlvl,
  maxlvl,
  add_mr,
  CASE WHEN haste > 0 THEN 1 ELSE 0 END,
  0,
  0,
  add_hit,
  add_dmg,
  0,
  0,
  0,
  CASE WHEN trade > 0 THEN 1 ELSE 0 END,
  CASE WHEN `drop` > 0 THEN 1 ELSE 0 END,
  continuous,
  NULL,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0,
  0
FROM `182`.items
WHERE type IN ('armor','helm','shield','cloak','boots','glove','shirt','amulet','belt','accessory');

INSERT INTO l1jdb.etc_items (
  id, name, unidentified_name_id, identified_name_id, item_type, use_type, material, weight,
  inv_gfx_id, grd_gfx_id, item_desc_id, stackable, max_charge_count, dmg_small, dmg_large,
  min_level, max_level, loc_x, loc_y, map_id, bless, tradable, deletable, sealable,
  delay_id, delay_time, delay_effect, food_volume, save_at_once, charge_time, expiration_time
)
SELECT
  item_id,
  name,
  nameid,
  nameid,
  CASE
    WHEN type = 'book' THEN 'spellbook'
    ELSE 'other'
  END,
  'none',
  material,
  weight,
  inv_gfx_id,
  grd_gfx_id,
  effect_id,
  CASE WHEN piles > 0 THEN 1 ELSE 0 END,
  0,
  dmg_small,
  dmg_large,
  minlvl,
  maxlvl,
  0,
  0,
  0,
  0,
  CASE WHEN trade > 0 THEN 1 ELSE 0 END,
  CASE WHEN `drop` > 0 THEN 1 ELSE 0 END,
  0,
  0,
  0,
  0,
  0,
  1,
  continuous,
  NULL
FROM `182`.items
WHERE type NOT IN ('sword','twohand','dagger','bow','arrow','spear','axe','wand','armor','helm','shield','cloak','boots','glove','shirt','amulet','belt','accessory');

SET FOREIGN_KEY_CHECKS=1;
