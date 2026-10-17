#include "RttRules.h"

#include "Misc/FileHelper.h"
#include "Misc/Paths.h"

DEFINE_LOG_CATEGORY_STATIC(LogRttRules, Log, All);

namespace
{
	bool ReadJson(const FString& Dir, const TCHAR* Name, Rtt::JsonValue& Out, FString& Error)
	{
		const FString Path = Dir / Name;
		TArray<uint8> Bytes;
		if (!FFileHelper::LoadFileToArray(Bytes, *Path))
		{
			Error = FString::Printf(TEXT("%s: cannot read the file"), *Path);
			return false;
		}
		std::string ParseError;
		Out = Rtt::ParseJson(std::string(reinterpret_cast<const char*>(Bytes.GetData()), Bytes.Num()), &ParseError);
		if (!ParseError.empty())
		{
			Error = FString::Printf(TEXT("%s: %s"), *Path, UTF8_TO_TCHAR(ParseError.c_str()));
			return false;
		}
		return true;
	}
}

FString RttDataDir()
{
	return FPaths::ConvertRelativePathToFull(FPaths::ProjectContentDir() / TEXT("Data"));
}

bool RttLoadRules(const FString& Dir, FRttRuleSet& Out, FString& Error)
{
	Rtt::JsonValue Progression;
	Rtt::JsonValue Combat;
	Rtt::JsonValue Armour;
	if (!ReadJson(Dir, TEXT("progression.json"), Progression, Error) || !ReadJson(Dir, TEXT("combat.json"), Combat, Error)
		|| !ReadJson(Dir, TEXT("armour_sets.json"), Armour, Error) || !ReadJson(Dir, TEXT("classes.json"), Out.Classes, Error)
		|| !ReadJson(Dir, TEXT("races.json"), Out.Races, Error) || !ReadJson(Dir, TEXT("abilities.json"), Out.Abilities, Error)
		|| !ReadJson(Dir, TEXT("items.json"), Out.Items, Error))
	{
		return false;
	}
	Out.Tuning.Load(Progression, Combat);
	Out.Armour.Load(Armour);
	return true;
}

void URttRules::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	FString Error;
	bLoaded = RttLoadRules(RttDataDir(), Rules, Error);
	if (bLoaded)
	{
		UE_LOG(LogRttRules, Log, TEXT("Rules loaded: %d armour sets, max level %d"), static_cast<int32>(Rules.Armour.Sets.size()), Rules.Tuning.MaxLevel);
	}
	else
	{
		UE_LOG(LogRttRules, Error, TEXT("The rules cannot load their data: %s"), *Error);
	}
}
