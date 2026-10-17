// Dauntless's seven weapon families (the Godot port's Weapons, Content/Data/weapons.json) on top of
// the Unity game's weapon kinds (RttSheet's WeaponKind: Attack and the basic combo): how their blows
// land on a colossus (cut, pierce, stagger) and their heavy move, the held right-click (Trảm and
// the others). A weapon the hero carries is a family, a Dauntless element and a name.
#pragma once

#include "Rules/RttElements.h"
#include "Rules/RttSheet.h"

#include <string>
#include <vector>

namespace Rtt
{
	enum class HeavyShape : int { Cone, Slam, Spin, Lunge, Shot };

	struct HeavyMove
	{
		std::string Name;
		// seconds to full power
		double Charge = 1.0;
		// × Attack: Power[0] with no charge, Power[0] + Power[1] at full; poise the same way
		double Power[2] = {1.0, 0.0};
		double Poise[2] = {0.0, 0.0};
		HeavyShape Shape = HeavyShape::Cone;
		// metres, and the cone's angle in degrees
		double Reach = 2.5;
		double Angle = 120.0;
		int Hits = 1;
		// metres the lunge carries the hero
		double Lunge = 0.0;
	};

	struct WeaponFamily
	{
		std::string Id;
		std::string Name;
		// the Unity weapon kind it uses (Attack, basic combo)
		std::string Stat;
		// cut: +50% part damage on a colossus; pierce: in between; blunt: neither, more stagger
		bool Cut = false;
		bool Pierce = false;
		// one in each hand
		bool Dual = false;
		// poise multiplier of its blows
		double Stagger = 1.0;
		HeavyMove Heavy;
	};

	struct WeaponItem
	{
		std::string Id;
		std::string Family;
		std::string Name;
		DElement Element = DElement::Neutral;
	};

	// One blow of a heavy move at `charge` (0..1).
	struct HeavyBlow
	{
		double Amount = 0.0;
		double Poise = 0.0;
		double Knockback = 0.0;
		double HitStop = 0.0;
	};

	class WeaponTable
	{
	public:
		std::vector<WeaponFamily> Families;
		// what the hero carries
		std::vector<WeaponItem> Armory;
		// what the townsfolk hold, never the hero
		std::vector<WeaponItem> Props;
		// the hero's starting weapons, in the order V cycles them
		std::vector<std::string> Start;

		void Load(const JsonValue& data);
		const WeaponFamily* FindFamily(const std::string& id) const;
		// The armory's or a prop's item; unknown ids fall back to the first armory weapon.
		const WeaponItem& Item(const std::string& id) const;
		bool IsArmory(const std::string& id) const;
		const WeaponFamily& FamilyOf(const std::string& itemId) const;
		// The Unity weapon kind of the item's family (its Attack and basic attack).
		const WeaponKind& KindOf(const std::string& itemId) const;
		// The combat element its blows deal (physical for neutral weapons).
		Element DamageType(const std::string& itemId) const;
		// The next weapon in Start after `itemId` (V).
		const std::string& Next(const std::string& itemId) const;

	private:
		WeaponItem Fallback;
		WeaponFamily NoFamily;
	};

	// Attack × (Power[0] + Power[1]·c) × dealt; poise Poise[0] + Poise[1]·c; knockback 6 + 4c;
	// hit-stop 0.08 + 0.07c (hero.gd's _heavy_strike).
	HeavyBlow HeavyBlowAt(const HeavyMove& move, double attack, double dealtMultiplier, double charge);
}
