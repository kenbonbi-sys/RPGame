// What the fight says on screen: damage numbers floating up from where a blow landed, and words
// across the middle (a monster's move, a break, a stagger). The HUD draws and expires them.
#pragma once

#include "CoreMinimal.h"
#include "Subsystems/WorldSubsystem.h"

#include "RttFeed.generated.h"

UENUM()
enum class ERttNumber : uint8
{
	// white-blue: damage to a colossus's life
	Core,
	// yellow: part damage on a part not yet broken
	Part,
	// orange: damage while staggered
	Staggered,
	// red: damage to a hero
	Hero,
	// gold: a crit or a heavy blow
	Crit,
	Heal,
};

UCLASS()
class RUNGTHITHAM_API URttFeed : public UWorldSubsystem
{
	GENERATED_BODY()

public:
	struct FNumber
	{
		FVector At = FVector::ZeroVector;
		FString Text;
		ERttNumber Kind = ERttNumber::Core;
		double Born = 0.0;
		bool bBig = false;
	};

	struct FWord
	{
		FString Text;
		FLinearColor Color = FLinearColor::White;
		double Born = 0.0;
		float Seconds = 1.6f;
	};

	TArray<FNumber> Numbers;
	TArray<FWord> Words;

	static URttFeed* Get(const UObject* WorldContext);
	void Number(const FVector& At, float Amount, ERttNumber Kind, bool bBig = false);
	void Word(const FString& Text, const FLinearColor& Color = FLinearColor(1.f, 0.86f, 0.55f), float Seconds = 1.6f);
	double Now() const;
	// Forgets what has faded out.
	void Expire();
};
