/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2015/4/29 9:12:15
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for char_hell
-- ----------------------------
CREATE TABLE `char_hell` (
  `objid` int(10) unsigned NOT NULL DEFAULT '0',
  `name` varchar(20) DEFAULT NULL,
  `time` int(10) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`objid`),
  KEY `ClanId` (`objid`),
  KEY `ClanName` (`name`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
