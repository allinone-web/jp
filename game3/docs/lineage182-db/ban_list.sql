/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:25:35
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for ban_list
-- ----------------------------
CREATE TABLE `ban_list` (
  `ip` varchar(20) NOT NULL,
  PRIMARY KEY (`ip`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
