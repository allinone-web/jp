#!/bin/bash
################################################################################
# L1J-JP Database Surgical Installer (M4 Mac / OrbStack / MySQL 5.6)
################################################################################

# --- 確定的環境變數 ---
CONTAINER="lineage-mysql"
DB_USER="root"
DB_PASS="7777"
DB_NAME="l1jdb"
PROJECT_ROOT="/Users/airtan/Documents/GitHub/jp"
CSV_DIR="csv/tw" #

echo "🚀 開始執行 100% 適配部署..."

# 1. 將 db 目錄暫時複製進容器，解決掛載缺失問題
echo "📁 正在同步本地文件至容器臨時空間..."
docker cp "${PROJECT_ROOT}/db" "${CONTAINER}:/tmp/l1j_db_setup"

# 2. 創建數據庫容器 (create_db.sql)
echo "📦 正在初始化數據庫: $DB_NAME ..."
docker exec -i $CONTAINER mysql -u$DB_USER -p$DB_PASS < "${PROJECT_ROOT}/db/create_db.sql"

# 3. 導入 63 個數據表結構 (Schema)
echo "🏗️ 正在建立數據表結構..."
docker exec -i $CONTAINER bash -c "for f in /tmp/l1j_db_setup/schema/mysql/*.sql; do 
    echo \"導入結構: \$f\"; 
    mysql -u$DB_USER -p$DB_PASS -L $DB_NAME < \$f; 
done"

# 4. 導入 CSV 遊戲核心數據
echo "📥 正在填充 CSV 數據 (tw 版本)..."
docker exec -i $CONTAINER bash -c "for f in /tmp/l1j_db_setup/$CSV_DIR/*.csv; do 
    echo \"導入數據: \$f\"; 
    mysqlimport -u$DB_USER -p$DB_PASS -L $DB_NAME \$f \
    --fields-terminated_by=',' \
    --lines-terminated_by='\r\n' \
    --ignore-lines=1; 
done"

# 5. 清理容器內臨時文件
echo "🧹 清理臨時文件..."
docker exec -i $CONTAINER rm -rf /tmp/l1j_db_setup

echo "✅ 100% 自動化部署完成！"