class_name TerrainData
extends Resource
## The baked land: heights and ground paint on a regular grid over the whole map.
## Written by tools/bake_world.gd from WorldMap, read by Terrain at run time.

@export var width := 0          ## samples along u (east)
@export var depth := 0          ## samples along v (north)
@export var spacing := 2.0      ## metres between samples
@export var heights := PackedFloat32Array()
## RGBA8 per sample: grass, earth, rock, mud (see WorldMap.splat)
@export var splat := PackedByteArray()


func index(ix: int, iv: int) -> int:
	return iv * width + ix


func height_at_sample(ix: int, iv: int) -> float:
	return heights[clampi(iv, 0, depth - 1) * width + clampi(ix, 0, width - 1)]


## Bilinear height at a map point (metres).
func height_at(u: float, v: float) -> float:
	var fx := clampf(u / spacing, 0.0, width - 1.001)
	var fv := clampf(v / spacing, 0.0, depth - 1.001)
	var ix := int(fx)
	var iv := int(fv)
	var tx := fx - ix
	var tv := fv - iv
	var a := lerpf(height_at_sample(ix, iv), height_at_sample(ix + 1, iv), tx)
	var b := lerpf(height_at_sample(ix, iv + 1), height_at_sample(ix + 1, iv + 1), tx)
	return lerpf(a, b, tv)


func height_at_world(p: Vector3) -> float:
	return height_at(p.x, -p.z)


func splat_at(u: float, v: float) -> Color:
	var ix := clampi(roundi(u / spacing), 0, width - 1)
	var iv := clampi(roundi(v / spacing), 0, depth - 1)
	var i := index(ix, iv) * 4
	return Color(splat[i] / 255.0, splat[i + 1] / 255.0, splat[i + 2] / 255.0, splat[i + 3] / 255.0)


func normal_at(u: float, v: float) -> Vector3:
	var e := spacing
	var hl := height_at(u - e, v)
	var hr := height_at(u + e, v)
	var hd := height_at(u, v - e)
	var hu := height_at(u, v + e)
	# world z = -v
	return Vector3(hl - hr, 2.0 * e, hu - hd).normalized()
