<<<<<<< Updated upstream
/*
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

package jp.l1j.server.datatables;

import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.ArrayList;
import java.util.Collection;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import java.util.logging.Level;
import java.util.logging.Logger;
import static jp.l1j.locale.I18N.*;
import jp.l1j.server.model.L1World;
import jp.l1j.server.model.instance.L1ItemInstance;
import jp.l1j.server.templates.L1Armor;
import jp.l1j.server.templates.L1EtcItem;
import jp.l1j.server.templates.L1Item;
import jp.l1j.server.templates.L1Weapon;
import jp.l1j.server.utils.IdFactory;
import jp.l1j.server.utils.L1DatabaseFactory;
import jp.l1j.server.utils.PerformanceTimer;
import jp.l1j.server.utils.SqlUtil;

public class ItemTable {
	private static final long serialVersionUID = 1L;

	private static Logger _log = Logger.getLogger(ItemTable.class.getName());

	private static final Map<String, Integer> _armorTypes = new HashMap<String, Integer>();

	private static final Map<String, Integer> _weaponTypes = new HashMap<String, Integer>();

	private static final Map<String, Integer> _weaponId = new HashMap<String, Integer>();

	private static final Map<String, Integer> _materialTypes = new HashMap<String, Integer>();

	private static final Map<String, Integer> _etcItemTypes = new HashMap<String, Integer>();

	private static final Map<String, Integer> _useTypes = new HashMap<String, Integer>();

	private static ItemTable _instance;
	
	private static Map<Integer, L1Item> _allTemplates;

	private static Map<Integer, L1EtcItem> _allEtcItems;

	private static Map<Integer, L1Armor> _allArmors;

	private static Map<Integer, L1Weapon> _allWeapons;
	
	private static Map<String, Integer> _allNames;
	
	private static Map<String, Integer> _allWithoutSpaceNames;

	static {
		_etcItemTypes.put("arrow", Integer.valueOf(0));
		_etcItemTypes.put("wand", Integer.valueOf(1));
		_etcItemTypes.put("light", Integer.valueOf(2));
		_etcItemTypes.put("gem", Integer.valueOf(3));
		_etcItemTypes.put("totem", Integer.valueOf(4));
		_etcItemTypes.put("firecracker", Integer.valueOf(5));
		_etcItemTypes.put("potion", Integer.valueOf(6));
		_etcItemTypes.put("food", Integer.valueOf(7));
		_etcItemTypes.put("scroll", Integer.valueOf(8));
		_etcItemTypes.put("questitem", Integer.valueOf(9));
		_etcItemTypes.put("spellbook", Integer.valueOf(10));
		_etcItemTypes.put("petitem", Integer.valueOf(11));
		_etcItemTypes.put("other", Integer.valueOf(12));
		_etcItemTypes.put("material", Integer.valueOf(13));
		_etcItemTypes.put("event", Integer.valueOf(14));
		_etcItemTypes.put("sting", Integer.valueOf(15));
		_etcItemTypes.put("treasure_box", Integer.valueOf(16));
		_etcItemTypes.put("magic_doll", Integer.valueOf(17));
		_etcItemTypes.put("spellscroll", Integer.valueOf(18));
		_etcItemTypes.put("spellwand", Integer.valueOf(19));
		_etcItemTypes.put("spellicon", Integer.valueOf(20));
		_etcItemTypes.put("protect_scroll", Integer.valueOf(21));
		_etcItemTypes.put("unique_scroll", Integer.valueOf(22));

		_useTypes.put("none", Integer.valueOf(-1)); // 使用不可能
		_useTypes.put("normal", Integer.valueOf(0));
		_useTypes.put("weapon", Integer.valueOf(1));
		_useTypes.put("armor", Integer.valueOf(2));
		// _useTypes.put("wand1", Integer.valueOf(3));
		// _useTypes.put("wand", Integer.valueOf(4));
		// ワンドを振るアクションをとる(C_RequestExtraCommandが送られる)
		_useTypes.put("spell_long", Integer.valueOf(5)); // 地面 / オブジェクト選択(遠距離)
		_useTypes.put("ntele", Integer.valueOf(6));
		_useTypes.put("identify", Integer.valueOf(7));
		_useTypes.put("res", Integer.valueOf(8));
		_useTypes.put("choice", Integer.valueOf(14));
		_useTypes.put("instrument", Integer.valueOf(15));
		_useTypes.put("sosc", Integer.valueOf(16));
		_useTypes.put("spell_short", Integer.valueOf(17)); // 地面 / オブジェクト選択(近距離)
		_useTypes.put("t_shirt", Integer.valueOf(18));
		_useTypes.put("cloak", Integer.valueOf(19));
		_useTypes.put("glove", Integer.valueOf(20));
		_useTypes.put("boots", Integer.valueOf(21));
		_useTypes.put("helm", Integer.valueOf(22));
		_useTypes.put("ring", Integer.valueOf(23));
		_useTypes.put("amulet", Integer.valueOf(24));
		_useTypes.put("shield", Integer.valueOf(25));
		_useTypes.put("guarder", Integer.valueOf(25));
		_useTypes.put("dai", Integer.valueOf(26));
		_useTypes.put("zel", Integer.valueOf(27));
		_useTypes.put("blank", Integer.valueOf(28));
		_useTypes.put("btele", Integer.valueOf(29));
		_useTypes.put("spell_buff", Integer.valueOf(30)); // オブジェクト選択(遠距離)
		_useTypes.put("belt", Integer.valueOf(37));
		_useTypes.put("spell_point", Integer.valueOf(39)); // 地面選択
		_useTypes.put("earring", Integer.valueOf(40));
		_useTypes.put("fishing_rod", Integer.valueOf(42));
		_useTypes.put("pattern_back", Integer.valueOf(45));
		_useTypes.put("pattern_left", Integer.valueOf(44));
		_useTypes.put("pattern_right", Integer.valueOf(43));
		_useTypes.put("talisman_left", Integer.valueOf(47));
		_useTypes.put("talisman_right", Integer.valueOf(48));
		_useTypes.put("elixir", Integer.valueOf(58));
		_useTypes.put("healing", Integer.valueOf(59));
		_useTypes.put("cure", Integer.valueOf(60));
		_useTypes.put("haste", Integer.valueOf(61));
		_useTypes.put("brave", Integer.valueOf(62));
		_useTypes.put("third_speed", Integer.valueOf(63));
		_useTypes.put("magic_eye", Integer.valueOf(64));
		_useTypes.put("magic_healing", Integer.valueOf(65));
		_useTypes.put("bless_eva", Integer.valueOf(66));
		_useTypes.put("magic_regeneration", Integer.valueOf(67));
		_useTypes.put("wisdom", Integer.valueOf(68));
		_useTypes.put("flora", Integer.valueOf(69));
		_useTypes.put("poly", Integer.valueOf(70));
		_useTypes.put("npc_talk", Integer.valueOf(71));
		_useTypes.put("roulette", Integer.valueOf(72));
		_useTypes.put("teleport", Integer.valueOf(73));
		_useTypes.put("spawn", Integer.valueOf(74));
		_useTypes.put("furniture", Integer.valueOf(75));
		_useTypes.put("material", Integer.valueOf(77));
		_useTypes.put("extra", Integer.valueOf(78));

		_armorTypes.put("none", Integer.valueOf(0));
		_armorTypes.put("helm", Integer.valueOf(1));
		_armorTypes.put("armor", Integer.valueOf(2));
		_armorTypes.put("t_shirt", Integer.valueOf(3));
		_armorTypes.put("cloak", Integer.valueOf(4));
		_armorTypes.put("glove", Integer.valueOf(5));
		_armorTypes.put("boots", Integer.valueOf(6));
		_armorTypes.put("shield", Integer.valueOf(7));
		_armorTypes.put("guarder", Integer.valueOf(8));
		_armorTypes.put("amulet", Integer.valueOf(10));
		_armorTypes.put("ring", Integer.valueOf(11));
		_armorTypes.put("earring", Integer.valueOf(12));
		_armorTypes.put("belt", Integer.valueOf(13));
		_armorTypes.put("pattern_back", Integer.valueOf(14));
		_armorTypes.put("pattern_left", Integer.valueOf(15));
		_armorTypes.put("pattern_right", Integer.valueOf(16));
		_armorTypes.put("talisman_left", Integer.valueOf(17));
		_armorTypes.put("talisman_right", Integer.valueOf(18));

		_weaponTypes.put("sword", Integer.valueOf(1));
		_weaponTypes.put("twohandsword", Integer.valueOf(2));
		_weaponTypes.put("dagger", Integer.valueOf(3));
		_weaponTypes.put("bow", Integer.valueOf(4));
		_weaponTypes.put("arrow", Integer.valueOf(5));
		_weaponTypes.put("spear", Integer.valueOf(6));
		_weaponTypes.put("blunt", Integer.valueOf(7));
		_weaponTypes.put("staff", Integer.valueOf(8));
		_weaponTypes.put("claw", Integer.valueOf(9));
		_weaponTypes.put("dualsword", Integer.valueOf(10));
		_weaponTypes.put("gauntlet", Integer.valueOf(11));
		_weaponTypes.put("sting", Integer.valueOf(12));
		_weaponTypes.put("chainsword", Integer.valueOf(13));
		_weaponTypes.put("kiringku", Integer.valueOf(14));

		_weaponId.put("sword", Integer.valueOf(4));
		_weaponId.put("twohandsword", Integer.valueOf(50));
		_weaponId.put("dagger", Integer.valueOf(46));
		_weaponId.put("bow", Integer.valueOf(20));
		_weaponId.put("arrow", Integer.valueOf(66));
		_weaponId.put("spear", Integer.valueOf(24));
		_weaponId.put("blunt", Integer.valueOf(11));
		_weaponId.put("staff", Integer.valueOf(40));
		_weaponId.put("claw", Integer.valueOf(58));
		_weaponId.put("dualsword", Integer.valueOf(54));
		_weaponId.put("gauntlet", Integer.valueOf(62));
		_weaponId.put("sting", Integer.valueOf(2922));
		_weaponId.put("chainsword", Integer.valueOf(24));
		_weaponId.put("kiringku", Integer.valueOf(58));

		_materialTypes.put("none", Integer.valueOf(0));
		_materialTypes.put("liquid", Integer.valueOf(1));
		_materialTypes.put("web", Integer.valueOf(2));
		_materialTypes.put("vegetation", Integer.valueOf(3));
		_materialTypes.put("animalmatter", Integer.valueOf(4));
		_materialTypes.put("paper", Integer.valueOf(5));
		_materialTypes.put("cloth", Integer.valueOf(6));
		_materialTypes.put("leather", Integer.valueOf(7));
		_materialTypes.put("wood", Integer.valueOf(8));
		_materialTypes.put("bone", Integer.valueOf(9));
		_materialTypes.put("dragonscale", Integer.valueOf(10));
		_materialTypes.put("iron", Integer.valueOf(11));
		_materialTypes.put("steel", Integer.valueOf(12));
		_materialTypes.put("copper", Integer.valueOf(13));
		_materialTypes.put("silver", Integer.valueOf(14));
		_materialTypes.put("gold", Integer.valueOf(15));
		_materialTypes.put("platinum", Integer.valueOf(16));
		_materialTypes.put("mithril", Integer.valueOf(17));
		_materialTypes.put("blackmithril", Integer.valueOf(18));
		_materialTypes.put("glass", Integer.valueOf(19));
		_materialTypes.put("gemstone", Integer.valueOf(20));
		_materialTypes.put("mineral", Integer.valueOf(21));
		_materialTypes.put("orichalcum", Integer.valueOf(22));
	}

	public static ItemTable getInstance() {
		if (_instance == null) {
			_instance = new ItemTable();
		}
		return _instance;
	}

	private ItemTable() {
		load();
	}

	private Map<Integer, L1EtcItem> allEtcItem() {
		Map<Integer, L1EtcItem> result = new HashMap<Integer, L1EtcItem>();
		Connection con = null;
		PreparedStatement pstm = null;
		ResultSet rs = null;
		L1EtcItem item = null;
		try {
			con = L1DatabaseFactory.getInstance().getConnection();
			pstm = con.prepareStatement("select * from etc_items");
			rs = pstm.executeQuery();
			while (rs.next()) {
				item = new L1EtcItem();
				item.setItemId(rs.getInt("id"));
				item.setName(rs.getString("name"));
				item.setUnidentifiedNameId(rs.getString("unidentified_name_id"));
				item.setIdentifiedNameId(rs.getString("identified_name_id"));
				item.setType((_etcItemTypes.get(rs.getString("item_type"))).intValue());
				item.setUseType(_useTypes.get(rs.getString("use_type")).intValue());
				// item.setType1(0); // 使わない
				item.setType2(0);
				item.setMaterial((_materialTypes.get(rs.getString("material"))).intValue());
				item.setWeight(rs.getInt("weight"));
				item.setGfxId(rs.getInt("inv_gfx_id"));
				item.setGroundGfxId(rs.getInt("grd_gfx_id"));
				item.setItemDescId(rs.getInt("item_desc_id"));
				item.setMinLevel(rs.getInt("min_level"));
				item.setMaxLevel(rs.getInt("max_level"));
				item.setBless(rs.getInt("bless"));
				item.setTradable(rs.getInt("tradable") == 1 ? true : false);
				item.setDeletable(rs.getInt("deletable") == 1 ? true : false);
				item.setSealable(rs.getInt("sealable") == 1 ? true : false);
				item.setDmgSmall(rs.getInt("dmg_small"));
				item.setDmgLarge(rs.getInt("dmg_large"));
				item.setStackable(rs.getInt("stackable") == 1 ? true : false);
				item.setMaxChargeCount(rs.getInt("max_charge_count"));
				item.setLocX(rs.getInt("loc_x"));
				item.setLocY(rs.getInt("loc_y"));
				item.setMapId(rs.getShort("map_id"));
				item.setDelayId(rs.getInt("delay_id"));
				item.setDelayTime(rs.getInt("delay_time"));
				item.setDelayEffect(rs.getInt("delay_effect"));
				item.setFoodVolume(rs.getInt("food_volume"));
				item.setToBeSavedAtOnce((rs.getInt("save_at_once") == 1) ? true : false);
				item.setChargeTime(rs.getInt("charge_time"));
				item.setExpirationTime(rs.getString("expiration_time"));
				result.put(Integer.valueOf(item.getItemId()), item);
			}
		} catch (NullPointerException e) {
			_log.log(Level.SEVERE, String.format(I18N_LOAD_ITEM_FAILED, item.getName(), item.getItemId()));
		} catch (SQLException e) {
			_log.log(Level.SEVERE, e.getLocalizedMessage(), e);
		} finally {
			SqlUtil.close(rs);
			SqlUtil.close(pstm);
			SqlUtil.close(con);
		}
		return result;
	}

	private Map<Integer, L1Weapon> allWeapon() {
		Map<Integer, L1Weapon> result = new HashMap<Integer, L1Weapon>();
		Connection con = null;
		PreparedStatement pstm = null;
		ResultSet rs = null;
		L1Weapon weapon = null;
		try {
			con = L1DatabaseFactory.getInstance().getConnection();
			pstm = con.prepareStatement("select * from weapons");
			rs = pstm.executeQuery();
			while (rs.next()) {
				weapon = new L1Weapon();
				weapon.setItemId(rs.getInt("id"));
				weapon.setName(rs.getString("name"));
				weapon.setUnidentifiedNameId(rs.getString("unidentified_name_id"));
				weapon.setIdentifiedNameId(rs.getString("identified_name_id"));
				weapon.setType((_weaponTypes.get(rs.getString("type"))).intValue());
				weapon.setType1((_weaponId.get(rs.getString("type"))).intValue());
				weapon.setType2(1);
				weapon.setUseType(1);
				weapon.setIsTwohanded(rs.getBoolean("is_twohanded"));
				weapon.setMaterial((_materialTypes.get(rs.getString("material"))).intValue());
				weapon.setWeight(rs.getInt("weight"));
				weapon.setGfxId(rs.getInt("inv_gfx_id"));
				weapon.setGroundGfxId(rs.getInt("grd_gfx_id"));
				weapon.setItemDescId(rs.getInt("item_desc_id"));
				weapon.setDmgSmall(rs.getInt("dmg_small"));
				weapon.setDmgLarge(rs.getInt("dmg_large"));
				weapon.setRange(rs.getInt("range"));
				weapon.setSafeEnchant(rs.getInt("safe_enchant"));
				weapon.setUseRoyal(rs.getInt("use_royal") == 0 ? false : true);
				weapon.setUseKnight(rs.getInt("use_knight") == 0 ? false : true);
				weapon.setUseElf(rs.getInt("use_elf") == 0 ? false : true);
				weapon.setUseWizard(rs.getInt("use_wizard") == 0 ? false : true);
				weapon.setUseDarkelf(rs.getInt("use_darkelf") == 0 ? false : true);
				weapon.setUseDragonknight(rs.getInt("use_dragonknight") == 0 ? false : true);
				weapon.setUseIllusionist(rs.getInt("use_illusionist") == 0 ? false : true);
				weapon.setHitModifier(rs.getInt("hit_modifier"));
				weapon.setDmgModifier(rs.getInt("dmg_modifier"));
				weapon.setStr(rs.getByte("str"));
				weapon.setDex(rs.getByte("dex"));
				weapon.setCon(rs.getByte("con"));
				weapon.setInt(rs.getByte("int"));
				weapon.setWis(rs.getByte("wis"));
				weapon.setCha(rs.getByte("cha"));
				weapon.setHp(rs.getInt("hp"));
				weapon.setMp(rs.getInt("mp"));
				weapon.setHpr(rs.getInt("hpr"));
				weapon.setMpr(rs.getInt("mpr"));
				weapon.setSp(rs.getInt("sp"));
				weapon.setMr(rs.getInt("mr"));
				weapon.setDoubleDmgChance(rs.getInt("double_dmg_chance"));
				weapon.setWeaknessExposure(rs.getInt("weakness_exposure"));
				weapon.setMagicDmgModifier(rs.getInt("magic_dmg_modifier"));
				weapon.setCanbeDmg(rs.getBoolean("can_be_dmg"));
				weapon.setMinLevel(rs.getInt("min_level"));
				weapon.setMaxLevel(rs.getInt("max_level"));
				weapon.setBless(rs.getInt("bless"));
				weapon.setTradable(rs.getInt("tradable") == 1 ? true : false);
				weapon.setDeletable(rs.getInt("deletable") == 1 ? true : false);
				weapon.setIsHaste(rs.getBoolean("is_haste"));
				weapon.setChargeTime(rs.getInt("charge_time"));
				weapon.setExpirationTime(rs.getString("expiration_time"));
				result.put(Integer.valueOf(weapon.getItemId()), weapon);
			}
		} catch (NullPointerException e) {
			_log.log(Level.SEVERE, String.format(I18N_LOAD_ITEM_FAILED, weapon.getName(), weapon.getItemId()));
		} catch (SQLException e) {
			_log.log(Level.SEVERE, e.getLocalizedMessage(), e);
		} finally {
			SqlUtil.close(rs);
			SqlUtil.close(pstm);
			SqlUtil.close(con);
		}
		return result;
	}

	private Map<Integer, L1Armor> allArmor() {
		Map<Integer, L1Armor> result = new HashMap<Integer, L1Armor>();
		Connection con = null;
		PreparedStatement pstm = null;
		ResultSet rs = null;
		L1Armor armor = null;
		try {
			con = L1DatabaseFactory.getInstance().getConnection();
			pstm = con.prepareStatement("select * from armors");
			rs = pstm.executeQuery();
			while (rs.next()) {
				armor = new L1Armor();
				armor.setItemId(rs.getInt("id"));
				armor.setName(rs.getString("name"));
				armor.setUnidentifiedNameId(rs.getString("unidentified_name_id"));
				armor.setIdentifiedNameId(rs.getString("identified_name_id"));
				armor.setType((_armorTypes.get(rs.getString("type"))).intValue());
				// armor.setType1((_armorId
				// .get(rs.getString("armor_type"))).intValue()); // 使わない
				armor.setType2(2);
				armor.setUseType((_useTypes.get(rs.getString("type"))).intValue());
				armor.setMaterial((_materialTypes.get(rs.getString("material"))).intValue());
				armor.setGrade(rs.getInt("grade"));
				armor.setWeight(rs.getInt("weight"));
				armor.setGfxId(rs.getInt("inv_gfx_id"));
				armor.setGroundGfxId(rs.getInt("grd_gfx_id"));
				armor.setItemDescId(rs.getInt("item_desc_id"));
				armor.setSafeEnchant(rs.getInt("safe_enchant"));
				armor.setUseRoyal(rs.getInt("use_royal") == 0 ? false : true);
				armor.setUseKnight(rs.getInt("use_knight") == 0 ? false : true);
				armor.setUseElf(rs.getInt("use_elf") == 0 ? false : true);
				armor.setUseWizard(rs.getInt("use_wizard") == 0 ? false : true);
				armor.setUseDarkelf(rs.getInt("use_darkelf") == 0 ? false : true);
				armor.setUseDragonknight(rs.getInt("use_dragonknight") == 0 ? false : true);
				armor.setUseIllusionist(rs.getInt("use_illusionist") == 0 ? false : true);
				armor.setMinLevel(rs.getInt("min_level"));
				armor.setMaxLevel(rs.getInt("max_level"));
				armor.setAc(rs.getInt("ac"));
				armor.setStr(rs.getByte("str"));
				armor.setCon(rs.getByte("con"));
				armor.setDex(rs.getByte("dex"));
				armor.setWis(rs.getByte("wis"));
				armor.setCha(rs.getByte("cha"));
				armor.setInt(rs.getByte("int"));
				armor.setHp(rs.getInt("hp"));
				armor.setHpr(rs.getInt("hpr"));
				armor.setMp(rs.getInt("mp"));
				armor.setMpr(rs.getInt("mpr"));
				armor.setSp(rs.getInt("sp"));
				armor.setMr(rs.getInt("mr"));
				armor.setDamageReduction(rs.getInt("damage_reduction"));
				armor.setWeightReduction(rs.getInt("weight_reduction"));
				armor.setHitModifierByArmor(rs.getInt("hit_modifier"));
				armor.setDmgModifierByArmor(rs.getInt("dmg_modifier"));
				armor.setBowHitModifierByArmor(rs.getInt("bow_hit_modifier"));
				armor.setBowDmgModifierByArmor(rs.getInt("bow_dmg_modifier"));
				armor.setDefenseEarth(rs.getInt("defense_earth"));
				armor.setDefenseWater(rs.getInt("defense_water"));
				armor.setDefenseWind(rs.getInt("defense_wind"));
				armor.setDefenseFire(rs.getInt("defense_fire"));
				armor.setResistStun(rs.getInt("resist_stun"));
				armor.setResistStone(rs.getInt("resist_stone"));
				armor.setResistSleep(rs.getInt("resist_sleep"));
				armor.setResistFreeze(rs.getInt("resist_freeze"));
				armor.setResistHold(rs.getInt("resist_hold"));
				armor.setResistBlind(rs.getInt("resist_blind"));
				armor.setBless(rs.getInt("bless"));
				armor.setTradable(rs.getInt("tradable") == 1 ? true : false);
				armor.setDeletable(rs.getInt("deletable") == 1 ? true : false);
				armor.setChargeTime(rs.getInt("charge_time"));
				armor.setExpirationTime(rs.getString("expiration_time"));
				armor.setIsHaste(rs.getInt("is_haste") == 0 ? false : true);
				armor.setExpBonus(rs.getInt("exp_bonus"));
				armor.setPotionRecoveryRate(rs.getInt("potion_recovery_rate"));
				result.put(Integer.valueOf(armor.getItemId()), armor);
			}
		} catch (NullPointerException e) {
			_log.log(Level.SEVERE, String.format(I18N_LOAD_ITEM_FAILED, armor.getName(), armor.getItemId()));
		} catch (SQLException e) {
			_log.log(Level.SEVERE, e.getLocalizedMessage(), e);
		} finally {
			SqlUtil.close(rs);
			SqlUtil.close(pstm);
			SqlUtil.close(con);
		}
		return result;
	}

	private void load() {
		PerformanceTimer timer = new PerformanceTimer();
		_allEtcItems = allEtcItem();
		_allWeapons = allWeapon();
		_allArmors = allArmor();
		_allTemplates = new HashMap<Integer, L1Item>();
		_allNames = new HashMap<String, Integer>();
		_allWithoutSpaceNames = new HashMap<String, Integer>();
		buildFastLookupTable(_allTemplates, _allEtcItems, _allWeapons, _allArmors,
				_allNames, _allWithoutSpaceNames);
		System.out.println("loading items...OK! " + timer.elapsedTimeMillis() + "ms");
	}
	
	public void reload() {
		PerformanceTimer timer = new PerformanceTimer();
		Map<Integer, L1EtcItem> allEtcItems = allEtcItem();
		Map<Integer, L1Weapon> allWeapons = allWeapon();
		Map<Integer, L1Armor> allArmors = allArmor();
		Map<Integer, L1Item> allTemplates = new HashMap<Integer, L1Item>();
		Map<String, Integer> allNames = new HashMap<String, Integer>();
		Map<String, Integer> allWithoutSpaceNames = new HashMap<String, Integer>();
		buildFastLookupTable(allTemplates, allEtcItems, allWeapons, allArmors,
				allNames, allWithoutSpaceNames);
		_allEtcItems = allEtcItems;
		_allWeapons = allWeapons;
		_allArmors = allArmors;
		_allTemplates = allTemplates;
		_allNames = allNames;
		_allWithoutSpaceNames = allWithoutSpaceNames;
		System.out.println("loading items...OK! " + timer.elapsedTimeMillis() + "ms");
	}
	
	private void buildFastLookupTable(Map<Integer, L1Item> allTemplates,
			Map<Integer, L1EtcItem> allEtcItems,
			Map<Integer, L1Weapon> allWeapons,
			Map<Integer, L1Armor> allArmors,
			Map<String, Integer> allNames,
			Map<String, Integer> allWithoutSpaceNames) {
		for (Iterator<Integer> iter = allEtcItems.keySet().iterator(); iter.hasNext();) {
			Integer id = iter.next();
			L1EtcItem item = allEtcItems.get(id);
			allTemplates.put(Integer.valueOf(id.intValue()), item);
			allNames.put(item.getName(), Integer.valueOf(id.intValue()));
			allWithoutSpaceNames.put(item.getName().replace(" ", ""), Integer.valueOf(id.intValue()));
		}
		for (Iterator<Integer> iter = allWeapons.keySet().iterator(); iter.hasNext();) {
			Integer id = iter.next();
			L1Weapon item = allWeapons.get(id);
			allTemplates.put(Integer.valueOf(id.intValue()), item);
			allNames.put(item.getName(), Integer.valueOf(id.intValue()));
			allWithoutSpaceNames.put(item.getName().replace(" ", ""), Integer.valueOf(id.intValue()));
		}
		for (Iterator<Integer> iter = allArmors.keySet().iterator(); iter.hasNext();) {
			Integer id = iter.next();
			L1Armor item = allArmors.get(id);
			allTemplates.put(Integer.valueOf(id.intValue()), item);
			allNames.put(item.getName(), Integer.valueOf(id.intValue()));
			allWithoutSpaceNames.put(item.getName().replace(" ", ""), Integer.valueOf(id.intValue()));
		}
	}

	public L1Item getTemplate(int id) {
		return _allTemplates.get(id);
	}

	public L1ItemInstance createItem(int itemId) {
		L1Item temp = getTemplate(itemId);
		if (temp == null) {
			return null;
		}
		L1ItemInstance item = new L1ItemInstance();
		item.setId(IdFactory.getInstance().nextId());
		item.setItem(temp);
		L1World.getInstance().storeObject(item);
		return item;
	}

	public int findItemIdByName(String name) {
		return _allNames.containsKey(name) ? _allNames.get(name) : 0;
	}

	public int findItemIdByNameWithoutSpace(String name) {
		String n = name.replace(" ", "");
		return _allWithoutSpaceNames.containsKey(n) ? _allWithoutSpaceNames.get(n) : 0;
	}
}
=======
/*
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

package jp.l1j.server.datatables;

import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.ArrayList;
import java.util.Collection;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import java.util.logging.Level;
import java.util.logging.Logger;
import static jp.l1j.locale.I18N.*;
import jp.l1j.server.model.L1World;
import jp.l1j.server.model.instance.L1ItemInstance;
import jp.l1j.server.templates.L1Armor;
import jp.l1j.server.templates.L1EtcItem;
import jp.l1j.server.templates.L1Item;
import jp.l1j.server.templates.L1Weapon;
import jp.l1j.server.utils.IdFactory;
import jp.l1j.server.utils.L1DatabaseFactory;
import jp.l1j.server.utils.PerformanceTimer;
import jp.l1j.server.utils.SqlUtil;

/**
 * 道具表：僅從合併表 {@code items} 讀取。
 * 舊表 etc_items / armors / weapons 已合併至 items，可隨時刪除。
 */
