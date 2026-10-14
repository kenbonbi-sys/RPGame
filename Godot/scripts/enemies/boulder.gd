class_name Boulder
extends Node3D
## Tảng Đá Lớn: a rock Gấu Ma tears from the ground and hurls. Where it lands it hurts, then it
## stays as a solid boulder for a while; a colossus that pounces into it is stunned (and the rock
## breaks). At most MAX_ROCKS stay on the field.

const MAX_ROCKS := 4
const GRAVITY := 16.0

static var _landed: Array[Boulder] = []

var visual: Node3D
var radius := 2.3
var velocity := Vector3.ZERO
var thrower: Node
var flying := false
var _life := 75.0
var _spin := Vector3.ZERO


func launch(from: Vector3, to: Vector3, flight: float, by: Node) -> void:
	global_position = from
	thrower = by
	if visual:
		visual.reparent(self, true)
	velocity = (to - from) / flight
	velocity.y += 0.5 * GRAVITY * flight
	_spin = Vector3(randf_range(-2, 2), randf_range(-2, 2), randf_range(-2, 2))
	flying = true


func _physics_process(delta: float) -> void:
	if flying:
		velocity.y -= GRAVITY * delta
		global_position += velocity * delta
		if visual:
			visual.rotation += _spin * delta
		var ground := Terrain.height_at_world(global_position)
		if velocity.y < 0.0 and global_position.y - radius * 0.6 <= ground:
			_land(ground)
	else:
		_life -= delta
		if _life <= 0.0:
			shatter()


func _land(ground: float) -> void:
	flying = false
	global_position.y = ground + radius * 0.45
	if thrower and thrower.has_method("hit_area"):
		thrower.hit_area(global_position, 5.5, 24.0, 9.0, 5.0, "Ném Đá Lớn")
	Fx.dust(global_position, 5.0, 60)
	Fx.burst(global_position + Vector3.UP, Vector3.UP, Color(0.45, 0.42, 0.38, 0.9), 20, 9.0, 0.5, 1.2, 50.0, -14.0)
	var h: Node3D = Game.hero if is_instance_valid(Game.hero) else null
	if h:
		Game.shake(0.6 * clampf(1.0 - h.global_position.distance_to(global_position) / 50.0, 0.1, 1.0))
	var body := StaticBody3D.new()
	body.collision_layer = 1
	body.collision_mask = 0
	var cs := CollisionShape3D.new()
	var s := SphereShape3D.new()
	s.radius = radius
	cs.shape = s
	body.add_child(cs)
	add_child(body)
	add_to_group("boulders")
	_landed.append(self)
	while _landed.size() > MAX_ROCKS:
		var old: Boulder = _landed.pop_front()
		if is_instance_valid(old):
			old.shatter()


func shatter() -> void:
	_landed.erase(self)
	Fx.dust(global_position, 4.0, 40)
	Fx.burst(global_position + Vector3.UP, Vector3.UP, Color(0.45, 0.42, 0.38, 0.9), 24, 8.0, 0.6, 1.0, 70.0, -14.0)
	queue_free()
