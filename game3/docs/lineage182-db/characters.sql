/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2014-7-12 9:38:23
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for characters
-- ----------------------------
CREATE TABLE `characters` (
  `name` varchar(20) NOT NULL DEFAULT '',
  `account` varchar(20) NOT NULL DEFAULT '',
  `account_uid` int(10) unsigned NOT NULL DEFAULT '0',
  `objID` int(10) unsigned NOT NULL,
  `level` int(10) unsigned NOT NULL DEFAULT '1',
  `nowHP` int(10) unsigned NOT NULL DEFAULT '0',
  `maxHP` int(10) unsigned NOT NULL DEFAULT '0',
  `nowMP` int(10) unsigned NOT NULL DEFAULT '0',
  `maxMP` int(10) unsigned NOT NULL DEFAULT '0',
  `ac` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `ac_dex` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `str` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `con` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `dex` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `wis` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `inter` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `cha` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `sex` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `exp` bigint(10) unsigned NOT NULL DEFAULT '0',
  `class` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `locX` smallint(1) unsigned NOT NULL DEFAULT '0',
  `locY` smallint(1) unsigned NOT NULL DEFAULT '0',
  `locMAP` mediumint(1) unsigned NOT NULL DEFAULT '0',
  `title` varchar(20) NOT NULL DEFAULT '',
  `food` smallint(1) unsigned NOT NULL DEFAULT '5',
  `gfx` smallint(1) unsigned NOT NULL DEFAULT '0',
  `lawful` mediumint(1) unsigned NOT NULL DEFAULT '65536',
  `gfx_mode` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `clanID` mediumint(1) unsigned NOT NULL DEFAULT '0',
  `clanNAME` varchar(20) NOT NULL DEFAULT '',
  `pkcount` smallint(1) unsigned NOT NULL DEFAULT '0',
  `pkTime` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `lvStat` varchar(20) NOT NULL DEFAULT '0 0 0 0 0 0',
  `global_chating` tinyint(1) NOT NULL DEFAULT '1',
  `trade_chating` tinyint(1) NOT NULL DEFAULT '1',
  `whisper_chating` tinyint(1) NOT NULL DEFAULT '1',
  `paper` tinyint(1) NOT NULL DEFAULT '1',
  `totem` tinyint(1) NOT NULL DEFAULT '0',
  `quest_graduation` tinyint(1) NOT NULL DEFAULT '0',
  `join_date` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `block_date` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `elf_attr` int(10) NOT NULL DEFAULT '0' COMMENT '属性(妖精专用)',
  PRIMARY KEY (`objID`,`name`),
  KEY `name` (`name`),
  KEY `account` (`account`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
