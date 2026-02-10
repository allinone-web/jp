/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:26:27
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for characters_skills
-- ----------------------------
CREATE TABLE `characters_skills` (
  `uid` int(10) NOT NULL AUTO_INCREMENT,
  `char_id` int(10) DEFAULT NULL,
  `skill_id` int(10) DEFAULT NULL,
  `skill_name` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`uid`),
  KEY `uid` (`uid`)
) ENGINE=InnoDB AUTO_INCREMENT=292787 DEFAULT CHARSET=utf8 COMMENT='技能';

-- ----------------------------
-- Records 
-- ----------------------------
