/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:28:55
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for warehouse
-- ----------------------------
CREATE TABLE `warehouse` (
  `uid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `account_uid` int(10) unsigned NOT NULL DEFAULT '0',
  `inv_id` int(10) unsigned NOT NULL DEFAULT '0',
  `pet_id` int(10) unsigned NOT NULL DEFAULT '0',
  `letter_id` int(10) unsigned NOT NULL DEFAULT '0',
  `id` int(10) unsigned NOT NULL DEFAULT '0',
  `type` tinyint(3) unsigned NOT NULL,
  `gfxid` int(2) NOT NULL DEFAULT '0',
  `name` varchar(255) NOT NULL DEFAULT '',
  `count` int(10) unsigned NOT NULL DEFAULT '0',
  `have_count` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `en` tinyint(3) NOT NULL DEFAULT '0',
  `definite` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `bless` tinyint(3) NOT NULL DEFAULT '0',
  `durability` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `time` int(10) NOT NULL,
  `slimerace_uid` int(10) unsigned NOT NULL DEFAULT '0',
  `slimeracer_idx` int(10) unsigned NOT NULL DEFAULT '0',
  `slimeracer_name` varchar(255) NOT NULL DEFAULT '',
  PRIMARY KEY (`uid`),
  KEY `account_uid` (`account_uid`)
) ENGINE=MyISAM AUTO_INCREMENT=26380 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
