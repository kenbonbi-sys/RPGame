class_name Armour
extends RefCounted
## Dauntless armour (the wiki's Armour page): four pieces, head, torso, arms and legs, each with a
## resistance value and an element; the sets are named after the behemoths they are carved from,
## the neutral starter sets (Krolachi, Recruit) first, then the elemental ones by tier (5 to 25).
## Every piece of a set shares its element and its value, so a set is written once.
##
## Dauntless's rule: armour of an element resists hits of that element and is weak to the opposing
## one (Blaze↔Frost, Shock↔Terra, Radiant↔Umbral, as Elements). Each worn piece adds its value to
## its element and takes it from the opposing one; every point is 0.5% less (or more) damage from
## hits of that element, at most ±50% (four pieces of 25 = 100 points). Neutral pieces change
## nothing. This sits on top of the Unity game's armour and resistances (Health), which stay the base.
##
## A hit's element is its combat element (Elements.of_type), or else the attacker's: a behemoth's
## blows are of its element (Gấu Ma's stomps hit as Terra, Nấm Độc's spores as Umbral).
## Perks and cells are not in yet.

const SLOTS := ["head", "torso", "arms", "legs"]
const SLOT_NAMES := {"head": "Mũ", "torso": "Giáp", "arms": "Găng", "legs": "Giày"}
const SLOT_ICONS := {"head": "helm", "torso": "armor", "arms": "fist", "legs": "boot"}
const PER_POINT := 0.005
const CAP := 0.5

