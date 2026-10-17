// Dauntless armour (as the Godot port's Armour): four pieces, head / torso / arms / legs, each worth
// resistance points of one element; pieces gather into the wiki's 35 sets (Content/Data/
// armour_sets.json). Each worn piece adds its points to its element and takes them from the
// opposing one; every point is PerPoint less (or more) damage of that element, within ±Cap.
#pragma once

#include "Rules/RttElements.h"

#include <array>
#include <string>
#include <vector>

namespace Rtt
{
	enum class Slot : int { Head, Torso, Arms, Legs };
	constexpr int SlotCount = 4;

	struct ArmourSet
	{
		std::string Id;
		std::string Name;
		DElement Element = DElement::Neutral;
		int Resist = 0;
		// "" where the set makes no piece for that slot (Recruit has no helm)
		std::string Pieces[SlotCount];
	};

	struct ArmourPiece
	{
		bool Valid = false;
		Slot Where = Slot::Head;
		std::string SetId;
		std::string Name;
		DElement Element = DElement::Neutral;
		int Resist = 0;
	};

	// What is worn: a set id per slot ("" = nothing).
	using Worn = std::array<std::string, SlotCount>;
	// Net points per Dauntless element (index = DElement).
	using ResistPoints = std::array<int, DElementCount>;

	class ArmourTable
	{
	public:
		double PerPoint = 0.005;
		double Cap = 0.5;
		std::vector<ArmourSet> Sets;
		std::vector<std::string> Wardrobe;
		std::string Start = "krolachi";

		void Load(const JsonValue& data);
		const ArmourSet* Find(const std::string& id) const;
		bool HasPiece(const std::string& setId, Slot slot) const;
		ArmourPiece Piece(const std::string& setId, Slot slot) const;
		// Every piece of a set on its slots, the way wearing the whole set fills them.
		Worn FullSet(const std::string& setId) const;
		ResistPoints Points(const Worn& worn) const;
		// Damage multiplier of a hit of `element` on someone whose armour gives these points.
		double Multiplier(const ResistPoints& points, DElement element) const;
		// The set every worn piece comes from, or "" when mixed (or nothing is worn).
		std::string WholeSet(const Worn& worn) const;
		// The element the worn pieces lean to (the most pieces; Neutral when none).
		DElement MainElement(const Worn& worn) const;
	};

	// The Dauntless element a hit counts as against armour: its own combat element, or, for a plain
	// blow, the attacker's element (a behemoth's blows are of its element). None when neither.
	DElement HitElement(Element hitType, DElement attackerElement);
}