public class ItemTable {
	private static final long serialVersionUID = 1L;

	private static Logger _log = Logger.getLogger(ItemTable.class.getName());

	private static final Map<String, Integer> _armorTypes = new HashMap<String, Integer>();

	private static final Map<String, Integer> _weaponTypes = new HashMap<String, Integer>();

	private static final Map<String, Integer> _weaponId = new HashMap<String, Integer>();

	private static final Map<String, Integer> _materialTypes = new HashMap<String, Integer>();

	private static final Map<String, Integer> _etcItemTypes = new HashMap<String, Integer>();

	private static final Map<String, Integer> _useTypes = new HashMap<String, Integer>();

	private static ItemTable _instance;
	
	private static Map<Integer, L1Item> _allTemplates;

	private static Map<Integer, L1EtcItem> _allEtcItems;

	private static Map<Integer, L1Armor> _allArmors;

	private static Map<Integer, L1Weapon> _allWeapons;
	
	private static Map<String, Integer> _allNames;
	
	private static Map<String, Integer> _allWithoutSpaceNames;

	static {
		_etcItemTypes.put("arrow", Integer.valueOf(0));
		_etcItemTypes.put("wand", Integer.valueOf(1));
		_etcItemTypes.put("light", Integer.valueOf(2));
		_etcItemTypes.put("gem", Integer.valueOf(3));
		_etcItemTypes.put("totem", Integer.valueOf(4));
		_etcItemTypes.put("firecracker", Integer.valueOf(5));
		_etcItemTypes.put("potion", Integer.valueOf(6));
		_etcItemTypes.put("food", Integer.valueOf(7));
		_etcItemTypes.put("scroll", Integer.valueOf(8));
		_etcItemTypes.put("questitem", Integer.valueOf(9));
		_etcItemTypes.put("spellbook", Integer.valueOf(10));
		_etcItemTypes.put("petitem", Integer.valueOf(11));
		_etcItemTypes.put("other", Integer.valueOf(12));
		_etcItemTypes.put("material", Integer.valueOf(13));
		_etcItemTypes.put("event", Integer.valueOf(14));
		_etcItemTypes.put("sting", Integer.valueOf(15));
		_etcItemTypes.put("treasure_box", Integer.valueOf(16));
		_etcItemTypes.put("spellscroll", Integer.valueOf(18));
		_etcItemTypes.put("spellwand", Integer.valueOf(19));
		_etcItemTypes.put("spellicon", Integer.valueOf(20));
		_etcItemTypes.put("protect_scroll", Integer.valueOf(21));
		_etcItemTypes.put("unique_scroll", Integer.valueOf(22));

		_useTypes.put("none", Integer.valueOf(-1)); // 使用不可能
		_useTypes.put("normal", Integer.valueOf(0));
		_useTypes.put("weapon", Integer.valueOf(1));
		_useTypes.put("armor", Integer.valueOf(2));
		// _useTypes.put("wand1", Integer.valueOf(3));
		// _useTypes.put("wand", Integer.valueOf(4));
		// ワンドを振るアクションをとる(C_RequestExtraCommandが送られる)
		_useTypes.put("spell_long", Integer.valueOf(5)); // 地面 / オブジェクト選択(遠距離)
		_useTypes.put("ntele", Integer.valueOf(6));
		_useTypes.put("identify", Integer.valueOf(7));
		_useTypes.put("res", Integer.valueOf(8));
		_useTypes.put("choice", Integer.valueOf(14));
		_useTypes.put("instrument", Integer.valueOf(15));
		_useTypes.put("sosc", Integer.valueOf(16));
		_useTypes.put("spell_short", Integer.valueOf(17)); // 地面 / オブジェクト選択(近距離)
		_useTypes.put("t_shirt", Integer.valueOf(18));
		_useTypes.put("cloak", Integer.valueOf(19));
		_useTypes.put("glove", Integer.valueOf(20));
		_useTypes.put("boots", Integer.valueOf(21));
		_useTypes.put("helm", Integer.valueOf(22));
		_useTypes.put("ring", Integer.valueOf(23));
		_useTypes.put("amulet", Integer.valueOf(24));
		_useTypes.put("shield", Integer.valueOf(25));
		_useTypes.put("guarder", Integer.valueOf(25));
		_useTypes.put("dai", Integer.valueOf(26));
		_useTypes.put("zel", Integer.valueOf(27));
		_useTypes.put("blank", Integer.valueOf(28));
		_useTypes.put("btele", Integer.valueOf(29));
		_useTypes.put("spell_buff", Integer.valueOf(30)); // オブジェクト選択(遠距離)
		_useTypes.put("belt", Integer.valueOf(37));
		_useTypes.put("spell_point", Integer.valueOf(39)); // 地面選択
		_useTypes.put("earring", Integer.valueOf(40));
		_useTypes.put("fishing_rod", Integer.valueOf(42));
		_useTypes.put("pattern_back", Integer.valueOf(45));
		_useTypes.put("pattern_left", Integer.valueOf(44));
		_useTypes.put("pattern_right", Integer.valueOf(43));
		_useTypes.put("talisman_left", Integer.valueOf(47));
		_useTypes.put("talisman_right", Integer.valueOf(48));
		_useTypes.put("elixir", Integer.valueOf(58));
		_useTypes.put("healing", Integer.valueOf(59));
		_useTypes.put("cure", Integer.valueOf(60));
		_useTypes.put("haste", Integer.valueOf(61));
		_useTypes.put("brave", Integer.valueOf(62));
		_useTypes.put("third_speed", Integer.valueOf(63));
		_useTypes.put("magic_eye", Integer.valueOf(64));
		_useTypes.put("magic_healing", Integer.valueOf(65));
		_useTypes.put("bless_eva", Integer.valueOf(66));
		_useTypes.put("magic_regeneration", Integer.valueOf(67));
		_useTypes.put("wisdom", Integer.valueOf(68));
		_useTypes.put("flora", Integer.valueOf(69));
		_useTypes.put("poly", Integer.valueOf(70));
		_useTypes.put("npc_talk", Integer.valueOf(71));
		_useTypes.put("roulette", Integer.valueOf(72));
		_useTypes.put("teleport", Integer.valueOf(73));
		_useTypes.put("spawn", Integer.valueOf(74));
		_useTypes.put("furniture", Integer.valueOf(75));
		_useTypes.put("material", Integer.valueOf(77));
		_useTypes.put("extra", Integer.valueOf(78));
		_useTypes.put("spellicon", Integer.valueOf(76)); // スペルアイコン表示用（item_type=20）

		_armorTypes.put("none", Integer.valueOf(0));
		_armorTypes.put("helm", Integer.valueOf(1));
		_armorTypes.put("armor", Integer.valueOf(2));
		_armorTypes.put("t_shirt", Integer.valueOf(3));
		_armorTypes.put("cloak", Integer.valueOf(4));
		_armorTypes.put("glove", Integer.valueOf(5));
		_armorTypes.put("boots", Integer.valueOf(6));
		_armorTypes.put("shield", Integer.valueOf(7));
		_armorTypes.put("guarder", Integer.valueOf(8));
		_armorTypes.put("amulet", Integer.valueOf(10));
		_armorTypes.put("ring", Integer.valueOf(11));
		_armorTypes.put("earring", Integer.valueOf(12));
		_armorTypes.put("belt", Integer.valueOf(13));
		_armorTypes.put("pattern_back", Integer.valueOf(14));
		_armorTypes.put("pattern_left", Integer.valueOf(15));
		_armorTypes.put("pattern_right", Integer.valueOf(16));
		_armorTypes.put("talisman_left", Integer.valueOf(17));
		_armorTypes.put("talisman_right", Integer.valueOf(18));

		_weaponTypes.put("sword", Integer.valueOf(1));
		_weaponTypes.put("twohandsword", Integer.valueOf(2));
		_weaponTypes.put("dagger", Integer.valueOf(3));
		_weaponTypes.put("bow", Integer.valueOf(4));
		_weaponTypes.put("arrow", Integer.valueOf(5));
		_weaponTypes.put("spear", Integer.valueOf(6));
		_weaponTypes.put("blunt", Integer.valueOf(7));
		_weaponTypes.put("staff", Integer.valueOf(8));
		_weaponTypes.put("claw", Integer.valueOf(9));
		_weaponTypes.put("dualsword", Integer.valueOf(10));
		_weaponTypes.put("gauntlet", Integer.valueOf(11));
		_weaponTypes.put("sting", Integer.valueOf(12));
		_weaponTypes.put("chainsword", Integer.valueOf(13));
		_weaponTypes.put("kiringku", Integer.valueOf(14));

		_weaponId.put("sword", Integer.valueOf(4));
		_weaponId.put("twohandsword", Integer.valueOf(50));
		_weaponId.put("dagger", Integer.valueOf(46));
		_weaponId.put("bow", Integer.valueOf(20));
		_weaponId.put("arrow", Integer.valueOf(66));
		_weaponId.put("spear", Integer.valueOf(24));
		_weaponId.put("blunt", Integer.valueOf(11));
		_weaponId.put("staff", Integer.valueOf(40));
		_weaponId.put("claw", Integer.valueOf(58));
		_weaponId.put("dualsword", Integer.valueOf(54));
		_weaponId.put("gauntlet", Integer.valueOf(62));
		_weaponId.put("sting", Integer.valueOf(2922));
		_weaponId.put("chainsword", Integer.valueOf(24));
		_weaponId.put("kiringku", Integer.valueOf(58));

		_materialTypes.put("none", Integer.valueOf(0));
		_materialTypes.put("liquid", Integer.valueOf(1));
		_materialTypes.put("web", Integer.valueOf(2));
		_materialTypes.put("vegetation", Integer.valueOf(3));
		_materialTypes.put("animalmatter", Integer.valueOf(4));
		_materialTypes.put("paper", Integer.valueOf(5));
		_materialTypes.put("cloth", Integer.valueOf(6));
		_materialTypes.put("leather", Integer.valueOf(7));
		_materialTypes.put("wood", Integer.valueOf(8));
		_materialTypes.put("bone", Integer.valueOf(9));
		_materialTypes.put("dragonscale", Integer.valueOf(10));
		_materialTypes.put("iron", Integer.valueOf(11));
		_materialTypes.put("steel", Integer.valueOf(12));
		_materialTypes.put("copper", Integer.valueOf(13));
		_materialTypes.put("silver", Integer.valueOf(14));
		_materialTypes.put("gold", Integer.valueOf(15));
		_materialTypes.put("platinum", Integer.valueOf(16));
		_materialTypes.put("mithril", Integer.valueOf(17));
		_materialTypes.put("blackmithril", Integer.valueOf(18));
		_materialTypes.put("glass", Integer.valueOf(19));
		_materialTypes.put("gemstone", Integer.valueOf(20));
		_materialTypes.put("mineral", Integer.valueOf(21));
		_materialTypes.put("oriharukon", Integer.valueOf(22)); // 奧里哈魯根（DB 拼寫）
	}

