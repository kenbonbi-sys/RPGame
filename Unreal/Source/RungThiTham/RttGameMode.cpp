#include "RttGameMode.h"

#include "Camera/CameraActor.h"
#include "Camera/CameraComponent.h"
#include "Camera/PlayerCameraManager.h"
#include "EngineUtils.h"
#include "RttGauMa.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/World.h"
#include "GameFramework/Character.h"
#include "GameFramework/PlayerController.h"
#include "HAL/PlatformMisc.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "RttHero.h"
#include "RttHud.h"
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
	HUDClass = ARttHud::StaticClass();
}

void ARttGameMode::StartPlay()
{
	Super::StartPlay();
	FParse::Value(FCommandLine::Get(), TEXT("RttShotView="), ShotView);
	if (FParse::Value(FCommandLine::Get(), TEXT("RttShot="), ShotPath))
	{
		float Delay = 8.f;
		FParse::Value(FCommandLine::Get(), TEXT("RttShotDelay="), Delay);
		ShotAt = GetWorld()->GetTimeSeconds() + Delay;
		GetWorldTimerManager().SetTimer(ShotTimer, this, &ARttGameMode::TickShot, 0.25f, true);
	}
}

// -RttShotView=side: the hero stands 13 m in front of the colossus, facing it, and a fixed camera
// looks at the two of them from the side (a steady frame to compare looks by).
void ARttGameMode::SetUpView()
{
	UWorld* World = GetWorld();
	APlayerController* Player = World->GetFirstPlayerController();
	TActorIterator<ARttGauMa> It(World);
	if (!Player || !Player->GetPawn() || !It)
	{
		return;
	}
	const FVector B = It->GetActorLocation();
	const FVector F = It->GetActorForwardVector();
	Player->GetPawn()->SetActorLocationAndRotation(B + F * 1300.0 + FVector(0.0, 0.0, 100.0), (-F).Rotation());
	const FVector Mid = B + F * 700.0 + FVector(0.0, 0.0, 380.0);
	const FVector Side = FVector::CrossProduct(F, FVector::UpVector);
	const FVector At = Mid + Side * 2300.0 + F * 700.0 + FVector(0.0, 0.0, 200.0);
	ACameraActor* Cam = World->SpawnActor<ACameraActor>(At, (Mid - At).Rotation());
	if (Cam)
	{
		Cam->GetCameraComponent()->SetFieldOfView(50.f);
		Cam->GetCameraComponent()->bConstrainAspectRatio = false;
		Player->SetViewTarget(Cam);
	}
}

void ARttGameMode::TickShot()
{
	const double Now = GetWorld()->GetTimeSeconds();
	if (!ShotView.IsEmpty() && !bViewSet && Now >= ShotAt - 2.0)
	{
		bViewSet = true;
		SetUpView();
	}
	if (!bShotTaken)
	{
		if (Now < ShotAt || CompilingJobs() > 0)
		{
			return;
		}
		UE_LOG(LogRttShot, Display, TEXT("Screenshot -> %s after %.1f s of play (%.1f s real, frame %llu)"), *ShotPath, GetWorld()->GetTimeSeconds(), GetWorld()->GetRealTimeSeconds(), static_cast<unsigned long long>(GFrameCounter));
		if (const APlayerController* Player = GetWorld()->GetFirstPlayerController())
		{
			const ACharacter* Hero = Cast<ACharacter>(Player->GetPawn());
			const USkeletalMeshComponent* Skin = Hero ? Hero->GetMesh() : nullptr;
			UE_LOG(LogRttShot, Display, TEXT("Hero at %s facing yaw %.1f, body yaw %.1f, root yaw %.1f, pelvis %s, camera %s"),
				Hero ? *Hero->GetActorLocation().ToString() : TEXT("-"), Hero ? Hero->GetActorRotation().Yaw : 0.0,
				Skin ? Skin->GetComponentRotation().Yaw : 0.0, Skin ? Skin->GetSocketRotation(TEXT("root")).Yaw : 0.0,
				Skin ? *Skin->GetSocketRotation(TEXT("pelvis")).ToString() : TEXT("-"), *Player->PlayerCameraManager->GetCameraRotation().ToString());
		}
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
