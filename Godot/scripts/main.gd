extends Node3D
## The world scene: puts the hero in Làng Lá Xanh with the camera behind them, wakes Gấu Ma Rừng
## Già in its den, brings the hero back to the village after a fall, and starts the AutoShot tour
## when asked (`-- --autoshot`).

@onready var hero: Hero = $Hero
@onready var camera: SotcCamera = $Camera
@onready var hud: Hud = $HUD
@onready var bear: GauMa = $GauMa

var spawn_point := Vector3.ZERO


func _ready() -> void:
	Game.world = self
	GameSettings.apply(self)
	var v := WorldMap.village_center() + Vector2(10, -3)
	spawn_point = Props.ground(v) + Vector3.UP * 0.2
	hero.global_position = spawn_point
	# face the road east toward the arena
	var to := WorldMap.world(WorldMap.arena_center()) - spawn_point
	hero.facing = Vector3(to.x, 0, to.z).normalized()
	camera.attach(hero)
	hud.bind_hero(hero)
	var menus := Menus.new()
	menus.name = "Menus"
	add_child(menus)
	var den := WorldMap.world(WorldMap.arena_center())
	var gate := WorldMap.arena_gate_dir()
	bear.reset_to(Transform3D(Basis(Vector3.UP, atan2(-gate.x, gate.y)), den))
	Game.hero_died.connect(_on_hero_died)
	if Game.has_arg("--autoshot"):
		var shot := AutoShot.new()
		shot.name = "AutoShot"
		add_child(shot)


func _on_hero_died() -> void:
	await get_tree().create_timer(3.5).timeout
	hero.revive(spawn_point)
	camera.snap()


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("screenshot"):
		var dir := OS.get_user_data_dir().path_join("screenshots")
		DirAccess.make_dir_recursive_absolute(dir)
		var path := dir.path_join("shot_%d.png" % Time.get_unix_time_from_system())
		get_viewport().get_texture().get_image().save_png(path)
		print("screenshot: ", path)
