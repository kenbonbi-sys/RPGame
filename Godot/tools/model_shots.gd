extends Node3D
## Photographs one model from a few angles under the game's sky and sun, for working on art:
##   Godot_console.exe --path . res://tools/model_shots.tscn -- --model slime --shots-dir D:/shots
## Models: slime, shroom, bear, hero, sword, juice, rack, weapons, armour (every set the hero carries,
## front and back; --close frames the chest).

var dir := ""
var model := ""
## Weapons: frame the right hand instead of the whole hero.
var close := false


func _ready() -> void:
	dir = Game.arg_value("--shots-dir", OS.get_user_data_dir().path_join("model_shots"))
	model = Game.arg_value("--model", "slime")
	close = Game.has_arg("--close")
	DirAccess.make_dir_recursive_absolute(dir)
	get_window().size = Vector2i(1280, 800)
	var env := WorldEnvironment.new()
	env.environment = load("res://world/environment.tres")
	add_child(env)
	var sun := DirectionalLight3D.new()
	sun.rotation = Vector3(-0.698, -0.698, 0)
	sun.light_color = Color(1, 0.89, 0.72)
	sun.light_energy = 1.85
	sun.shadow_enabled = true
	sun.shadow_blur = 1.5
	add_child(sun)
	var ground := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	pm.size = Vector2(400, 400)
	ground.mesh = pm
	ground.material_override = CreatureArt.painted(Color(0.42, 0.62, 0.26), 0.2)
	add_child(ground)
	var floor_body := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(400, 1, 400)
	cs.shape = box
	cs.position = Vector3(0, -0.5, 0)
	floor_body.add_child(cs)
	add_child(floor_body)
	var cam := Camera3D.new()
	cam.fov = 50.0
	add_child(cam)
	var views := _build()
	for i in (90 if model in ["hero", "sword", "weapons", "armour"] else 2):
		await get_tree().physics_frame
	for v in views:
		cam.global_position = v[1]
		cam.look_at(v[2])
		for i in 8:
			await get_tree().process_frame
		if model == "juice":
			await _juice_burst(v[0])
			continue
		if model == "armour":
			_weapon_hero.wear_set(String(v[0]).get_slice("_", 0), false)
			for i in 6:
				await get_tree().process_frame
		if model == "weapons":
			_weapon_hero.equip_weapon(v[0], false)
			for i in 20:
				await get_tree().process_frame
			if close:
				var sk: Skeleton3D = _weapon_hero.model.skeleton
				var hand := sk.global_transform * sk.get_bone_global_pose(sk.find_bone("hand_r")).origin
				cam.global_position = hand + Vector3(-0.9, 0.35, -1.1)
				cam.look_at(hand + Vector3(0, 0.15, 0))
				for i in 4:
					await get_tree().process_frame
		var img := get_viewport().get_texture().get_image()
		img.save_png(dir.path_join("%s_%s.png" % [model, v[0]]))
	print("MODEL_SHOTS: %d shots of %s in %s" % [views.size(), model, dir])
	get_tree().quit()


