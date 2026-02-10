<<<<<<< Updated upstream
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

package jp.l1j.server.datatables;

import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.HashMap;
import java.util.logging.Level;
import java.util.logging.Logger;
import jp.l1j.server.model.L1PolyMorph;
import jp.l1j.server.utils.L1DatabaseFactory;
import jp.l1j.server.utils.PerformanceTimer;
import jp.l1j.server.utils.SqlUtil;

public class PolyTable {
	private static Logger _log = Logger.getLogger(PolyTable.class.getName());

	private static PolyTable _instance;

	private static HashMap<String, L1PolyMorph> _polymorphs = new HashMap<String, L1PolyMorph>();
	
	private static HashMap<Integer, L1PolyMorph> _polyIds = new HashMap<Integer, L1PolyMorph>();

	public static PolyTable getInstance() {
		if (_instance == null) {
			_instance = new PolyTable();
		}
		return _instance;
	}

	private PolyTable() {
		loadPolymorphs(_polymorphs, _polyIds);
	}

	private void loadPolymorphs(HashMap<String, L1PolyMorph> polymorphs,
			HashMap<Integer, L1PolyMorph> polyIds) {
		Connection con = null;
		PreparedStatement pstm = null;
		ResultSet rs = null;
		try {
			PerformanceTimer timer = new PerformanceTimer();
			con = L1DatabaseFactory.getInstance().getConnection();
			pstm = con.prepareStatement("SELECT * FROM polymorphs");
			rs = pstm.executeQuery();
			while (rs.next()) {
				int id = rs.getInt("id");
				String name = rs.getString("name");
				int gfxId = rs.getInt("gfx_id");
				int minLevel = rs.getInt("min_level");
				int weaponEquipFlg = rs.getInt("weapon_equip");
				int armorEquipFlg = rs.getInt("armor_equip");
				boolean canUseSkill = rs.getBoolean("can_use_skill");
				int causeFlg = rs.getInt("cause");
				L1PolyMorph poly = new L1PolyMorph(id, name, gfxId, minLevel,
						weaponEquipFlg, armorEquipFlg, canUseSkill, causeFlg);
				polymorphs.put(name, poly);
				polyIds.put(gfxId, poly);
			}
			_log.fine("loaded poly: " + polymorphs.size() + " records");
			System.out.println("loading polymorphs...OK! " + timer.elapsedTimeMillis() + "ms");
		} catch (SQLException e) {
			_log.log(Level.SEVERE, "error while creating polymorph table", e);
		} finally {
			SqlUtil.close(rs, pstm, con);
		}
	}

	public void reload() {
		HashMap<String, L1PolyMorph> polymorphs = new HashMap<String, L1PolyMorph>();
		HashMap<Integer, L1PolyMorph> polyIds = new HashMap<Integer, L1PolyMorph>();
		loadPolymorphs(polymorphs, polyIds);
		_polymorphs = polymorphs;
		_polyIds = polyIds;
	}
	
	public L1PolyMorph getTemplate(String name) {
		return _polymorphs.get(name);
	}

	public L1PolyMorph getTemplate(int polyId) {
		return _polyIds.get(polyId);
	}
}
=======
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

package jp.l1j.server.datatables;

import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.util.HashMap;
import java.util.logging.Level;
import java.util.logging.Logger;
import jp.l1j.server.model.L1PolyMorph;
import jp.l1j.server.utils.L1DatabaseFactory;
import jp.l1j.server.utils.PerformanceTimer;
import jp.l1j.server.utils.SqlUtil;

// 【對齊 182】PolymorphTable - 使用 db 做英文查找、polyid 做整數查找
public class PolyTable {
	private static Logger _log = Logger.getLogger(PolyTable.class.getName());

	private static PolyTable _instance;

	// db(英文名) → L1PolyMorph（客戶端 HTML action 發送英文名查找用）
	private static HashMap<String, L1PolyMorph> _polymorphs = new HashMap<String, L1PolyMorph>();
	
	// polyid(gfx_id) → L1PolyMorph（變身後裝備檢查用）
	private static HashMap<Integer, L1PolyMorph> _polyIds = new HashMap<Integer, L1PolyMorph>();

