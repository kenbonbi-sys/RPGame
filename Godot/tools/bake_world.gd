extends SceneTree
## Samples WorldMap into world/terrain_data.res and draws world/map_preview.png.
## Run after changing the layout:
##   Godot_console.exe --headless --path . -s tools/bake_world.gd

const OUT := "res://world/terrain_data.res"
const PREVIEW := "res://world/map_preview.png"
const SPACING := 2.0


func _init() -> void:
	var t0 := Time.get_ticks_msec()
	var data := TerrainData.new()
	data.spacing = SPACING
	data.width = int(WorldMap.SIZE_U / SPACING) + 1
	data.depth = int(WorldMap.SIZE_V / SPACING) + 1
	data.heights.resize(data.width * data.depth)
	data.splat.resize(data.width * data.depth * 4)
	for iv in data.depth:
		var v := iv * SPACING
		for ix in data.width:
			var u := ix * SPACING
			var h := WorldMap.height(u, v)
			var s := WorldMap.splat(u, v, h)
			var i := iv * data.width + ix
			data.heights[i] = h
			data.splat[i * 4] = int(s.r * 255.0)
			data.splat[i * 4 + 1] = int(s.g * 255.0)
			data.splat[i * 4 + 2] = int(s.b * 255.0)
			data.splat[i * 4 + 3] = int(s.a * 255.0)
		if iv % 32 == 0:
			print("  row %d / %d" % [iv, data.depth])
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://world"))
	var err := ResourceSaver.save(data, OUT, ResourceSaver.FLAG_COMPRESS)
	print("saved ", OUT, " err=", err, " in ", (Time.get_ticks_msec() - t0) / 1000.0, " s")
	_preview(data).save_png(PREVIEW)
	print("saved ", PREVIEW)
	quit(0 if err == OK else 1)


## Top-down map: ground paint shaded by the slope, water in blue, north up.
func _preview(d: TerrainData) -> Image:
	var img := Image.create(d.width, d.depth, false, Image.FORMAT_RGB8)
	var grass := Color(0.36, 0.52, 0.27)
	var earth := Color(0.55, 0.45, 0.31)
	var rock := Color(0.52, 0.52, 0.5)
	var mud := Color(0.32, 0.3, 0.22)
	var light := Vector3(-0.5, 0.8, 0.4).normalized()
	for iv in d.depth:
		for ix in d.width:
			var i := iv * d.width + ix
			var h := d.heights[i]
			var c := grass * (d.splat[i * 4] / 255.0) + earth * (d.splat[i * 4 + 1] / 255.0) \
				+ rock * (d.splat[i * 4 + 2] / 255.0) + mud * (d.splat[i * 4 + 3] / 255.0)
			var n := d.normal_at(ix * d.spacing, iv * d.spacing)
			c = c * clampf(0.45 + 0.75 * n.dot(light), 0.25, 1.25)
			c = c.lerp(Color.WHITE, clampf((h - 30.0) / 120.0, 0.0, 0.35))
			if h < WorldMap.WATER_LEVEL:
				c = Color(0.2, 0.32, 0.36).lerp(c, 0.3)
			c.a = 1.0
			img.set_pixel(ix, d.depth - 1 - iv, c)
	return img
