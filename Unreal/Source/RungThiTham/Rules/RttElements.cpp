#include "Rules/RttElements.h"

namespace Rtt
{
	namespace
	{
		const ElementInfo Infos[DElementCount] = {
			{"", "", Element::Physical, DElement::None},
			{"neutral", "V\xc3\xb4 H\xe1\xbb\x87", Element::Physical, DElement::None},
			{"blaze", "Ho\xe1\xba\xa3", Element::Fire, DElement::Frost},
			{"frost", "B\xc4\x83ng", Element::Ice, DElement::Blaze},
			{"shock", "L\xc3\xb4i", Element::Lightning, DElement::Terra},
			{"terra", "Th\xe1\xbb\x95", Element::Terra, DElement::Shock},
			{"radiant", "Quang", Element::Holy, DElement::Umbral},
			{"umbral", "\xc3\x81m", Element::Dark, DElement::Radiant},
		};
	}

	const ElementInfo& Info(DElement e)
	{
		const int i = static_cast<int>(e);
		return Infos[i >= 0 && i < DElementCount ? i : 0];
	}

	DElement ElementFromId(const std::string& id)
	{
		for (int i = 1; i < DElementCount; ++i)
		{
			if (id == Infos[i].Id)
			{
				return static_cast<DElement>(i);
			}
		}
		return DElement::None;
	}

	DElement OfType(Element type)
	{
		for (int i = 2; i < DElementCount; ++i)
		{
			if (Infos[i].Type == type)
			{
				return static_cast<DElement>(i);
			}
		}
		return DElement::None;
	}

	double Matchup(Element type, DElement target)
	{
		const DElement e = OfType(type);
		if (e == DElement::None || target == DElement::None || target == DElement::Neutral)
		{
			return 1.0;
		}
		if (Info(e).Opposite == target)
		{
			return ElementStrong;
		}
		if (e == target)
		{
			return ElementWeak;
		}
		return 1.0;
	}

	double ProcThreshold(double maxHp)
	{
		const double t = maxHp * ProcShare;
		return t > ProcMin ? t : ProcMin;
	}
}
