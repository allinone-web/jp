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

package jp.l1j.server.templates;

public class L1EtcItem extends L1Item {
	/**
	 * 
	 */
	private static final long serialVersionUID = 1L;

	public L1EtcItem() {
	}

	private boolean _stackable;

	private int _locX;

	private int _locY;

	private short _mapId;

	private int _delayId;

	private int _delayTime;

	private int _delayEffect;

	private int _maxChargeCount;
	private int _effectId;
	private boolean _useRoyal;
	private boolean _useKnight;
	private boolean _useWizard;
	private boolean _useElf;
	private int _addStr;
	private int _addDex;
	private int _addCon;
	private int _addInt;
	private int _addWis;
	private int _addSp;
	private int _addHit;
	private int _addDmg;
	private int _addBowHit;
	private int _addBowDmg;
	private int _addHp;
	private int _addMp;
	private int _addHpr;
	private int _addMpr;
	private int _addMr;

	private boolean _isSealable; // ● 封印スクロールで封印可能

	@Override
	public boolean isStackable() {
		return _stackable;
	}

	public void setStackable(boolean stackable) {
		_stackable = stackable;
	}

	public void setLocX(int locX) {
		_locX = locX;
	}

	@Override
	public int getLocX() {
		return _locX;
	}

	public void setLocY(int locY) {
		_locY = locY;
	}

	@Override
	public int getLocY() {
		return _locY;
	}

	public void setMapId(short mapId) {
		_mapId = mapId;
	}

	@Override
	public short getMapId() {
		return _mapId;
	}

	public void setDelayId(int delayId) {
		_delayId = delayId;
	}

	@Override
	public int getDelayId() {
		return _delayId;
	}

	public void setDelayTime(int delayTime) {
		_delayTime = delayTime;
	}

	@Override
	public int getDelayTime() {
		return _delayTime;
	}

	public void setDelayEffect(int delayEffect) {
		_delayEffect = delayEffect;
	}

	public int getDelayEffect() {
		return _delayEffect;
	}

	public void setMaxChargeCount(int i) {
		_maxChargeCount = i;
	}

	@Override
	public int getMaxChargeCount() {
		return _maxChargeCount;
	}

	public void setEffectId(int effectId) {
		_effectId = effectId;
	}

	public int getEffectId() {
		return _effectId;
	}

	public void setUseRoyal(boolean flag) {
		_useRoyal = flag;
	}

	public boolean isUseRoyal() {
		return _useRoyal;
	}

	public void setUseKnight(boolean flag) {
		_useKnight = flag;
	}

	public boolean isUseKnight() {
		return _useKnight;
	}

	public void setUseWizard(boolean flag) {
		_useWizard = flag;
	}

	public boolean isUseWizard() {
		return _useWizard;
	}

	public void setUseElf(boolean flag) {
		_useElf = flag;
	}

	public boolean isUseElf() {
		return _useElf;
	}

	public void setAddStr(int i) {
		_addStr = i;
	}

	public int getAddStr() {
		return _addStr;
	}

	public void setAddDex(int i) {
		_addDex = i;
	}

	public int getAddDex() {
		return _addDex;
	}

	public void setAddCon(int i) {
		_addCon = i;
	}

	public int getAddCon() {
		return _addCon;
	}

	public void setAddInt(int i) {
		_addInt = i;
	}

	public int getAddInt() {
		return _addInt;
	}

	public void setAddWis(int i) {
		_addWis = i;
	}

	public int getAddWis() {
		return _addWis;
	}

	public void setAddSp(int i) {
		_addSp = i;
	}

	public int getAddSp() {
		return _addSp;
	}

	public void setAddHit(int i) {
		_addHit = i;
	}

	public int getAddHit() {
		return _addHit;
	}

	public void setAddDmg(int i) {
		_addDmg = i;
	}

	public int getAddDmg() {
		return _addDmg;
	}

	public void setAddBowHit(int i) {
		_addBowHit = i;
	}

	public int getAddBowHit() {
		return _addBowHit;
	}

	public void setAddBowDmg(int i) {
		_addBowDmg = i;
	}

	public int getAddBowDmg() {
		return _addBowDmg;
	}

	public void setAddHp(int i) {
		_addHp = i;
	}

	public int getAddHp() {
		return _addHp;
	}

	public void setAddMp(int i) {
		_addMp = i;
	}

	public int getAddMp() {
		return _addMp;
	}

	public void setAddHpr(int i) {
		_addHpr = i;
	}

	public int getAddHpr() {
		return _addHpr;
	}

	public void setAddMpr(int i) {
		_addMpr = i;
	}

	public int getAddMpr() {
		return _addMpr;
	}

	public void setAddMr(int i) {
		_addMr = i;
	}

	public int getAddMr() {
		return _addMr;
	}

	@Override
	public boolean isSealable() {
		return _isSealable;
	}

	public void setSealable(boolean flag) {
		_isSealable = flag;
	}


}
