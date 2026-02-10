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
package jp.l1j.server.model;

import jp.l1j.server.model.instance.L1ItemInstance;
import jp.l1j.server.model.instance.L1PcInstance;

/**
 * 家具系統（stub：尚未實作時供編譯通過）
 */
public class L1Furniture {
	private static L1Furniture _instance;

	public static L1Furniture getInstance() {
		if (_instance == null) {
			_instance = new L1Furniture();
		}
		return _instance;
	}

	/**
	 * 家具除去ワンドで家具を除去する。未實作時は常に false。
	 */
	public boolean remove(L1PcInstance pc, int itemId, L1ItemInstance item) {
		return false;
	}
}
