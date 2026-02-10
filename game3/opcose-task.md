# opcode-task.md

## Scope
- Client: `/Users/airtan/Documents/GitHub/game2` (modifiable)
- Server reference: `/Users/airtan/Documents/GitHub/jp` (read-only)
- Source of truth: `jp/src/jp/l1j/server/codes/Opcodes.java`

## Progress Log
### 2026-02-03
- Extracted full opcode list from `Opcodes.java` (217 entries).
- Confirmed restart flow in server: `C_OPCODE_RESTART = 71` handled by `jp/src/jp/l1j/server/packets/client/C_Restart.java` (no payload).
- Updated client restart behavior to send `C_OPCODE_RESTART` instead of returning to character select.
- Mapped `S_OPCODE_DOACTIONGFX = 218` to `ObjectAction` (death action now arrives via correct opcode; movement lock uses this).
- Mapped `S_OPCODE_WAR = 123` to `ParseWar` (previously misrouted to `ParsePacketBox`).
- Confirmed server “restart window after crash” is not a packet; server uses `S_Disconnect` (95) for kicks/overspeed, crash yields socket close only.
- Updated move send log to report opcode 95 (matches `C_OPCODE_MOVECHAR`).
- Build fix: added `using Client.Network;` to `GameWorld.UI.cs` for `PacketWriter`.
- Parsed `S_OPCODE_EXP (121)` and `S_OPCODE_ITEMNAME (195)`, and added `Opcode33` subcode `0x42` (equipment window) to sync equip state with UI.
- Added inventory name/equip update handler to refresh ChaWindow after equipment switching; preserved double-click fast equip behavior with code comment.
- Fixed client delete-item opcode to `C_OPCODE_DELETEINVENTORYITEM=209`.
- Adjusted identify flow: double-click scroll, then double-click target item to identify (single click now only cancels in identify mode).
- Tested/passed (do not modify): backpack item identification, item deletion, item pickup; double-click equip/unequip with instant appearance swap; ChaWindow equipment icon/name sync.
- HP bar + Z auto-target: allow monster targeting/HP bar when list.spr entry is missing (fallback to monster alignment; ground items still excluded).
- UseItem: teleport scroll (use_type 6/29) now sends mapid+objid payload (0,0) to satisfy server readH/readD; log opcode corrected to 44.
- Movement: if list.spr has no walk sequence, client no longer switches to walk animation on start move.
- Added magic cooldown log throttled (once/3s per skill) and exposed remaining cooldown in ms for UI.
- SkillCooldownDisplay now shows milliseconds (e.g., `1234ms`) to visualize speed changes.
- JP `S_OPCODE_SKILLSOUNDGFX (232)` short packet now parsed as object effect (fixes potion/scroll effect visuals).
- Poly update now refreshes visuals immediately after `S_OPCODE_POLY` (self transform should apply).
- Mage bow long-range attacks tested OK; do not modify.

