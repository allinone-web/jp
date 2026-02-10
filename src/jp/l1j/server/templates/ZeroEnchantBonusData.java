/*
 * 全等級加成皆為 0 的 EnchantBonus 實作（用於僅需註冊 item_id 無實際加成的道具）。
 */
package jp.l1j.server.templates;

/**
 * 強化等級對應的裝備加成全部回傳 0。用於 EnchantBonusTable 註冊特定 item_id 以避免無資料警告。
 */
public enum ZeroEnchantBonusData implements L1EnchantBonusData {
	INSTANCE;

	@Override
	public int getAc(int enchantLevel) { return 0; }
	@Override
	public int getStr(int enchantLevel) { return 0; }
	@Override
	public int getDex(int enchantLevel) { return 0; }
	@Override
	public int getCon(int enchantLevel) { return 0; }
	@Override
	public int getInt(int enchantLevel) { return 0; }
	@Override
	public int getWis(int enchantLevel) { return 0; }
	@Override
	public int getCha(int enchantLevel) { return 0; }
	@Override
	public int getHp(int enchantLevel) { return 0; }
	@Override
	public int getHpr(int enchantLevel) { return 0; }
	@Override
	public int getMp(int enchantLevel) { return 0; }
	@Override
	public int getMpr(int enchantLevel) { return 0; }
	@Override
	public int getMr(int enchantLevel) { return 0; }
	@Override
	public int getSp(int enchantLevel) { return 0; }
	@Override
	public int getHitModifier(int enchantLevel) { return 0; }
	@Override
	public int getDmgModifier(int enchantLevel) { return 0; }
	@Override
	public int getBowHitModifier(int enchantLevel) { return 0; }
	@Override
	public int getBowDmgModifier(int enchantLevel) { return 0; }
	@Override
	public int getWeightReduction(int enchantLevel) { return 0; }
	@Override
	public int getDamageReduction(int enchantLevel) { return 0; }
	@Override
	public int getDefenseEarth(int enchantLevel) { return 0; }
	@Override
	public int getDefenseWater(int enchantLevel) { return 0; }
	@Override
	public int getDefenseFire(int enchantLevel) { return 0; }
	@Override
	public int getDefenseWind(int enchantLevel) { return 0; }
	@Override
	public int getResistStun(int enchantLevel) { return 0; }
	@Override
	public int getResistStone(int enchantLevel) { return 0; }
	@Override
	public int getResistSleep(int enchantLevel) { return 0; }
	@Override
	public int getResistFreeze(int enchantLevel) { return 0; }
	@Override
	public int getResistHold(int enchantLevel) { return 0; }
	@Override
	public int getResistBlind(int enchantLevel) { return 0; }
	@Override
	public int getExpBonus(int enchantLevel) { return 0; }
	@Override
	public int getPotionRecoveryRate(int enchantLevel) { return 0; }
}
