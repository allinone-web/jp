-- ----------------------------
-- SpellIcon: 為 item_id 26、30 設定 use_type=spellicon（スペルアイコン表示用）
-- 執行後服務器會將此二道具視為 item_type=20（spellicon）
-- ----------------------------

-- 若使用 items 表（主鍵 item_id）
UPDATE `items` SET `use_type` = 'spellicon' WHERE `item_id` IN (26, 30);

-- 若使用 etc_items 表（主鍵 id，對應 l1jdb 等）
UPDATE `etc_items` SET `use_type` = 'spellicon' WHERE `id` IN (26, 30);