	public static ItemTable getInstance() {
		if (_instance == null) {
			_instance = new ItemTable();
		}
		return _instance;
	}

	private ItemTable() {
		load();
	}

	private static String normalizeItemType(String type) {
		String t = type.toLowerCase();
		if (t.equals("twohand")) {
			return "twohandsword";
		}
		if (t.equals("axe")) {
			return "blunt";
		}
		if (t.equals("wand")) { // 【修復】wand → staff（對齊 182 的 wand=6 → JP staff=8）
			return "staff";
		}
		if (t.equals("shirt")) {
			return "t_shirt";
		}
		if (t.equals("accessory")) {
			// TODO verify 182 accessory category (ring/amulet/earring/belt)
			return "ring";
		}
		return t;
	}

	private static int resolveType2(String normalizedType) {
		if (_weaponTypes.containsKey(normalizedType)) {
			return 1;
		}
		if (_armorTypes.containsKey(normalizedType)) {
			return 2;
		}
		return 0;
	}

	private static int resolveUseType(String useType) {
		return _useTypes.get(useType).intValue();
	}

	private static String resolveAccessoryType(String useType) {
		String t = useType.toLowerCase();
		if (t.equals("ring")) {
			return "ring";
		}
		if (t.equals("amulet")) {
			return "amulet";
		}
		if (t.equals("earring")) {
			return "earring";
		}
		if (t.equals("belt")) {
			return "belt";
		}
		// TODO verify accessory mapping for other use_type values
		return "ring";
	}

