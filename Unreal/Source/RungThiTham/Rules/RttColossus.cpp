#include "Rules/RttColossus.h"

#include <cmath>

namespace Rtt
{
	namespace
	{
		constexpr double Pi = 3.14159265358979323846;

		double Lerp(double a, double b, double t)
		{
			return a + (b - a) * t;
		}

		double Clamp(double v, double lo, double hi)
		{
			return v < lo ? lo : (v > hi ? hi : v);
		}

		double Down(double v, double dt)
		{
			return v - dt > 0.0 ? v - dt : 0.0;
		}
	}

	double Beats::Windup(double t, double dip) const
	{
		if (t <= 0.0)
		{
			return 0.0;
		}
		if (t < Tell)
		{
			return -dip * std::sin(t / Tell * Pi * 0.5);
		}
		if (t < Tell + Wind)
		{
			const double k = (t - Tell) / Wind;
			return Lerp(-dip, 1.0, k * k * (3.0 - 2.0 * k));
		}
		return 1.0;
	}

	double Beats::Tremble(double t) const
	{
		if (t < HoldAt() || t >= StrikeAt())
		{
			return 0.0;
		}
		return std::sin(t * 75.0);
	}

	double Beats::Strike(double t, double seconds) const
	{
		const double k = Clamp((t - StrikeAt()) / (seconds > 0.001 ? seconds : 0.001), 0.0, 1.0);
		return 1.0 - std::pow(1.0 - k, 3.0);
	}

	void BearCooldowns::Tick(double dt)
	{
		Swipe = Down(Swipe, dt);
		Stomp = Down(Stomp, dt);
		Throw = Down(Throw, dt);
		Pounce = Down(Pounce, dt);
		Charge = Down(Charge, dt);
	}

	BearMove ChooseBearMove(double d, double front, bool rage, BearCooldowns& cd, bool canPounce, bool canThrow)
	{
		const double r = BearReach;
		const double k = rage ? 0.7 : 1.0;
		if (d < 15.0 * r && front > 0.45 && cd.Swipe <= 0.0)
		{
			cd.Swipe = 3.6 * k;
			return BearMove::Swipe;
		}
		if (d < 17.0 * r && front > 0.0 && cd.Stomp <= 0.0)
		{
			cd.Stomp = 6.5 * k;
			return BearMove::Stomp;
		}
		if (d > 14.0 * r && d < 46.0 * r && cd.Charge <= 0.0 && front > 0.75)
		{
			cd.Charge = 11.0 * k;
			return BearMove::Charge;
		}
		if (canPounce && d > 17.0 * r && d < 42.0 * r && cd.Pounce <= 0.0 && front > 0.6)
		{
			cd.Pounce = 13.0 * k;
			return BearMove::Pounce;
		}
		if (canThrow && d > 26.0 * r && cd.Throw <= 0.0 && front > 0.7)
		{
			cd.Throw = 7.5 * k;
			return BearMove::Throw;
		}
		return BearMove::Walk;
	}

	double PartMultiplier(bool cutting, bool piercing)
	{
		return cutting ? 1.5 : (piercing ? 1.25 : 1.0);
	}

	bool DamagePart(BossPart& part, double amount, bool cutting, bool piercing, bool dot)
	{
		if (!part.Breakable() || part.Broken || dot || amount <= 0.0)
		{
			return false;
		}
		part.Hp = part.Hp - amount * PartMultiplier(cutting, piercing);
		if (part.Hp > 0.0)
		{
			return false;
		}
		part.Hp = 0.0;
		part.Broken = true;
		return true;
	}

	double AreaFalloff(double amount, double distance, double radius)
	{
		return radius > 0.0 ? amount * (1.0 - 0.4 * Clamp(distance / radius, 0.0, 1.0)) : amount;
	}

	std::vector<BossPart> BearStats::Parts()
	{
		// Vietnamese names as UTF-8: Đầu, Cổ, Thân, Chân trước trái/phải, Chân sau trái/phải
		std::vector<BossPart> parts;
		parts.push_back({"head", "\xc4\x90\xe1\xba\xa7u", 640.0, 640.0, false, 2.0, true});
		parts.push_back({"neck", "C\xe1\xbb\x95", 0.0, 0.0, false, 1.5, true});
		parts.push_back({"body", "Th\xc3\xa2n", 0.0, 0.0, false, 1.0, false});
		parts.push_back({"fl", "Ch\xc3\xa2n tr\xc6\xb0\xe1\xbb\x9b\x63 tr\xc3\xa1i", 400.0, 400.0, false, 1.2, false});
		parts.push_back({"fr", "Ch\xc3\xa2n tr\xc6\xb0\xe1\xbb\x9b\x63 ph\xe1\xba\xa3i", 400.0, 400.0, false, 1.2, false});
		parts.push_back({"hl", "Ch\xc3\xa2n sau tr\xc3\xa1i", 400.0, 400.0, false, 1.2, false});
		parts.push_back({"hr", "Ch\xc3\xa2n sau ph\xe1\xba\xa3i", 400.0, 400.0, false, 1.2, false});
		return parts;
	}
}
