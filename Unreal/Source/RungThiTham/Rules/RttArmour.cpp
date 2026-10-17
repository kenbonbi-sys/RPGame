#include "Rules/RttArmour.h"

namespace Rtt
{
	void ArmourTable::Load(const JsonValue& data)
	{
		PerPoint = data.Num("perPoint", PerPoint);
		Cap = data.Num("cap", Cap);
		Sets.clear();
		const JsonValue& sets = data["sets"];
		for (size_t i = 0; i < sets.Size(); ++i)
		{
			const JsonValue& s = sets[i];
			ArmourSet set;
			set.Id = s.Str("id");
			set.Name = s.Str("name");
			set.Element = ElementFromId(s.Str("element", "neutral"));
			set.Resist = s.Int("resist", 0);
			const JsonValue& pieces = s["pieces"];
			for (int k = 0; k < SlotCount; ++k)
			{
				set.Pieces[k] = pieces[static_cast<size_t>(k)].Str();
			}
			Sets.push_back(set);
		}
		Wardrobe.clear();
		const JsonValue& wardrobe = data["wardrobe"];
		for (size_t i = 0; i < wardrobe.Size(); ++i)
		{
			Wardrobe.push_back(wardrobe[i].Str());
		}
		Start = data.Str("start", Start);
	}

	const ArmourSet* ArmourTable::Find(const std::string& id) const
	{
		for (const ArmourSet& s : Sets)
		{
			if (s.Id == id)
			{
				return &s;
			}
		}
		return nullptr;
	}

	bool ArmourTable::HasPiece(const std::string& setId, Slot slot) const
	{
		const ArmourSet* s = Find(setId);
		return s && !s->Pieces[static_cast<int>(slot)].empty();
	}

	ArmourPiece ArmourTable::Piece(const std::string& setId, Slot slot) const
	{
		ArmourPiece p;
		const ArmourSet* s = Find(setId);
		if (!s || s->Pieces[static_cast<int>(slot)].empty())
		{
			return p;
		}
		p.Valid = true;
		p.Where = slot;
		p.SetId = s->Id;
		p.Name = s->Pieces[static_cast<int>(slot)];
		p.Element = s->Element;
		p.Resist = s->Resist;
		return p;
	}

	Worn ArmourTable::FullSet(const std::string& setId) const
	{
		Worn worn;
		for (int k = 0; k < SlotCount; ++k)
		{
			if (HasPiece(setId, static_cast<Slot>(k)))
			{
				worn[static_cast<size_t>(k)] = setId;
			}
		}
		return worn;
	}

	ResistPoints ArmourTable::Points(const Worn& worn) const
	{
		ResistPoints out{};
		for (int k = 0; k < SlotCount; ++k)
		{
			const ArmourPiece p = Piece(worn[static_cast<size_t>(k)], static_cast<Slot>(k));
			if (!p.Valid || p.Element == DElement::Neutral || p.Element == DElement::None)
			{
				continue;
			}
			out[static_cast<size_t>(p.Element)] += p.Resist;
			out[static_cast<size_t>(Info(p.Element).Opposite)] -= p.Resist;
		}
		return out;
	}

	double ArmourTable::Multiplier(const ResistPoints& points, DElement element) const
	{
		if (element == DElement::None || element == DElement::Neutral)
		{
			return 1.0;
		}
		double share = points[static_cast<size_t>(element)] * PerPoint;
		if (share > Cap) share = Cap;
		if (share < -Cap) share = -Cap;
		return 1.0 - share;
	}

	std::string ArmourTable::WholeSet(const Worn& worn) const
	{
		std::string id;
		int count = 0;
		for (const std::string& w : worn)
		{
			if (w.empty())
			{
				continue;
			}
			if (id.empty())
			{
				id = w;
			}
			else if (w != id)
			{
				return "";
			}
			++count;
		}
		if (id.empty())
		{
			return "";
		}
		int full = 0;
		for (const std::string& w : FullSet(id))
		{
			full += w.empty() ? 0 : 1;
		}
		return count == full ? id : "";
	}

	DElement ArmourTable::MainElement(const Worn& worn) const
	{
		int count[DElementCount] = {};
		for (int k = 0; k < SlotCount; ++k)
		{
			const ArmourPiece p = Piece(worn[static_cast<size_t>(k)], static_cast<Slot>(k));
			if (p.Valid && p.Element != DElement::Neutral && p.Element != DElement::None)
			{
				++count[static_cast<int>(p.Element)];
			}
		}
		// on a tie, the element of the first slot that wears it (head first)
		DElement best = DElement::Neutral;
		int most = 0;
		for (int k = 0; k < SlotCount; ++k)
		{
			const ArmourPiece p = Piece(worn[static_cast<size_t>(k)], static_cast<Slot>(k));
			if (p.Valid && p.Element != DElement::Neutral && p.Element != DElement::None && count[static_cast<int>(p.Element)] > most)
			{
				most = count[static_cast<int>(p.Element)];
				best = p.Element;
			}
		}
		return best;
	}

	DElement HitElement(Element hitType, DElement attackerElement)
	{
		const DElement e = OfType(hitType);
		if (e != DElement::None)
		{
			return e;
		}
		return attackerElement == DElement::Neutral ? DElement::None : attackerElement;
	}
}
