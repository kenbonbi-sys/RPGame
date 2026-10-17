#include "Rules/RttCombat.h"

#include <cmath>

namespace Rtt
{
	const char* ElementName(Element e)
	{
		// UTF-8, as escapes (a following hex letter is split off into its own literal: "\xad" "a")
		static const char* Names[ElementCount] = {"V\xe1\xba\xadt l\xc3\xbd", "L\xe1\xbb\xad" "a", "B\xc4\x83ng", "L\xc3\xb4i", "\xc4\x90\xe1\xbb\x99\x63",
			"Th\xc3\xa1nh M\xe1\xbb\x99\x63", "\xc3\x81m", "Th\xe1\xbb\x95"};
		const int i = static_cast<int>(e);
		return i >= 0 && i < ElementCount ? Names[i] : "";
	}

	bool CanHurt(Team attacker, Team target)
	{
		if (attacker == target)
		{
			return false;
		}
		if (target == Team::Neutral)
		{
			return attacker == Team::Player;
		}
		return true;
	}

	double RoundHalfEven(double x)
	{
		const double f = std::floor(x);
		const double diff = x - f;
		if (diff > 0.5)
		{
			return f + 1.0;
		}
		if (diff < 0.5)
		{
			return f;
		}
		return std::fmod(f, 2.0) == 0.0 ? f : f + 1.0;
	}

	double ArmorReduction(double armor, int attackerLevel, const Tuning& t)
	{
		if (armor <= 0.0)
		{
			return 0.0;
		}
		const int level = attackerLevel < 1 ? 1 : attackerLevel;
		return armor / (armor + t.ArmorConstant + t.ArmorPerAttackerLevel * level);
	}

	double Spread(double random01, const Tuning& t)
	{
		return 1.0 + (random01 * 2.0 - 1.0) * t.DamageSpread;
	}
}
