// The game mode: brings the hero. With -RttShot=<file.png> on the command line it also takes a
// screenshot once the world has settled (no shaders or assets left compiling, -RttShotDelay seconds
// of play, 8 by default) and quits: Tools/Unreal/ue.cmd shot, the way the Godot port's screenshots worked.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"

#include "RttGameMode.generated.h"

UCLASS()
class RUNGTHITHAM_API ARttGameMode : public AGameModeBase
{
	GENERATED_BODY()

public:
	ARttGameMode();
	virtual void StartPlay() override;

private:
	FTimerHandle ShotTimer;
	FString ShotPath;
	double ShotAt = 0.0;
	bool bShotTaken = false;

	void TickShot();
};
