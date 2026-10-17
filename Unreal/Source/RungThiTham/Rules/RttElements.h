// Dauntless elements on weapons, armour and monsters (as the Godot port's Elements). Blaze <-> Frost,
// Shock <-> Terra and Radiant <-> Umbral oppose each other: a hit of the opposing element is +50%,
// one of the target's own element −20%. They ride on the combat elements (Blaze is Lửa, Frost Băng,
// Shock Lôi, Terra Thổ, Radiant Thánh Mộc, Umbral Ám); physical and poison hits have none.
#pragma once

#include "Rules/RttCombat.h"

#include <string>

namespace Rtt
{
	enum class DElement : int { None, Neutral, Blaze, Frost, Shock, Terra, Radiant, Umbral };
	constexpr int DElementCount = 8;

	constexpr double ElementStrong = 1.5;
	constexpr double ElementWeak = 0.8;
	// A weapon's hits fill a target's meter; it procs at this share of its life (at least ProcMin).
	constexpr double ProcShare = 0.12;
	constexpr double ProcMin = 30.0;
	constexpr double ProcCooldown = 6.0;

	struct ElementInfo
	{
		const char* Id;      // "blaze"
		const char* Name;    // "Hoả" (UTF-8)
		Element Type;        // the combat element it rides on
		DElement Opposite;
	};

	const ElementInfo& Info(DElement e);
	// "blaze" -> Blaze; "" or unknown -> None.
	DElement ElementFromId(const std::string& id);
	// The Dauntless element a combat element belongs to (None for physical and poison).
	DElement OfType(Element type);
	// Damage multiplier of a hit of combat element `type` on a target of element `target`.
	double Matchup(Element type, DElement target);
	// The meter a target's element needs before a weapon's element procs on it.
	double ProcThreshold(double maxHp);
}
