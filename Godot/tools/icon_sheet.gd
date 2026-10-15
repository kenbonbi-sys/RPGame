extends Node
## Draws every skill and loot icon on one sheet, for checking the art:
##   Godot_console.exe --headless --path . res://tools/icon_sheet.tscn -- --out D:/icons.png


func _ready() -> void:
	var out := Game.arg_value("--out", OS.get_user_data_dir().path_join("icons.png"))
	var ids: Array = GameIcons.SKILLS.keys()
	var items := ["coin", "gold", "xp", "kill", "level", "potion_red", "potion_blue", "potion_green", "gel", "shroom_cap", "herb", "apple",
		"meat", "claw", "pelt", "gem_red", "gem_blue", "bone", "key", "ring", "tome_ice", "armor_bear", "boots_wind", "helm_leather",
		"shield", "crystal_shard", "spider_silk", "snake_fang", "eagle_feather", "lotus", "bread", "scroll"]
	var px := 96
	var cols := 12
	var total := ids.size() + items.size()
	var rows := int(ceil(total / float(cols)))
	var sheet := Image.create(cols * (px + 8) + 8, rows * (px + 8) + 8, false, Image.FORMAT_RGBA8)
	sheet.fill(Color(0.13, 0.12, 0.11))
	var i := 0
	for id in ids:
		var a := Ability.get_by_id(id)
		var tex := GameIcons.skill(a, px) if a else GameIcons.icon("x:" + id, GameIcons.SKILLS[id], "blade", px)
		_put(sheet, tex, i, cols, px)
		i += 1
	for id in items:
		_put(sheet, GameIcons.item(id, px), i, cols, px)
		i += 1
	sheet.save_png(out)
	print("ICON_SHEET: %d icons in %s" % [i, out])
	get_tree().quit()


func _put(sheet: Image, tex: Texture2D, i: int, cols: int, px: int) -> void:
	if tex == null:
		return
	var img := tex.get_image()
	img.convert(Image.FORMAT_RGBA8)
	var at := Vector2i(8 + (i % cols) * (px + 8), 8 + (i / cols) * (px + 8))
	sheet.blend_rect(img, Rect2i(Vector2i.ZERO, img.get_size()), at)
