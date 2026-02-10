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

import jp.l1j.server.datatables.ItemTable;
import jp.l1j.server.model.instance.L1ItemInstance;
import jp.l1j.server.model.instance.L1PcInstance;
import jp.l1j.server.templates.L1Item;

/**
 * 負責物品狀態檢查是否作弊。
 * 道具資料來源為合併表 items（ItemTable），不再查詢 etc_items / armors / weapons。
 */
public class L1ItemCheck {

        public boolean ItemCheck(L1ItemInstance item, L1PcInstance pc) {
                int itemId = item.getItem().getItemId();
                int itemCount = item.getCount();
                boolean isCheat = false;

                L1Item template = ItemTable.getInstance().getTemplate(itemId);
                if (template == null) {
                        return false;
                }
                int type2 = template.getType2();
                boolean isStackable = template.isStackable();

                if ((type2 == 1 || type2 == 2) && itemCount != 1) {
                        isCheat = true;
                } else if (type2 == 0) {
                        if (!isStackable && itemCount != 1) {
                                isCheat = true;
                        } else if (itemId == 5
                                        && (itemCount > 2000000000 || itemCount < 0)) {
                                isCheat = true;
                        } else if (isStackable && itemId != 5
                                        && (itemCount > 100000 || itemCount < 0)) {
                                isCheat = true;
                        }
                }
                if (isCheat) {
                        pc.getInventory().removeItem(item, itemCount);
                }
                return isCheat;
        }
}