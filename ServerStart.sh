#!/bin/bash
# ------------------------------------------------------------------------------
# L1J-JP Server Startup Script (Surgical Precision - M4 Mac JDK 17)
# ------------------------------------------------------------------------------

# 1. 自動定位到腳本所在的資料夾
cd "$(dirname "$0")"

# 2. 執行 Java 指令
# -XX:MetaspaceSize: 替代已廢棄的 PermSize (針對 JDK 17 優化)
# -Duser.language/country: 強制鎖定語系，解決 en_PH 導致的啟動報錯
# -Dfile.encoding=UTF-8: 確保繁體中文資料不亂碼
java -server \
  -XX:MetaspaceSize=256m \
  -XX:MaxMetaspaceSize=256m \
  -Xms1024m \
  -Xmx1024m \
  -XX:NewRatio=2 \
  -XX:SurvivorRatio=8 \
  -Dfile.encoding=UTF-8 \
  -jar l1jserver.jar

# 3. 實現 Windows @pause 功能
echo "----------------------------------------------------"
echo "伺服器已停止運行。按任意鍵結束並關閉視窗..."
read -n 1 -s