class_name ForestCamps
extends Node3D
## The monster camps of Rừng Thì Thầm, where the Unity WorldBuilder puts them (PlaceActors):
## 9 Slime Rêu in three clearings along the road, 5 Nấm Độc in two further east.
## Spawn radius is the Unity one × 3 (the clearings are 4× bigger here).

const CAMPS := [
	{"name": "SlimeCamp_Edge", "at": Vector2(35.5, 22), "species": "slime", "count": 2, "r": 1.8},
	{"name": "SlimeCamp_Clearing", "at": Vector2(47, 27.5), "species": "slime", "count": 4, "r": 2.6},
	{"name": "SlimeCamp_South", "at": Vector2(53, 15), "species": "slime", "count": 3, "r": 2.2},
	{"name": "ShroomCamp_East", "at": Vector2(62, 29.5), "species": "shroom", "count": 3, "r": 2.2},
	{"name": "ShroomCamp_Path", "at": Vector2(71.5, 35), "species": "shroom", "count": 2, "r": 1.8},
]


func _ready() -> void:
	for c in CAMPS:
		var sp := EnemySpawner.new()
		sp.name = c.name
		sp.species = Slime if c.species == "slime" else Shroom
		sp.count = c.count
		sp.radius = c.r * 3.0
		var m := WorldMap.tile(c.at)
		sp.position = Vector3(m.x, Terrain.height_at_world(Vector3(m.x, 0, -m.y)), -m.y)
		add_child(sp)
