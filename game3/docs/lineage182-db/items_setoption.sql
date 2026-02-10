/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:27:14
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for items_setoption
-- ----------------------------
CREATE TABLE `items_setoption` (
  `uid` int(10) unsigned NOT NULL DEFAULT '0',
  `name` varchar(50) NOT NULL DEFAULT '',
  `count` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `add_hp` tinyint(3) NOT NULL DEFAULT '0',
  `add_mp` tinyint(3) NOT NULL DEFAULT '0',
  `add_str` tinyint(3) NOT NULL DEFAULT '0',
  `add_dex` tinyint(3) NOT NULL DEFAULT '0',
  `add_con` tinyint(3) NOT NULL DEFAULT '0',
  `add_int` tinyint(3) NOT NULL DEFAULT '0',
  `add_wis` tinyint(3) NOT NULL DEFAULT '0',
  `add_cha` tinyint(3) NOT NULL DEFAULT '0',
  `add_ac` tinyint(3) NOT NULL DEFAULT '0',
  `add_mr` tinyint(3) NOT NULL DEFAULT '0',
  `tic_hp` tinyint(3) NOT NULL DEFAULT '0',
  `tic_mp` tinyint(3) NOT NULL DEFAULT '0',
  `polymorph` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `windress` tinyint(3) NOT NULL DEFAULT '0',
  `wateress` tinyint(3) NOT NULL DEFAULT '0',
  `fireress` tinyint(3) NOT NULL DEFAULT '0',
  `earthress` tinyint(3) NOT NULL DEFAULT '0',
  `gm` tinyint(3) NOT NULL DEFAULT '0',
  PRIMARY KEY (`uid`),
  KEY `uid` (`uid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COMMENT='物品组合设置';

-- ----------------------------
-- Records 
-- ----------------------------
INSERT INTO `items_setoption` VALUES ('1', '歐西斯套裝', '4', '0', '0', '0', '0', '0', '0', '0', '0', '3', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `items_setoption` VALUES ('2', '侏儒套裝', '3', '5', '0', '0', '0', '0', '0', '0', '0', '1', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `items_setoption` VALUES ('3', '骷髏套裝', '3', '10', '0', '0', '0', '0', '0', '0', '0', '2', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `items_setoption` VALUES ('4', '法師套裝', '2', '0', '50', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0');
INSERT INTO `items_setoption` VALUES ('5', '鋼鐵套裝', '5', '0', '0', '0', '0', '0', '0', '0', '0', '3', '0', '0', '0', '0', '0', '0', '0', '0', '0');
