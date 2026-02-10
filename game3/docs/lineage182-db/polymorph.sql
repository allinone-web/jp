/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2014-6-15 18:56:56
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for polymorph
-- ----------------------------
CREATE TABLE `polymorph` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `db` varchar(255) NOT NULL,
  `polyid` int(11) NOT NULL,
  `minlevel` int(11) NOT NULL,
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
-- Records 
-- ----------------------------
INSERT INTO `polymorph` VALUES ('30', '鹿', 'deer', '947', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('29', '奶牛', 'milkcow', '945', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('31', '野豬', 'wild boar', '979', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('1', '妖魔', 'orc', '56', '0', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('9', '妖魔弓箭手', 'bow orc', '57', '0', '4', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('34', '哥布林', 'goblin', '1022', '0', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('36', '地靈', 'kobolds', '1059', '0', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('2', '侏儒', 'dwarf', '54', '0', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('11', '妖魔鬥士', 'orc fighter', '94', '0', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('6', '狼人', 'werewolf', '1110', '0', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('4', '高崙石頭怪', 'stone golem', '49', '0', '7', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('8', '漂浮之眼', 'floating eye', '29', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('21', '甘地妖魔', 'gandi orc', '784', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('22', '羅孚妖魔', 'rova orc', '784', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('18', '阿吐巴妖魔', 'atuba orc', '786', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('20', '都達瑪拉妖魔', 'dudamara orc', '788', '10', '5', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('19', '那魯加妖魔', 'neruga orc', '788', '10', '5', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('7', '骷髏', 'skeleton', '30', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('39', '骷髏槍兵', 'skeleton pike', '1106', '10', '5', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('37', '骷髏弓箭手', 'skeleton archer', '1096', '10', '4', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('38', '骷髏斧手', 'skeleton axeman', '1104', '10', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('12', '夏洛伯', 'shelob', '95', '10', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('3', '人形僵屍', 'zombie', '52', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('13', '食屍鬼', 'ghoul', '144', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('14', '史巴托', 'spartoi', '145', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('41', '萊肯', 'lycanthrope', '1108', '10', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('15', '楊果里恩', 'ungoliant', '146', '10', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('17', '卡司特', 'ghast', '255', '10', '7', '1', '1', '0', '0', '0', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorph` VALUES ('16', '食人妖精', 'bugbear', '152', '10', '7', '1', '1', '0', '0', '0', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorph` VALUES ('28', '地獄犬', 'cerberus', '951', '15', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('35', '毒蠍', 'scorpion', '1047', '15', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('33', '歐吉', 'ogre', '1020', '15', '7', '1', '1', '0', '0', '0', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorph` VALUES ('40', '多羅', 'troll', '1098', '15', '7', '1', '1', '0', '0', '0', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorph` VALUES ('5', '長者', 'elder', '32', '15', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('32', '食人妖精王', 'king bugbear', '894', '15', '7', '1', '1', '0', '0', '0', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorph` VALUES ('42', '獨眼巨人', 'cyclops', '1202', '40', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('44', '格利芬', 'griffon', '1204', '40', '0', '0', '0', '0', '0', '0', '1', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('45', '亞力安', 'cockatrice', '1052', '40', '0', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('46', '阿魯巴', 'ettin', '1128', '40', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('47', '黑長者', 'dark elder', '183', '45', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('48', '馬庫爾', 'necromancer3', '187', '45', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('49', '西瑪', 'necromancer4', '183', '45', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('50', '巴士瑟', 'necromancer1', '185', '45', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('51', '卡士伯', 'necromancer2', '173', '45', '6', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('52', '巴風特', 'baphomet', '53', '50', '7', '0', '1', '1', '1', '1', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorph` VALUES ('53', '巴列斯', 'beleth', '1011', '50', '7', '0', '1', '1', '1', '1', '1', '1', '0', '1', '1', '0');
INSERT INTO `polymorph` VALUES ('54', '惡魔', 'demon', '1180', '51', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('55', '死亡騎士', 'death knight', '240', '52', '7', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('10', '狼', 'wolf', '96', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('23', '聖伯納犬', 'saint burnard', '929', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('24', '杜賓狗', 'dobermann', '931', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('25', '柯利', 'collie', '934', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('26', '牧羊犬', 'shepherd', '936', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('27', '小獵犬', 'beagle', '938', '99', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('43', '黑暗精靈', 'dark elf', '1125', '52', '4', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('56', '妖魔巡守', 'orc scout polymorph', '2323', '15', '4', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1', '1');
INSERT INTO `polymorph` VALUES ('57', '巨蚁', 'giant ant', '1037', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `polymorph` VALUES ('58', '巨大兵蚁', 'giant ant soldier', '1039', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