	private static int resolveEtcItemType(int itemId, String rawType, int useType, int foodVolume) {
		if (itemId == 3) {
			return _etcItemTypes.get("light").intValue();
		}
		if (itemId == 26 || itemId == 30) {
			return _etcItemTypes.get("spellicon").intValue();
		}
		if (useType == 76) {
			return _etcItemTypes.get("spellicon").intValue();
		}
		String t = rawType.toLowerCase();
		if (t.equals("arrow")) {
			return _etcItemTypes.get("arrow").intValue();
		}
		if (t.equals("wand")) {
			return _etcItemTypes.get("wand").intValue();
		}
		if (t.equals("book")) {
			return _etcItemTypes.get("spellbook").intValue();
		}
		if (foodVolume > 0) {
			return _etcItemTypes.get("food").intValue();
		}
		if (useType == 59 || useType == 60 || useType == 61 || useType == 62 || useType == 63
				|| useType == 64 || useType == 65 || useType == 66 || useType == 67 || useType == 68
				|| useType == 69 || useType == 78) {
			return _etcItemTypes.get("potion").intValue();
		}
		if (useType == 77) {
			return _etcItemTypes.get("material").intValue();
		}
		if (useType == 5 || useType == 17) {
			return _etcItemTypes.get("wand").intValue();
		}
		if (useType == 6 || useType == 7 || useType == 8 || useType == 14 || useType == 16
				|| useType == 26 || useType == 27 || useType == 28 || useType == 29 || useType == 30
				|| useType == 39 || useType == 70 || useType == 71 || useType == 72 || useType == 73
				|| useType == 74 || useType == 75) {
			return _etcItemTypes.get("scroll").intValue();
		}
		// TODO verify light/spell icon/treasure box mapping after item_type removal
		return _etcItemTypes.get("other").intValue();
	}

