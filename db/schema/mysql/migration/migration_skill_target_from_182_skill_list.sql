-- ----------------------------
-- 從 182.skill_list 的 type 導入到 l1jdb.skills.target，以 name 對應
-- 182 表為 skill_list，欄位為 type（對應 l1jdb.skills.target 的 buff/attack/none 等）
-- ----------------------------

UPDATE l1jdb.skills s
INNER JOIN `182`.skill_list t ON s.name = t.name
SET s.target = t.type
WHERE t.type IS NOT NULL AND t.type != '';