## Server Opcode To-Do List (JP)
```
[C -> Client Send]
C_OPCODE_PUTSOLDIER=3 [pending]
C_OPCODE_CHANGEWARTIME=4 [pending]
C_OPCODE_PUTHIRESOLDIER=5 [pending]
C_OPCODE_PUTBOWSOLDIER=7 [pending]
C_OPCODE_COMMONINFO=9 [pending]
C_OPCODE_DELETECHAR=10 [pending]
C_OPCODE_BOARDDELETE=12 [pending]
C_OPCODE_BOARDWRITE=14 [pending]
C_OPCODE_SHOP=16 [done]
C_OPCODE_MAIL=22 [pending]
C_OPCODE_FISHCLICK=26 [pending]
C_OPCODE_JOINCLAN=30 [pending]
C_OPCODE_DEPOSIT=35 [pending]
C_OPCODE_NPCACTION=37 [done]
C_OPCODE_RESULT=40 [pending]
C_OPCODE_SENDLOCATION=41 [pending]
C_OPCODE_MAPSYSTEM=41 [pending]
C_OPCODE_PARTYLIST=42 [pending]
C_OPCODE_USEITEM=44 [pending]
C_OPCODE_SMS=45 [pending]
C_OPCODE_FIGHT=47 [pending]
C_OPCODE_WHO=49 [pending]
C_OPCODE_COMMONCLICK=53 [pending]
C_OPCODE_DROPITEM=54 [pending]
C_OPCODE_LOGINPACKET=57 [pending]
C_OPCODE_NPCTALK=58 [pending]
C_OPCODE_BOARDREAD=59 [pending]
C_OPCODE_BUDDYLIST=60 [pending]
C_OPCODE_ATTR=61 [pending]
C_OPCODE_CHATGLOBAL=62 [pending]
C_OPCODE_CHANGEHEADING=65 [pending]
C_OPCODE_ATTACK=68 [pending]
C_OPCODE_BANPARTY=70 [pending]
C_OPCODE_RESTART=71 [done]
C_OPCODE_BOARD=73 [pending]
C_OPCODE_LOGINTOSERVEROK=75 [pending]
C_OPCODE_WAREHOUSELOCK=81 [pending]
C_OPCODE_RANK=88 [pending]
C_OPCODE_MOVECHAR=95 [pending]
C_OPCODE_TITLE=96 [pending]
C_OPCODE_HIRESOLDIER=97 [pending]
C_OPCODE_ADDBUDDY=99 [pending]
C_OPCODE_EXCLUDE=101 [pending]
C_OPCODE_TRADE=103 [pending]
C_OPCODE_QUITGAME=104 [done]
C_OPCODE_FIX_WEAPON_LIST=106 [pending]
C_OPCODE_EMBLEM=107 [pending]
C_OPCODE_AMOUNT=109 [pending]
C_OPCODE_TRADEADDOK=110 [pending]
C_OPCODE_CAHTPARTY=113 [pending]
C_OPCODE_USESKILL=115 [pending]
C_OPCODE_SHIP=117 [pending]
C_OPCODE_LEAVECLANE=121 [pending]
C_OPCODE_CHATWHISPER=122 [pending]
C_OPCODE_CASTLESECURITY=125 [pending]
C_OPCODE_CLIENTVERSION=127 [pending]
C_OPCODE_CHARACTERCONFIG=129 [pending]
C_OPCODE_LOGINTOSERVER=131 [pending]
C_OPCODE_BOOKMARK=134 [pending]
C_OPCODE_CHECKPK=137 [pending]
C_OPCODE_CALL=144 [pending]
C_OPCODE_CREATECLAN=154 [pending]
C_OPCODE_SELECTTARGET=155 [pending]
C_OPCODE_EXTCOMMAND=157 [pending]
C_OPCODE_AUTOLOGIN=162 [pending]
C_OPCODE_CREATEPARTY=166 [pending]
C_OPCODE_TRADEADDCANCEL=167 [pending]
C_OPCODE_SKILLBUY=173 [pending]
C_OPCODE_KEEPALIVE=182 [pending]
C_OPCODE_PROPOSE=185 [pending]
C_OPCODE_PICKUPITEM=188 [pending]
C_OPCODE_CHAT=190 [done]
C_OPCODE_DRAWAL=192 [pending]
C_OPCODE_PRIVATESHOPLIST=193 [pending]
C_OPCODE_DOOR=199 [pending]
C_OPCODE_TAXRATE=200 [pending]
C_OPCODE_LEAVEPARTY=204 [pending]
C_OPCODE_SKILLBUYOK=207 [pending]
C_OPCODE_DELETEINVENTORYITEM=209 [pending]
C_OPCODE_EXIT_GHOST=210 [pending]
C_OPCODE_DELETEBUDDY=211 [pending]
C_OPCODE_USEPETITEM=213 [pending]
C_OPCODE_PETMENU=217 [pending]
C_OPCODE_RETURNTOLOGIN=218 [pending]
C_OPCODE_BOARDNEXT=221 [pending]
C_OPCODE_BANCLAN=222 [pending]
C_OPCODE_BOOKMARKDELETE=223 [pending]
C_OPCODE_PLEDGE=225 [pending]
C_OPCODE_TELEPORTLOCK=226 [pending]
C_OPCODE_PLEDGE_RECOMMENDATION=228 [pending]
C_OPCODE_WAR=235 [pending]
C_OPCODE_CHARRESET=236 [pending]
C_OPCODE_CHANGECHAR=237 [pending]
C_OPCODE_SELECTLIST=238 [pending]
C_OPCODE_TRADEADDITEM=241 [pending]
C_OPCODE_TELEPORT=242 [pending]
C_OPCODE_GIVEITEM=244 [done]
C_OPCODE_CLAN=246 [pending]
C_OPCODE_ARROWATTACK=247 [pending]
C_OPCODE_ENTERPORTAL=249 [pending]
C_OPCODE_NEWCHAR=253 [pending]

[S -> Client Receive]
S_OPCODE_MAIL=1 [pending]
S_OPCODE_DROPITEM=3 [pending]
S_OPCODE_CHARPACK=3 [pending]
S_OPCODE_TELEPORT=4 [pending]
S_OPCODE_DETELECHAROK=5 [pending]
S_OPCODE_GLOBALCHAT=10 [pending]
S_OPCODE_SYSMSG=10 [pending]
S_OPCODE_FIX_WEAPON_MENU=10 [pending]
S_OPCODE_BOOKMARKS=11 [pending]
S_OPCODE_PUTBOWSOLDIERLIST=11 [pending]
S_OPCODE_BLESSOFEVA=12 [pending]
S_OPCODE_PUTHIRESOLDIER=13 [pending]
S_OPCODE_SERVERMSG=14 [pending]
S_OPCODE_OWNCHARATTRDEF=15 [pending]
S_OPCODE_RANGESKILLS=16 [pending]
S_OPCODE_CHAROUT=17 [pending]
S_OPCODE_DELSKILL=18 [pending]
S_OPCODE_HOUSELIST=24 [pending]
S_OPCODE_DEXUP=28 [pending]
S_OPCODE_CLAN=29 [pending]
S_OPCODE_COMMONNEWS=30 [pending]
S_OPCODE_LIQUOR=31 [pending]
S_OPCODE_CHARRESET=33 [pending]
S_OPCODE_PETCTRL=33 [pending]
S_OPCODE_ATTRIBUTE=35 [pending]
S_OPCODE_PACKETBOX=40 [pending]
S_OPCODE_ACTIVESPELLS=40 [pending]
S_OPCODE_SKILLICONGFX=40 [pending]
S_OPCODE_HPUPDATE=42 [pending]
S_OPCODE_UNDERWATER=42 [pending]
S_OPCODE_IDENTIFYDESC=43 [pending]
S_OPCODE_HOUSEMAP=44 [pending]
S_OPCODE_ADDSKILL=48 [pending]
S_OPCODE_EMBLEM=50 [pending]
S_OPCODE_LOGINRESULT=51 [pending]
S_OPCODE_LIGHT=53 [pending]
S_OPCODE_BOARDREAD=56 [pending]
S_OPCODE_INVIS=57 [pending]
S_OPCODE_BLUEMESSAGE=59 [pending]
S_OPCODE_ADDITEM=63 [pending]
S_OPCODE_BOARD=64 [pending]
S_OPCODE_CASTLEMASTER=66 [pending]
S_OPCODE_SKILLMAKE=68 [pending]
S_OPCODE_SKILLICONSHIELD=69 [pending]
S_OPCODE_TAXRATE=72 [pending]
S_OPCODE_MPUPDATE=73 [pending]
S_OPCODE_NORMALCHAT=76 [pending]
S_OPCODE_TRADE=77 [pending]
S_OPCODE_CHANGENAME=81 [pending]
S_OPCODE_SOUND=84 [pending]
S_OPCODE_TRADEADDITEM=86 [pending]
S_OPCODE_REDMESSAGE=90 [pending]
S_OPCODE_POISON=93 [pending]
S_OPCODE_DISCONNECT=95 [done]
S_OPCODE_USEMAP=100 [pending]
S_OPCODE_TRUETARGET=110 [pending]
S_OPCODE_EFFECTLOCATION=112 [pending]
S_OPCODE_CHARVISUALUPDATE=113 [pending]
S_OPCODE_ABILITY=116 [pending]
S_OPCODE_SHOWHTML=119 [pending]
S_OPCODE_STRUP=120 [pending]
S_OPCODE_EXP=121 [done]
S_OPCODE_MOVEOBJECT=122 [pending]
S_OPCODE_WAR=123 [done]
S_OPCODE_CHARAMOUNT=126 [pending]
S_OPCODE_HIRESOLDIER=126 [pending]
S_OPCODE_ITEMAMOUNT=127 [pending]
S_OPCODE_ITEMSTATUS=127 [pending]
S_OPCODE_HPMETER=128 [done]
S_OPCODE_LOGINTOGAME=131 [pending]
S_OPCODE_NPCSHOUT=133 [pending]
S_OPCODE_TELEPORTLOCK=135 [pending]
S_OPCODE_LAWFUL=140 [done]
S_OPCODE_ATTACKPACKET=142 [pending]
S_OPCODE_ITEMCOLOR=144 [pending]
S_OPCODE_OWNCHARSTATUS=145 [pending]
S_OPCODE_DELETEINVENTORYITEM=148 [pending]
S_OPCODE_SKILLHASTE=149 [pending]
S_OPCODE_WARTIME=150 [pending]
S_OPCODE_MAPID=150 [pending]
S_OPCODE_SERVERVERSION=151 [pending]
S_OPCODE_NEWCHARWRONG=153 [pending]
S_OPCODE_YES_NO=155 [pending]
S_OPCODE_INITPACKET=161 [pending]
S_OPCODE_POLY=164 [pending]
S_OPCODE_PARALYSIS=165 [pending]
S_OPCODE_SHOWSHOPSELLLIST=170 [pending]
S_OPCODE_SPMR=174 [pending]
S_OPCODE_SELECTTARGET=177 [pending]
S_OPCODE_INVLIST=180 [pending]
S_OPCODE_CHARLIST=184 [pending]
S_OPCODE_REMOVE_OBJECT=185 [pending]
S_OPCODE_PRIVATESHOPLIST=190 [pending]
S_OPCODE_PLEDGE_RECOMMENDATION=192 [pending]
S_OPCODE_WEATHER=193 [pending]
S_OPCODE_GAMETIME=194 [pending]
S_OPCODE_ITEMNAME=195 [done]
S_OPCODE_CHANGEHEADING=199 [pending]
S_OPCODE_SKILLBRAVE=200 [pending]
S_OPCODE_CHARTITLE=202 [pending]
S_OPCODE_DEPOSIT=203 [pending]
S_OPCODE_SELECTLIST=208 [pending]
S_OPCODE_NEWCHARPACK=212 [pending]
S_OPCODE_OWNCHARSTATUS2=216 [pending]
S_OPCODE_DOACTIONGFX=218 [done]
S_OPCODE_SKILLBUY=222 [pending]
S_OPCODE_DRAWAL=224 [pending]
S_OPCODE_RESURRECTION=227 [done]
S_OPCODE_SKILLSOUNDGFX=232 [pending]
S_OPCODE_CURSEBLIND=238 [pending]
S_OPCODE_TRADESTATUS=239 [pending]
S_OPCODE_SHOWRETRIEVELIST=250 [pending]
S_OPCODE_PINKNAME=252 [pending]
S_OPCODE_INPUTAMOUNT=253 [pending]
S_OPCODE_SHOWSHOPBUYLIST=254 [pending]
S_OPCODE_WHISPERCHAT=255 [pending]
```

