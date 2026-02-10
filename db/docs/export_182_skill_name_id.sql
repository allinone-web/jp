-- ----------------------------
-- 導出 skills 的 name_id（編碼如 $10、$1436）供寫入 l1jdb
-- 說明：本機 182 庫為 skill_list 表且無 name_id 欄位；
--       實際已用 jpdbbackup.skills 以 id 對應更新 l1jdb，見 migration_skill_name_id_from_182.sql
-- 若您有「含 skills.name_id 的 182 庫」且要以 name 對應，可在此庫執行下方導出。
-- ----------------------------

-- 導出 (name, name_id)，供匯入 l1jdb 暫存表後以 name 對應更新
SELECT name, name_id FROM skills WHERE name IS NOT NULL AND name != '';
