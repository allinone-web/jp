/*
 * DB-driven enchant protect scroll: down_level when enchant fails. When no data: use 1 and log warning (do not crash).
 */
package jp.l1j.server.datatables;

import java.util.HashMap;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import java.util.logging.Logger;

/**
 * 強化保護捲：失敗時降級數。無資料時回傳預設 1 並記錄警告。
 */
public class EnchantProtectTable {
	private static final Logger _log = Logger.getLogger(EnchantProtectTable.class.getName());
	private static EnchantProtectTable _instance;
	/** protect_item_id -> (target_item_id -> down_level), 簡化為 protect_item_id -> down_level */
	private final Map<Integer, Integer> _downLevelByProtectItemId = new HashMap<Integer, Integer>();
	private final Set<Integer> _warnedProtectIds = ConcurrentHashMap.newKeySet();

	public static EnchantProtectTable getInstance() {
		if (_instance == null) {
			_instance = new EnchantProtectTable();
		}
		return _instance;
	}

	private EnchantProtectTable() {
		// 可從 DB 載入；目前無表則預設降 1
	}

	/**
	 * 保護捲失敗時目標裝備降幾級。無資料時回傳 1 並記錄一次警告。
	 */
	public int getDownLevel(int protectItemId, int targetItemId) {
		Integer down = _downLevelByProtectItemId.get(protectItemId);
		if (down == null) {
			if (_warnedProtectIds.add(protectItemId)) {
				_log.warning("EnchantProtect: no data for protect_item_id=" + protectItemId + " (using down_level=1).");
			}
			return 1;
		}
		return down.intValue();
	}
}
