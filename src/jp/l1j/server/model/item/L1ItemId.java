<<<<<<< Updated upstream
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

package jp.l1j.server.model.item;

public class L1ItemId {
	/**
	 * レッドポーション
	 */
	public static final int POTION_OF_HEALING = 40010;

	/**
	 * 祝福されたレッドポーション
	 */
	public static final int B_POTION_OF_HEALING = 140010;

	/**
	 * 呪われたレッドポーション
	 */
	public static final int C_POTION_OF_HEALING = 240010;

	/**
	 * オレンジポーション
	 */
	public static final int POTION_OF_EXTRA_HEALING = 40011;

	/**
	 * 祝福されたオレンジポーション
	 */
	public static final int B_POTION_OF_EXTRA_HEALING = 140011;

	/**
	 * クリアーポーション
	 */
	public static final int POTION_OF_GREATER_HEALING = 40012;

	/**
	 * 祝福されたクリアーポーション
	 */
	public static final int B_POTION_OF_GREATER_HEALING = 140012;

	/**
	 * ヘイストポーション
	 */
	public static final int POTION_OF_HASTE_SELF = 40013;

	/**
	 * 祝福されたヘイストポーション
	 */
	public static final int B_POTION_OF_HASTE_SELF = 140013;

	/**
	 * 強化ヘイストポーション
	 */
	public static final int POTION_OF_GREATER_HASTE_SELF = 40018;

	/**
	 * 祝福された強化ヘイストポーション
	 */
	public static final int B_POTION_OF_GREATER_HASTE_SELF = 140018;

	/**
	 * ブレイブポーション
	 */
	public static final int POTION_OF_EMOTION_BRAVERY = 40014;

	/**
	 * 祝福されたブレイブポーション
	 */
	public static final int B_POTION_OF_EMOTION_BRAVERY = 140014;

	/**
	 * 魔力回復ポーション
	 */
	public static final int POTION_OF_MANA = 40015;

	/**
	 * 祝福された魔力回復ポーション
	 */
	public static final int B_POTION_OF_MANA = 140015;

	/**
	 * ウィズダムポーション
	 */
	public static final int POTION_OF_EMOTION_WISDOM = 40016;

	/**
	 * 祝福されたウィズダムポーション
	 */
	public static final int B_POTION_OF_EMOTION_WISDOM = 140016;

	/**
	 * シアンポーション
	 */
	public static final int POTION_OF_CURE_POISON = 40017;

	/**
	 * 濃縮体力回復剤
	 */
	public static final int CONDENSED_POTION_OF_HEALING = 40019;

	/**
	 * 濃縮高級体力回復剤
	 */
	public static final int CONDENSED_POTION_OF_EXTRA_HEALING = 40020;

	/**
	 * 濃縮強力体力回復剤
	 */
	public static final int CONDENSED_POTION_OF_GREATER_HEALING = 40021;

	/**
	 * ブラインドポーション
	 */
	public static final int POTION_OF_BLINDNESS = 40025;

	/**
	 * 防具強化スクロール
	 */
	public static final int SCROLL_OF_ENCHANT_ARMOR = 40074;

	/**
	 * 祝福された防具強化スクロール
	 */
	public static final int B_SCROLL_OF_ENCHANT_ARMOR = 140074;

	/**
	 * 呪われた防具強化スクロール
	 */
	public static final int C_SCROLL_OF_ENCHANT_ARMOR = 240074;

	/**
	 * 武器強化スクロール
	 */
	public static final int SCROLL_OF_ENCHANT_WEAPON = 40087;

	/**
	 * 祝福された武器強化スクロール
	 */
	public static final int B_SCROLL_OF_ENCHANT_WEAPON = 140087;

	/**
	 * 呪われた武器強化スクロール
	 */
	public static final int C_SCROLL_OF_ENCHANT_WEAPON = 240087;

	/**
	 * 試練のスクロール
	 */
	public static final int SCROLL_OF_ENCHANT_QUEST_WEAPON = 40660;

	/**
	 * アデナ
	 */
	public static final int ADENA = 40308;

	/**
	 * テレポートスクロール
	 */
	public static final int SCROLL_OF_TELEPORT = 40100;

	/**
	 * 祝福されたテレポートスクロール
	 */
	public static final int B_SCROLL_OF_TELEPORT = 140100;

	/**
	 * スペルスクロール(テレポート)
	 */
	public static final int SPELL_SCROLL_TELEPORT = 40863;

	/**
	 * マステレポートスクロール
	 */
	public static final int SCROLL_OF_MASS_TELEPORT = 40086;

	/**
	 * 象牙の塔のテレポートスクロール
	 */
	public static final int IVORY_TOWER_TELEPORT_SCROLL = 40099;
}
=======
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

package jp.l1j.server.model.item;

/**
 * 道具 ID 常量表（已對齊 182 統一 items 表）。
 * <p>
 * 祝福/詛咒 ID 規則：blessed = base + 100000, cursed = base + 200000
 */
