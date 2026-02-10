/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:26:15
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for characters_letter
-- ----------------------------
CREATE TABLE `characters_letter` (
  `uid` int(10) unsigned NOT NULL,
  `type` enum('savePaper','clanPaper','Paper') NOT NULL,
  `paperFrom` varchar(20) NOT NULL,
  `paperTo` varchar(20) NOT NULL,
  `paperSubject` varchar(20) NOT NULL,
  `paperMemo` text NOT NULL,
  `paperYear` tinyint(3) NOT NULL,
  `paperMonth` tinyint(3) NOT NULL,
  `paperDate` tinyint(3) NOT NULL,
  `paperInventory` tinyint(3) NOT NULL DEFAULT '0',
  PRIMARY KEY (`uid`),
  KEY `type` (`type`),
  KEY `paperFrom` (`paperFrom`),
  KEY `uid` (`uid`)
) ENGINE=MyISAM AUTO_INCREMENT=9 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