	/** 若欄位不存在則回傳 defaultValue（相容合併表 items 未含 spell_consume / enchant_* 等欄位） */
	private static int getIntOptional(ResultSet rs, String columnName, int defaultValue) {
		try {
			return rs.getInt(columnName);
		} catch (SQLException e) {
			return defaultValue;
		}
	}

	private Map<Integer, L1EtcItem> allEtcItem() {
		Map<Integer, L1EtcItem> result = new HashMap<Integer, L1EtcItem>();
		Connection con = null;
		PreparedStatement pstm = null;
		ResultSet rs = null;
		L1EtcItem item = null;
		try {
			con = L1DatabaseFactory.getInstance().getConnection();
			pstm = con.prepareStatement("select * from items");
			rs = pstm.executeQuery();
			while (rs.next()) {
				String rawType = rs.getString("type");
				String normalizedType = normalizeItemType(rawType);
				int type2 = resolveType2(normalizedType);
				if (type2 != 0) {
					continue;
				}
				item = new L1EtcItem();
				int itemId = rs.getInt("item_id");
				item.setItemId(itemId);
				item.setName(rs.getString("name"));
				String nameId = rs.getString("nameid");
				item.setUnidentifiedNameId(nameId);
				item.setIdentifiedNameId(nameId);
				int useType = resolveUseType(rs.getString("use_type"));
				item.setType(resolveEtcItemType(itemId, rawType, useType, rs.getInt("food_volume")));
				item.setUseType(useType);
				// item.setType1(0); // 使わない
				item.setType2(0);
				item.setMaterial((_materialTypes.get(rs.getString("material"))).intValue());
				item.setWeight(rs.getInt("weight"));
				item.setGfxId(rs.getInt("inv_gfx_id"));
				item.setGroundGfxId(rs.getInt("grd_gfx_id"));
				item.setItemDescId(rs.getInt("item_desc_id"));
				item.setSkillId(rs.getInt("skill_id"));
				item.setSpellConsume(getIntOptional(rs, "spell_consume", 0) != 0);
				item.setEnchantAc(getIntOptional(rs, "enchant_ac", 0));
				item.setEnchantStr(getIntOptional(rs, "enchant_str", 0));
				item.setEnchantDex(getIntOptional(rs, "enchant_dex", 0));
				item.setEnchantCon(getIntOptional(rs, "enchant_con", 0));
				item.setEnchantInt(getIntOptional(rs, "enchant_int", 0));
				item.setEnchantWis(getIntOptional(rs, "enchant_wis", 0));
				item.setEnchantCha(getIntOptional(rs, "enchant_cha", 0));
				item.setEnchantHp(getIntOptional(rs, "enchant_hp", 0));
				item.setEnchantMp(getIntOptional(rs, "enchant_mp", 0));
				item.setEnchantHpr(getIntOptional(rs, "enchant_hpr", 0));
				item.setEnchantMpr(getIntOptional(rs, "enchant_mpr", 0));
				item.setEnchantMr(getIntOptional(rs, "enchant_mr", 0));
				item.setEnchantSp(getIntOptional(rs, "enchant_sp", 0));
				item.setEnchantHit(getIntOptional(rs, "enchant_hit", 0));
				item.setEnchantDmg(getIntOptional(rs, "enchant_dmg", 0));
				item.setEnchantBowHit(getIntOptional(rs, "enchant_bow_hit", 0));
				item.setEnchantBowDmg(getIntOptional(rs, "enchant_bow_dmg", 0));
				item.setEnchantWeightReduction(getIntOptional(rs, "enchant_weight_reduction", 0));
				item.setEnchantDamageReduction(getIntOptional(rs, "enchant_damage_reduction", 0));
				item.setEnchantDefenseEarth(getIntOptional(rs, "enchant_defense_earth", 0));
				item.setEnchantDefenseWater(getIntOptional(rs, "enchant_defense_water", 0));
				item.setEnchantDefenseFire(getIntOptional(rs, "enchant_defense_fire", 0));
				item.setEnchantDefenseWind(getIntOptional(rs, "enchant_defense_wind", 0));
				item.setEnchantResistStun(getIntOptional(rs, "enchant_resist_stun", 0));
				item.setEnchantResistStone(getIntOptional(rs, "enchant_resist_stone", 0));
				item.setEnchantResistSleep(getIntOptional(rs, "enchant_resist_sleep", 0));
				item.setEnchantResistFreeze(getIntOptional(rs, "enchant_resist_freeze", 0));
				item.setEnchantResistHold(getIntOptional(rs, "enchant_resist_hold", 0));
				item.setEnchantResistBlind(getIntOptional(rs, "enchant_resist_blind", 0));
				item.setEnchantExpBonus(getIntOptional(rs, "enchant_exp_bonus", 0));
				item.setEnchantPotionRecoveryRate(getIntOptional(rs, "enchant_potion_recovery_rate", 0));
				item.setMinLevel(rs.getInt("minlvl"));
				item.setMaxLevel(rs.getInt("maxlvl"));
				item.setBless(rs.getInt("bless"));
				item.setTradable(rs.getInt("tradable") == 1 ? true : false);
				item.setDeletable(rs.getInt("deletable") == 1 ? true : false);
				item.setSealable(rs.getInt("sealable") == 1 ? true : false);
				item.setDmgSmall(rs.getInt("dmg_small"));
				item.setDmgLarge(rs.getInt("dmg_large"));
				item.setStackable(rs.getInt("stackable") == 1 ? true : false);
				item.setMaxChargeCount(rs.getInt("charge_count"));
				item.setLocX(rs.getInt("loc_x"));
				item.setLocY(rs.getInt("loc_y"));
				item.setMapId(rs.getShort("map_id"));
				item.setDelayId(rs.getInt("delay_id"));
				item.setDelayTime(rs.getInt("delay_time"));
				item.setDelayEffect(rs.getInt("delay_effect"));
				item.setFoodVolume(rs.getInt("food_volume"));
				item.setToBeSavedAtOnce((rs.getInt("save_at_once") == 1) ? true : false);
				item.setChargeTime(rs.getInt("charge_time"));
				item.setExpirationTime(rs.getString("expiration_time"));
				item.setEffectId(rs.getInt("effect_id"));
				item.setUseRoyal(rs.getInt("royal") == 0 ? false : true);
				item.setUseKnight(rs.getInt("knight") == 0 ? false : true);
				item.setUseWizard(rs.getInt("mage") == 0 ? false : true);
				item.setUseElf(rs.getInt("elf") == 0 ? false : true);
				item.setAddStr(rs.getInt("add_str"));
				item.setAddDex(rs.getInt("add_dex"));
				item.setAddCon(rs.getInt("add_con"));
				item.setAddInt(rs.getInt("add_int"));
				item.setAddWis(rs.getInt("add_wis"));
				item.setAddSp(rs.getInt("add_sp"));
				item.setAddHit(rs.getInt("add_hit"));
				item.setAddDmg(rs.getInt("add_dmg"));
				item.setAddHp(rs.getInt("add_hp"));
				item.setAddMp(rs.getInt("add_mp"));
				item.setAddHpr(rs.getInt("add_hpr"));
				item.setAddMpr(rs.getInt("add_mpr"));
				item.setAddMr(rs.getInt("add_mr"));
				result.put(Integer.valueOf(item.getItemId()), item);
			}
		} catch (NullPointerException e) {
			_log.log(Level.SEVERE, String.format(I18N_LOAD_ITEM_FAILED, item.getName(), item.getItemId()));
		} catch (SQLException e) {
			_log.log(Level.SEVERE, e.getLocalizedMessage(), e);
		} finally {
			SqlUtil.close(rs);
			SqlUtil.close(pstm);
			SqlUtil.close(con);
		}
		return result;
	}