## Next Actions (Auto)
- Inventory client opcode usage (handlers/builders) and map to server list.
- Record mismatches + fix list.
- Apply fixes in `/game2` only.

## Client Opcode Usage (auto-scan)
### Client Send Opcodes (WriteByte)
- 0: server=UNKNOWN
  - Client/Boot.cs
  - Client/Game/GameWorld.Chat.cs
- 12: server=C_OPCODE_BOARDDELETE
  - Client/Game/GameWorld.Pet.cs
  - Client/Network/C_ShopPacket.cs
- 16: server=C_OPCODE_SHOP
  - Client/Game/GameWorld.Pet.cs
  - Client/Network/C_ShopPacket.cs
- 37: server=C_OPCODE_NPCACTION
  - Client/Network/C_NpcPacket.cs
- 44: server=C_OPCODE_USEITEM
  - Client/Game/GameWorld.Inventory.cs
- 53: server=C_OPCODE_COMMONCLICK
  - Client/Boot.cs
- 57: server=C_OPCODE_LOGINPACKET
  - Client/Boot.cs
  - Client/Network/C_LoginPacket.cs
- 58: server=C_OPCODE_NPCTALK
  - Client/Network/C_NpcPacket.cs
- 67: server=UNKNOWN
  - Client/Network/C_StatDicePacket.cs
