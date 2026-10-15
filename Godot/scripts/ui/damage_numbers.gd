class_name DamageNumbers
extends RefCounted
## Floating numbers over whoever got hit (Label3D, always facing the camera): white for normal
## hits, gold and bigger for crits, green for healing, status colours for damage over time. On a
## colossus (Dauntless): yellow part damage, white-blue core damage, orange staggered damage.

const FONT := preload("res://assets/fonts/BarlowCondensed-ExtraBold.ttf")
const COLORS := {
	"normal": Color(1.0, 0.97, 0.9),
	"crit": Color(1.0, 0.78, 0.25),
	"heal": Color(0.45, 1.0, 0.5),
	"hero": Color(1.0, 0.4, 0.35),
	"burn": Color(1.0, 0.55, 0.2),
	"poison": Color(0.6, 0.95, 0.3),
	"shock": Color(0.6, 0.85, 1.0),
	"text": Color(0.95, 0.92, 0.8),
	"part": Color(1.0, 0.84, 0.18),
	"core": Color(0.86, 0.94, 1.0),
	"staggered": Color(1.0, 0.52, 0.16),
}


static func show(pos: Vector3, text: String, kind := "normal", big := false) -> void:
	var tree := Engine.get_main_loop() as SceneTree
	if tree == null or tree.current_scene == null:
		return
	var l := Label3D.new()
	l.text = text
	l.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	l.no_depth_test = true
	l.fixed_size = true
	l.pixel_size = 0.0011 if big else 0.0008
	l.font = FONT
	l.font_size = 52
	l.outline_size = 14
	l.modulate = Elements.color(kind.trim_prefix("proc_")) if kind.begins_with("proc_") else COLORS.get(kind, COLORS.normal)
	l.outline_modulate = Color(0, 0, 0, 0.75)
	l.render_priority = 10
	l.layers = 2
	tree.current_scene.add_child(l)
	var side := randf_range(-0.6, 0.6)
	l.global_position = pos + Vector3(side, 0, 0)
	var tw := l.create_tween()
	tw.set_parallel(true)
	tw.tween_property(l, "global_position", l.global_position + Vector3(side * 0.4, 1.4 if big else 1.0, 0), 0.9).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	tw.tween_property(l, "modulate:a", 0.0, 0.35).set_delay(0.6)
	tw.chain().tween_callback(l.queue_free)
