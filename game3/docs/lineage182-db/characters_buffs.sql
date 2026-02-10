/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:26:04
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for characters_buffs
-- ----------------------------
CREATE TABLE `characters_buffs` (
  `uid` int(10) NOT NULL AUTO_INCREMENT,
  `char_id` int(10) NOT NULL,
  `type` varchar(50) NOT NULL,
  `tid` int(10) NOT NULL,
  `ttime` int(10) NOT NULL,
  PRIMARY KEY (`uid`),
  KEY `uid` (`uid`)
) ENGINE=InnoDB AUTO_INCREMENT=91413 DEFAULT CHARSET=utf8 COMMENT='状态';

-- ----------------------------
-- Records 
-- ----------------------------