public class L1ItemId {
	// ========== 回復系藥水 ==========

	/** 橙色药水（原 JP 40010 レッドポーション） */
	public static final int POTION_OF_HEALING = 103;

	/** 祝福橙色药水 */
	public static final int B_POTION_OF_HEALING = 100103;

	/** 詛咒橙色药水 */
	public static final int C_POTION_OF_HEALING = 200103;

	/** 白色药水（原 JP 40011 オレンジポーション） */
	public static final int POTION_OF_EXTRA_HEALING = 105;

	/** 祝福白色药水 */
	public static final int B_POTION_OF_EXTRA_HEALING = 100105;

	/** 安特的水果（原 JP 40012 クリアーポーション） */
	public static final int POTION_OF_GREATER_HEALING = 239;

	/** 祝福安特的水果 */
	public static final int B_POTION_OF_GREATER_HEALING = 100239;

	// ========== 加速系藥水 ==========

	/** 绿色药水（原 JP 40013 ヘイストポーション） */
	public static final int POTION_OF_HASTE_SELF = 102;

	/** 祝福绿色药水 */
	public static final int B_POTION_OF_HASTE_SELF = 100102;

	/** 强化绿色药水（原 JP 40018 強化ヘイストポーション） */
	public static final int POTION_OF_GREATER_HASTE_SELF = 354;

	/** 祝福强化绿色药水 */
	public static final int B_POTION_OF_GREATER_HASTE_SELF = 100354;

	// ========== 勇氣 / 智慧 / 魔力 / 解毒 ==========

	/** 勇敢药水（原 JP 40014 ブレイブポーション） */
	public static final int POTION_OF_EMOTION_BRAVERY = 253;

	/** 祝福勇敢药水 */
	public static final int B_POTION_OF_EMOTION_BRAVERY = 100253;

	/** 蓝色药水（原 JP 40015 魔力回復ポーション） */
	public static final int POTION_OF_MANA = 100;

	/** 祝福蓝色药水 */
	public static final int B_POTION_OF_MANA = 100100;

	/** 慎重药水（原 JP 40016 ウィズダムポーション） */
	public static final int POTION_OF_EMOTION_WISDOM = 254;

	/** 祝福慎重药水 */
	public static final int B_POTION_OF_EMOTION_WISDOM = 100254;

	/** 翡翠药水（原 JP 40017 シアンポーション） */
	public static final int POTION_OF_CURE_POISON = 101;

	// ========== 濃縮藥水 ==========

	/** 浓缩体力恢复剂（原 JP 40019） */
	public static final int CONDENSED_POTION_OF_HEALING = 321;

	/** 浓缩强力体力恢复剂（原 JP 40020） */
	public static final int CONDENSED_POTION_OF_EXTRA_HEALING = 322;

	/** 浓缩终极体力恢复剂（原 JP 40021） */
	public static final int CONDENSED_POTION_OF_GREATER_HEALING = 323;

	/** 182 版不存在（原 JP 40025 ブラインドポーション） */
	public static final int POTION_OF_BLINDNESS = -1;

	// ========== 卷軸類 ==========

	/** 对盔甲施法的卷轴（原 JP 40074） */
	public static final int SCROLL_OF_ENCHANT_ARMOR = 110;

	/** 祝福对盔甲施法的卷轴 */
	public static final int B_SCROLL_OF_ENCHANT_ARMOR = 100110;

	/** 詛咒对盔甲施法的卷轴 */
	public static final int C_SCROLL_OF_ENCHANT_ARMOR = 200110;

	/** 对武器施法的卷轴（原 JP 40087） */
	public static final int SCROLL_OF_ENCHANT_WEAPON = 109;

	/** 祝福对武器施法的卷轴 */
	public static final int B_SCROLL_OF_ENCHANT_WEAPON = 100109;

	/** 詛咒对武器施法的卷轴 */
	public static final int C_SCROLL_OF_ENCHANT_WEAPON = 200109;

	/** 182 版不存在（原 JP 40660 試練のスクロール） */
	public static final int SCROLL_OF_ENCHANT_QUEST_WEAPON = -1;

	// ========== 貨幣 ==========

	/** 金币（原 JP 40308 アデナ） */
	public static final int ADENA = 5;

	// ========== 傳送卷軸 ==========

	/** 瞬间移动卷轴（原 JP 40100） */
	public static final int SCROLL_OF_TELEPORT = 99;

	/** 祝福瞬间移动卷轴 */
	public static final int B_SCROLL_OF_TELEPORT = 100099;

	/** 182 版不存在（原 JP 40863 スペルスクロール(テレポート)） */
	public static final int SPELL_SCROLL_TELEPORT = -1;

	/** 全体传送术的卷轴（原 JP 40086） */
	public static final int SCROLL_OF_MASS_TELEPORT = 203;

	/** 182 版不存在（原 JP 40099 象牙の塔のテレポートスクロール） */
	public static final int IVORY_TOWER_TELEPORT_SCROLL = -1;
}
>>>>>>> Stashed changes
