-- ----------------------------
-- 將 name_id（編碼如 $10、$1436）寫入 l1jdb.skills
-- 對應方式：僅以 name 對應（兩表 id 僅前幾筆一致，其後不同，故不可用 id）。
-- ----------------------------

-- ========== 以 name 對應，從 jpdbbackup 複製 name_id ==========
UPDATE l1jdb.skills s
INNER JOIN jpdbbackup.skills t ON s.name = t.name
SET s.name_id = t.name_id
WHERE t.name_id IS NOT NULL AND t.name_id != '';


-- ========== 若來源為 182 庫（表名 skills 且有 name_id），改為： ==========
-- UPDATE l1jdb.skills s
-- INNER JOIN `182`.skills t ON s.name = t.name
-- SET s.name_id = t.name_id
-- WHERE t.name_id IS NOT NULL AND t.name_id != '';


-- ========== 若 182 在另一台，先導出 (name, name_id) 再匯入 l1jdb.skill_name_id_182 後： ==========
-- UPDATE l1jdb.skills s
-- INNER JOIN l1jdb.skill_name_id_182 t ON s.name = t.name
-- SET s.name_id = t.name_id;
-- DROP TABLE IF EXISTS l1jdb.skill_name_id_182;