	// id(表主鍵) → L1PolyMorph（182 的 getPoly(int id) 用）
	private static HashMap<Integer, L1PolyMorph> _polyById = new HashMap<Integer, L1PolyMorph>();

	public static PolyTable getInstance() {
		if (_instance == null) {
			_instance = new PolyTable();
		}
		return _instance;
	}

	private PolyTable() {
		loadPolymorphs(_polymorphs, _polyIds, _polyById);
	}

	private void loadPolymorphs(HashMap<String, L1PolyMorph> polymorphs,
			HashMap<Integer, L1PolyMorph> polyIds,
			HashMap<Integer, L1PolyMorph> polyById) {
		Connection con = null;
		PreparedStatement pstm = null;
		ResultSet rs = null;
		try {
			PerformanceTimer timer = new PerformanceTimer();
			con = L1DatabaseFactory.getInstance().getConnection();
			pstm = con.prepareStatement("SELECT * FROM polymorphs");
			rs = pstm.executeQuery();
			while (rs.next()) {
				int id = rs.getInt("id");
				String name = rs.getString("name");
				String db = rs.getString("db");
				int polyid = rs.getInt("polyid");
				int minlevel = rs.getInt("minlevel");
				int weapon = rs.getInt("isWeapon");
				boolean helm = rs.getInt("isHelm") == 1;
				boolean earring = rs.getInt("isEarring") == 1;
				boolean necklace = rs.getInt("isNecklace") == 1;
				boolean t = rs.getInt("isT") == 1;
				boolean armor = rs.getInt("isArmor") == 1;
				boolean cloak = rs.getInt("isCloak") == 1;
				boolean ring = rs.getInt("isRing") == 1;
				boolean belt = rs.getInt("isBelt") == 1;
				boolean glove = rs.getInt("isGlove") == 1;
				boolean shield = rs.getInt("isShield") == 1;
				boolean boots = rs.getInt("isBoots") == 1;

				L1PolyMorph poly = new L1PolyMorph(id, name, db, polyid, minlevel,
						weapon, helm, earring, necklace, t, armor, cloak,
						ring, belt, glove, shield, boots);

				// 用英文 db 名作為字串查找鍵（客戶端 HTML action 發送英文名）
				polymorphs.put(db, poly);
				// 用 polyid(gfx_id) 作為整數查找鍵（裝備檢查等）
				polyIds.put(polyid, poly);
				// 用 id(表主鍵) 作為查找鍵
				polyById.put(id, poly);
			}
			_log.fine("loaded poly: " + polymorphs.size() + " records");
			System.out.println("loading polymorphs...OK! " + timer.elapsedTimeMillis() + "ms");
		} catch (SQLException e) {
			_log.log(Level.SEVERE, "error while creating polymorph table", e);
		} finally {
			SqlUtil.close(rs, pstm, con);
		}
	}

	public void reload() {
		HashMap<String, L1PolyMorph> polymorphs = new HashMap<String, L1PolyMorph>();
		HashMap<Integer, L1PolyMorph> polyIds = new HashMap<Integer, L1PolyMorph>();
		HashMap<Integer, L1PolyMorph> polyById = new HashMap<Integer, L1PolyMorph>();
		loadPolymorphs(polymorphs, polyIds, polyById);
		_polymorphs = polymorphs;
		_polyIds = polyIds;
		_polyById = polyById;
	}
	
	/** 用英文 db 名查找（客戶端發送的 polyName） */
	public L1PolyMorph getTemplate(String db) {
		return _polymorphs.get(db);
	}

	/** 用 polyid(gfx_id) 查找（裝備檢查等） */
	public L1PolyMorph getTemplate(int polyId) {
		return _polyIds.get(polyId);
	}

	/** 用表主鍵 id 查找（對齊 182 的 getPoly） */
	public L1PolyMorph getPoly(int id) {
		return _polyById.get(id);
	}

	public int getSize() {
		return _polymorphs.size();
	}
}
>>>>>>> Stashed changes
