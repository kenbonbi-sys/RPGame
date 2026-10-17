// Shared combat basics (the Unity game's Combat.cs, as the Godot port has them): teams, the combat
// elements in the order the data stores them, who may hurt whom, rounding and armour.
#pragma once

#include "Rules/RttTuning.h"

namespace Rtt
{
	enum class Team : int { Player, Enemy, Neutral };

	// Same order as the Unity DamageType enum (the JSON stores these numbers); Terra is the 3D
	// versions' own (Dauntless's Terra), after the Unity ones.
	enum class Element : int { Physical, Fire, Ice, Lightning, Poison, Holy, Dark, Terra };
	constexpr int ElementCount = 8;
	// Resistances are stored for the seven Unity elements; Terra reads the physical slot.
	constexpr int ResistanceSlots = 7;

	const char* ElementName(Element e);

	// A team never hurts itself; neutral things only take hits from heroes.
	bool CanHurt(Team attacker, Team target);

	// Mathf.Round: halves go to the even number (2.5 -> 2, 3.5 -> 4).
	double RoundHalfEven(double x);

	// Share of a hit stopped by armour against an attacker of `attackerLevel` (0 when unarmoured).
	double ArmorReduction(double armor, int attackerLevel, const Tuning& t);

	// The ±spread every hit gets, from a uniform `random01`.
	double Spread(double random01, const Tuning& t);
}
