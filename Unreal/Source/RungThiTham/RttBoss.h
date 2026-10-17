// What every colossus shows the rest of the game: the HUD reads its name, life and stagger; the
// hero's blows find the closest spot of its body and land there as part damage; screenshots frame
// it. Gấu Ma (procedural, from the Godot sculpt) and the Paragon behemoths both answer to it.
#pragma once

#include "CoreMinimal.h"
#include "RttHealth.h"
#include "UObject/Interface.h"

#include "RttBoss.generated.h"

UINTERFACE(MinimalAPI, meta = (CannotImplementInterfaceInBlueprint))
class URttBoss : public UInterface
{
	GENERATED_BODY()
};

class RUNGTHITHAM_API IRttBoss
{
	GENERATED_BODY()

public:
	virtual URttHealth* GetBossHealth() const = 0;
	virtual FString GetBossName() const = 0;
	virtual bool IsEngaged() const = 0;
	virtual bool IsDefeated() const = 0;
	virtual bool IsRaging() const = 0;
	virtual bool IsStaggered() const = 0;
	virtual float StaggerFraction() const = 0;
	virtual float RespawnIn() const = 0;
	// The closest spot on its body to `Point` and the part it is on (Unreal space).
	virtual FVector ClosestPoint(const FVector& Point, int32& OutPart) const = 0;
	// A hero's blow landing at Blow.Point on `Part` (from ClosestPoint).
	virtual float TakeHeroBlow(FRttBlow Blow, int32 Part) = 0;

	// Every colossus in the world, with its actor.
	static TArray<TPair<AActor*, IRttBoss*>> All(const UObject* WorldContext);
};
