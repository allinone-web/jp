/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2014/1/3 12:59:18
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for account
-- ----------------------------
CREATE TABLE `account` (
  `uid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `id` varchar(20) NOT NULL DEFAULT '',
  `pw` varchar(50) NOT NULL DEFAULT '',
  `status` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `level` int(10) unsigned NOT NULL DEFAULT '0',
  `register_date` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `logins_date` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `block_date` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `last_ip` varchar(20) NOT NULL DEFAULT '0',
  `month_cards` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  PRIMARY KEY (`uid`),
  KEY `id` (`id`),
  KEY `pw` (`pw`),
  KEY `status` (`status`)
) ENGINE=MyISAM AUTO_INCREMENT=533 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
