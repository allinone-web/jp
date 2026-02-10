/*
Navicat MariaDB Data Transfer

Source Server         : localhost
Source Server Version : 100014
Source Host           : localhost:3306
Source Database       : lineage

Target Server Type    : MariaDB
Target Server Version : 100014
File Encoding         : 65001

Date: 2015-11-17 13:44:28
*/

SET FOREIGN_KEY_CHECKS=0;

-- ----------------------------
-- Table structure for beginner
-- ----------------------------
DROP TABLE IF EXISTS `beginner`;
CREATE TABLE `beginner` (
  `id` int(10) NOT NULL AUTO_INCREMENT,
  `item_id` int(6) NOT NULL DEFAULT '0',
  `count` int(10) NOT NULL DEFAULT '0',
  `charge_count` int(10) NOT NULL DEFAULT '0',
  `enchantlvl` int(6) NOT NULL DEFAULT '0',
  `item_name` varchar(50) NOT NULL DEFAULT '',
  `activate` char(1) NOT NULL DEFAULT 'A',
  `bless` int(11) unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=37 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records of beginner
-- ----------------------------
INSERT INTO `beginner` VALUES ('1', '351', '1', '0', '0', '初学者之剑', 'A', '1');
INSERT INTO `beginner` VALUES ('2', '352', '1', '0', '0', '初学者之皮夹克', 'A', '1');
INSERT INTO `beginner` VALUES ('3', '353', '1', '0', '0', '初学者之弓', 'A', '1');
INSERT INTO `beginner` VALUES ('4', '44', '500', '0', '0', '银箭', 'A', '1');
INSERT INTO `beginner` VALUES ('10', '40145', '1', '0', '0', '红色 烟火', 'P', '1');
INSERT INTO `beginner` VALUES ('11', '40141', '1', '0', '0', '蓝色 烟火', 'K', '1');
INSERT INTO `beginner` VALUES ('12', '40152', '1', '0', '0', '绿色 烟火', 'E', '1');
INSERT INTO `beginner` VALUES ('13', '40160', '1', '0', '0', '黄色 烟火', 'W', '1');
