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

package jp.l1j.server.packets.client;

import java.nio.ByteBuffer;
import java.util.logging.Level;
import java.util.logging.Logger;
import jp.l1j.configure.Config;
import jp.l1j.server.ClientThread;

public abstract class ClientBasePacket {
	private static Logger _log = Logger.getLogger(ClientBasePacket.class
			.getName());

	private static final String CLIENT_LANGUAGE_CODE = Config
			.CLIENT_LANGUAGE_CODE;

	private byte _decrypt[];

	private int _off;

	public ClientBasePacket(byte abyte0[]) {
		_log.finest("type=" + getType() + ", len=" + abyte0.length);
		_decrypt = abyte0;
		_off = 1;
	}

	public ClientBasePacket(ByteBuffer bytebuffer, ClientThread clientthread) {
	}

	public int readD() {
		int i = _decrypt[_off++] & 0xff;
		i |= _decrypt[_off++] << 8 & 0xff00;
		i |= _decrypt[_off++] << 16 & 0xff0000;
		i |= _decrypt[_off++] << 24 & 0xff000000;
		return i;
	}

	public int readC() {
		int i = _decrypt[_off++] & 0xff;
		return i;
	}

	public int readH() {
		int i = _decrypt[_off++] & 0xff;
		i |= _decrypt[_off++] << 8 & 0xff00;
		return i;
	}

	public int readCH() {
		int i = _decrypt[_off++] & 0xff;
		i |= _decrypt[_off++] << 8 & 0xff00;
		i |= _decrypt[_off++] << 16 & 0xff0000;
		return i;
	}

	public double readF() {
		long l = _decrypt[_off++] & 0xff;
		l |= _decrypt[_off++] << 8 & 0xff00;
		l |= _decrypt[_off++] << 16 & 0xff0000;
		l |= _decrypt[_off++] << 24 & 0xff000000;
		l |= (long) _decrypt[_off++] << 32 & 0xff00000000L;
		l |= (long) _decrypt[_off++] << 40 & 0xff0000000000L;
		l |= (long) _decrypt[_off++] << 48 & 0xff000000000000L;
		l |= (long) _decrypt[_off++] << 56 & 0xff00000000000000L;
		return Double.longBitsToDouble(l);
	}

	public String readS() {
		return readS(0);
	}
	
	public String readS(int adjust) {
		String s = null;
		try {
			int startOffset = _off + adjust;
			int remainingLength = _decrypt.length - startOffset;
			
			// 【調試日誌】打印讀取前的狀態
			_log.finest("readS() - startOffset=" + startOffset + ", remainingLength=" + remainingLength);
			
			if (remainingLength <= 0) {
				_log.warning("readS() - No remaining bytes to read!");
				return null;
			}
			
			// 【調試日誌】打印要讀取的字節（前32字節）
			StringBuilder bytesHex = new StringBuilder();
			for (int i = startOffset; i < _decrypt.length && i < startOffset + 32; i++) {
				bytesHex.append(String.format("%02X ", _decrypt[i] & 0xFF));
			}
			_log.finest("readS() - Bytes to read (hex): " + bytesHex.toString());
			
			s = new String(_decrypt, startOffset, remainingLength, CLIENT_LANGUAGE_CODE);
			
			// 【調試日誌】打印解碼後的字符串（前100字符）
			_log.finest("readS() - Decoded string (first 100 chars): " + (s.length() > 100 ? s.substring(0, 100) : s));
			
			int nullIndex = s.indexOf('\0');
			_log.finest("readS() - Null terminator index: " + nullIndex);
			
			if (nullIndex < 0) {
				_log.warning("readS() - Null terminator not found! String length: " + s.length());
				// 【調試日誌】打印所有字節值，查找可能的 null terminator
				StringBuilder allBytes = new StringBuilder();
				for (int i = startOffset; i < _decrypt.length && i < startOffset + 64; i++) {
					allBytes.append(String.format("%02X(%c) ", _decrypt[i] & 0xFF, 
						(_decrypt[i] >= 32 && _decrypt[i] < 127) ? (char)_decrypt[i] : '.'));
				}
				_log.warning("readS() - All bytes: " + allBytes.toString());
				return null;
			}
			
			s = s.substring(0, nullIndex);
			_off += s.getBytes(CLIENT_LANGUAGE_CODE).length + 1;
			
			_log.finest("readS() - Successfully read: '" + s + "', new _off=" + (_off - 1));
		} catch (StringIndexOutOfBoundsException e) {
			// TODO 生存の叫び(Ctrl+E)時、87行目でエラーが発生する。
			_log.warning("readS() - StringIndexOutOfBoundsException: " + e.getMessage());
		} catch (Exception e) {
			_log.log(Level.SEVERE, "OpCode=" + (_decrypt[0] & 0xff), e);
		}
		return s;
	}

	public byte[] readByte() {
		byte[] result = new byte[_decrypt.length - _off];
		try {
			System.arraycopy(_decrypt, _off, result, 0, _decrypt.length - _off);
			_off = _decrypt.length;
		} catch (Exception e) {
			_log.log(Level.SEVERE, "OpCode=" + (_decrypt[0] & 0xff), e);
		}
		return result;
	}

	/**
	 * クライアントパケットの種類を表す文字列を返す。("[C] C_DropItem" 等)
	 */
	public String getType() {
		return "[C] " + this.getClass().getSimpleName();
	}
}
