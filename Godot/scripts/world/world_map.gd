class_name WorldMap
extends RefCounted
## The layout of the one seamless world, carried over from the Unity WorldBuilder: same places,
## same roads, measured in the Unity map's tiles. One tile is TILE metres here.
## Map coordinates (u east, v north) in metres sit at world (u, height, -v).
##
## height() and splat() describe the land; tools/bake_world.gd samples them into
## world/terrain_data.res, which the game loads (the land is not recomputed at run time).

const TILE := 4.0
const W_TILES := 200
const H_TILES := 128
const SIZE_U := W_TILES * TILE   # 800 m
const SIZE_V := H_TILES * TILE   # 512 m
## The southern band (forest + swamp); the steppe plateau and the cave mountains lie north of it.
const LOW_V := 64 * TILE
const WATER_LEVEL := 0.0

# ------------------------------------------------------------------ places (Unity tiles)
const VILLAGE := Vector2(18, 20)
const ARENA := Vector2(84, 45)
const CAMPS := [Vector2(35.5, 22), Vector2(47, 27.5), Vector2(53, 15), Vector2(62, 29.5), Vector2(71.5, 35)]
const CAMP_RADIUS := [2.5, 4.0, 3.5, 3.5, 3.0]
const MAIN_PATH := [
	Vector2(18, 20), Vector2(26, 19), Vector2(33, 22), Vector2(40, 25), Vector2(47, 27),
	Vector2(54, 24), Vector2(60, 28), Vector2(66, 32), Vector2(72, 36), Vector2(78, 40), Vector2(84, 45)]
const SIDE_PATH := [Vector2(40, 25), Vector2(44, 19), Vector2(53, 15)]
const NORTH_PATH := [Vector2(18, 20), Vector2(17, 27), Vector2(15, 34)]
const EAST_ROAD := [
	Vector2(66, 32), Vector2(72, 29.5), Vector2(80, 27.5), Vector2(88, 27.5), Vector2(96, 29),
	Vector2(104, 29.5), Vector2(110, 29.5)]

# the swamp (east)
const OUTPOST := Vector2(113, 30)
const TOAD_POND := Vector2(152.5, 50.5)
const SNAKE_LAIR := Vector2(182, 30)
const MUD_FIELD := Vector2(141, 18)
const TRAILS := [
	[Vector2(110, 29.5), Vector2(118, 27), Vector2(126, 24), Vector2(134, 22), Vector2(141, 21.5),
	 Vector2(149, 22.5), Vector2(157, 25.5), Vector2(164, 28.5), Vector2(170, 30)],
	[Vector2(113, 33), Vector2(115, 36), Vector2(120, 40.5), Vector2(127, 44.5), Vector2(136, 47), Vector2(143, 48)],
	[Vector2(134, 22), Vector2(136, 15), Vector2(142, 9), Vector2(152, 8), Vector2(162, 10),
	 Vector2(168, 14), Vector2(168, 21), Vector2(164, 28.5)],
	[Vector2(164, 28.5), Vector2(166, 36), Vector2(167, 44), Vector2(168, 52), Vector2(168, 60), Vector2(168, 64)],
]
## (centre, radius x, radius y) in tiles
const PONDS := [
	[Vector2(124, 33), 3.4, 2.4], [Vector2(133, 35.5), 4.5, 3.0], [Vector2(156, 36), 5.0, 3.4],
	[Vector2(146, 15), 4.0, 2.6], [Vector2(175, 44), 4.2, 3.4], [Vector2(153, 51.5), 6.5, 4.6],
	[Vector2(182, 30), 10.5, 8.5],
]
const MOUNDS := [Vector2(176.5, 33.2), Vector2(187.5, 33.2), Vector2(176.5, 26.8), Vector2(187.5, 26.8)]
const MOUND_RADIUS := 2.3

# the north
const CAVE_MOUTH := Vector2(168, 64)
const STEPPE_RECT := Rect2(5, 67.5, 93, 56.5)
const WIND_GATE := Vector2(95, 91.5)

# ------------------------------------------------------------------ the arena of Gấu Ma Rừng Già
## Flat floor where the colossus walks, in metres.
const ARENA_FLOOR_R := 50.0
const ARENA_RIDGE_IN := 60.0
const ARENA_RIDGE_OUT := 94.0
const ARENA_HEIGHT := 8.0
const VILLAGE_HEIGHT := 7.0
const VILLAGE_R := 40.0

static var _lo: FastNoiseLite
static var _hi: FastNoiseLite
static var _paths_m: Array = []     # [points (metres), half width]
static var _trails_m: Array = []


