/*
 * DB-driven enchant bonus per level. When no data, use 0 and log warning (do not crash).
 */
package jp.l1j.server.templates;

/**
 * 強化等級對應的裝備加成（由 DB 或 EnchantBonusTable 提供）。
 * 無資料時回傳 0，並由 Table 記錄警告。
 */
public interface L1EnchantBonusData {
	int getAc(int enchantLevel);
	int getStr(int enchantLevel);
	int getDex(int enchantLevel);
	int getCon(int enchantLevel);
	int getInt(int enchantLevel);
	int getWis(int enchantLevel);
	int getCha(int enchantLevel);
	int getHp(int enchantLevel);
	int getHpr(int enchantLevel);
	int getMp(int enchantLevel);
	int getMpr(int enchantLevel);
	int getMr(int enchantLevel);
	int getSp(int enchantLevel);
	int getHitModifier(int enchantLevel);
	int getDmgModifier(int enchantLevel);
	int getBowHitModifier(int enchantLevel);
	int getBowDmgModifier(int enchantLevel);
	int getWeightReduction(int enchantLevel);
	int getDamageReduction(int enchantLevel);
	int getDefenseEarth(int enchantLevel);
	int getDefenseWater(int enchantLevel);
	int getDefenseFire(int enchantLevel);
	int getDefenseWind(int enchantLevel);
	int getResistStun(int enchantLevel);
	int getResistStone(int enchantLevel);
	int getResistSleep(int enchantLevel);
	int getResistFreeze(int enchantLevel);
	int getResistHold(int enchantLevel);
	int getResistBlind(int enchantLevel);
	int getExpBonus(int enchantLevel);
	int getPotionRecoveryRate(int enchantLevel);
}
