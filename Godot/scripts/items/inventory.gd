class_name Inventory
extends Node
## A hero's gold and bag, as the Unity game's Inventory.cs: 48 stacks, each up to the item's
## maxStack; coins go straight to gold. Equipment, crafting and the bag window come in stage 3.

signal changed

const CAPACITY := 48

var gold := 25
## [{"id": item id, "count": n}]
var stacks: Array[Dictionary] = []


static func max_stack(id: String) -> int:
	var it := DB.item(id)
	return int(it.get("maxStack", 99)) if not it.is_empty() else 99


static func is_currency(id: String) -> bool:
	return int(DB.item(id).get("kind", -1)) == 0


func count(id: String) -> int:
	var n := 0
	for s in stacks:
		if s.id == id:
			n += int(s.count)
	return n


## Adds items; true only if everything fit (leftovers are lost, as in the Unity game).
func add(id: String, n := 1) -> bool:
	if n <= 0:
		return true
	if is_currency(id):
		gold += n * maxi(1, int(DB.item(id).get("value", 1)))
		changed.emit()
		return true
	var cap := max_stack(id)
	for s in stacks:
		if n <= 0:
			break
		if s.id == id and s.count < cap:
			var put := mini(n, cap - int(s.count))
			s.count += put
			n -= put
	while n > 0 and stacks.size() < CAPACITY:
		var put := mini(n, cap)
		stacks.append({"id": id, "count": put})
		n -= put
	changed.emit()
	return n <= 0


func has_room_for(id: String) -> bool:
	if is_currency(id):
		return true
	if stacks.size() < CAPACITY:
		return true
	for s in stacks:
		if s.id == id and s.count < max_stack(id):
			return true
	return false


## Takes n items from the last stacks first; false (and nothing taken) when there are too few.
func remove(id: String, n := 1) -> bool:
	if count(id) < n:
		return false
	var i := stacks.size() - 1
	while n > 0 and i >= 0:
		if stacks[i].id == id:
			var take := mini(n, int(stacks[i].count))
			stacks[i].count -= take
			n -= take
			if stacks[i].count <= 0:
				stacks.remove_at(i)
		i -= 1
	changed.emit()
	return true
