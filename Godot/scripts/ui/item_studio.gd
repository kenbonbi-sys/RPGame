class_name ItemStudio
extends Node
## A little photo studio for gear, so menus can show each piece as itself the way Dauntless does
## (the weapon's own model, the armour piece's own plates) instead of a flat glyph. A piece is set
## up in a world of its own with a key light, filmed once from the front, slightly above and to the
## side, and the picture is kept for every menu after (`shots`). Headless runs get no pictures
## (menus fall back to their icons).

const SIZE := 192

## key -> ImageTexture, shared by every studio.
static var shots := {}

var _queue: Array = []        # [key, builder Callable]
var _busy := false


## The picture for `key`, or null while it is being taken (`build` fills a Node3D with meshes).
func picture(key: String, build: Callable) -> Texture2D:
	if shots.has(key):
		return shots[key]
	if not Game.has_screen():
		return null
	for q in _queue:
		if q[0] == key:
			return null
	_queue.append([key, build])
	if not _busy:
		_next.call_deferred()
	return null


func _next() -> void:
	if _queue.is_empty():
		_busy = false
		return
	_busy = true
	var job: Array = _queue.pop_front()
	var vp := SubViewport.new()
	vp.size = Vector2i(SIZE, SIZE)
	vp.own_world_3d = true
	vp.transparent_bg = true
	vp.msaa_3d = Viewport.MSAA_4X
	vp.render_target_update_mode = SubViewport.UPDATE_ONCE
	add_child(vp)
	var stage := Node3D.new()
	vp.add_child(stage)
	var focus: AABB = (job[1] as Callable).call(stage)
	var env := WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_CLEAR_COLOR
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color(0.62, 0.68, 0.78)
	e.ambient_light_energy = 0.75
	e.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	e.glow_enabled = true
	e.glow_intensity = 0.4
	env.environment = e
	vp.add_child(env)
	var key_light := DirectionalLight3D.new()
	key_light.rotation = Vector3(deg_to_rad(-40.0), deg_to_rad(35.0), 0.0)
	key_light.light_energy = 1.7
	key_light.light_color = Color(1.0, 0.95, 0.88)
	vp.add_child(key_light)
	var rim := DirectionalLight3D.new()
	rim.rotation = Vector3(deg_to_rad(-15.0), deg_to_rad(200.0), 0.0)
	rim.light_energy = 1.1
	rim.light_color = Color(0.6, 0.8, 1.0)
	vp.add_child(rim)
	var cam := Camera3D.new()
	cam.fov = 28.0
	vp.add_child(cam)
	var c := focus.get_center()
	var r := maxf(0.05, focus.size.length() * 0.5)
	var dir := Vector3(0.35, 0.22, 1.0).normalized()
	cam.global_position = c + dir * (r / tan(deg_to_rad(cam.fov * 0.5)) * 1.05)
	cam.look_at(c, Vector3.UP)
	await RenderingServer.frame_post_draw
	await RenderingServer.frame_post_draw
	if is_instance_valid(vp):
		var img := vp.get_texture().get_image()
		if img:
			shots[job[0]] = ImageTexture.create_from_image(img)
		vp.queue_free()
	_next()


## A weapon of the armory, laid on the diagonal.
static func weapon_builder(id: String) -> Callable:
	return func(stage: Node3D) -> AABB:
		var pieces := WeaponArt.build(id)
		var box := AABB()
		var first := true
		var glow := WeaponArt.material("glow:" + String(Weapons.element(id)))
		var holder := Node3D.new()
		holder.rotation = Vector3(0.0, deg_to_rad(90.0), deg_to_rad(-38.0))
		stage.add_child(holder)
		for part in [["metal", WeaponArt.material("metal")], ["glow", glow], ["grip", WeaponArt.material("grip")]]:
			var mesh: Mesh = pieces[0].get(part[0])
			if mesh == null:
				continue
			var mi := MeshInstance3D.new()
			mi.mesh = mesh
			mi.material_override = part[1]
			holder.add_child(mi)
			var b := holder.transform * mesh.get_aabb()
			box = b if first else box.merge(b)
			first = false
		return box


## One armour piece as it is drawn on the slayer (its plates in the rest pose), or the cloth of
## the outfit under it for a set that draws none (Krolachi).
static func armour_builder(model: HeroModel, slot: String, set_id: String) -> Callable:
	return func(stage: Node3D) -> AABB:
		var sources := model._armour_sources()
		var box := AABB()
		var first := true
		var e := String(Armour.set_of(set_id).element)
		var parts: Array = ArmourArt.PARTS.get("hood" if slot == "head" and e in ArmourArt.HOODED else slot, [])
		for part in parts:
			var src: MeshInstance3D = sources.get(String(part.mesh))
			if src == null:
				continue
			var meshes: Array = []
			var mats: Array = []
			if ArmourArt.drawn(set_id):
				var plates := ArmourArt.plate(src.mesh, src.skin, part)
				for i in plates.size():
					meshes.append(plates[i])
					mats.append(ArmourArt.material("%s:%s" % [part.tone, e]) if i % 2 == 0 else ArmourArt.material("trim:" + e))
			else:
				meshes.append(src.mesh)
				mats.append(null)
			for i in meshes.size():
				var mesh: Mesh = meshes[i]
				if mesh == null or mesh.get_surface_count() == 0:
					continue
				var mi := MeshInstance3D.new()
				mi.mesh = mesh
				if mats[i]:
					mi.material_override = mats[i]
				else:
					for s in mesh.get_surface_count():
						var m := src.get_surface_override_material(s)
						if m:
							mi.set_surface_override_material(s, m)
				stage.add_child(mi)
				var b := mesh.get_aabb()
				box = b if first else box.merge(b)
				first = false
		# arms are spread wide in the rest pose: frame one of them
		if slot == "arms" and box.size.x > 0.6:
			box = AABB(Vector3(box.end.x - 0.42, box.position.y, box.position.z), Vector3(0.42, box.size.y, box.size.z))
		return box