	private Map<Integer, L1Weapon> allWeapon() {
		Map<Integer, L1Weapon> result = new HashMap<Integer, L1Weapon>();
		Connection con = null;
		PreparedStatement pstm = null;
		ResultSet rs = null;
		L1Weapon weapon = null;
		try {
			con = L1DatabaseFactory.getInstance().getConnection();
			pstm = con.prepareStatement("select * from items");
			rs = pstm.executeQuery();
			while (rs.next()) {
				String rawType = rs.getString("type");
				String normalizedType = normalizeItemType(rawType);
				if (normalizedType.equals("accessory")) {
					normalizedType = resolveAccessoryType(rs.getString("use_type"));
				}
				int type2 = resolveType2(normalizedType);
				if (type2 != 1) {
					continue;
				}
				weapon = new L1Weapon();
				weapon.setItemId(rs.getInt("item_id"));
				weapon.setName(rs.getString("name"));
				String nameId = rs.getString("nameid");
				weapon.setUnidentifiedNameId(nameId);
				weapon.setIdentifiedNameId(nameId);
				weapon.setType((_weaponTypes.get(normalizedType)).intValue());
				weapon.setType1((_weaponId.get(normalizedType)).intValue());
				weapon.setType2(1);
				weapon.setUseType(1);
				weapon.setIsTwohanded(rs.getInt("two_hand") != 0);
				weapon.setMaterial((_materialTypes.get(rs.getString("material"))).intValue());
				weapon.setWeight(rs.getInt("weight"));
				weapon.setGfxId(rs.getInt("inv_gfx_id"));
				weapon.setGroundGfxId(rs.getInt("grd_gfx_id"));
				weapon.setItemDescId(rs.getInt("item_desc_id"));
				weapon.setSkillId(rs.getInt("skill_id"));
				weapon.setSpellConsume(getIntOptional(rs, "spell_consume", 0) != 0);
				weapon.setEnchantAc(getIntOptional(rs, "enchant_ac", 0));
				weapon.setEnchantStr(getIntOptional(rs, "enchant_str", 0));
				weapon.setEnchantDex(getIntOptional(rs, "enchant_dex", 0));
				weapon.setEnchantCon(getIntOptional(rs, "enchant_con", 0));
				weapon.setEnchantInt(getIntOptional(rs, "enchant_int", 0));
				weapon.setEnchantWis(getIntOptional(rs, "enchant_wis", 0));
				weapon.setEnchantCha(getIntOptional(rs, "enchant_cha", 0));
				weapon.setEnchantHp(getIntOptional(rs, "enchant_hp", 0));
				weapon.setEnchantMp(getIntOptional(rs, "enchant_mp", 0));
				weapon.setEnchantHpr(getIntOptional(rs, "enchant_hpr", 0));
				weapon.setEnchantMpr(getIntOptional(rs, "enchant_mpr", 0));
				weapon.setEnchantMr(getIntOptional(rs, "enchant_mr", 0));
				weapon.setEnchantSp(getIntOptional(rs, "enchant_sp", 0));
				weapon.setEnchantHit(getIntOptional(rs, "enchant_hit", 0));
				weapon.setEnchantDmg(getIntOptional(rs, "enchant_dmg", 0));
				weapon.setEnchantBowHit(getIntOptional(rs, "enchant_bow_hit", 0));
				weapon.setEnchantBowDmg(getIntOptional(rs, "enchant_bow_dmg", 0));
				weapon.setEnchantWeightReduction(getIntOptional(rs, "enchant_weight_reduction", 0));
				weapon.setEnchantDamageReduction(getIntOptional(rs, "enchant_damage_reduction", 0));
				weapon.setEnchantDefenseEarth(getIntOptional(rs, "enchant_defense_earth", 0));
				weapon.setEnchantDefenseWater(getIntOptional(rs, "enchant_defense_water", 0));
				weapon.setEnchantDefenseFire(getIntOptional(rs, "enchant_defense_fire", 0));
				weapon.setEnchantDefenseWind(getIntOptional(rs, "enchant_defense_wind", 0));
				weapon.setEnchantResistStun(getIntOptional(rs, "enchant_resist_stun", 0));
				weapon.setEnchantResistStone(getIntOptional(rs, "enchant_resist_stone", 0));
				weapon.setEnchantResistSleep(getIntOptional(rs, "enchant_resist_sleep", 0));
				weapon.setEnchantResistFreeze(getIntOptional(rs, "enchant_resist_freeze", 0));
				weapon.setEnchantResistHold(getIntOptional(rs, "enchant_resist_hold", 0));
				weapon.setEnchantResistBlind(getIntOptional(rs, "enchant_resist_blind", 0));
				weapon.setEnchantExpBonus(getIntOptional(rs, "enchant_exp_bonus", 0));
				weapon.setEnchantPotionRecoveryRate(getIntOptional(rs, "enchant_potion_recovery_rate", 0));
				weapon.setDmgSmall(rs.getInt("dmg_small"));
				weapon.setDmgLarge(rs.getInt("dmg_large"));
				weapon.setRange(rs.getInt("range"));
				weapon.setSafeEnchant(rs.getInt("safe_enchant"));
				weapon.setUseRoyal(rs.getInt("royal") == 0 ? false : true);
				weapon.setUseKnight(rs.getInt("knight") == 0 ? false : true);
				weapon.setUseElf(rs.getInt("elf") == 0 ? false : true);
				weapon.setUseWizard(rs.getInt("mage") == 0 ? false : true);
				weapon.setHitModifier(rs.getInt("hit_modifier"));
				weapon.setDmgModifier(rs.getInt("dmg_modifier"));
				weapon.setStr(rs.getByte("add_str"));
				weapon.setDex(rs.getByte("add_dex"));
				weapon.setCon(rs.getByte("add_con"));
				weapon.setInt(rs.getByte("add_int"));
				weapon.setWis(rs.getByte("add_wis"));
				weapon.setCha(rs.getByte("add_cha"));
				weapon.setHp(rs.getInt("add_hp"));
				weapon.setMp(rs.getInt("add_mp"));
				weapon.setHpr(rs.getInt("add_hpr"));
				weapon.setMpr(rs.getInt("add_mpr"));
				weapon.setSp(rs.getInt("add_sp"));
				weapon.setMr(rs.getInt("add_mr"));
				weapon.setCanbeDmg(rs.getInt("can_be_dmg") != 0);
				weapon.setMinLevel(rs.getInt("minlvl"));
				weapon.setMaxLevel(rs.getInt("maxlvl"));
				weapon.setBless(rs.getInt("bless"));
				weapon.setTradable(rs.getInt("tradable") == 1 ? true : false);
				weapon.setDeletable(rs.getInt("deletable") == 1 ? true : false);
				weapon.setIsHaste(rs.getInt("haste") != 0);
				weapon.setChargeTime(rs.getInt("charge_time"));
				weapon.setExpirationTime(rs.getString("expiration_time"));
				result.put(Integer.valueOf(weapon.getItemId()), weapon);
			}
		} catch (NullPointerException e) {
			_log.log(Level.SEVERE, String.format(I18N_LOAD_ITEM_FAILED, weapon.getName(), weapon.getItemId()));
		} catch (SQLException e) {
			_log.log(Level.SEVERE, e.getLocalizedMessage(), e);
		} finally {
			SqlUtil.close(rs);
			SqlUtil.close(pstm);
			SqlUtil.close(con);
		}
		return result;
	}