static func _init_noise() -> void:
	if _lo:
		return
	_lo = FastNoiseLite.new()
	_lo.seed = 1337
	_lo.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
	_lo.frequency = 0.0055
	_lo.fractal_octaves = 3
	_hi = FastNoiseLite.new()
	_hi.seed = 4242
	_hi.noise_type = FastNoiseLite.TYPE_SIMPLEX
	_hi.frequency = 0.03
	_hi.fractal_octaves = 2
	for p in [[MAIN_PATH, 3.6], [SIDE_PATH, 2.6], [NORTH_PATH, 2.6], [EAST_ROAD, 3.2]]:
		_paths_m.append([_to_m(p[0]), p[1]])
	for t in TRAILS:
		_trails_m.append([_to_m(t), 2.6])


static func _to_m(points: Array) -> PackedVector2Array:
	var out := PackedVector2Array()
	for p in points:
		out.append(p * TILE)
	return out


# ------------------------------------------------------------------ coordinates
## Unity tile -> map metres.
static func tile(t: Vector2) -> Vector2:
	return t * TILE


## Map metres -> world position at the given height.
static func world(m: Vector2, h := 0.0) -> Vector3:
	return Vector3(m.x, h, -m.y)


static func map_of(world_pos: Vector3) -> Vector2:
	return Vector2(world_pos.x, -world_pos.z)


# ------------------------------------------------------------------ the land
static func noise_lo(u: float, v: float) -> float:
	_init_noise()
	return _lo.get_noise_2d(u, v)


static func noise_hi(u: float, v: float) -> float:
	_init_noise()
	return _hi.get_noise_2d(u, v)


static func swamp_weight(u: float, v: float) -> float:
	return smoothstep(380.0, 440.0, u + noise_lo(u * 0.7, v) * 40.0) * (1.0 - north_weight(u, v))


static func north_weight(u: float, v: float) -> float:
	return smoothstep(LOW_V - 6.0, LOW_V + 16.0, v + noise_hi(u, v) * 5.0)


## 0 on the steppe plateau, 1 in the cave mountains (east of the plateau).
static func mountain_weight(u: float, v: float) -> float:
	return smoothstep(385.0, 420.0, u + noise_lo(u, v * 1.3) * 20.0) * north_weight(u, v)


static func border_weight(u: float, v: float) -> float:
	var edge := minf(minf(u, v), minf(SIZE_U - u, SIZE_V - v))
	return 1.0 - smoothstep(6.0, 46.0, edge + noise_hi(u, v) * 10.0)


static func path_distance(u: float, v: float) -> Vector2:
	## x = distance to the nearest road edge (negative inside), y = that road's half width
	_init_noise()
	var p := Vector2(u, v)
	var best := INF
	var best_w := 0.0
	for set in [_paths_m, _trails_m]:
		for entry in set:
			var pts: PackedVector2Array = entry[0]
			var w: float = entry[1]
			for i in range(pts.size() - 1):
				var d := _seg_dist(p, pts[i], pts[i + 1]) - w
				if d < best:
					best = d
					best_w = w
	return Vector2(best, best_w)


static func _seg_dist(p: Vector2, a: Vector2, b: Vector2) -> float:
	var ab := b - a
	var t := clampf((p - a).dot(ab) / maxf(ab.length_squared(), 0.0001), 0.0, 1.0)
	return p.distance_to(a + ab * t)


static func arena_center() -> Vector2:
	return tile(ARENA)


static func village_center() -> Vector2:
	return tile(VILLAGE)


## Where the main road enters the arena (the ridge is broken there).
static func arena_gate_dir() -> Vector2:
	return (tile(Vector2(76, 38.5)) - arena_center()).normalized()


static func _base_height(u: float, v: float) -> float:
	var n1 := noise_lo(u, v)
	var n2 := noise_hi(u, v)
	var forest := 6.0 + n1 * 7.0 + n2 * 1.1
	var swamp := 1.1 + n1 * 1.1 + n2 * 0.45
	var h := lerpf(forest, swamp, swamp_weight(u, v))
	var nw := north_weight(u, v)
	if nw > 0.0:
		var steppe := 34.0 + n1 * 6.0 + n2 * 1.2
		var ridged := 1.0 - absf(noise_lo(u * 1.7 + 300.0, v * 1.7))
		var mountain := 52.0 + ridged * ridged * 70.0 + n2 * 6.0
		var north := lerpf(steppe, mountain, mountain_weight(u, v) / maxf(nw, 0.001))
		h = lerpf(h, north, nw)
	var bw := border_weight(u, v)
	if bw > 0.0:
		var ridged2 := 1.0 - absf(noise_lo(u * 1.3, v * 1.3 + 500.0))
		h = lerpf(h, 48.0 + ridged2 * 45.0 + n2 * 5.0, bw)
	return h


