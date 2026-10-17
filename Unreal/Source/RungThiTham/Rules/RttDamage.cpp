#include "Rules/RttDamage.h"

namespace Rtt
{
	double Resistance(const Defender& d, Element type, const Tuning& t)
	{
		const int i = static_cast<int>(type);
		double r = d.Resistances[i >= 0 && i < ResistanceSlots ? i : 0];
		if (type != Element::Physical)
		{
			r += d.ElementalResist;
		}
		if (r < t.ResistMin) r = t.ResistMin;
		if (r > t.ResistMax) r = t.ResistMax;
		return r;
	}

	DamageOutcome ResolveDamage(const Hit& hit, const Defender& d, const Tuning& t, const ArmourTable& armour, double spread)
	{
		DamageOutcome out;
		if (d.Dead || !CanHurt(hit.SourceTeam, d.Side))
		{
			return out;
		}
		if (d.Invulnerable)
		{
			out.Evaded = true;
			return out;
		}
		double dealt = hit.Amount;
		if (hit.Pure)
		{
			const double amount = RoundHalfEven(dealt);
			out.Amount = amount < 1.0 ? 1.0 : amount;
			return out;
		}
		if (hit.HasSourceStats && !hit.AttackScaled)
		{
			dealt *= hit.SourceDamageScale;
		}
		dealt *= hit.AttackerDealt;
		double raw = dealt * d.DamageTakenMultiplier * d.StatusDamageTakenMultiplier;
		if (d.HasGuard && !hit.Dot)
		{
			raw *= d.Guard > 0.0 ? d.Guard : 0.0;
		}
		const int attackerLevel = hit.SourceLevel > 0 ? hit.SourceLevel : d.Level;
		raw = raw * (1.0 - ArmorReduction(d.Armor, attackerLevel, t)) * (1.0 - Resistance(d, hit.Type, t)) * spread;
		raw *= Matchup(hit.Type, d.Element);
		if (d.HasArmour)
		{
			raw *= armour.Multiplier(d.ArmourPoints, HitElement(hit.Type, hit.AttackerElement));
		}
		const double amount = RoundHalfEven(raw);
		out.Amount = amount < 1.0 ? 1.0 : amount;
		return out;
	}
}