- 68: server=C_OPCODE_ATTACK
  - Client/Game/GameWorld.Combat.cs
  - Client/Network/C_AttackPacket.cs
- 71: server=C_OPCODE_RESTART
  - Client/Game/GameWorld.UI.cs
- 95: server=C_OPCODE_MOVECHAR
  - Client/Network/C_MoveCharPacket.cs
- 104: server=C_OPCODE_QUITGAME
  - Client/Boot.cs
- 115: server=C_OPCODE_USESKILL
  - Client/Network/C_MagicPacket.cs
- 127: server=C_OPCODE_CLIENTVERSION
  - Client/Network/GodotTcpSession.cs
- 131: server=C_OPCODE_LOGINTOSERVER
  - Client/Boot.cs
  - Client/Network/C_EnterWorldPacket.cs
- 173: server=C_OPCODE_SKILLBUY
  - Client/Network/C_SkillBuyPacket.cs
- 188: server=C_OPCODE_PICKUPITEM
  - Client/Network/C_ItemPickupPacket.cs
- 190: server=C_OPCODE_CHAT
  - Client/Game/GameWorld.Chat.cs
- 207: server=C_OPCODE_SKILLBUYOK
  - Client/Network/C_SkillBuyPacket.cs
- 244: server=C_OPCODE_GIVEITEM
  - Client/Network/C_GiveItemPacket.cs
