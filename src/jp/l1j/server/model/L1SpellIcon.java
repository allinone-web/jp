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

/**
 * スペルアイコン（法術圖示）資料。stub：尚未由 itemId 對應表實作時供編譯通過。
 */
public class L1SpellIcon {

	/**
	 * itemId 對應的スペルアイコン。未實作時回傳 null（道具名不追加顯示）。
	 */
	public static L1SpellIcon get(int itemId) {
		return null;
	}

	/**
	 * 道具名に追加する表示文字列（例：對應法術名）。
	 */
	public String getAppendName() {
		return "";
	}
}
