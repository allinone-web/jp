-- ------------------------------------------------------------
-- 補齊 GM 指令：若資料庫為舊版或未從含 learn50 的 CSV 匯入，執行此腳本即可。
-- 執行方式：mysql -u root -p l1jdb < db/schema/mysql/maintenance/insert_commands_learn50.sql
-- ------------------------------------------------------------
INSERT INTO `commands` (`name`, `access_level`, `class_name`) VALUES
('learn50', 200, 'L1Learn50Spells')
ON DUPLICATE KEY UPDATE `access_level` = VALUES(`access_level`), `class_name` = VALUES(`class_name`);