static func height(u: float, v: float) -> float:
	var h := _base_height(u, v)
	var p := Vector2(u, v)

	# swamp ponds and the lake of Xà Mẫu, with the lake's four dry mounds
	for pond in PONDS:
		var c: Vector2 = pond[0] * TILE
		var e := Vector2((u - c.x) / (pond[1] * TILE), (v - c.y) / (pond[2] * TILE)).length()
		e += noise_hi(u * 2.0, v * 2.0) * 0.12
		if e < 1.35:
			h = lerpf(h, -1.8, 1.0 - smoothstep(0.75, 1.35, e))
	for m in MOUNDS:
		var dm := p.distance_to(m * TILE) / (MOUND_RADIUS * TILE)
		if dm < 1.4:
			h = lerpf(h, 1.4, 1.0 - smoothstep(0.8, 1.4, dm))

	# roads: flatten toward a smooth version of the land
	var pd := path_distance(u, v)
	if pd.x < 9.0:
		var smooth := lerpf(6.0 + noise_lo(u, v) * 7.0, 1.4, swamp_weight(u, v))
		var road := 1.0 - smoothstep(0.0, 9.0, pd.x)
		h = lerpf(h, maxf(smooth, 1.3), road * 0.85)

	# monster camps in the forest: small clearings
	for i in CAMPS.size():
		var dc := p.distance_to(CAMPS[i] * TILE)
		var r: float = CAMP_RADIUS[i] * TILE
		if dc < r * 2.0:
			h = lerpf(h, 6.0 + noise_lo(u, v) * 3.0, (1.0 - smoothstep(r, r * 2.0, dc)) * 0.7)

	# the village: a flat green
	var dv := p.distance_to(village_center())
	if dv < VILLAGE_R * 1.6:
		h = lerpf(h, VILLAGE_HEIGHT, 1.0 - smoothstep(VILLAGE_R, VILLAGE_R * 1.6, dv))

	# the arena: a wide flat meadow ringed by a rocky ridge, broken where the road comes in
	h = _arena(u, v, h)
	return h


static func arena_ridge_weight(u: float, v: float) -> float:
	var off := Vector2(u, v) - arena_center()
	var r := off.length()
	if r < ARENA_RIDGE_IN - 10.0 or r > ARENA_RIDGE_OUT + 20.0:
		return 0.0
	var ring := smoothstep(ARENA_RIDGE_IN - 8.0, ARENA_RIDGE_IN + 10.0, r) * (1.0 - smoothstep(ARENA_RIDGE_OUT - 18.0, ARENA_RIDGE_OUT + 18.0, r))
	var ang := rad_to_deg(off.normalized().angle_to(arena_gate_dir()))
	var gap := smoothstep(16.0, 30.0, absf(ang))
	return ring * gap


static func _arena(u: float, v: float, h: float) -> float:
	var r := Vector2(u, v).distance_to(arena_center())
	if r > ARENA_RIDGE_OUT + 30.0:
		return h
	var floor_w := 1.0 - smoothstep(ARENA_FLOOR_R, ARENA_RIDGE_IN + 6.0, r)
	h = lerpf(h, ARENA_HEIGHT + noise_hi(u, v) * 0.35, floor_w)
	var ridge := arena_ridge_weight(u, v)
	if ridge > 0.0:
		var crags := 0.75 + 0.5 * (1.0 - absf(noise_hi(u * 1.5, v * 1.5)))
		h += ridge * 26.0 * crags
	return h


## Ground paint as weights: r = grass, g = bare earth / road, b = rock, a = swamp mud.
static func splat(u: float, v: float, h: float) -> Color:
	var pd := path_distance(u, v)
	var n2 := noise_hi(u * 1.7, v * 1.7)
	var road := 1.0 - smoothstep(-1.4, 0.8, pd.x + n2 * 0.9)
	var rock := maxf(maxf(border_weight(u, v), mountain_weight(u, v)), arena_ridge_weight(u, v))
	rock = clampf(rock * 1.3 - 0.15, 0.0, 1.0)
	var mud := swamp_weight(u, v) * clampf(0.55 + noise_lo(u * 3.0, v * 3.0) * 0.9, 0.0, 1.0)
	if h < 0.6:
		mud = maxf(mud, 1.0 - smoothstep(-0.4, 0.6, h))
	var dv := Vector2(u, v).distance_to(village_center())
	road = maxf(road, (1.0 - smoothstep(10.0, 16.0, dv)) * 0.7)
	var c := Color(1.0, road, rock, mud)
	c.r = maxf(0.0, 1.0 - road - rock - mud)
	var s := c.r + c.g + c.b + c.a
	return Color(c.r / s, c.g / s, c.b / s, c.a / s)


## Name of the area at a map point, for the area banner (Unity: ZoneArea).
static func area_name(m: Vector2) -> String:
	if m.distance_to(village_center()) < VILLAGE_R + 6.0:
		return "Làng Lá Xanh"
	if m.distance_to(arena_center()) < ARENA_RIDGE_IN:
		return "Hang Gấu Ma"
	if m.y > LOW_V + 8.0:
		return "Thảo Nguyên Gió" if m.x < 400.0 else "Núi Pha Lê"
	if swamp_weight(m.x, m.y) > 0.5:
		return "Đầm Lầy Sương Mù"
	return "Rừng Thì Thầm"