## Builds the model and returns [name, camera position, look at] views.
func _build() -> Array:
	var root := Node3D.new()
	add_child(root)
	match model:
		"slime":
			var b := CreatureArt.moss_slime(root, 0.55)
			b.position.x = 0.0
			var other := Node3D.new()
			other.position = Vector3(1.6, 0, 0.6)
			other.rotation.y = -0.5
			add_child(other)
			CreatureArt.moss_slime(other, 0.55)
			return [["front", Vector3(0.6, 0.9, -2.4), Vector3(0.3, 0.45, 0)], ["side", Vector3(2.6, 1.2, -0.2), Vector3(0.4, 0.4, 0)],
				["back", Vector3(-1.2, 1.6, 2.2), Vector3(0.3, 0.4, 0)], ["far", Vector3(3.0, 3.0, -6.0), Vector3(0.5, 0.4, 0)]]
		"shroom":
			CreatureArt.poison_shroom(root, 1.0)
			return [["front", Vector3(0.8, 1.3, -3.0), Vector3(0, 0.9, 0)], ["side", Vector3(3.0, 1.4, -0.4), Vector3(0, 0.9, 0)],
				["back", Vector3(-1.6, 2.4, 2.4), Vector3(0, 0.9, 0)], ["low", Vector3(0.3, 0.5, -1.8), Vector3(0, 0.9, 0)]]
		"bear":
			var bear := GauMa.new()
			bear.scripted = true
			root.add_child(bear)
			bear.reset_to(Transform3D.IDENTITY)
			var s := GauMa.S
			return [["front", Vector3(14, 6, -26) * s, Vector3(0, 5, -4) * s], ["side", Vector3(30, 7, 0) * s, Vector3(0, 5, -2) * s],
				["back", Vector3(-16, 12, 22) * s, Vector3(0, 5, -2) * s], ["head", Vector3(5, 7, -18) * s, Vector3(0, 7, -12) * s],
				["hero", Vector3(6, 2.2, -14) * s, Vector3(0, 8, -9) * s]]
		"juice":
			var slime := Slime.new()
			add_child(slime)
			slime.revive(Vector3(0, 0.05, 0))
			slime.wander_radius = 0.01
			_juice_target = slime
			return [["hit", Vector3(0.4, 1.4, -3.4), Vector3(0, 0.6, 0)]]
		"rack":
			# every weapon blade up, side on, in a row
			var x := 0.0
			for id in Weapons.START:
				for piece in WeaponArt.build(id):
					var holder := Node3D.new()
					root.add_child(holder)
					var off := 0.0 if piece.hand == "r" else 0.28
					holder.transform = Transform3D(Basis(Vector3.RIGHT, -PI * 0.5).rotated(Vector3.UP, PI * 0.5), Vector3(x + off, 0.9, 0))
					for part in ["metal", "glow", "grip"]:
						if piece.get(part) == null:
							continue
						var mi := MeshInstance3D.new()
						mi.mesh = piece.get(part)
						mi.material_override = WeaponArt.material("glow:" + String(Weapons.element(id)) if part == "glow" else part)
						holder.add_child(mi)
				x += 0.75
			return [["front", Vector3(2.4, 1.0, 3.6), Vector3(2.4, 1.0, 0)], ["angle", Vector3(0.8, 1.6, 2.8), Vector3(2.2, 1.0, 0)]]
		"weapons":
			_weapon_hero = preload("res://scenes/hero.tscn").instantiate()
			root.add_child(_weapon_hero)
			var out := []
			for id in Weapons.START:
				out.append([id, Vector3(-1.0, 1.35, -2.2), Vector3(0.05, 0.95, 0)])
			return out
		"armour":
			_weapon_hero = preload("res://scenes/hero.tscn").instantiate()
			root.add_child(_weapon_hero)
			var out := []
			var near := 0.55 if close else 1.0
			for id in Armour.WARDROBE + ["recruit", "torgadoro", "thrax"]:
				var aim := Vector3(0.0, 1.25 if close else 0.95, 0)
				out.append([id + "_front", aim + Vector3(0.7, 0.35, -2.3) * near, aim])
				out.append([id + "_back", aim + Vector3(-0.9, 0.45, 2.2) * near, aim])
			return out
		"hero", "sword":
			var hero: Node3D = preload("res://scenes/hero.tscn").instantiate()
			root.add_child(hero)
			if model == "sword":
				return [["near", Vector3(0.9, 1.3, -1.2), Vector3(0.1, 0.9, 0)], ["side", Vector3(1.5, 1.0, 0.2), Vector3(0, 0.9, 0)]]
			return [["front", Vector3(1.0, 1.4, -2.6), Vector3(0, 0.9, 0)], ["feet", Vector3(0.9, 0.35, -1.6), Vector3(0, 0.15, 0)],
				["side", Vector3(2.4, 0.5, 0), Vector3(0, 0.2, 0)]]
	return []


var _juice_target: Node3D
var _weapon_hero: Hero


## A blow from the left lands on the slime; frames at 0, 30, 60, 100, 160 ms of real time.
func _juice_burst(view: String) -> void:
	var h: Health = _juice_target.get("health")
	var d := DamageInfo.make(12.0, Combat.Team.PLAYER, null, h.hit_point(Vector3(-2, 0.6, 0)), Vector3.RIGHT)
	d.hit_stop = 0.05
	h.take_damage(d)
	Combat.on_hit_feedback(h, d)
	var t0 := Game.now()
	for ms in [0, 30, 60, 100, 160]:
		while (Game.now() - t0) * 1000.0 < ms:
			await get_tree().process_frame
		await RenderingServer.frame_post_draw
		get_viewport().get_texture().get_image().save_png(dir.path_join("juice_%s_%03d.png" % [view, ms]))