	private Map<Integer, L1Armor> allArmor() {
		Map<Integer, L1Armor> result = new HashMap<Integer, L1Armor>();
		Connection con = null;
		PreparedStatement pstm = null;
		ResultSet rs = null;
		L1Armor armor = null;
		try {
			con = L1DatabaseFactory.getInstance().getConnection();
			pstm = con.prepareStatement("select * from items");
			rs = pstm.executeQuery();
			while (rs.next()) {
				String rawType = rs.getString("type");
				String normalizedType = normalizeItemType(rawType);
				if (normalizedType.equals("accessory")) {
					normalizedType = resolveAccessoryType(rs.getString("use_type"));
				}
				int type2 = resolveType2(normalizedType);
				if (type2 != 2) {
					continue;
				}
				armor = new L1Armor();
				armor.setItemId(rs.getInt("item_id"));
				armor.setName(rs.getString("name"));
				String nameId = rs.getString("nameid");
				armor.setUnidentifiedNameId(nameId);
				armor.setIdentifiedNameId(nameId);
				armor.setType((_armorTypes.get(normalizedType)).intValue());
				// armor.setType1((_armorId
				// .get(rs.getString("armor_type"))).intValue()); // 使わない
				armor.setType2(2);
				armor.setUseType((_useTypes.get(normalizedType)).intValue());
				armor.setMaterial((_materialTypes.get(rs.getString("material"))).intValue());
				armor.setGrade(rs.getInt("grade"));
				armor.setWeight(rs.getInt("weight"));
				armor.setGfxId(rs.getInt("inv_gfx_id"));
				armor.setGroundGfxId(rs.getInt("grd_gfx_id"));
				armor.setItemDescId(rs.getInt("item_desc_id"));
				armor.setSkillId(rs.getInt("skill_id"));
				armor.setSpellConsume(getIntOptional(rs, "spell_consume", 0) != 0);
				armor.setEnchantAc(getIntOptional(rs, "enchant_ac", 0));
				armor.setEnchantStr(getIntOptional(rs, "enchant_str", 0));
				armor.setEnchantDex(getIntOptional(rs, "enchant_dex", 0));
				armor.setEnchantCon(getIntOptional(rs, "enchant_con", 0));
				armor.setEnchantInt(getIntOptional(rs, "enchant_int", 0));
				armor.setEnchantWis(getIntOptional(rs, "enchant_wis", 0));
				armor.setEnchantCha(getIntOptional(rs, "enchant_cha", 0));
				armor.setEnchantHp(getIntOptional(rs, "enchant_hp", 0));
				armor.setEnchantMp(getIntOptional(rs, "enchant_mp", 0));
				armor.setEnchantHpr(getIntOptional(rs, "enchant_hpr", 0));
				armor.setEnchantMpr(getIntOptional(rs, "enchant_mpr", 0));
				armor.setEnchantMr(getIntOptional(rs, "enchant_mr", 0));
				armor.setEnchantSp(getIntOptional(rs, "enchant_sp", 0));
				armor.setEnchantHit(getIntOptional(rs, "enchant_hit", 0));
				armor.setEnchantDmg(getIntOptional(rs, "enchant_dmg", 0));
				armor.setEnchantBowHit(getIntOptional(rs, "enchant_bow_hit", 0));
				armor.setEnchantBowDmg(getIntOptional(rs, "enchant_bow_dmg", 0));
				armor.setEnchantWeightReduction(getIntOptional(rs, "enchant_weight_reduction", 0));
				armor.setEnchantDamageReduction(getIntOptional(rs, "enchant_damage_reduction", 0));
				armor.setEnchantDefenseEarth(getIntOptional(rs, "enchant_defense_earth", 0));
				armor.setEnchantDefenseWater(getIntOptional(rs, "enchant_defense_water", 0));
				armor.setEnchantDefenseFire(getIntOptional(rs, "enchant_defense_fire", 0));
				armor.setEnchantDefenseWind(getIntOptional(rs, "enchant_defense_wind", 0));
				armor.setEnchantResistStun(getIntOptional(rs, "enchant_resist_stun", 0));
				armor.setEnchantResistStone(getIntOptional(rs, "enchant_resist_stone", 0));
				armor.setEnchantResistSleep(getIntOptional(rs, "enchant_resist_sleep", 0));
				armor.setEnchantResistFreeze(getIntOptional(rs, "enchant_resist_freeze", 0));
				armor.setEnchantResistHold(getIntOptional(rs, "enchant_resist_hold", 0));
				armor.setEnchantResistBlind(getIntOptional(rs, "enchant_resist_blind", 0));
				armor.setEnchantExpBonus(getIntOptional(rs, "enchant_exp_bonus", 0));
				armor.setEnchantPotionRecoveryRate(getIntOptional(rs, "enchant_potion_recovery_rate", 0));
				armor.setSafeEnchant(rs.getInt("safe_enchant"));
				armor.setUseRoyal(rs.getInt("royal") == 0 ? false : true);
				armor.setUseKnight(rs.getInt("knight") == 0 ? false : true);
				armor.setUseElf(rs.getInt("elf") == 0 ? false : true);
				armor.setUseWizard(rs.getInt("mage") == 0 ? false : true);
				armor.setMinLevel(rs.getInt("minlvl"));
				armor.setMaxLevel(rs.getInt("maxlvl"));
				armor.setAc(rs.getInt("add_ac"));
				armor.setStr(rs.getByte("add_str"));
				armor.setCon(rs.getByte("add_con"));
				armor.setDex(rs.getByte("add_dex"));
				armor.setWis(rs.getByte("add_wis"));
				armor.setCha(rs.getByte("add_cha"));
				armor.setInt(rs.getByte("add_int"));
				armor.setHp(rs.getInt("add_hp"));
				armor.setHpr(rs.getInt("add_hpr"));
				armor.setMp(rs.getInt("add_mp"));
				armor.setMpr(rs.getInt("add_mpr"));
				armor.setSp(rs.getInt("add_sp"));
				armor.setMr(rs.getInt("add_mr"));
				armor.setHitModifierByArmor(rs.getInt("hit_modifier"));
				armor.setDmgModifierByArmor(rs.getInt("dmg_modifier"));
				armor.setBless(rs.getInt("bless"));
				armor.setTradable(rs.getInt("tradable") == 1 ? true : false);
				armor.setDeletable(rs.getInt("deletable") == 1 ? true : false);
				armor.setChargeTime(rs.getInt("charge_time"));
				armor.setExpirationTime(rs.getString("expiration_time"));
				armor.setIsHaste(rs.getInt("haste") == 0 ? false : true);
				result.put(Integer.valueOf(armor.getItemId()), armor);
			}
		} catch (NullPointerException e) {
			_log.log(Level.SEVERE, String.format(I18N_LOAD_ITEM_FAILED, armor.getName(), armor.getItemId()));
		} catch (SQLException e) {
			_log.log(Level.SEVERE, e.getLocalizedMessage(), e);
		} finally {
			SqlUtil.close(rs);
			SqlUtil.close(pstm);
			SqlUtil.close(con);
		}
		return result;
	}

