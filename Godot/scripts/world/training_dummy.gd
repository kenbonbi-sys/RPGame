class_name TrainingDummy
extends StaticBody3D
## A practice dummy at Bulvar Rosk's Training Grounds, as Dauntless has them: hit it with anything,
## it never falls, and the board over it keeps count: total damage, damage per second, the last
## hit, the hits landed and the stagger dealt. The count starts over 5 s after the last blow.

const RESET_AFTER := 5.0

var health: Health
var total := 0.0
var stagger := 0.0
var hits := 0
var last := 0.0
var _first_at := 0.0
var _last_at := -100.0
var _board: Label3D
var _body: Node3D
var _wobble := 0.0


func _ready() -> void:
	collision_layer = Combat.LAYER_ENEMY
	collision_mask = 0
	var cs := CollisionShape3D.new()
	var cap := CapsuleShape3D.new()
	cap.radius = 0.4
	cap.height = 1.8
	cs.shape = cap
	cs.position = Vector3(0, 0.95, 0)
	add_child(cs)
	health = Health.new()
	health.name = "Health"
	health.team = Combat.Team.ENEMY
	health.display_name = "Hình Nộm"
	health.max_hp = 1000000.0
	health.head_height = 2.0
	add_child(health)
	health.damaged.connect(_on_hit)
	_body = Node3D.new()
	add_child(_body)
	var mi := MeshInstance3D.new()
	mi.mesh = _dummy_mesh()
	_body.add_child(mi)
	if Game.has_screen():
		_board = Label3D.new()
		_board.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		_board.font = Hud.FONT_BOLD
		_board.fixed_size = true
		_board.font_size = 32
		_board.outline_size = 9
		_board.outline_modulate = Color(0, 0, 0, 0.75)
		_board.pixel_size = 0.0007
		_board.position = Vector3(0, 2.45, 0)
		_board.visibility_range_end = 30.0
		add_child(_board)
		_refresh()


static func _dummy_mesh() -> ArrayMesh:
	var kits := HouseArt.Kits.new()
	var straw := Color(0.86, 0.72, 0.42)
	var sack := Color(0.78, 0.68, 0.5)
	kits.k("wood").tube(0.07, 0.06, 1.0, 7, Transform3D(Basis.IDENTITY, Vector3.ZERO), HouseArt.TIMBER, Vector3.ZERO, 1)
	kits.k("wood").rounded_box(Vector3(1.2, 0.1, 0.1), 0.03, Transform3D(Basis.IDENTITY, Vector3(0, 1.42, 0)), HouseArt.TIMBER)
	kits.k("plaster").lump(Vector3(0.36, 0.5, 0.3), 2, Transform3D(Basis.IDENTITY, Vector3(0, 1.25, 0)), 0.08, 3,
		func(_p: Vector3, _q: Vector3) -> Color: return straw)
	kits.k("plaster").lump(Vector3(0.22, 0.24, 0.22), 2, Transform3D(Basis.IDENTITY, Vector3(0, 1.92, 0)), 0.06, 4,
		func(_p: Vector3, _q: Vector3) -> Color: return sack)
	# the target painted on its chest, and rope ties
	kits.k("trim").tube(0.16, 0.16, 0.02, 14, Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(0, 1.3, 0.3)), Color(0.75, 0.12, 0.1), Vector3.ZERO, 1)
	kits.k("trim").tube(0.09, 0.09, 0.03, 14, Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(0, 1.3, 0.31)), Color(0.95, 0.9, 0.8), Vector3.ZERO, 1)
	for y in [0.95, 1.6]:
		kits.k("trim").tube(0.33, 0.33, 0.05, 12, Transform3D(Basis.IDENTITY, Vector3(0, y, 0)), Color(0.5, 0.36, 0.2), Vector3.ZERO, 1)
	return kits.mesh()


func _on_hit(d: DamageInfo, amount: float) -> void:
	var t := Game.now()
	if t - _last_at > RESET_AFTER:
		total = 0.0
		stagger = 0.0
		hits = 0
		_first_at = t
	_last_at = t
	total += amount
	stagger += d.poise
	hits += 1
	last = amount
	_wobble = minf(1.0, _wobble + 0.5)
	# it never falls
	health.hp = health.max_hp
	_refresh()


func dps() -> float:
	return total / maxf(1.0, _last_at - _first_at)


## A hit flashes the straw a little brighter (Combat.show_hit calls this).
func flash(_c: Color, _strength: float, _seconds: float) -> void:
	_wobble = minf(1.0, _wobble + 0.2)


func _refresh() -> void:
	if _board == null:
		return
	if hits == 0:
		_board.text = "HÌNH NỘM\nđánh thử đi"
		_board.modulate = Color(0.95, 0.92, 0.85)
	else:
		_board.text = "TỔNG %d  ·  DPS %d\nĐÒN CUỐI %d  ·  %d ĐÒN  ·  TRẤN ÁP %d" % [roundi(total), roundi(dps()), roundi(last), hits, roundi(stagger)]
		_board.modulate = Color(1.0, 0.86, 0.5)


func _process(delta: float) -> void:
	if _wobble > 0.0:
		_wobble = maxf(0.0, _wobble - delta * 1.6)
		_body.rotation.x = -sin(Game.now() * 18.0) * 0.12 * _wobble
	if hits > 0 and Game.now() - _last_at > RESET_AFTER and _board:
		hits = 0
		_refresh()
