-- ----------------------------
-- Table structure for polymorphs
-- 【對齊 182】完整變身表：個別裝備布林欄位 + 英文 db 查找鍵
-- ----------------------------
DROP TABLE IF EXISTS `polymorphs`;
CREATE TABLE `polymorphs` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `db` varchar(255) NOT NULL,
  `polyid` int(11) NOT NULL,
  `minlevel` int(11) NOT NULL DEFAULT '0',
  `isWeapon` int(1) NOT NULL DEFAULT '0',
  `isHelm` int(1) NOT NULL DEFAULT '0',
  `isEarring` int(1) NOT NULL DEFAULT '0',
  `isNecklace` int(1) NOT NULL DEFAULT '0',
  `isT` int(1) NOT NULL DEFAULT '0',
  `isArmor` int(1) NOT NULL DEFAULT '0',
  `isCloak` int(1) NOT NULL DEFAULT '0',
  `isRing` int(1) NOT NULL DEFAULT '0',
  `isBelt` int(1) NOT NULL DEFAULT '0',
  `isGlove` int(1) NOT NULL DEFAULT '0',
  `isShield` int(1) NOT NULL DEFAULT '0',
  `isBoots` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=59 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records (完整 182 數據)
-- ----------------------------
INSERT INTO `polymorphs` VALUES ('1', '妖魔', 'orc', '56', '0', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('2', '侏儒', 'dwarf', '54', '0', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('3', '人形僵屍', 'zombie', '52', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('4', '高崙石頭怪', 'stone golem', '49', '0', '7', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('5', '長者', 'elder', '32', '15', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('6', '狼人', 'werewolf', '1110', '0', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('7', '骷髏', 'skeleton', '30', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('8', '漂浮之眼', 'floating eye', '29', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('9', '妖魔弓箭手', 'bow orc', '57', '0', '4', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('10', '狼', 'wolf', '96', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('11', '妖魔鬥士', 'orc fighter', '94', '0', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('12', '夏洛伯', 'shelob', '95', '10', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('13', '食屍鬼', 'ghoul', '144', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('14', '史巴托', 'spartoi', '145', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('15', '楊果里恩', 'ungoliant', '146', '10', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('16', '食人妖精', 'bugbear', '152', '10', '7', '1', '1', '0', '0', '0', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorphs` VALUES ('17', '卡司特', 'ghast', '255', '10', '7', '1', '1', '0', '0', '0', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorphs` VALUES ('18', '阿吐巴妖魔', 'atuba orc', '786', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('19', '那魯加妖魔', 'neruga orc', '788', '10', '5', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('20', '都達瑪拉妖魔', 'dudamara orc', '788', '10', '5', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('21', '甘地妖魔', 'gandi orc', '784', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('22', '羅孚妖魔', 'rova orc', '784', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('23', '聖伯納犬', 'saint burnard', '929', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('24', '杜賓狗', 'dobermann', '931', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('25', '柯利', 'collie', '934', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('26', '牧羊犬', 'shepherd', '936', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('27', '小獵犬', 'beagle', '938', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('28', '地獄犬', 'cerberus', '951', '15', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('29', '奶牛', 'milkcow', '945', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('30', '鹿', 'deer', '947', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('31', '野豬', 'wild boar', '979', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('32', '食人妖精王', 'king bugbear', '894', '15', '7', '1', '1', '0', '0', '0', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorphs` VALUES ('33', '歐吉', 'ogre', '1020', '15', '7', '1', '1', '0', '0', '0', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorphs` VALUES ('34', '哥布林', 'goblin', '1022', '0', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('35', '毒蠍', 'scorpion', '1047', '15', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('36', '地靈', 'kobolds', '1059', '0', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('37', '骷髏弓箭手', 'skeleton archer', '1096', '10', '4', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('38', '骷髏斧手', 'skeleton axeman', '1104', '10', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('39', '骷髏槍兵', 'skeleton pike', '1106', '10', '5', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('40', '多羅', 'troll', '1098', '15', '7', '1', '1', '0', '0', '0', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorphs` VALUES ('41', '萊肯', 'lycanthrope', '1108', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('42', '獨眼巨人', 'cyclops', '1202', '40', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('43', '黑暗精靈', 'dark elf', '1125', '52', '4', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('44', '格利芬', 'griffon', '1204', '40', '0', '0', '0', '0', '0', '0', '1', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('45', '亞力安', 'cockatrice', '1052', '40', '0', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('46', '阿魯巴', 'ettin', '1128', '40', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('47', '黑長者', 'dark elder', '183', '45', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('48', '馬庫爾', 'necromancer3', '187', '45', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('49', '西瑪', 'necromancer4', '183', '45', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('50', '巴士瑟', 'necromancer1', '185', '45', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('51', '卡士伯', 'necromancer2', '173', '45', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('52', '巴風特', 'baphomet', '53', '50', '7', '0', '1', '1', '1', '1', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorphs` VALUES ('53', '巴列斯', 'beleth', '1011', '50', '7', '0', '1', '1', '1', '1', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorphs` VALUES ('54', '惡魔', 'demon', '1180', '51', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('55', '死亡騎士', 'death knight', '240', '52', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('56', '妖魔巡守', 'orc scout polymorph', '2323', '15', '4', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorphs` VALUES ('57', '巨蚁', 'giant ant', '1037', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorphs` VALUES ('58', '巨大兵蚁', 'giant ant soldier', '1039', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