## id: behemoth (the set's name), element, resistance of every piece, and the pieces' names
## (head, torso, arms, legs; "" = the set has no such piece). Order as on the wiki.
const SETS := {
	"krolachi": {"name": "Krolachi", "element": "neutral", "resist": 3,
		"pieces": ["Krolachi Shave", "Krolachi Tunic", "Krolachi Wraps", "Krolachi Shorts"]},
	"recruit": {"name": "Recruit", "element": "neutral", "resist": 3,
		"pieces": ["", "Recruit's Breastplate", "Recruit's Gloves", "Recruit's Greaves"]},
	"embermane": {"name": "Embermane", "element": "blaze", "resist": 5,
		"pieces": ["Fiery Helm", "Fiery Breastplate", "Fiery Gauntlets", "Fiery Greaves"]},
	"boreus": {"name": "Boreus", "element": "frost", "resist": 5,
		"pieces": ["Boreal Epiphany", "Boreal Resolve", "Boreal Might", "Boreal March"]},
	"drask": {"name": "Drask", "element": "shock", "resist": 5,
		"pieces": ["Draskscale Helmet", "Draskscale Plate", "Draskscale Gauntlets", "Draskscale Greaves"]},
	"skarn": {"name": "Skarn", "element": "terra", "resist": 5,
		"pieces": ["Sight of Stone", "Heart of Stone", "Might of Stone", "Pillars of Stone"]},
	# the wiki's torso table leaves out Volcanic Aegis; it is given the set's 10 Blaze
	"charrogg": {"name": "Charrogg", "element": "blaze", "resist": 10,
		"pieces": ["Volcanic Helm", "Volcanic Aegis", "Volcanic Grips", "Volcanic Treads"]},
	"pangar": {"name": "Pangar", "element": "frost", "resist": 10,
		"pieces": ["Brow of Ice", "Core of Ice", "Arms of Ice", "Feet of Ice"]},
	"quillshot": {"name": "Quillshot", "element": "frost", "resist": 10,
		"pieces": ["Quillspike Mask", "Quillspike Jacket", "Quillspike Grips", "Quillspike Boots"]},
	"nayzaga": {"name": "Nayzaga", "element": "shock", "resist": 10,
		"pieces": ["Shocking Gaze", "Shocking Heart", "Shocking Grasp", "Shocking Stride"]},
	"shrike": {"name": "Shrike", "element": "shock", "resist": 10,
		"pieces": ["Shrikedown Helm", "Shrikedown Plate", "Shrikedown Gloves", "Shrikedown Greaves"]},
	"gnasher": {"name": "Gnasher", "element": "terra", "resist": 10,
		"pieces": ["Gnasher Cap", "Gnasher Cloak", "Gnasher Grips", "Gnasher Treads"]},
	"kharabak": {"name": "Kharabak", "element": "terra", "resist": 10,
		"pieces": ["Eye of the Swarm", "Shell of the Swarm", "Clutches of the Swarm", "Dance of the Swarm"]},
	"karkonos": {"name": "Karkonos", "element": "umbral", "resist": 10,
		"pieces": ["Brineguard Visage", "Brineguard Aegis", "Brineguard Embrace", "Brineguard Greaves"]},
	"frostwulf": {"name": "Frostwülf", "element": "frost", "resist": 15,
		"pieces": ["Wülf Mane", "Wülf Plate", "Wülf Claws", "Wülf Paws"]},
	"skraev": {"name": "Skraev", "element": "frost", "resist": 15,
		"pieces": ["Skraevwing Helmet", "Skraevwing Jacket", "Skraevwing Gloves", "Skraevwing Boots"]},
	"valomyr": {"name": "Valomyr", "element": "radiant", "resist": 15,
		"pieces": ["Crest of Valour", "Aegis of Valour", "Gauntlets of Valour", "Greaves of Valour"]},
	"sahvyt": {"name": "Sahvyt", "element": "shock", "resist": 15,
		"pieces": ["Bladed Mask", "Bladed Jacket", "Bladed Gloves", "Bladed Boots"]},
	"fenroar": {"name": "Fenroar", "element": "terra", "resist": 15,
		"pieces": ["Stonewood Great Helm", "Stonewood Chest Plate", "Stonewood Gauntlets", "Stonewood Boots"]},
	"koshai": {"name": "Koshai", "element": "terra", "resist": 15,
		"pieces": ["Visage of Thorns", "Mantle of Thorns", "Grasp of Thorns", "Stride of Thorns"]},
	"riftstalker": {"name": "Riftstalker", "element": "umbral", "resist": 15,
		"pieces": ["Guise of the Rift", "Mantle of the Rift", "Gauntlets of the Rift", "Striders of the Rift"]},
	"hellion": {"name": "Hellion", "element": "blaze", "resist": 20,
		"pieces": ["Hellplate Casque", "Hellplate Cuirass", "Hellplate Bracers", "Hellplate Greaves"]},
	"phaelanx": {"name": "Phaelanx", "element": "blaze", "resist": 20,
		"pieces": ["Phaelanx's Spearhead", "Phaelanx's Vanguard", "Phaelanx's Firearms", "Phaelanx's Heatsinks"]},
	"alyra": {"name": "Alyra", "element": "radiant", "resist": 20,
		"pieces": ["Spellbound Hood", "Spellbound Wings", "Spellbound Gauntlets", "Spellbound Boots"]},
	"rezakiri": {"name": "Rezakiri", "element": "radiant", "resist": 20,
		"pieces": ["Light's Crown", "Light's Virtue", "Light's Refuge", "Light's Walk"]},
	"timeweave": {"name": "Timeweave", "element": "radiant", "resist": 20,
		"pieces": ["Timeweave Helm", "Timeweave Robes", "Timeweave Gauntlets", "Timeweave Leggings"]},
	"stormclaw": {"name": "Stormclaw", "element": "shock", "resist": 20,
		"pieces": ["Lightning Helm", "Lightning Plate", "Lightning Gloves", "Lightning Boots"]},
	"thunderdeep": {"name": "Thunderdeep Drask", "element": "shock", "resist": 20,
		"pieces": ["Thunderdeep's Capacitor", "Thunderdeep's Dynamo", "Thunderdeep's Regulators", "Thunderdeep's Turbines"]},
	"shrowd": {"name": "Shrowd", "element": "umbral", "resist": 20,
		"pieces": ["Dark Watch", "Dark Marrow", "Dark Embrace", "Dark Path"]},
	"torgadoro": {"name": "Torgadoro", "element": "blaze", "resist": 25,
		"pieces": ["Torgadoro's Apex", "Torgadoro's Core", "Torgadoro's Brawn", "Torgadoro's Gait"]},
	"urska": {"name": "Urska", "element": "frost", "resist": 25,
		"pieces": ["Adversary's Guile", "Adversary's Pride", "Adversary's Wrath", "Adversary's Drive"]},
	"chronovore": {"name": "The Chronovore", "element": "radiant", "resist": 25,
		"pieces": ["Chronohelm", "Chronoplate", "Chronogrips", "Chronotreads"]},
	"malkarion": {"name": "Malkarion", "element": "shock", "resist": 25,
		"pieces": ["Malkarion's Sight", "Malkarion's Soul", "Malkarion's Grasp", "Malkarion's March"]},
	"agarus": {"name": "Agarus", "element": "terra", "resist": 25,
		"pieces": ["Agaric Canopy", "Agaric Bole", "Agaric Branches", "Agaric Roots"]},
	"thrax": {"name": "Thrax", "element": "umbral", "resist": 25,
		"pieces": ["Thrax's Scream", "Thrax's Shadow", "Thrax's Embrace", "Thrax's Guile"]},
}

