/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:28:37
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for server
-- ----------------------------
CREATE TABLE `server` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `gametime` int(10) unsigned NOT NULL DEFAULT '0',
  `objectID` int(10) unsigned NOT NULL DEFAULT '1',
  `etcID` int(10) unsigned NOT NULL DEFAULT '10000000',
  `clanID` int(10) unsigned NOT NULL DEFAULT '1',
  `player_count` int(10) unsigned NOT NULL DEFAULT '0',
  `status` tinyint(1) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=2 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
INSERT INTO `server` VALUES ('1', '0', '10000', '1000000', '1', '0', '0');
