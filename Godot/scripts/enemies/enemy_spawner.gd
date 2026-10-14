class_name EnemySpawner
extends Node3D
## A monster camp (EnemySpawner.cs): keeps `count` monsters around its centre; a fallen one comes
## back `respawn_delay` seconds after it vanished, at a random spot, but only while no hero stands
## near the camp.

@export var species: Script
@export var count := 3
@export var radius := 6.0
@export var respawn_delay := 25.0
@export var min_hero_distance := 18.0

var members: Array[Enemy] = []
var _queue: Array = []   # [time, enemy]


func _ready() -> void:
	for i in count:
		var a := i * TAU / count + 0.4
		var p := global_position + Vector3(cos(a), 0, sin(a)) * radius * 0.6
		var e: Enemy = species.new()
		e.spawner = self
		get_parent().add_child.call_deferred(e)
		e.position = _ground(p)
		members.append(e)


func _ground(p: Vector3) -> Vector3:
	return Vector3(p.x, Terrain.height_at_world(p) + 0.1, p.z)


func notify_dead(e: Enemy) -> void:
	_queue.append([Game.now() + respawn_delay, e])


func _process(_delta: float) -> void:
	if _queue.is_empty() or not Game.is_authority():
		return
	for h in get_tree().get_nodes_in_group("heroes"):
		if h.global_position.distance_to(global_position) < min_hero_distance:
			return
	var t := Game.now()
	for i in range(_queue.size() - 1, -1, -1):
		if t >= _queue[i][0]:
			var e: Enemy = _queue[i][1]
			_queue.remove_at(i)
			if is_instance_valid(e):
				e.revive(_ground(global_position + Enemy._random_in_disc(radius)))
