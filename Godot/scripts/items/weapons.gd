class_name Weapons
extends RefCounted
## Dauntless's seven weapon families on top of the Unity game's weapons (PlayerStats.WEAPONS keeps
## their Attack and their basic combo, so the numbers stay one source): each family adds how its
## blows land on a colossus (cut +50% part damage, blunt +60% stagger, pierce in between), the
## animations of its combo, its idle pose, and its Trảm (the held right-click). A weapon the hero
## carries is a family plus a Dauntless element (Elements) and a name.

const FAMILIES := {
	"sword": {"name": "Kiếm", "stat": "sword", "cut": true, "stagger": 1.0,
		"anims": ["Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C"], "idle": "Sword_Idle",
		"heavy": {"name": "Trảm", "anim": "Sword_Regular_C", "charge": 0.9, "power": [1.4, 1.8], "poise": [30, 50],
			"shape": "cone", "reach": 2.7, "angle": 130.0}},
	"axe": {"name": "Rìu", "stat": "axe", "cut": true, "stagger": 1.1,
		"anims": ["Sword_Regular_B", "TreeChopping", "Sword_Regular_C"], "idle": "Sword_Idle",
		"heavy": {"name": "Bổ Núi", "anim": "TreeChopping", "charge": 1.2, "power": [1.8, 2.4], "poise": [40, 60],
			"shape": "cone", "reach": 3.0, "angle": 150.0}},
	"hammer": {"name": "Búa", "stat": "mace", "cut": false, "stagger": 1.6,
		"anims": ["Sword_Regular_C", "TreeChopping", "Sword_Regular_B"], "idle": "Sword_Idle",
		"heavy": {"name": "Nện Aether", "anim": "TreeChopping", "charge": 1.1, "power": [1.5, 1.9], "poise": [70, 110],
			"shape": "slam", "reach": 3.2, "angle": 360.0}},
	"chain_blades": {"name": "Song Liêm", "stat": "dagger", "cut": true, "stagger": 0.8,
		"anims": ["Sword_Regular_A", "Melee_Hook", "Sword_Regular_B"], "idle": "Sword_Idle", "dual": true,
		"heavy": {"name": "Lốc Liêm", "anim": "Sword_Regular_Combo", "charge": 0.7, "power": [0.7, 0.7], "poise": [12, 18],
			"shape": "spin", "reach": 2.8, "angle": 360.0, "hits": 3}},
	"war_pike": {"name": "Thương", "stat": "spear", "cut": false, "pierce": true, "stagger": 1.0,
		"anims": ["Sword_Attack", "Sword_Attack", "Sword_Regular_B"], "idle": "Sword_Idle",
		"heavy": {"name": "Xuyên Phá", "anim": "Sword_Attack", "charge": 1.0, "power": [1.5, 2.0], "poise": [30, 45],
			"shape": "lunge", "reach": 4.4, "angle": 40.0, "lunge": 3.2}},
	"aether_strikers": {"name": "Găng Aether", "stat": "fist", "cut": false, "stagger": 1.6,
		"anims": ["Punch_Jab", "Punch_Cross", "Melee_Hook"], "idle": "Idle", "dual": true,
		"heavy": {"name": "Quyền Aether", "anim": "Punch_Cross", "charge": 0.7, "power": [1.3, 1.7], "poise": [60, 80],
			"shape": "lunge", "reach": 2.4, "angle": 90.0, "lunge": 2.6}},
	"repeaters": {"name": "Súng Ostia", "stat": "bow", "cut": false, "pierce": true, "stagger": 0.7,
		"anims": ["Pistol_Shoot"], "idle": "Pistol_Idle", "dual": true,
		"heavy": {"name": "Phát Tụ Lực", "anim": "Pistol_Shoot", "charge": 0.9, "power": [1.6, 2.2], "poise": [20, 30],
			"shape": "shot", "reach": 30.0, "angle": 0.0}},
}

## What the hero carries until the forge comes (one family per element, and the old sword).
const ARMORY := {
	"w_kiem_hong_anh": {"family": "sword", "element": "neutral", "name": "Kiếm Hồng Anh"},
	"w_riu_hoa_nguc": {"family": "axe", "element": "blaze", "name": "Rìu Hoả Ngục"},
	"w_bua_bang_phong": {"family": "hammer", "element": "frost", "name": "Búa Băng Phong"},
	"w_song_liem_loi_dieu": {"family": "chain_blades", "element": "shock", "name": "Song Liêm Lôi Điểu"},
	"w_thuong_moc_lam": {"family": "war_pike", "element": "terra", "name": "Thương Mộc Lâm"},
	"w_gang_thanh_quang": {"family": "aether_strikers", "element": "radiant", "name": "Găng Thánh Quang"},
	"w_sung_hu_anh": {"family": "repeaters", "element": "umbral", "name": "Súng Hư Ảnh"},
}
const START := ["w_kiem_hong_anh", "w_riu_hoa_nguc", "w_bua_bang_phong", "w_song_liem_loi_dieu", "w_thuong_moc_lam",
	"w_gang_thanh_quang", "w_sung_hu_anh"]


## Weapons the townsfolk hold (never the hero's): Wils's and Moyra's hammers, Janek Zai's repeater
## glowing with aether, Kat's blade, Bulvar's axe.
const PROPS := {
	"npc_bua_ren": {"family": "hammer", "element": "neutral", "name": "Búa Rèn"},
	"npc_sung_janek": {"family": "repeaters", "element": "frost", "name": "Súng Ostia"},
	"npc_kiem_kat": {"family": "sword", "element": "neutral", "name": "Kiếm"},
	"npc_riu_bulvar": {"family": "axe", "element": "frost", "name": "Rìu"},
}


static func item(id: String) -> Dictionary:
	return ARMORY.get(id, PROPS.get(id, ARMORY.w_kiem_hong_anh))


static func family(id: String) -> Dictionary:
	return FAMILIES[item(id).family]


static func element(id: String) -> String:
	return item(id).element


## The combat element its blows deal (physical for neutral weapons).
static func damage_type(id: String) -> int:
	return int(Elements.ALL[element(id)].type)
