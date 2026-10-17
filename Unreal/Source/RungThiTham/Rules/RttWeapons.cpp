#include "Rules/RttWeapons.h"

namespace Rtt
{
	namespace
	{
		HeavyShape ShapeFromId(const std::string& id)
		{
			if (id == "slam") return HeavyShape::Slam;
			if (id == "spin") return HeavyShape::Spin;
			if (id == "lunge") return HeavyShape::Lunge;
			if (id == "shot") return HeavyShape::Shot;
			return HeavyShape::Cone;
		}

		std::vector<WeaponItem> ReadItems(const JsonValue& items)
		{
			std::vector<WeaponItem> out;
			for (size_t i = 0; i < items.keys.size(); ++i)
			{
				const JsonValue& v = items.items[i];
				WeaponItem w;
				w.Id = items.keys[i];
				w.Family = v.Str("family", "sword");
				w.Name = v.Str("name", w.Id);
				w.Element = ElementFromId(v.Str("element", "neutral"));
				if (w.Element == DElement::None)
				{
					w.Element = DElement::Neutral;
				}
				out.push_back(w);
			}
			return out;
		}
	}

	void WeaponTable::Load(const JsonValue& data)
	{
		Families.clear();
		const JsonValue& families = data["families"];
		for (size_t i = 0; i < families.keys.size(); ++i)
		{
			const JsonValue& v = families.items[i];
			WeaponFamily f;
			f.Id = families.keys[i];
			f.Name = v.Str("name", f.Id);
			f.Stat = v.Str("stat", "sword");
			f.Cut = v.Bool("cut", false);
			f.Pierce = v.Bool("pierce", false);
			f.Dual = v.Bool("dual", false);
			f.Stagger = v.Num("stagger", 1.0);
			const JsonValue& h = v["heavy"];
			f.Heavy.Name = h.Str("name", "");
			f.Heavy.Charge = h.Num("charge", 1.0);
			for (size_t k = 0; k < 2; ++k)
			{
				f.Heavy.Power[k] = h["power"][k].Num(f.Heavy.Power[k]);
				f.Heavy.Poise[k] = h["poise"][k].Num(f.Heavy.Poise[k]);
			}
			f.Heavy.Shape = ShapeFromId(h.Str("shape", "cone"));
			f.Heavy.Reach = h.Num("reach", 2.5);
			f.Heavy.Angle = h.Num("angle", 120.0);
			f.Heavy.Hits = h.Int("hits", 1);
			f.Heavy.Lunge = h.Num("lunge", 0.0);
			Families.push_back(f);
		}
		Armory = ReadItems(data["armory"]);
		Props = ReadItems(data["props"]);
		Start.clear();
		const JsonValue& start = data["start"];
		for (size_t i = 0; i < start.Size(); ++i)
		{
			Start.push_back(start[i].Str());
		}
	}

	const WeaponFamily* WeaponTable::FindFamily(const std::string& id) const
	{
		for (const WeaponFamily& f : Families)
		{
			if (f.Id == id)
			{
				return &f;
			}
		}
		return nullptr;
	}

	bool WeaponTable::IsArmory(const std::string& id) const
	{
		for (const WeaponItem& w : Armory)
		{
			if (w.Id == id)
			{
				return true;
			}
		}
		return false;
	}

	const WeaponItem& WeaponTable::Item(const std::string& id) const
	{
		for (const WeaponItem& w : Armory)
		{
			if (w.Id == id)
			{
				return w;
			}
		}
		for (const WeaponItem& w : Props)
		{
			if (w.Id == id)
			{
				return w;
			}
		}
		return Armory.empty() ? Fallback : Armory[0];
	}

	const WeaponFamily& WeaponTable::FamilyOf(const std::string& itemId) const
	{
		const WeaponFamily* f = FindFamily(Item(itemId).Family);
		return f ? *f : NoFamily;
	}

	const WeaponKind& WeaponTable::KindOf(const std::string& itemId) const
	{
		return WeaponKindOf(FamilyOf(itemId).Stat);
	}

	Element WeaponTable::DamageType(const std::string& itemId) const
	{
		return Info(Item(itemId).Element).Type;
	}

	const std::string& WeaponTable::Next(const std::string& itemId) const
	{
		for (size_t i = 0; i < Start.size(); ++i)
		{
			if (Start[i] == itemId)
			{
				return Start[(i + 1) % Start.size()];
			}
		}
		return Start.empty() ? Fallback.Id : Start[0];
	}

	HeavyBlow HeavyBlowAt(const HeavyMove& move, double attack, double dealtMultiplier, double charge)
	{
		const double c = charge < 0.0 ? 0.0 : (charge > 1.0 ? 1.0 : charge);
		HeavyBlow b;
		b.Amount = attack * (move.Power[0] + move.Power[1] * c) * dealtMultiplier;
		b.Poise = move.Poise[0] + move.Poise[1] * c;
		b.Knockback = 6.0 + 4.0 * c;
		b.HitStop = 0.08 + 0.07 * c;
		return b;
	}
}
