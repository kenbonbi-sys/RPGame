class_name Arena
extends Node3D
## Hang Gấu Ma: the wide meadow inside the rocky ring where Gấu Ma Rừng Già lives. Four Tảng Đá
## Lớn stand on the floor (lure its Chụp Quăng into one and it is stunned), and the broken columns
## of something much older lean along the ridge.

const BOULDER_ANGLES := [55.0, 145.0, 235.0, 320.0]
const RUIN_ANGLES := [20.0, 75.0, 110.0, 165.0, 190.0, 260.0, 285.0, 345.0]


func _ready() -> void:
	var c := WorldMap.arena_center()
	var rng := RandomNumberGenerator.new()
	rng.seed = 45
	for a in BOULDER_ANGLES:
		var r := 30.0 + rng.randf_range(-3.0, 3.0)
		var m := c + Vector2(cos(deg_to_rad(a)), sin(deg_to_rad(a))) * r
		Props.place(self, ["rock_tallA", "rock_tallB", "rock_tallC", "rock_tallE"][rng.randi() % 4], m,
			rng.randf() * TAU, 7.0, true, 0.6, "boulders")
	for a in RUIN_ANGLES:
		var m := c + Vector2(cos(deg_to_rad(a)), sin(deg_to_rad(a))) * rng.randf_range(50.0, 56.0)
		var model: String = ["statue_column", "statue_columnDamaged", "statue_columnDamaged", "statue_block"][rng.randi() % 4]
		var n := Props.place(self, model, m, rng.randf() * TAU, 6.0, true, 0.4)
		n.rotation.z = rng.randf_range(-0.12, 0.12)
	var gate := c + WorldMap.arena_gate_dir() * 46.0
	Props.place(self, "statue_head", gate + Vector2(10, -4), 2.4, 7.0, true, 1.2)
