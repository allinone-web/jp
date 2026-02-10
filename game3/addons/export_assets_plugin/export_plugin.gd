@tool
extends EditorExportPlugin

const ASSETS_ROOT = "res://Assets"
# Assets 目錄下全部匯出到 iOS，僅排除 .zip
const EXCLUDE_EXTENSION = "zip"

func _get_name() -> String:
	return "Export Assets (PAK/SPR/Data)"

func _export_begin(features: PackedStringArray, is_debug: bool, path: String, flags: int) -> void:
	_add_assets_dir(ASSETS_ROOT)

func _add_assets_dir(dir_path: String) -> void:
	var dir = DirAccess.open(dir_path)
	if dir == null:
		push_warning("[ExportAssets] 無法開啟: %s" % dir_path)
		return
	dir.list_dir_begin()
	var file_name = dir.get_next()
	while file_name != "":
		var full = dir_path.path_join(file_name)
		if dir.current_is_dir():
			if file_name != "." and file_name != "..":
				_add_assets_dir(full)
		else:
			var ext = file_name.get_extension().to_lower()
			if ext != EXCLUDE_EXTENSION:
				var data = _read_file_bytes(full)
				if data != null and data.size() > 0:
					add_file(full, data, false)
					print("[ExportAssets] 已加入: %s (%d bytes)" % [full, data.size()])
				else:
					push_warning("[ExportAssets] 讀取失敗或空檔: %s" % full)
		file_name = dir.get_next()
	dir.list_dir_end()

func _read_file_bytes(path: String) -> PackedByteArray:
	var f = FileAccess.open(path, FileAccess.READ)
	if f == null:
		return PackedByteArray()
	var data = f.get_buffer(f.get_length())
	f.close()
	return data
