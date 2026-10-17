#include "RttBoss.h"

#include "Engine/World.h"
#include "EngineUtils.h"

TArray<TPair<AActor*, IRttBoss*>> IRttBoss::All(const UObject* WorldContext)
{
	TArray<TPair<AActor*, IRttBoss*>> Out;
	UWorld* World = WorldContext ? WorldContext->GetWorld() : nullptr;
	if (!World)
	{
		return Out;
	}
	for (TActorIterator<AActor> It(World); It; ++It)
	{
		if (IRttBoss* Boss = Cast<IRttBoss>(*It))
		{
			Out.Add(TPair<AActor*, IRttBoss*>(*It, Boss));
		}
	}
	return Out;
}
