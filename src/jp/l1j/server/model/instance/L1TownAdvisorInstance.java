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

package jp.l1j.server.model.instance;

import jp.l1j.server.datatables.ItemTable;
import jp.l1j.server.datatables.TownTable;
import jp.l1j.server.model.inventory.L1Inventory;
import jp.l1j.server.packets.server.S_ServerMessage;
import jp.l1j.server.templates.L1Npc;

public class L1TownAdvisorInstance extends L1NpcInstance {
	private static int[] _returnScrollIds = {41467, 41472, 41468, 41471,
		41470, 41469, 41473, 41474, 41476, 41475};
	
	public L1TownAdvisorInstance(L1Npc template) {
		super(template);
	}

	private void createItem(L1PcInstance pc, int itemId, int amount, int price,
			int needItemId, int needAmount) {
		L1ItemInstance item = ItemTable.getInstance().createItem(itemId);
<<<<<<< Updated upstream
		if (pc.getInventory().checkItem(40308, price)
				&& (needItemId == 0 || (needItemId > 0 && pc.getInventory().checkItem(needItemId, needAmount)))) {
			if (pc.getInventory().checkAddItem(item, amount, true) == L1Inventory.OK) {
				pc.getInventory().consumeItem(40308, price);
=======
		if (item == null) {
			return;
		}
		if (needItemId > 0 && ItemTable.getInstance().getTemplate(needItemId) == null) {
			return;
		}
		if (pc.getInventory().checkItem(5, price)
				&& (needItemId == 0 || (needItemId > 0 && pc.getInventory().checkItem(needItemId, needAmount)))) {
			if (pc.getInventory().checkAddItem(item, amount, true) == L1Inventory.OK) {
				pc.getInventory().consumeItem(5, price);
>>>>>>> Stashed changes
				if (needItemId > 0) {
					pc.getInventory().consumeItem(needItemId, needAmount);
				}
				pc.addContribution(price / 100); // 貢献度
				TownTable.getInstance().addSalesMoney(pc.getHomeTownId(), price); // 町の売上
				pc.getInventory().storeItem(itemId, amount);
				pc.sendPackets(new S_ServerMessage(143, getNpcTemplate().getName(), item.getLogName()));
				// \f1%0が%1をくれました。
			}
		}
	}
	
	public void create(L1PcInstance pc, String s) {
		// 村人のブレイブポーションを作る
		if (s.equals("NUL")) {
			createItem(pc, 41480, 1, 1000, 253, 3);
		} else if (s.equals("OUL")) {
			createItem(pc, 41480, 2, 2000, 253, 6);
		} else if (s.equals("PUL")) {
			createItem(pc, 41480, 3, 3000, 253, 9);
		} else if (s.equals("QUL")) {
			createItem(pc, 41480, 4, 4000, 253, 12);
		} else if (s.equals("RUL")) {
			createItem(pc, 41480, 5, 5000, 253, 15);
		} else if (s.equals("SUL")) {
			createItem(pc, 41480, 6, 6000, 253, 18);
		} else if (s.equals("TUL")) {
			createItem(pc, 41480, 7, 7000, 253, 21);
		} else if (s.equals("UUL")) {
			createItem(pc, 41480, 8, 8000, 253, 24);
		} else if (s.equals("VUL")) {
			createItem(pc, 41480, 9, 9000, 253, 27);
		} else if (s.equals("WUL")) {
			createItem(pc, 41480, 10, 10000, 253, 30);
		} else if (s.equals("XUL")) {
			createItem(pc, 41480, 11, 11000, 253, 33);
		} else if (s.equals("YUL")) {
			createItem(pc, 41480, 12, 12000, 253, 36);
		} else if (s.equals("ZUL")) {
			createItem(pc, 41480, 13, 13000, 253, 39);
		} else if (s.equals("[UL")) {
			createItem(pc, 41480, 14, 14000, 253, 42);
		} else if (s.equals("\\UL")) {
			createItem(pc, 41480, 15, 15000, 253, 45);
		// 村人の集中ポーションを作る
		} else if (s.equals("SI")) {
			createItem(pc, 41479, 1, 1000, 40068, 3);
		} else if (s.equals("TI")) {
			createItem(pc, 41479, 2, 2000, 40068, 6);
		} else if (s.equals("UI")) {
			createItem(pc, 41479, 3, 3000, 40068, 9);
		} else if (s.equals("VI")) {
			createItem(pc, 41479, 4, 4000, 40068, 12);
		} else if (s.equals("WI")) {
			createItem(pc, 41479, 5, 5000, 40068, 15);
		} else if (s.equals("XI")) {
			createItem(pc, 41479, 6, 6000, 40068, 18);
		} else if (s.equals("YI")) {
			createItem(pc, 41479, 7, 7000, 40068, 21);
		} else if (s.equals("ZI")) {
			createItem(pc, 41479, 8, 8000, 40068, 24);
		} else if (s.equals("[I")) {
			createItem(pc, 41479, 9, 9000, 40068, 27);
		} else if (s.equals("\\I")) {
			createItem(pc, 41479, 10, 10000, 40068, 30);
		} else if (s.equals("]I")) {
			createItem(pc, 41479, 11, 11000, 40068, 33);
		} else if (s.equals("^I")) {
			createItem(pc, 41479, 12, 12000, 40068, 36);
		} else if (s.equals("_I")) {
			createItem(pc, 41479, 13, 13000, 40068, 39);
		} else if (s.equals("`I")) {
			createItem(pc, 41479, 14, 14000, 40068, 42);
		} else if (s.equals("aI")) {
			createItem(pc, 41479, 15, 15000, 40068, 45);
		// 村人のウィズダムポーションを作る
		} else if (s.equals("RS")) {
			createItem(pc, 41482, 1, 500, 254, 3);
		} else if (s.equals("SS")) {
			createItem(pc, 41482, 2, 1000, 254, 6);
		} else if (s.equals("TS")) {
			createItem(pc, 41482, 3, 1500, 254, 9);
		} else if (s.equals("US")) {
			createItem(pc, 41482, 4, 2000, 254, 12);
		} else if (s.equals("VS")) {
			createItem(pc, 41482, 5, 2500, 254, 15);
		} else if (s.equals("WS")) {
			createItem(pc, 41482, 6, 3000, 254, 18);
		} else if (s.equals("XS")) {
			createItem(pc, 41482, 7, 3500, 254, 21);
		} else if (s.equals("YS")) {
			createItem(pc, 41482, 8, 4000, 254, 24);
		} else if (s.equals("ZS")) {
			createItem(pc, 41482, 9, 4500, 254, 27);
		} else if (s.equals("[S")) {
			createItem(pc, 41482, 10, 5000, 254, 30);
		} else if (s.equals("\\S")) {
			createItem(pc, 41482, 11, 5500, 254, 33);
		} else if (s.equals("]S")) {
			createItem(pc, 41482, 12, 6000, 254, 36);
		} else if (s.equals("^S")) {
			createItem(pc, 41482, 13, 6500, 254, 39);
		} else if (s.equals("_S")) {
			createItem(pc, 41482, 14, 7000, 254, 42);
		} else if (s.equals("`S")) {
			createItem(pc, 41482, 15, 7500, 254, 45);
		// 村人の魔力ポーションを作る
		} else if (s.equals("-")) {
			createItem(pc, 41481, 1, 1000, 100, 3);
		} else if (s.equals(".")) {
			createItem(pc, 41481, 2, 2000, 100, 6);
		} else if (s.equals("/")) {
			createItem(pc, 41481, 3, 3000, 100, 9);
		} else if (s.equals("0")) {
			createItem(pc, 41481, 4, 4000, 100, 12);
		} else if (s.equals("1")) {
			createItem(pc, 41481, 5, 5000, 100, 15);
		} else if (s.equals("2")) {
			createItem(pc, 41481, 6, 6000, 100, 18);
		} else if (s.equals("3")) {
			createItem(pc, 41481, 7, 7000, 100, 21);
		} else if (s.equals("4")) {
			createItem(pc, 41481, 8, 8000, 100, 24);
		} else if (s.equals("5")) {
			createItem(pc, 41481, 9, 9000, 100, 27);
		} else if (s.equals("6")) {
			createItem(pc, 41481, 10, 10000, 100, 30);
		} else if (s.equals("7")) {
			createItem(pc, 41481, 11, 11000, 100, 33);
		} else if (s.equals("8")) {
			createItem(pc, 41481, 12, 12000, 100, 36);
		} else if (s.equals("9")) {
			createItem(pc, 41481, 13, 13000, 100, 39);
		} else if (s.equals(":")) {
			createItem(pc, 41481, 14, 14000, 100, 42);
		} else if (s.equals(";")) {
			createItem(pc, 41481, 15, 15000, 100, 45);
		// 村人のヘイストポーションを作る
		} else if (s.equals("<")) {
			createItem(pc, 41477, 1, 500, 102, 3);
		} else if (s.equals("=")) {
			createItem(pc, 41477, 2, 1000, 102, 6);
		} else if (s.equals(">")) {
			createItem(pc, 41477, 3, 1500, 102, 9);
		} else if (s.equals("?")) {
			createItem(pc, 41477, 4, 2000, 102, 12);
		} else if (s.equals("@")) {
			createItem(pc, 41477, 5, 2500, 102, 15);
		} else if (s.equals("A")) {
			createItem(pc, 41477, 6, 3000, 102, 18);
		} else if (s.equals("B")) {
			createItem(pc, 41477, 7, 3500, 102, 21);
		} else if (s.equals("C")) {
			createItem(pc, 41477, 8, 4000, 102, 24);
		} else if (s.equals("D")) {
			createItem(pc, 41477, 9, 4500, 102, 27);
		} else if (s.equals("E")) {
			createItem(pc, 41477, 10, 5000, 102, 30);
		} else if (s.equals("F")) {
			createItem(pc, 41477, 11, 5500, 102, 33);
		} else if (s.equals("G")) {
			createItem(pc, 41477, 12, 6000, 102, 36);
		} else if (s.equals("H")) {
			createItem(pc, 41477, 13, 6500, 102, 39);
		} else if (s.equals("I")) {
			createItem(pc, 41477, 14, 7000, 102, 42);
		} else if (s.equals("J")) {
			createItem(pc, 41477, 15, 7500, 102, 45);
		// 村人の呼吸ポーションを作る
		} else if (s.equals("K")) {
			createItem(pc, 41478, 1, 500, 40032, 3);
		} else if (s.equals("L")) {
			createItem(pc, 41478, 2, 1000, 40032, 6);
		} else if (s.equals("M")) {
			createItem(pc, 41478, 3, 1500, 40032, 9);
		} else if (s.equals("N")) {
			createItem(pc, 41478, 4, 2000, 40032, 12);
		} else if (s.equals("O")) {
			createItem(pc, 41478, 5, 2500, 40032, 15);
		} else if (s.equals("P")) {
			createItem(pc, 41478, 6, 3000, 40032, 18);
		} else if (s.equals("Q")) {
			createItem(pc, 41478, 7, 3500, 40032, 21);
		} else if (s.equals("R")) {
			createItem(pc, 41478, 8, 4000, 40032, 24);
		} else if (s.equals("S")) {
			createItem(pc, 41478, 9, 4500, 40032, 27);
		} else if (s.equals("T")) {
			createItem(pc, 41478, 10, 5000, 40032, 30);
		} else if (s.equals("U")) {
			createItem(pc, 41478, 11, 5500, 40032, 33);
		} else if (s.equals("V")) {
			createItem(pc, 41478, 12, 6000, 40032, 36);
		} else if (s.equals("W")) {
			createItem(pc, 41478, 13, 6500, 40032, 39);
		} else if (s.equals("X")) {
			createItem(pc, 41478, 14, 7000, 40032, 42);
		} else if (s.equals("Y")) {
			createItem(pc, 41478, 15, 7500, 40032, 45);
		// 村人の変身ポーションを作る
		} else if (s.equals("Z")) {
			createItem(pc, 41483, 1, 1000, 40088, 3);
		} else if (s.equals("[")) {
			createItem(pc, 41483, 2, 2000, 40088, 6);
		} else if (s.equals("\\")) {
			createItem(pc, 41483, 3, 3000, 40088, 9);
		} else if (s.equals("]")) {
			createItem(pc, 41483, 4, 4000, 40088, 12);
		} else if (s.equals("^")) {
			createItem(pc, 41483, 5, 5000, 40088, 15);
		} else if (s.equals("_")) {
			createItem(pc, 41483, 6, 6000, 40088, 18);
		} else if (s.equals("`")) {
			createItem(pc, 41483, 7, 7000, 40088, 21);
		} else if (s.equals("a")) {
			createItem(pc, 41483, 8, 8000, 40088, 24);
		} else if (s.equals("b")) {
			createItem(pc, 41483, 9, 9000, 40088, 27);
		} else if (s.equals("c")) {
			createItem(pc, 41483, 10, 10000, 40088, 30);
		} else if (s.equals("d")) {
			createItem(pc, 41483, 11, 11000, 40088, 33);
		} else if (s.equals("e")) {
			createItem(pc, 41483, 12, 12000, 40088, 36);
		} else if (s.equals("f")) {
			createItem(pc, 41483, 13, 13000, 40088, 39);
		} else if (s.equals("g")) {
			createItem(pc, 41483, 14, 14000, 40088, 42);
		} else if (s.equals("h")) {
			createItem(pc, 41483, 15, 15000, 40088, 45);
		// 村人の帰還スクロールを購入する
		} else if (s.equals("i")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 1, 400, 0, 0);
		} else if (s.equals("j")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 2, 800, 0, 0);
		} else if (s.equals("k")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 3, 1200, 0, 0);
		} else if (s.equals("l")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 4, 1600, 0, 0);
		} else if (s.equals("m")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 5, 2000, 0, 0);
		} else if (s.equals("n")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 6, 2400, 0, 0);
		} else if (s.equals("o")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 7, 2800, 0, 0);
		} else if (s.equals("p")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 8, 3200, 0, 0);
		} else if (s.equals("q")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 9, 3600, 0, 0);
		} else if (s.equals("r")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 10, 4000, 0, 0);
		} else if (s.equals("s")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 11, 4400, 0, 0);
		} else if (s.equals("t")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 12, 4800, 0, 0);
		} else if (s.equals("u")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 13, 5200, 0, 0);
		} else if (s.equals("v")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 14, 5600, 0, 0);
		} else if (s.equals("w")) {
			createItem(pc, _returnScrollIds[pc.getHomeTownId() - 1], 15, 6000, 0, 0);
		}
	}
}
