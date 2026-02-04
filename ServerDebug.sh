#!/bin/bash
# ------------------------------------------------------------------------------
# L1J-JP Server Startup Script (Surgical Precision - Root Mode)
# ------------------------------------------------------------------------------

# 1. 定位到項目根目錄
cd "/Users/airtan/Documents/GitHub/jp"

# 2. 執行 Java 啟動 (整合您提供的參數並適配 JDK 17)
# 注意：Java 17 已經移除了 PermSize，改用 Metaspace 確保不會報錯
java -server \
  -Xlog:gc*:file=l1jserver.log:time,uptime,level,tags \
  -Xms1024m \
  -Xmx1024m \
  -XX:NewRatio=2 \
  -XX:SurvivorRatio=8 \
  -XX:MetaspaceSize=256m \
  -XX:MaxMetaspaceSize=256m \
  -Dfile.encoding=UTF-8 \
  -jar l1jserver.jar

# 3. 模擬 @pause 功能 (macOS Bash 版)
echo "Press any key to continue..."
read -n 1 -s