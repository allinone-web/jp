/*
MySQL Data Transfer
Source Host: localhost
Source Database: lineage
Target Host: localhost
Target Database: lineage
Date: 2013/12/2 13:25:57
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for characters_books
-- ----------------------------
CREATE TABLE `characters_books` (
  `uid` int(10) NOT NULL AUTO_INCREMENT,
  `book_id` int(10) NOT NULL,
  `char_id` int(10) NOT NULL,
  `location` varchar(50) NOT NULL,
  `loc_x` int(10) NOT NULL,
  `loc_y` int(10) NOT NULL,
  `loc_map` int(10) NOT NULL,
  PRIMARY KEY (`uid`)
) ENGINE=InnoDB AUTO_INCREMENT=561121 DEFAULT CHARSET=utf8 COMMENT='记忆坐标';

-- ----------------------------
-- Records 
-- ----------------------------
