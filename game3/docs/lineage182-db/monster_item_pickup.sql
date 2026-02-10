/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:27:52
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for monster_item_pickup
-- ----------------------------
CREATE TABLE `monster_item_pickup` (
  `uid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(50) NOT NULL DEFAULT '',
  `monid` int(10) unsigned NOT NULL DEFAULT '0',
  `itemit` int(10) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`uid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COMMENT='怪物拾物';

-- ----------------------------
-- Records 
-- ----------------------------
