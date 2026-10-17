#include "RttGameMode.h"

#include "Engine/World.h"
#include "HAL/PlatformMisc.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "RttHero.h"
#include "TimerManager.h"
#include "UnrealClient.h"
#if WITH_EDITOR
#include "AssetCompilingManager.h"
#include "ShaderCompiler.h"
#endif

DEFINE_LOG_CATEGORY_STATIC(LogRttShot, Log, All);

namespace
{
	// Shaders and assets still compiling; a screenshot waits for them so nothing is drawn grey.
	int32 CompilingJobs()
	{
#if WITH_EDITOR
		int32 Jobs = FAssetCompilingManager::Get().GetNumRemainingAssets();
		if (GShaderCompilingManager)
		{
			Jobs += GShaderCompilingManager->GetNumRemainingJobs();
		}
		return Jobs;
#else
		return 0;
#endif
	}
}

ARttGameMode::ARttGameMode()
{
	DefaultPawnClass = ARttHero::StaticClass();
}

void ARttGameMode::StartPlay()
{
	Super::StartPlay();
	if (FParse::Value(FCommandLine::Get(), TEXT("RttShot="), ShotPath))
	{
		float Delay = 8.f;
		FParse::Value(FCommandLine::Get(), TEXT("RttShotDelay="), Delay);
		ShotAt = GetWorld()->GetRealTimeSeconds() + Delay;
		GetWorldTimerManager().SetTimer(ShotTimer, this, &ARttGameMode::TickShot, 0.25f, true);
	}
}

void ARttGameMode::TickShot()
{
	const double Now = GetWorld()->GetRealTimeSeconds();
	if (!bShotTaken)
	{
		if (Now < ShotAt || CompilingJobs() > 0)
		{
			return;
		}
		UE_LOG(LogRttShot, Display, TEXT("Screenshot -> %s"), *ShotPath);
		FScreenshotRequest::RequestScreenshot(ShotPath, false, false);
		bShotTaken = true;
		ShotAt = Now + 2.0;
		return;
	}
	if (Now >= ShotAt)
	{
		GetWorldTimerManager().ClearTimer(ShotTimer);
		FPlatformMisc::RequestExit(false);
	}
}
