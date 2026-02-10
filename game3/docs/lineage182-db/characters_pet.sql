/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:26:21
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for characters_pet
-- ----------------------------
CREATE TABLE `characters_pet` (
  `id` int(10) unsigned NOT NULL DEFAULT '0',
  `name` varchar(20) NOT NULL DEFAULT '',
  `classId` int(10) unsigned NOT NULL,
  `level` int(10) unsigned NOT NULL DEFAULT '0',
  `nowHp` int(10) unsigned NOT NULL DEFAULT '0',
  `maxHp` int(10) unsigned NOT NULL DEFAULT '0',
  `nowMp` int(10) unsigned NOT NULL DEFAULT '0',
  `maxMp` int(10) unsigned NOT NULL DEFAULT '0',
  `exp` int(10) unsigned NOT NULL DEFAULT '0',
  `lawful` int(10) unsigned NOT NULL DEFAULT '0',
  `gfx` int(10) unsigned NOT NULL DEFAULT '0',
  `food` varchar(10) NOT NULL DEFAULT '',
  `del` int(10) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `id` (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
