#include "Rules/RttProcs.h"

namespace Rtt
{
	Proc ProcMeter::Fill(Element type, double amount, double maxHp, DElement own, double now)
	{
		Proc out;
		const DElement e = OfType(type);
		if (e == DElement::None || e == own)
		{
			return out;
		}
		const int i = static_cast<int>(e);
		if (now < ReadyAt[i])
		{
			return out;
		}
		const double threshold = ProcThreshold(maxHp);
		const double meter = Meters[i] + amount;
		if (meter < threshold)
		{
			Meters[i] = meter;
			return out;
		}
		Meters[i] = 0.0;
		ReadyAt[i] = now + ProcCooldown;
		out.Element = e;
		out.Threshold = threshold;
		return out;
	}

	void ProcMeter::Reset()
	{
		for (int i = 0; i < DElementCount; ++i)
		{
			Meters[i] = 0.0;
			ReadyAt[i] = 0.0;
		}
	}

	bool ApplyProc(const Proc& proc, Team source, StatusEffects& status, double now)
	{
		switch (proc.Element)
		{
		case DElement::Blaze:
			status.Burn(ProcBurnStacks, proc.Threshold * ProcBurnShare, source, now);
			return true;
		case DElement::Frost:
			status.Slow(ProcSlow, ProcSlowSeconds, now);
			return true;
		case DElement::Shock:
			status.ForceStun(ProcStunSeconds, now);
			return true;
		default:
			return false;
		}
	}
}