- 247: server=C_OPCODE_ARROWATTACK
  - Client/Game/GameWorld.Combat.cs
- 253: server=C_OPCODE_NEWCHAR
  - Client/Network/C_CreateCharPacket.cs

### Client Receive Opcodes (PacketHandler cases)
- 3: server=S_OPCODE_CHARPACK
- 10: server=S_OPCODE_FIX_WEAPON_MENU
- 14: server=S_OPCODE_SERVERMSG
- 15: server=S_OPCODE_OWNCHARATTRDEF
- 17: server=S_OPCODE_CHAROUT
- 18: server=S_OPCODE_DELSKILL
- 23: server=UNKNOWN
- 24: server=S_OPCODE_HOUSELIST
- 27: server=UNKNOWN
- 28: server=S_OPCODE_DEXUP
- 29: server=S_OPCODE_CLAN
- 30: server=S_OPCODE_COMMONNEWS
- 33: server=S_OPCODE_PETCTRL
- 34: server=UNKNOWN
- 37: server=C_OPCODE_NPCACTION
- 38: server=UNKNOWN
- 40: server=S_OPCODE_SKILLICONGFX
- 42: server=S_OPCODE_UNDERWATER
- 43: server=S_OPCODE_IDENTIFYDESC
- 44: server=S_OPCODE_HOUSEMAP
- 45: server=C_OPCODE_SMS
- 48: server=S_OPCODE_ADDSKILL
- 51: server=S_OPCODE_LOGINRESULT
- 55: server=UNKNOWN
- 57: server=S_OPCODE_INVIS
- 63: server=S_OPCODE_ADDITEM
- 69: server=S_OPCODE_SKILLICONSHIELD
- 71: server=C_OPCODE_RESTART
- 73: server=S_OPCODE_MPUPDATE
- 76: server=S_OPCODE_NORMALCHAT
- 77: server=S_OPCODE_TRADE
- 78: server=UNKNOWN
- 79: server=UNKNOWN
- 81: server=S_OPCODE_CHANGENAME
- 83: server=UNKNOWN
- 84: server=S_OPCODE_SOUND
- 86: server=S_OPCODE_TRADEADDITEM
- 93: server=S_OPCODE_POISON
- 95: server=S_OPCODE_DISCONNECT
- 106: server=C_OPCODE_FIX_WEAPON_LIST
- 111: server=UNKNOWN
- 113: server=S_OPCODE_CHARVISUALUPDATE
- 119: server=S_OPCODE_SHOWHTML
- 120: server=S_OPCODE_STRUP
- 122: server=S_OPCODE_MOVEOBJECT
- 123: server=S_OPCODE_WAR
- 126: server=S_OPCODE_HIRESOLDIER
- 128: server=S_OPCODE_HPMETER
- 131: server=S_OPCODE_LOGINTOGAME
- 140: server=S_OPCODE_LAWFUL
- 142: server=S_OPCODE_ATTACKPACKET
- 145: server=S_OPCODE_OWNCHARSTATUS
- 148: server=S_OPCODE_DELETEINVENTORYITEM
- 149: server=S_OPCODE_SKILLHASTE
- 150: server=S_OPCODE_MAPID
- 151: server=S_OPCODE_SERVERVERSION
- 153: server=S_OPCODE_NEWCHARWRONG
- 164: server=S_OPCODE_POLY
- 174: server=S_OPCODE_SPMR
- 180: server=S_OPCODE_INVLIST
- 184: server=S_OPCODE_CHARLIST
- 185: server=S_OPCODE_REMOVE_OBJECT
- 193: server=S_OPCODE_WEATHER
- 194: server=S_OPCODE_GAMETIME
- 199: server=S_OPCODE_CHANGEHEADING
- 200: server=S_OPCODE_SKILLBRAVE
- 202: server=S_OPCODE_CHARTITLE
- 212: server=S_OPCODE_NEWCHARPACK
- 218: server=S_OPCODE_DOACTIONGFX
- 221: server=C_OPCODE_BOARDNEXT
- 227: server=S_OPCODE_RESURRECTION
- 232: server=S_OPCODE_SKILLSOUNDGFX
- 238: server=S_OPCODE_CURSEBLIND
- 239: server=S_OPCODE_TRADESTATUS
- 250: server=S_OPCODE_SHOWRETRIEVELIST

