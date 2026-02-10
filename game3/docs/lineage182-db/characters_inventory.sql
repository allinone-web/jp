/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:26:10
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for characters_inventory
-- ----------------------------
CREATE TABLE `characters_inventory` (
  `uid` int(10) NOT NULL DEFAULT '0',
  `char_id` int(10) DEFAULT NULL,
  `pet_id` int(10) DEFAULT NULL,
  `letter_id` int(10) DEFAULT NULL,
  `item_id` int(10) DEFAULT NULL,
  `count` int(10) DEFAULT NULL,
  `have_count` int(10) DEFAULT NULL,
  `en` int(10) DEFAULT NULL,
  `equipped` int(1) DEFAULT NULL,
  `definite` int(1) DEFAULT NULL,
  `bless` int(1) DEFAULT NULL,
  `durability` int(10) DEFAULT NULL,
  `time` int(10) DEFAULT NULL,
  `slimerace_uid` int(10) DEFAULT NULL,
  `silmerace_idx` int(10) DEFAULT NULL,
  `silmerace_name` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`uid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='背包';

-- ----------------------------
-- Records 
-- ----------------------------
