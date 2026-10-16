extends RefCounted
## How moves feel: the beats of a monster's wind-up (long and readable), the hero's quick squat,
## stretch and landing squash, the stretch of a Lướt. Run by test_runner.

const HERO := preload("res://scenes/hero.tscn")

var t: Node


func _init(runner: Node) -> void:
	t = runner


func check(ok: bool, what: String) -> void:
	t.check(ok, what)


func run_all() -> void:
	await t._run("feel: wind-up beats", beats)
	await t._run("feel: the hero squats, stretches and lands", jump)
	await t._run("feel: a Lướt stretches along the run", dash)
	await t._run("feel: the slime tells its lunge", slime_tell)
	await t._run("feel: the shroom tells its spore", shroom_tell)


func _flat_ground(at: Vector3) -> StaticBody3D:
	var ground := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(60, 1, 60)
	cs.shape = box
	ground.add_child(cs)
	ground.position = at + Vector3(0, -0.5, 0)
	t.add_child(ground)
	return ground


func _hero_at(p: Vector3) -> Hero:
	var hero: Hero = HERO.instantiate()
	t.add_child(hero)
	hero.revive(p)
	return hero


func beats() -> void:
	var b := Anticipation.Beats.new(0.1, 0.4, 0.2)
	check(is_equal_approx(b.strike_at(), 0.7) and is_equal_approx(b.hold_at(), 0.5), "the strike comes after tell + wind + hold")
	check(b.windup(0.0) == 0.0 and b.windup(0.1, 0.3) < -0.29, "the tell moves the other way first")
	check(b.windup(0.3, 0.3) > -0.3 and b.windup(0.3, 0.3) < 1.0 and b.windup(0.5) == 1.0 and b.windup(0.65) == 1.0,
		"the wind builds to the peak and holds it")
	check(b.tremble(0.3) == 0.0 and absf(b.tremble(0.55)) > 0.0, "it trembles only while holding")
	check(b.hold_begins(0.505, 0.016) and not b.hold_begins(0.6, 0.016), "the glint fires once, as the hold begins")
	check(b.strike(0.7, 0.3) == 0.0 and b.strike(0.75, 0.3) > 0.4 and b.strike(1.0, 0.3) == 1.0, "the strike is fast out of the gate")
	var bear := GauMa.new()
	check(is_equal_approx(bear.charge_beats.strike_at(), GauMa.CHARGE_WIND), "Húc's beats fill its interrupt window")
	for bb in [bear.swipe_beats, bear.stomp_beats, bear.throw_beats, bear.pounce_beats]:
		check(bb.strike_at() >= 0.9, "every bear attack warns for at least 0.9 s (%.2f)" % bb.strike_at())
	bear.free()


func jump() -> void:
	var at := Vector3(200, 0, 200)
	var ground := _flat_ground(at)
	var hero := _hero_at(at + Vector3.UP * 0.05)
	await t._frames(20)
	Input.action_press("jump")
	await t._frames(2)
	Input.action_release("jump")
	check(hero.state == Hero.State.GROUND and hero.velocity.y <= 0.0 and hero.squash.y < 0.92,
		"first a squat on the ground (y %.2f)" % hero.squash.y)
	var took_off := -1
	var tallest := 0.0
	for f in 12:
		await t._frames(1)
		if hero.state == Hero.State.AIR and took_off < 0:
			took_off = f + 2
		tallest = maxf(tallest, hero.squash.y)
	check(took_off > 0 and took_off <= 6, "then it leaves the ground within a tenth of a second (frame %d)" % took_off)
	check(tallest > 1.12, "stretched tall rising out of the squat (%.2f)" % tallest)
	var flattest := 9.0
	var landed := false
	for f in 90:
		await t._frames(1)
		if hero.state == Hero.State.GROUND:
			landed = true
		if landed:
			flattest = minf(flattest, hero.squash.y)
	check(landed and flattest < 0.95, "flattened on landing (%.2f)" % flattest)
	check(absf(hero.squash.y - 1.0) < 0.03, "and settled round again (%.2f)" % hero.squash.y)
	var vol := hero.squash.x * hero.squash.y * hero.squash.z
	check(absf(vol - 1.0) < 0.05, "the body keeps its volume (%.2f)" % vol)
	hero.queue_free()
	ground.queue_free()
	await t._frames(2)


func dash() -> void:
	var at := Vector3(-200, 0, 200)
	var ground := _flat_ground(at)
	var hero := _hero_at(at + Vector3.UP * 0.05)
	await t._frames(20)
	hero.dash(Vector3.FORWARD, 5.0, 0.22, 0.28, true)
	var longest := 0.0
	var lowest := 9.0
	for f in 12:
		await t._frames(1)
		longest = maxf(longest, hero.squash.z)
		lowest = minf(lowest, hero.squash.y)
	check(longest > 1.25 and lowest < 0.92, "drawn out along the dash (z %.2f, y %.2f)" % [longest, lowest])
	await t._frames(60)
	check(hero.squash.distance_to(Vector3.ONE) < 0.05, "back to normal after it (%s)" % str(hero.squash))
	hero.queue_free()
	ground.queue_free()
	await t._frames(2)


func slime_tell() -> void:
	var at := Vector3(200, 0, -200)
	var ground := _flat_ground(at)
	var hero := _hero_at(at + Vector3.UP * 0.05)
	hero.invulnerable_until = 1e9
	var slime := Slime.new()
	t.add_child(slime)
	slime.revive(at + Vector3(0, 0.1, -1.6))
	await t._frames(3)
	slime.target = hero
	slime._lunged = false
	slime.set_state(Enemy.State.ATTACK)
	var lunge_at := -1.0
	for f in 70:
		await t._frames(1)
		if slime.is_dashing() and lunge_at < 0.0:
			lunge_at = slime.state_time
	check(lunge_at >= 0.6 and lunge_at <= slime.beats.strike_at() + 0.05,
		"it lunges only after %.2f s of warning (%.2f)" % [slime.beats.strike_at(), lunge_at])
	slime.queue_free()
	hero.queue_free()
	ground.queue_free()
	await t._frames(2)


func shroom_tell() -> void:
	var at := Vector3(-200, 0, -200)
	var ground := _flat_ground(at)
	var hero := _hero_at(at + Vector3.UP * 0.05)
	hero.invulnerable_until = 1e9
	var shroom := Shroom.new()
	t.add_child(shroom)
	shroom.revive(at + Vector3(0, 0.1, -6.0))
	await t._frames(3)
	shroom.target = hero
	shroom._fired = false
	shroom.set_state(Enemy.State.ATTACK)
	var fired_at := -1.0
	for f in 70:
		await t._frames(1)
		if shroom._fired and fired_at < 0.0:
			fired_at = shroom.state_time
	check(fired_at >= 0.6, "it spits only after %.2f s of warning (%.2f)" % [shroom.beats.strike_at(), fired_at])
	for c in t.get_children():
		if c is Shot:
			c.queue_free()
	shroom.queue_free()
	hero.queue_free()
	ground.queue_free()
	await t._frames(2)
