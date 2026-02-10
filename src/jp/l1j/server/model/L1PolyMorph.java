/**
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
package jp.l1j.server.model;

import java.util.logging.Logger;

import jp.l1j.server.datatables.PolyTable;
import jp.l1j.server.model.instance.L1ItemInstance;
import jp.l1j.server.model.instance.L1MonsterInstance;
import jp.l1j.server.model.instance.L1PcInstance;
import jp.l1j.server.model.skill.L1SkillId;
import jp.l1j.server.packets.server.S_ChangeShape;
import jp.l1j.server.packets.server.S_CharVisualUpdate;
import jp.l1j.server.packets.server.S_CloseList;
import jp.l1j.server.packets.server.S_ServerMessage;
import jp.l1j.server.packets.server.S_SkillIconGFX;
import static jp.l1j.server.model.skill.L1SkillId.*;

// 【對齊 182】變身數據類 - 使用 182 的個別布林欄位結構
public class L1PolyMorph {
	private static Logger _log = Logger.getLogger(L1PolyMorph.class.getName());

	// 変身の原因を示す定数（保留 - doPoly 簽名相容）
	public static final int MORPH_BY_ITEMMAGIC = 1;
	public static final int MORPH_BY_GM = 2;
	public static final int MORPH_BY_NPC = 4;
	public static final int MORPH_BY_KEPLISHA = 8;
	public static final int MORPH_BY_LOGIN = 0;

	// 182 Poly 欄位
	private int _id;
	private String _name;
	private String _db;
	private int _polyId;     // gfx_id
	private int _minLevel;
	private int _weapon;     // 武器裝備模式 (0-7)
	private boolean _helm;
	private boolean _earring;
	private boolean _necklace;
	private boolean _t;       // T恤
	private boolean _armor;
	private boolean _cloak;
	private boolean _ring;
	private boolean _belt;
	private boolean _glove;
	private boolean _shield;
	private boolean _boots;

	public L1PolyMorph(int id, String name, String db, int polyId, int minLevel,
			int weapon, boolean helm, boolean earring, boolean necklace,
			boolean t, boolean armor, boolean cloak, boolean ring,
			boolean belt, boolean glove, boolean shield, boolean boots) {
		_id = id;
		_name = name;
		_db = db;
		_polyId = polyId;
		_minLevel = minLevel;
		_weapon = weapon;
		_helm = helm;
		_earring = earring;
		_necklace = necklace;
		_t = t;
		_armor = armor;
		_cloak = cloak;
		_ring = ring;
		_belt = belt;
		_glove = glove;
		_shield = shield;
		_boots = boots;
	}

	public int getId() { return _id; }
	public String getName() { return _name; }
	public String getDb() { return _db; }
	public int getPolyId() { return _polyId; }
	public int getMinLevel() { return _minLevel; }
	public int getWeapon() { return _weapon; }
	public boolean isHelm() { return _helm; }
	public boolean isEarring() { return _earring; }
	public boolean isNecklace() { return _necklace; }
	public boolean isT() { return _t; }
	public boolean isArmor() { return _armor; }
	public boolean isCloak() { return _cloak; }
	public boolean isRing() { return _ring; }
	public boolean isBelt() { return _belt; }
	public boolean isGlove() { return _glove; }
	public boolean isShield() { return _shield; }
	public boolean isBoots() { return _boots; }

	public static void handleCommands(L1PcInstance pc, String s) {
		if (pc == null || pc.isDead()) {
			return;
		}
		L1PolyMorph poly = PolyTable.getInstance().getTemplate(s);
		if (poly != null || s.equals("none")) {
			if (s.equals("none")) {
				if (pc.getTempCharGfx() == 6034
						|| pc.getTempCharGfx() == 6035) {
				} else {
					pc.removeSkillEffect(SHAPE_CHANGE);
					pc.sendPackets(new S_CloseList(pc.getId()));
				}
			} else if (pc.getLevel() >= poly.getMinLevel() || pc.isGm()) {
				if (pc.getTempCharGfx() == 6034
						|| pc.getTempCharGfx() == 6035) {
					pc.sendPackets(new S_ServerMessage(181));
				} else {
				doPoly(pc, poly.getPolyId(), 7200, MORPH_BY_ITEMMAGIC);
				pc.sendPackets(new S_CloseList(pc.getId()));
				}
			} else {
				pc.sendPackets(new S_ServerMessage(181));
			}
		}
	}

	public static void doPoly(L1Character cha, int polyId, int timeSecs,
				int cause) {
		if (cha == null || cha.isDead()) {
			return;
		}
		if (cha instanceof L1PcInstance) {
			L1PcInstance pc = (L1PcInstance) cha;
			if (pc.getMapId() == 5124 // 釣り場
					|| pc.getMap().getBaseMapId()==9000
					|| pc.getMap().getBaseMapId()==9101
					){
				pc.sendPackets(new S_ServerMessage(1170));
				return;
			}
			if (pc.getTempCharGfx() == 6034
					|| pc.getTempCharGfx() == 6035) {
				pc.sendPackets(new S_ServerMessage(181));
				return;
			}
			// 【對齊 182】移除 isMatchCause 檢查 - 182 無此機制

 			pc.killSkillEffectTimer(SHAPE_CHANGE);
			pc.setSkillEffect(SHAPE_CHANGE, timeSecs * 1000);
			if (pc.getTempCharGfx() != polyId) {
				L1ItemInstance weapon = pc.getWeapon();
				boolean weaponTakeoff = (weapon != null && !isEquipableWeapon(
						polyId, weapon.getItem().getType()));
				pc.setTempCharGfx(polyId);
				pc.sendPackets(new S_ChangeShape(pc.getId(), polyId,
						weaponTakeoff));
				if (!pc.isGmInvis() && !pc.isInvisble()) {
					pc.broadcastPacket(new S_ChangeShape(pc.getId(), polyId));
				}
				if (pc.isGmInvis()) {
				} else if (pc.isInvisble()) {
					pc.broadcastPacketForFindInvis(new S_ChangeShape(pc
							.getId(), polyId), true);
				} else {
					pc.broadcastPacket(new S_ChangeShape(pc.getId(), polyId));
				}
				pc.getInventory().takeoffEquip(polyId);
				weapon = pc.getWeapon();
				if (weapon != null) {
					S_CharVisualUpdate charVisual = new S_CharVisualUpdate(pc);
					pc.sendPackets(charVisual);
					pc.broadcastPacket(charVisual);
				}
			}
			pc.sendPackets(new S_SkillIconGFX(35, timeSecs));
		} else if (cha instanceof L1MonsterInstance) {
			L1MonsterInstance mob = (L1MonsterInstance) cha;
			mob.killSkillEffectTimer(SHAPE_CHANGE);
			mob.setSkillEffect(SHAPE_CHANGE, timeSecs * 1000);
			if (mob.getTempCharGfx() != polyId) {
				mob.setTempCharGfx(polyId);
				mob.broadcastPacket(new S_ChangeShape(mob.getId(), polyId));
			}
		}
	}

	public static void undoPoly(L1Character cha) {
		if (cha instanceof L1PcInstance) {
			L1PcInstance pc = (L1PcInstance) cha;
			int classId = 0;
			if (pc.getBasePoly() == 0){
				classId = pc.getClassId();
			} else {
				classId = pc.getBasePoly();
			}
			pc.setTempCharGfx(classId);
			pc.sendPackets(new S_ChangeShape(pc.getId(), classId));
			pc.broadcastPacket(new S_ChangeShape(pc.getId(), classId));
			L1ItemInstance weapon = pc.getWeapon();
			if (weapon != null) {
				S_CharVisualUpdate charVisual = new S_CharVisualUpdate(pc);
				pc.sendPackets(charVisual);
				pc.broadcastPacket(charVisual);
			}
		} else if (cha instanceof L1MonsterInstance) {
			L1MonsterInstance mob = (L1MonsterInstance) cha;
			mob.setTempCharGfx(0);
			mob.broadcastPacket(new S_ChangeShape(mob.getId(), mob.getGfxId()));
		}
	}

	/**
	 * 【對齊 182】武器裝備檢查
	 * 182 isWeapon 模式 (0-7) 翻譯為 JP 武器類型編號:
	 *   182: arrow=1, axe=2, bow=3, spear=4, sword=5, wand=6, dagger=8, twohand=24
	 *   JP:  sword=1, twohand=2, dagger=3, bow=4, spear=6, blunt=7, staff=8
	 */
	public static boolean isEquipableWeapon(int polyId, int weaponType) {
		L1PolyMorph poly = PolyTable.getInstance().getTemplate(polyId);
		if (poly == null) {
			return true;
		}
		switch (poly.getWeapon()) {
			case 0: // 不可裝備武器（動物/怪物形態）
				return false;
			case 1: // 182: sword(5), dagger(8), twohand(24) → JP: sword(1), twohand(2), dagger(3)
				return (weaponType == 1) || (weaponType == 2) || (weaponType == 3);
			case 2: // 182: 單手武器 → JP: 排除雙手劍(2)、弓(4)、矛(6)、雙刀(10)
				return (weaponType != 2) && (weaponType != 4) && (weaponType != 6) && (weaponType != 10);
			case 3: // 182: 雙手武器（弓除外） → JP: twohand(2), spear(6), dualsword(10)
				return (weaponType == 2) || (weaponType == 6) || (weaponType == 10);
			case 4: // 182: bow(3) → JP: bow(4)
				return (weaponType == 4);
			case 5: // 182: spear(4) → JP: spear(6)
				return (weaponType == 6);
			case 6: // 182: wand(6) → JP: staff(8)
				return (weaponType == 8);
			case 7: // 182: axe(2)+spear(4)+wand(6)+sword(5)+twohand(24)
				// → JP: sword(1)+twohand(2)+spear(6)+blunt(7)+staff(8)
				return (weaponType == 1) || (weaponType == 2) || (weaponType == 6)
						|| (weaponType == 7) || (weaponType == 8);
			default:
				return true;
		}
	}

	/**
	 * 【對齊 182】防具裝備檢查
	 * 182 使用個別布林欄位，由 JP armorType 對應：
	 *   JP: helm=1, armor=2, tshirt=3, cloak=4, glove=5, boots=6, shield=7,
	 *       guarder=8, amulet=10, ring=11, earring=12, belt=13
	 */
	public static boolean isEquipableArmor(int polyId, int armorType) {
		L1PolyMorph poly = PolyTable.getInstance().getTemplate(polyId);
		if (poly == null) {
			return true;
		}
		switch (armorType) {
			case 1:  return poly.isHelm();     // 頭盔
			case 2:  return poly.isArmor();    // 盔甲
			case 3:  return poly.isT();        // T恤
			case 4:  return poly.isCloak();    // 斗篷
			case 5:  return poly.isGlove();    // 手套
			case 6:  return poly.isBoots();    // 靴子
			case 7:  return poly.isShield();   // 盾牌
			case 11: return poly.isRing();     // 戒指
			case 12: return poly.isEarring();  // 耳環
			case 13: return poly.isBelt();     // 腰帶
			default: return true;              // 其餘（護身符等）一律允許
		}
	}
}