## What the hero carries until the forge comes: the Krolachi clothes it starts in, and one set
## per element from the lower tiers (Terra against Gấu Ma and the slimes, Umbral against Nấm Độc).
const WARDROBE := ["krolachi", "charrogg", "pangar", "shrike", "gnasher", "valomyr", "karkonos"]
const START := "krolachi"


static func set_of(id: String) -> Dictionary:
	return SETS.get(id, SETS[START])


## Whether set `id` makes a piece for `slot`.
static func has_piece(id: String, slot: String) -> bool:
	return SETS.has(id) and String(SETS[id].pieces[SLOTS.find(slot)]) != ""


## One piece: {slot, set, name, element, resist}, or {} when the set has none for that slot.
static func piece(id: String, slot: String) -> Dictionary:
	if not has_piece(id, slot):
		return {}
	var s: Dictionary = SETS[id]
	return {"slot": slot, "set": id, "name": String(s.pieces[SLOTS.find(slot)]), "element": String(s.element),
		"resist": int(s.resist)}


## Every piece of a set on its slots (slot -> set id), the way wearing the whole set fills them.
static func full_set(id: String) -> Dictionary:
	var worn := {}
	for slot in SLOTS:
		if has_piece(id, slot):
			worn[slot] = id
	return worn


## Net resistance points per Dauntless element of the pieces worn (slot -> set id): each piece
## adds its value to its element and takes it from the opposing one. Only elements that are not 0.
static func points(worn: Dictionary) -> Dictionary:
	var out := {}
	for slot in worn:
		var p := piece(String(worn[slot]), String(slot))
		if p.is_empty() or p.element == "neutral":
			continue
		var e: String = p.element
		var opposite: String = Elements.ALL[e].opposite
		out[e] = int(out.get(e, 0)) + int(p.resist)
		out[opposite] = int(out.get(opposite, 0)) - int(p.resist)
	for e in out.keys():
		if int(out[e]) == 0:
			out.erase(e)
	return out


## Damage multiplier of a hit of Dauntless element `element` on someone whose armour gives these
## points: 0.5% less per point resisted, 0.5% more per point of weakness, within ±50%.
static func multiplier(armour_points: Dictionary, element: String) -> float:
	if element == "" or element == "neutral":
		return 1.0
	var p := float(armour_points.get(element, 0))
	return 1.0 - clampf(p * PER_POINT, -CAP, CAP)


## The Dauntless element a hit counts as against armour: its combat element, or the attacker's
## own element when the blow itself is plain (a behemoth's blows are of its element).
static func hit_element(d: DamageInfo) -> String:
	var e := Elements.of_type(d.type)
	if e != "":
		return e
	if d.source and is_instance_valid(d.source):
		var h := Combat.health_of(d.source)
		if h and h.element != "" and h.element != "neutral":
			return h.element
	return ""


## The element the worn pieces lean to (the one with the most pieces; "neutral" when none).
static func main_element(worn: Dictionary) -> String:
	var count := {}
	for slot in worn:
		var p := piece(String(worn[slot]), String(slot))
		if not p.is_empty() and p.element != "neutral":
			count[p.element] = int(count.get(p.element, 0)) + 1
	var best := "neutral"
	for e in count:
		if best == "neutral" or int(count[e]) > int(count[best]):
			best = e
	return best


## The set every worn piece comes from, or "" when the pieces are mixed (or nothing is worn).
static func whole_set(worn: Dictionary) -> String:
	var id := ""
	for slot in worn:
		if id == "":
			id = String(worn[slot])
		elif String(worn[slot]) != id:
			return ""
	return id if id != "" and worn.size() == full_set(id).size() else ""


## What the HUD says the armour does: "Kháng Hoả 40 · Yếu Băng 40", or "Không kháng hệ".
static func summary(worn: Dictionary) -> String:
	var pts := points(worn)
	var parts: PackedStringArray = []
	for e in Elements.ALL:
		if int(pts.get(e, 0)) > 0:
			parts.append("Kháng %s %d" % [Elements.display_name(e), int(pts[e])])
	for e in Elements.ALL:
		if int(pts.get(e, 0)) < 0:
			parts.append("Yếu %s %d" % [Elements.display_name(e), -int(pts[e])])
	return " · ".join(parts) if parts.size() > 0 else "Không kháng hệ"
