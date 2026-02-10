/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:28:49
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for slimerace_log
-- ----------------------------
CREATE TABLE `slimerace_log` (
  `uid` int(10) NOT NULL,
  `search_item` int(10) unsigned NOT NULL,
  PRIMARY KEY (`uid`)
) ENGINE=MyISAM AUTO_INCREMENT=2 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
