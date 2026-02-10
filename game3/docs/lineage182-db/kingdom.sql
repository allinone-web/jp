/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:27:27
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for kingdom
-- ----------------------------
CREATE TABLE `kingdom` (
  `id` int(4) unsigned NOT NULL DEFAULT '0',
  `name` varchar(50) NOT NULL DEFAULT '',
  `loc` varchar(50) NOT NULL DEFAULT '',
  `clan_id` int(4) unsigned NOT NULL DEFAULT '0',
  `clan_name` varchar(50) NOT NULL DEFAULT '',
  `agent_id` int(4) unsigned NOT NULL DEFAULT '0',
  `agent_name` varchar(100) NOT NULL DEFAULT '',
  `tax` int(3) unsigned NOT NULL DEFAULT '0',
  `tax_total` int(10) unsigned NOT NULL DEFAULT '0',
  `tax_day` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `war` varchar(6) NOT NULL DEFAULT '',
  `war_day` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `war_day_last` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  PRIMARY KEY (`id`),
  KEY `id` (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COMMENT='城堡';

-- ----------------------------
-- Records 
-- ----------------------------
INSERT INTO `kingdom` VALUES ('6', '侏儒城', '32858 32806 66', '0', '', '0', '', '0', '0', '1970-01-01 09:00:00', 'false', '2013-12-06 20:00:00', '2013-12-02 11:42:56');
INSERT INTO `kingdom` VALUES ('5', '海音城', '32572 32826 64', '0', '', '0', '', '0', '0', '1970-01-01 09:00:00', 'false', '2013-12-04 20:00:23', '2013-11-30 16:17:29');
INSERT INTO `kingdom` VALUES ('4', '奇巖城', '32729 32803 52', '0', '', '0', '', '0', '0', '1970-01-01 09:00:00', 'false', '2013-12-04 20:00:23', '2013-11-30 16:17:29');
INSERT INTO `kingdom` VALUES ('3', '風木城', '32735 32794 29', '0', '', '0', '', '0', '0', '1970-01-01 09:00:00', 'false', '2013-12-04 20:00:23', '2013-11-30 16:17:29');
INSERT INTO `kingdom` VALUES ('2', '妖魔城', '32776 32300 4', '0', '', '0', '', '0', '0', '1970-01-01 09:00:00', 'false', '2013-12-04 20:00:23', '2013-11-30 16:17:29');
INSERT INTO `kingdom` VALUES ('1', '肯特城', '32735 32794 15', '0', '', '0', '', '0', '0', '1970-01-01 09:00:00', 'false', '2013-12-04 20:00:23', '2013-11-30 16:17:29');
