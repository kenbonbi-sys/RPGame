extends Node
## The game's numbers, read from Godot/data/*.json. Those files are made from the Unity game's
## ScriptableObjects by Tools/GodotData/unity_to_json.py, so both versions play by the same rules.
## Autoload "DB".

var abilities := {}
var items := {}
var classes := {}
var races := {}
var quests := {}
## Combat.asset: hit-stop, Lướt Hoàn Hảo, status effects, crowd-control rules.
var combat := {}
## Progression.asset: levels, XP, stats.
var progression := {}


func _enter_tree() -> void:
	abilities = _load("abilities")
	items = _load("items")
	classes = _load("classes")
	races = _load("races")
	quests = _load("quests")
	combat = _load("combat")
	progression = _load("progression")


func _load(category: String) -> Dictionary:
	var path := "res://data/%s.json" % category
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		push_error("DB: cannot open " + path)
		return {}
	var parsed = JSON.parse_string(f.get_as_text())
	if parsed is Dictionary:
		return parsed
	push_error("DB: bad JSON in " + path)
	return {}


func c(key: String, fallback := 0.0) -> float:
	return float(combat.get(key, fallback))


func p(key: String, fallback := 0.0) -> float:
	return float(progression.get(key, fallback))


func ability(id: String) -> Dictionary:
	return abilities.get(id, {})


func item(id: String) -> Dictionary:
	return items.get(id, {})


## "abilities/fireball" style references from the converter -> the record they point at.
func resolve(ref) -> Dictionary:
	if ref is Dictionary and ref.has("$ref"):
		var bits: PackedStringArray = String(ref["$ref"]).split("/")
		if bits.size() == 2:
			var table: Dictionary = get(bits[0]) if bits[0] in ["abilities", "items", "classes", "races", "quests"] else {}
			return table.get(bits[1], {})
	return {}
