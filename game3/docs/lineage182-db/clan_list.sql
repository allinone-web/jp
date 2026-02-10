/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:26:33
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for clan_list
-- ----------------------------
CREATE TABLE `clan_list` (
  `ClanId` int(10) unsigned NOT NULL DEFAULT '0',
  `ClanName` varchar(20) DEFAULT NULL,
  `lord` varchar(20) DEFAULT NULL,
  `Icon` text,
  `List` text,
  `WarClan` varchar(20) DEFAULT NULL,
  PRIMARY KEY (`ClanId`),
  KEY `ClanId` (`ClanId`),
  KEY `ClanName` (`ClanName`),
  KEY `WarClan` (`WarClan`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
