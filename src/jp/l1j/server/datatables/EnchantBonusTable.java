/*
 * DB-driven enchant bonus. When no data for item_id: return null and log warning (expose fault, do not crash).
 */
package jp.l1j.server.datatables;

import java.util.HashMap;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import java.util.logging.Logger;
import jp.l1j.server.templates.L1EnchantBonusData;
import jp.l1j.server.templates.ZeroEnchantBonusData;

/**
 * 裝備強化加成表。可從 DB 載入；無資料時 get() 回傳 null 並記錄警告（便於發現設定缺失），呼叫端加 0 不崩潰。
 */
public class EnchantBonusTable {
	private static final Logger _log = Logger.getLogger(EnchantBonusTable.class.getName());
	private static EnchantBonusTable _instance;
	private final Map<Integer, L1EnchantBonusData> _map = new HashMap<Integer, L1EnchantBonusData>();
	private final Set<Integer> _warnedItemIds = ConcurrentHashMap.newKeySet();

	public static EnchantBonusTable getInstance() {
		if (_instance == null) {
			_instance = new EnchantBonusTable();
		}
		return _instance;
	}

	private EnchantBonusTable() {
		// 可在此從 DB 載入 enchant_bonus 表；目前無表則保持空 map
		// 道具 id=54 註冊為 EnchantBonus（加成 0，避免無資料警告）
		_map.put(54, ZeroEnchantBonusData.INSTANCE);
	}

	/**
	 * 取得該裝備的強化加成資料。無資料時回傳 null 並記錄一次警告（不崩潰）。
	 */
	public L1EnchantBonusData get(int itemId) {
		L1EnchantBonusData data = _map.get(itemId);
		if (data == null && _warnedItemIds.add(itemId)) {
			_log.warning("EnchantBonus: no data for item_id=" + itemId + " (using 0). Server may be misconfigured.");
		}
		return data;
	}
}
