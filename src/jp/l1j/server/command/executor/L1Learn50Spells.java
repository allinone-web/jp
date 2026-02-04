/*
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

package jp.l1j.server.command.executor;

import java.util.logging.Logger;
import static jp.l1j.locale.I18N.*;
import jp.l1j.server.datatables.SkillTable;
import jp.l1j.server.model.instance.L1PcInstance;
import jp.l1j.server.packets.server.S_AddSkill;
import jp.l1j.server.packets.server.S_SkillSound;
import jp.l1j.server.packets.server.S_SystemMessage;
import jp.l1j.server.templates.L1Skill;

/**
 * GM 命令：給予自身 50 個一般魔法（技能 ID 1～50）。
 * 用於測試或 GM 快速取得常用魔法。
 */
public class L1Learn50Spells implements L1CommandExecutor {
	private static Logger _log = Logger.getLogger(L1Learn50Spells.class.getName());

	private static final int SPELL_COUNT = 50;

	private L1Learn50Spells() {
	}

	public static L1CommandExecutor getInstance() {
		return new L1Learn50Spells();
	}

	@Override
	public void execute(L1PcInstance pc, String cmdName, String arg) {
		try {
			int objectId = pc.getId();
			pc.sendPackets(new S_SkillSound(objectId, '\343'));
			pc.broadcastPacket(new S_SkillSound(objectId, '\343'));

			// 一般魔法 1～50：前 6 字節 255（技能 1～48），第 7 字節 3（技能 49、50）
			int s0 = 255, s1 = 255, s2 = 255, s3 = 255, s4 = 255, s5 = 255, s6 = 3; // 49,50 = bit0,1
			int s7 = 0, s8 = 0, s9 = 0;
			pc.sendPackets(new S_AddSkill(s0, s1, s2, s3, s4, s5, s6, s7, s8, s9,
					0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

			int learned = 0;
			for (int skillId = 1; skillId <= SPELL_COUNT; skillId++) {
				L1Skill skill = SkillTable.getInstance().findBySkillId(skillId);
				if (skill == null) continue;
				SkillTable.getInstance().spellMastery(objectId, skillId, skill.getName(), 0, 0);
				learned++;
			}

			pc.sendPackets(new S_SystemMessage(String.format("習得 %d 個魔法（技能 ID 1～%d）", learned, SPELL_COUNT)));
			_log.info(String.format(I18N_USED_THE_COMMAND, pc.getName(), cmdName, arg));
		} catch (Exception e) {
			pc.sendPackets(new S_SystemMessage(String.format(I18N_COMMAND_ERROR, cmdName)));
			_log.warning("L1Learn50Spells error: " + e.getMessage());
		}
	}
}
