// How much a hit takes off (the Unity game's Health.TakeDamage, as the Godot port has it), in the
// same order: team check, invulnerability (evaded), attacker-side scaling (the hero's Attack, a
// curse on the attacker), damage-taken multipliers, a guard by direction, armour against the
// attacker's level, resistance, the ±5% spread, then the Dauntless rules (the element matchup on
// monsters, the armour a hero wears), rounding half to even with a minimum of 1. A pure hit (a
// fall, a kill) skips everything but the rounding.
#pragma once

#include "Rules/RttArmour.h"

namespace Rtt
{
	struct Hit
	{
		double Amount = 0.0;
		Element Type = Element::Physical;
		Team SourceTeam = Team::Enemy;
		// attacker level for armour; 0 = the defender's own level
		int SourceLevel = 0;
		// a hero's skill numbers are scaled by the hero's Attack unless they already include it
		bool HasSourceStats = false;
		double SourceDamageScale = 1.0;
		bool AttackScaled = false;
		// the attacker's own dealt multiplier (a curse on it)
		double AttackerDealt = 1.0;
		// the attacker's Dauntless element (a plain blow counts as it against armour)
		DElement AttackerElement = DElement::None;
		bool Pure = false;
		bool Dot = false;
	};

	struct Defender
	{
		Team Side = Team::Enemy;
		int Level = 1;
		double Armor = 0.0;
		double ElementalResist = 0.0;
		// per combat element, Unity order (Terra reads the physical slot)
		double Resistances[ResistanceSlots] = {};
		double DamageTakenMultiplier = 1.0;
		double StatusDamageTakenMultiplier = 1.0;
		bool HasGuard = false;
		double Guard = 1.0;
		// the creature's Dauntless element (monsters)
		DElement Element = DElement::None;
		// the armour it wears (heroes)
		bool HasArmour = false;
		ResistPoints ArmourPoints{};
		bool Invulnerable = false;
		bool Dead = false;
	};

	struct DamageOutcome
	{
		double Amount = 0.0;
		// a hostile hit that landed on an invulnerable target (Lướt Hoàn Hảo listens for this)
		bool Evaded = false;
	};

	// Resistance of `d` to `type`, within the tuning's bounds.
	double Resistance(const Defender& d, Element type, const Tuning& t);

	// `spread` is the hit's spread factor (Spread(random01, t); 1 = none).
	DamageOutcome ResolveDamage(const Hit& hit, const Defender& d, const Tuning& t, const ArmourTable& armour, double spread);
}
