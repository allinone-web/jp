-- 导出  表 lineage.user_behavior 结构
CREATE TABLE IF NOT EXISTS `user_behavior` (
  `id` int(11) NOT NULL AUTO_INCREMENT COMMENT '主键',
  `ip` char(15) DEFAULT NULL COMMENT '操作者IP',
  `account` char(50) DEFAULT NULL COMMENT '账号',
  `operID` int(11) DEFAULT NULL COMMENT '角色ID',
  `operType` char(50) DEFAULT NULL COMMENT '操作类型',
  `targetID` char(12) DEFAULT NULL COMMENT '目标ID',
  `locX` int(11) DEFAULT NULL COMMENT '角色坐标X',
  `locY` int(11) DEFAULT NULL COMMENT '角色坐标Y',
  `locMap` int(11) DEFAULT NULL COMMENT '角色坐标地图编号',
  `operTime` datetime DEFAULT NULL COMMENT '操作时间',
  `remark` char(200) DEFAULT NULL COMMENT '其他备注',
  PRIMARY KEY (`id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='用户行为记录';