### Missing In Client Send (by opcode value)
```
[3, 4, 5, 7, 9, 10, 14, 22, 26, 30, 35, 40, 41, 42, 45, 47, 49, 54, 59, 60, 61, 62, 65, 70, 73, 75, 81, 88, 96, 97, 99, 101, 103, 106, 107, 109, 110, 113, 117, 121, 122, 125, 129, 134, 137, 144, 154, 155, 157, 162, 166, 167, 182, 185, 192, 193, 199, 200, 204, 209, 210, 211, 213, 217, 218, 221, 222, 223, 225, 226, 228, 235, 236, 237, 238, 241, 242, 246, 249]
```

### Missing In Client Receive (by opcode value)
```
[1, 4, 5, 11, 12, 13, 16, 31, 35, 50, 53, 56, 59, 64, 66, 68, 72, 90, 100, 110, 112, 116, 121, 127, 133, 135, 144, 155, 161, 165, 170, 177, 190, 192, 195, 203, 208, 216, 222, 224, 252, 253, 254, 255]
```

### 2026-02-03 (cont.)
- Fix: `C_NpcPacket.MakeAction` opcode corrected from 39 to 37 (`C_OPCODE_NPCACTION`).
- Fix: `C_GiveItemPacket` opcode corrected from 17 to 244 (`C_OPCODE_GIVEITEM`).
- Fix: `Boot.Action_QuitGame` opcode corrected from 15 to 104 (`C_OPCODE_QUITGAME`).
- Fix: `GameWorld.SendChatPacket` opcode corrected from 19 to 190 (`C_OPCODE_CHAT`).
- Fix: `GameWorld.SendRestartRequest` now sends `C_OPCODE_RESTART` (71) instead of returning to CharacterSelect.

