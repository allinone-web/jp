/*
Navicat MySQL Data Transfer

Source Server         : localhost_3306
Source Server Version : 50519
Source Host           : localhost:3306
Source Database       : lineage

Target Server Type    : MYSQL
Target Server Version : 50519
File Encoding         : 65001

Date: 2015-02-04 01:34:48
*/

SET FOREIGN_KEY_CHECKS=0;

-- ----------------------------
-- Table structure for `npc_teleport`
-- ----------------------------
DROP TABLE IF EXISTS `npc_teleport`;
CREATE TABLE `npc_teleport` (
  `action` varchar(100) NOT NULL DEFAULT '',
  `npc_id` int(10) NOT NULL,
  `tele_num` int(1) NOT NULL,
  `check_lv_min` int(10) DEFAULT '0' COMMENT '大于等于该等级条件通过',
  `check_lv_max` int(10) DEFAULT '0' COMMENT '小于等于该等级条件通过',
  `check_map` int(10) unsigned NOT NULL DEFAULT '0',
  `x` int(10) unsigned NOT NULL DEFAULT '0',
  `y` int(10) unsigned NOT NULL DEFAULT '0',
  `map` int(10) unsigned NOT NULL DEFAULT '0',
  `aden` int(10) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`check_map`,`action`),
  KEY `action` (`action`),
  KEY `npc_id` (`npc_id`),
  KEY `check_map` (`check_map`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COMMENT='NPC传送';

-- ----------------------------
-- Records of npc_teleport
-- ----------------------------
INSERT INTO `npc_teleport` VALUES ('teleport island-gludin', '135', '1', '0', '0', '0', '32612', '32734', '4', '1500');
INSERT INTO `npc_teleport` VALUES ('teleport gludin-island', '136', '1', '0', '0', '4', '32583', '32924', '0', '3200');
INSERT INTO `npc_teleport` VALUES ('teleport gludin-kent', '136', '2', '0', '0', '4', '33050', '32782', '4', '550');
INSERT INTO `npc_teleport` VALUES ('teleport gludin-willow', '136', '3', '0', '0', '4', '32750', '32441', '4', '150');
INSERT INTO `npc_teleport` VALUES ('teleport gludin-woods', '136', '4', '0', '0', '4', '32640', '33203', '4', '400');
INSERT INTO `npc_teleport` VALUES ('teleport giran-dragonvalley', '373', '2', '0', '0', '4', '33432', '32546', '4', '1000');
INSERT INTO `npc_teleport` VALUES ('teleport giran-heine', '373', '3', '0', '0', '4', '33612', '33257', '4', '450');
INSERT INTO `npc_teleport` VALUES ('teleport giran-kent', '373', '1', '0', '0', '4', '33050', '32782', '4', '550');
INSERT INTO `npc_teleport` VALUES ('teleport giran-werldern', '373', '4', '0', '0', '4', '33709', '32500', '4', '710');
INSERT INTO `npc_teleport` VALUES ('teleport heine-giran', '424', '1', '0', '0', '4', '33438', '32796', '4', '990');
INSERT INTO `npc_teleport` VALUES ('teleport heine-silver', '424', '2', '0', '0', '4', '33080', '33386', '4', '450');
INSERT INTO `npc_teleport` VALUES ('teleport heine-werldern', '424', '3', '0', '0', '4', '33709', '32500', '4', '710');
INSERT INTO `npc_teleport` VALUES ('teleport kent-giran', '137', '2', '0', '0', '4', '33438', '32796', '4', '990');
INSERT INTO `npc_teleport` VALUES ('teleport kent-gludin', '137', '1', '0', '0', '4', '32608', '32734', '4', '680');
INSERT INTO `npc_teleport` VALUES ('teleport silver-heine', '219', '3', '0', '0', '4', '33612', '33257', '4', '450');
INSERT INTO `npc_teleport` VALUES ('teleport silver-kent', '219', '1', '0', '0', '4', '33050', '32782', '4', '550');
INSERT INTO `npc_teleport` VALUES ('teleport silver-woods', '219', '2', '0', '0', '4', '32640', '33203', '4', '400');
INSERT INTO `npc_teleport` VALUES ('teleport woods-silver', '218', '2', '0', '0', '4', '33080', '33386', '4', '500');
INSERT INTO `npc_teleport` VALUES ('teleport dwarf-giran', '468', '1', '0', '0', '4', '33438', '32796', '4', '990');
INSERT INTO `npc_teleport` VALUES ('teleport dwarf-heine', '468', '2', '0', '0', '4', '33612', '33257', '4', '350');
INSERT INTO `npc_teleport` VALUES ('teleport woods-gludin', '218', '1', '0', '0', '4', '32608', '32734', '4', '710');
INSERT INTO `npc_teleport` VALUES ('teleport hidden-velley-for-newbie', '410', '1', '0', '0', '69', '33086', '33389', '4', '0');
INSERT INTO `npc_teleport` VALUES ('teleport talking-island-for-newbie', '406', '1', '0', '0', '69', '32599', '32915', '0', '0');
INSERT INTO `npc_teleport` VALUES ('teleport werldern-oren', '468', '3', '0', '0', '4', '34062', '32279', '4', '550');
INSERT INTO `npc_teleport` VALUES ('teleport oren-werldern', '70532', '1', '0', '0', '4', '33709', '32500', '4', '710');
INSERT INTO `npc_teleport` VALUES ('teleport heine-yiwang', '70550', '1', '45', '0', '4', '32936', '33058', '70', '100000');
INSERT INTO `npc_teleport` VALUES ('teleport escape-forgotten-island', '70552', '1', '0', '0', '70', '33433', '33484', '4', '0');
INSERT INTO `npc_teleport` VALUES ('teleport escape-forgotten-island1', '70555', '1', '0', '0', '70', '33433', '33484', '4', '0');
INSERT INTO `npc_teleport` VALUES ('teleport singing-island', '407', '1', '0', '12', '0', '32688', '32847', '69', '0');
INSERT INTO `npc_teleport` VALUES ('teleport hidden-valley', '411', '1', '0', '12', '4', '32687', '32871', '69', '0');
INSERT INTO `npc_teleport` VALUES ('teleport elvenforest-in', '70561', '1', '0', '0', '69', '33052', '32347', '4', '0');
INSERT INTO `npc_teleport` VALUES ('teleport valley-in', '70565', '1', '0', '12', '4', '32696', '32852', '69', '0');
INSERT INTO `npc_teleport` VALUES ('teleport hidden-velley-for-newbie1', '70560', '1', '0', '0', '69', '33086', '33389', '4', '0');