	private void load() {
		PerformanceTimer timer = new PerformanceTimer();
		_allEtcItems = allEtcItem();
		_allWeapons = allWeapon();
		_allArmors = allArmor();
		_allTemplates = new HashMap<Integer, L1Item>();
		_allNames = new HashMap<String, Integer>();
		_allWithoutSpaceNames = new HashMap<String, Integer>();
		buildFastLookupTable(_allTemplates, _allEtcItems, _allWeapons, _allArmors,
				_allNames, _allWithoutSpaceNames);
		System.out.println("loading items...OK! " + timer.elapsedTimeMillis() + "ms");
	}
	
	public void reload() {
		PerformanceTimer timer = new PerformanceTimer();
		Map<Integer, L1EtcItem> allEtcItems = allEtcItem();
		Map<Integer, L1Weapon> allWeapons = allWeapon();
		Map<Integer, L1Armor> allArmors = allArmor();
		Map<Integer, L1Item> allTemplates = new HashMap<Integer, L1Item>();
		Map<String, Integer> allNames = new HashMap<String, Integer>();
		Map<String, Integer> allWithoutSpaceNames = new HashMap<String, Integer>();
		buildFastLookupTable(allTemplates, allEtcItems, allWeapons, allArmors,
				allNames, allWithoutSpaceNames);
		_allEtcItems = allEtcItems;
		_allWeapons = allWeapons;
		_allArmors = allArmors;
		_allTemplates = allTemplates;
		_allNames = allNames;
		_allWithoutSpaceNames = allWithoutSpaceNames;
		System.out.println("loading items...OK! " + timer.elapsedTimeMillis() + "ms");
	}
	
	private void buildFastLookupTable(Map<Integer, L1Item> allTemplates,
			Map<Integer, L1EtcItem> allEtcItems,
			Map<Integer, L1Weapon> allWeapons,
			Map<Integer, L1Armor> allArmors,
			Map<String, Integer> allNames,
			Map<String, Integer> allWithoutSpaceNames) {
		for (Iterator<Integer> iter = allEtcItems.keySet().iterator(); iter.hasNext();) {
			Integer id = iter.next();
			L1EtcItem item = allEtcItems.get(id);
			allTemplates.put(Integer.valueOf(id.intValue()), item);
			allNames.put(item.getName(), Integer.valueOf(id.intValue()));
			allWithoutSpaceNames.put(item.getName().replace(" ", ""), Integer.valueOf(id.intValue()));
		}
		for (Iterator<Integer> iter = allWeapons.keySet().iterator(); iter.hasNext();) {
			Integer id = iter.next();
			L1Weapon item = allWeapons.get(id);
			allTemplates.put(Integer.valueOf(id.intValue()), item);
			allNames.put(item.getName(), Integer.valueOf(id.intValue()));
			allWithoutSpaceNames.put(item.getName().replace(" ", ""), Integer.valueOf(id.intValue()));
		}
		for (Iterator<Integer> iter = allArmors.keySet().iterator(); iter.hasNext();) {
			Integer id = iter.next();
			L1Armor item = allArmors.get(id);
			allTemplates.put(Integer.valueOf(id.intValue()), item);
			allNames.put(item.getName(), Integer.valueOf(id.intValue()));
			allWithoutSpaceNames.put(item.getName().replace(" ", ""), Integer.valueOf(id.intValue()));
		}
	}

	public L1Item getTemplate(int id) {
		return _allTemplates.get(id);
	}

	public L1ItemInstance createItem(int itemId) {
		L1Item temp = getTemplate(itemId);
		if (temp == null) {
			return null;
		}
		L1ItemInstance item = new L1ItemInstance();
		item.setId(IdFactory.getInstance().nextId());
		item.setItem(temp);
		L1World.getInstance().storeObject(item);
		return item;
	}

	public int findItemIdByName(String name) {
		return _allNames.containsKey(name) ? _allNames.get(name) : 0;
	}

	public int findItemIdByNameWithoutSpace(String name) {
		String n = name.replace(" ", "");
		return _allWithoutSpaceNames.containsKey(n) ? _allWithoutSpaceNames.get(n) : 0;
	}
}
>>>>>>> Stashed changes