### 2026-02-03 (cont.)
- Fix: `GameWorld.Pet.OpenPetWarehouse` opcode corrected from 40 to 16 (`C_OPCODE_SHOP`).
- Fix: `GameWorld.Pet.FeedMonster` comment updated to `C_OPCODE_GIVEITEM=244`.
- Fix: `Boot.Action_RequestNewChar` no longer sends Op 67 (no server opcode in jp).

### Server crash / restart UI behavior
- Server does not send a “restart window” packet. A crash results in socket close; optional normal shutdown may send `S_OPCODE_DISCONNECT=95`.
- Restart UI is client-side; restart action should send `C_OPCODE_RESTART=71` to server (implemented).

### 2026-02-03 (cont.)
- Add: `S_OPCODE_DISCONNECT=95` parsing in `PacketHandler` with code extraction + system message.

### 2026-02-03 (cont.)
- Fix: `SendClientOption` no longer sends unknown opcode 13 (jp has no C opcode 13). Now logs and applies locally only.

### 2026-02-03 (cont.)
- Add: `S_OPCODE_RESURRECTION=227` parsing in `PacketHandler` (logs + SystemMessage).

### 2026-02-03 (cont.)
- Fix: `S_OPCODE_HPMETER` handler corrected to opcode 128 (was 104).

### 2026-02-03 (cont.)
- Add: `S_OPCODE_ITEMAMOUNT/S_OPCODE_ITEMSTATUS=127` parsing to update inventory name, count, and status detail.
- Record tested functions in `tested-functions.md` (do not modify those behaviors).

### 2026-02-03 (cont.)
- Move speed alignment: match jp AcceleratorChecker strictness (CHECK_STRICTNESS=102 -> +3.1% interval) and use weapon-based move action id in SprDataTable.
- Move send gating uses strict minimum interval (no 0.97 slack) to avoid overspeed false positives and walk-stop jitter.

### 2026-02-03 (cont.)
- Fix: `SkillDbData` now loads `action_id` and supports lookup for skill animation timing.
- Fix: `SkillCooldownManager` now supports per-cast cooldown override (ms) and uses ms-precision for gating.
- Fix: `UseMagic` now gates by skill cooldown + action_id interval and records effective cooldown for UI display.
- Fix: `SkillCooldownDisplay` shows sub-1s cooldowns (min display 0.1s).
- Fix: Mage bow range: mage Z-magic no longer overrides bow attacks; bow range takes priority.
- Fix: `S_ObjectPoly` parsing handles jp opcode 164 structure (objId + polyId + takeoff flag).
- Fix: player self now receives poly/effect visuals via `OnObjectVisualUpdated` and `OnObjectEffect`.

### 2026-02-03 (cont.)
- Add: magic cooldown debug log (throttled) in `GameWorld.Skill` to record `skillId`, `actionId`, `reuseDelayMs`, `animIntervalMs`, and `effectiveCooldownMs`.
- Fix: `SkillCooldownDisplay` now renders cooldown time in milliseconds (e.g., `640ms`) with color thresholds.
- Fix: `PacketHandler.ParseObjectAttackMagic` now parses short `S_SkillSound` packets (opcode 232) and emits `ObjectEffect` to play potion/scroll effects (prevents `GfxId=0` skip).
- Fix: `GameWorld.OnObjectVisualUpdated` now calls `entity.RefreshVisual()` to ensure polymorph visuals apply.
- Fix: Skill range now uses `skills.csv` `ranged` value via `SkillDbData.GetRanged`.
- Fix: Magic auto-attack range now respects skill range (not melee) in `GetAttackRange`.
- Improve: throttle repetitive “Hotkey Skill blocked” logs and include remaining cooldown ms.
