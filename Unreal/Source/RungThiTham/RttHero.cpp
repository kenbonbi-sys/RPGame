#include "RttHero.h"

#include "Animation/AnimInstance.h"
#include "Animation/AnimSequenceBase.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/SkeletalMesh.h"
#include "Misc/App.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "Engine/LocalPlayer.h"
#include "Engine/StaticMesh.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/SpringArmComponent.h"
#include "InputAction.h"
#include "InputActionValue.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"
#include "UObject/ConstructorHelpers.h"

ARttHero::ARttHero()
{
	PrimaryActorTick.bCanEverTick = true;
	GetCapsuleComponent()->InitCapsuleSize(34.f, 88.f);

	// The hero turns to where it runs; the mouse turns the camera, not the hero.
	bUseControllerRotationYaw = false;
	bUseControllerRotationPitch = false;
	bUseControllerRotationRoll = false;
	UCharacterMovementComponent* Moves = GetCharacterMovement();
	Moves->bOrientRotationToMovement = true;
	Moves->RotationRate = FRotator(0.f, 720.f, 0.f);
	Moves->MaxWalkSpeed = RunSpeed;
	Moves->MaxAcceleration = 3000.f;
	Moves->BrakingDecelerationWalking = 3000.f;
	// hero.gd: jump 7.4 m/s against a gravity of 21 m/s²
	Moves->JumpZVelocity = 740.f;
	Moves->GravityScale = 2100.f / 980.f;
	Moves->AirControl = 0.35f;

	// The camera rig places the arm itself every frame (UpdateCamera): it hangs from a focus point
	// that lags behind the hero, so it is not carried rigidly by the capsule. The arm still pulls
	// in against walls (a 30 cm probe, as Godot's 0.3 m sphere).
	Boom = CreateDefaultSubobject<USpringArmComponent>(TEXT("Boom"));
	Boom->SetupAttachment(RootComponent);
	Boom->SetUsingAbsoluteLocation(true);
	Boom->SetUsingAbsoluteRotation(true);
	Boom->TargetArmLength = CameraDistance;
	Boom->bUsePawnControlRotation = false;
	Boom->bEnableCameraLag = false;
	Boom->bEnableCameraRotationLag = false;
	Boom->bDoCollisionTest = true;
	Boom->ProbeSize = 30.f;
	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Camera"));
	Camera->SetupAttachment(Boom, USpringArmComponent::SocketName);
	Camera->bUsePawnControlRotation = false;
	Camera->FieldOfView = 90.f;

	// A stand-in body: a 1.5 m column and a head, until the Paragon hero (U1).
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cylinder(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Sphere(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	Body = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Body"));
	Body->SetupAttachment(RootComponent);
	Body->SetStaticMesh(Cylinder.Object);
	Body->SetRelativeLocation(FVector(0.f, 0.f, -13.f));
	Body->SetRelativeScale3D(FVector(0.6f, 0.6f, 1.5f));
	Body->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Head = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Head"));
	Head->SetupAttachment(RootComponent);
	Head->SetStaticMesh(Sphere.Object);
	Head->SetRelativeLocation(FVector(6.f, 0.f, 72.f));
	Head->SetRelativeScale3D(FVector(0.45f));
	Head->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

double ARttHero::Now() const
{
	return GetWorld() ? GetWorld()->GetTimeSeconds() : 0.0;
}

double ARttHero::RealNow() const
{
	return GetWorld() ? GetWorld()->GetRealTimeSeconds() : 0.0;
}

void ARttHero::BeginPlay()
{
	Super::BeginPlay();
	TakeParagonBody();
	bDemoRun = FParse::Param(FCommandLine::Get(), TEXT("RttDemoRun"));
}

void ARttHero::TakeParagonBody()
{
	USkeletalMesh* Asset = HeroMesh.IsNull() ? nullptr : Cast<USkeletalMesh>(HeroMesh.TryLoad());
	if (!Asset)
	{
		if (!HeroMesh.IsNull())
		{
			UE_LOG(LogTemp, Warning, TEXT("The hero's mesh %s is missing: the stand-in body stays"), *HeroMesh.ToString());
		}
		return;
	}
	USkeletalMeshComponent* Skin = GetMesh();
	Skin->SetSkeletalMeshAsset(Asset);
	if (UClass* Anim = HeroAnimClass.IsNull() ? nullptr : HeroAnimClass.TryLoadClass<UAnimInstance>())
	{
		Skin->SetAnimInstanceClass(Anim);
	}
	// Paragon heroes stand on their origin facing +Y: put the feet on the capsule's floor, face +X
	Skin->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -GetCapsuleComponent()->GetUnscaledCapsuleHalfHeight()), FRotator(0.f, -90.f, 0.f));
	Skin->SetRelativeScale3D(FVector(HeroScale));
	Dodge = DodgeAnimation.IsNull() ? nullptr : Cast<UAnimSequenceBase>(DodgeAnimation.TryLoad());
	Body->SetVisibility(false);
	Head->SetVisibility(false);
	bParagonBody = true;
}

bool ARttHero::IsInvulnerable() const
{
	return Now() < InvulnerableUntil;
}

void ARttHero::BuildControls()
{
	if (Controls)
	{
		return;
	}
	Controls = NewObject<UInputMappingContext>(this, TEXT("Controls"));
	auto MakeAction = [this](const TCHAR* Name, EInputActionValueType Type)
	{
		UInputAction* Action = NewObject<UInputAction>(this, Name);
		Action->ValueType = Type;
		return Action;
	};
	MoveAction = MakeAction(TEXT("Move"), EInputActionValueType::Axis2D);
	LookAction = MakeAction(TEXT("Look"), EInputActionValueType::Axis2D);
	JumpAction = MakeAction(TEXT("Jump"), EInputActionValueType::Boolean);
	DodgeAction = MakeAction(TEXT("Dodge"), EInputActionValueType::Boolean);

	// W/S drive Y and A/D drive X of the 2D move, as in Unreal's own third-person template.
	auto Map = [this](UInputAction* Action, const FKey& Key, bool bNegate, bool bSwizzle, bool bNegateYOnly = false)
	{
		FEnhancedActionKeyMapping& Mapping = Controls->MapKey(Action, Key);
		if (bSwizzle)
		{
			Mapping.Modifiers.Add(NewObject<UInputModifierSwizzleAxis>(Controls));
		}
		if (bNegate || bNegateYOnly)
		{
			UInputModifierNegate* Negate = NewObject<UInputModifierNegate>(Controls);
			if (bNegateYOnly)
			{
				Negate->bX = false;
				Negate->bZ = false;
			}
			Mapping.Modifiers.Add(Negate);
		}
		return &Mapping;
	};
	Map(MoveAction, EKeys::W, false, true);
	Map(MoveAction, EKeys::Up, false, true);
	Map(MoveAction, EKeys::S, true, true);
	Map(MoveAction, EKeys::Down, true, true);
	Map(MoveAction, EKeys::D, false, false);
	Map(MoveAction, EKeys::Right, false, false);
	Map(MoveAction, EKeys::A, true, false);
	Map(MoveAction, EKeys::Left, true, false);
	Map(MoveAction, EKeys::Gamepad_Left2D, false, false)->Modifiers.Add(NewObject<UInputModifierDeadZone>(Controls));
	Map(LookAction, EKeys::Mouse2D, false, false, true);
	Map(LookAction, EKeys::Gamepad_Right2D, false, false, true)->Modifiers.Add(NewObject<UInputModifierDeadZone>(Controls));
	Map(JumpAction, EKeys::SpaceBar, false, false);
	Map(JumpAction, EKeys::Gamepad_FaceButton_Bottom, false, false);
	Map(DodgeAction, EKeys::LeftShift, false, false);
	Map(DodgeAction, EKeys::Gamepad_FaceButton_Right, false, false);
}

void ARttHero::SetupPlayerInputComponent(UInputComponent* PlayerInput)
{
	Super::SetupPlayerInputComponent(PlayerInput);
	BuildControls();
	UEnhancedInputComponent* Input = Cast<UEnhancedInputComponent>(PlayerInput);
	if (!Input)
	{
		UE_LOG(LogTemp, Error, TEXT("The hero needs Enhanced Input (Config/DefaultInput.ini)"));
		return;
	}
	Input->BindAction(MoveAction, ETriggerEvent::Triggered, this, &ARttHero::Move);
	Input->BindAction(LookAction, ETriggerEvent::Triggered, this, &ARttHero::Look);
	Input->BindAction(JumpAction, ETriggerEvent::Started, this, &ACharacter::Jump);
	Input->BindAction(JumpAction, ETriggerEvent::Completed, this, &ACharacter::StopJumping);
	Input->BindAction(DodgeAction, ETriggerEvent::Started, this, &ARttHero::DodgePressed);
	Input->BindAction(DodgeAction, ETriggerEvent::Completed, this, &ARttHero::DodgeReleased);
}

void ARttHero::PawnClientRestart()
{
	Super::PawnClientRestart();
	BuildControls();
	const APlayerController* Player = Cast<APlayerController>(GetController());
	const ULocalPlayer* Local = Player ? Player->GetLocalPlayer() : nullptr;
	if (UEnhancedInputLocalPlayerSubsystem* Inputs = Local ? Local->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>() : nullptr)
	{
		Inputs->ClearAllMappings();
		Inputs->AddMappingContext(Controls, 0);
	}
	PlaceCamera();
}

void ARttHero::Move(const FInputActionValue& Value)
{
	const FVector2D Axis = Value.Get<FVector2D>();
	const FRotator Yaw(0.f, GetControlRotation().Yaw, 0.f);
	const FRotationMatrix Facing(Yaw);
	AddMovementInput(Facing.GetUnitAxis(EAxis::X), Axis.Y);
	AddMovementInput(Facing.GetUnitAxis(EAxis::Y), Axis.X);
}

void ARttHero::Look(const FInputActionValue& Value)
{
	const FVector2D Axis = Value.Get<FVector2D>();
	AddControllerYawInput(Axis.X);
	AddControllerPitchInput(Axis.Y);
	if (!Axis.IsNearlyZero())
	{
		LastLookInput = RealNow();
	}
}

void ARttHero::DodgePressed()
{
	DodgeDownAt = Now();
}

void ARttHero::DodgeReleased()
{
	if (!bSprinting && DodgeDownAt >= 0.0)
	{
		Dash();
	}
	DodgeDownAt = -1.0;
	bSprinting = false;
	GetCharacterMovement()->MaxWalkSpeed = RunSpeed;
}

void ARttHero::Dash()
{
	if (GetCharacterMovement()->IsFalling())
	{
		return;
	}
	FVector Direction = GetLastMovementInputVector();
	Direction.Z = 0.0;
	if (Direction.IsNearlyZero())
	{
		Direction = GetActorForwardVector();
	}
	Direction = Direction.GetSafeNormal2D();
	SetActorRotation(Direction.Rotation());
	LaunchCharacter(Direction * DashSpeed, true, false);
	InvulnerableUntil = Now() + DashInvulnerable;
	UAnimInstance* Anim = bParagonBody ? GetMesh()->GetAnimInstance() : nullptr;
	if (Anim && Dodge)
	{
		Anim->PlaySlotAnimationAsDynamicMontage(Dodge, TEXT("DefaultSlot"), 0.05f, 0.15f, 1.2f);
	}
}

void ARttHero::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (DodgeDownAt >= 0.0 && !bSprinting && Now() - DodgeDownAt >= DodgeTapTime)
	{
		bSprinting = true;
		GetCharacterMovement()->MaxWalkSpeed = SprintSpeed;
	}
	if (bDemoRun)
	{
		AddMovementInput(GetActorForwardVector(), 1.f);
	}
	// the camera runs on real time so hit-stop and slow motion do not freeze it
	UpdateCamera(FMath::Min(static_cast<float>(FApp::GetDeltaTime()), 0.1f));
}

void ARttHero::PlaceCamera()
{
	if (AController* Control = GetController())
	{
		Control->SetControlRotation(FRotator(DefaultPitch, GetActorRotation().Yaw, 0.f));
	}
	Focus = GetActorLocation() + FVector(0.f, 0.f, LookHeight - GetCapsuleComponent()->GetUnscaledCapsuleHalfHeight());
	ArmLength = CameraDistance;
	Fov = FovBase;
	bCameraPlaced = true;
	UpdateCamera(0.f);
}

void ARttHero::UpdateCamera(float RealDelta)
{
	AController* Control = GetController();
	if (!Control || !bCameraPlaced)
	{
		return;
	}
	const float Dt = RealDelta;
	FRotator View = Control->GetControlRotation();
	View.Pitch = FMath::Clamp(FRotator::NormalizeAxis(View.Pitch), MinPitch, MaxPitch);
	View.Roll = 0.f;

	// drift round behind the running hero once the camera has been left alone
	const FVector Flat(GetVelocity().X, GetVelocity().Y, 0.f);
	const float Speed = Flat.Size();
	if (RealNow() - LastLookInput > RecenterDelay && Speed >= 200.f)
	{
		const float Turn = FRotator::NormalizeAxis(Flat.Rotation().Yaw - View.Yaw);
		// running toward the camera: leave it be rather than whip around
		if (FMath::Abs(Turn) <= 125.f)
		{
			const float K = 1.f - FMath::Exp(-RecenterSpeed * FMath::SmoothStep(200.f, 600.f, Speed) * Dt);
			View.Yaw += Turn * K;
			View.Pitch = FMath::Lerp(View.Pitch, DefaultPitch, K * 0.5f);
		}
	}
	Control->SetControlRotation(View);

	// follow with a little lag, the height slower; never let the hero slip far out of the frame
	const FVector Goal = GetActorLocation() + FVector(0.f, 0.f, LookHeight - GetCapsuleComponent()->GetUnscaledCapsuleHalfHeight());
	const float K = 1.f - FMath::Exp(-FollowLag * Dt);
	const float KV = 1.f - FMath::Exp(-VerticalLag * Dt);
	Focus.X = FMath::Lerp(Focus.X, Goal.X, K);
	Focus.Y = FMath::Lerp(Focus.Y, Goal.Y, K);
	Focus.Z = FMath::Lerp(Focus.Z, Goal.Z, KV);
	const FVector Slip = Focus - Goal;
	if (Slip.Size() > 160.f)
	{
		Focus = Goal + Slip.GetSafeNormal() * 160.f;
	}

	// a little wider at a sprint
	const float WantFov = FovBase + FMath::SmoothStep(600.f, 850.f, Speed) * 6.f;
	ArmLength = FMath::Lerp(ArmLength, CameraDistance, 1.f - FMath::Exp(-2.5f * Dt));
	Fov = FMath::Lerp(Fov, WantFov, 1.f - FMath::Exp(-3.f * Dt));
	Boom->SetWorldLocationAndRotation(Focus, View);
	Boom->TargetArmLength = ArmLength;

	// Godot's field of view is vertical; Unreal's is horizontal
	float Aspect = 16.f / 9.f;
	if (const APlayerController* Player = Cast<APlayerController>(Control))
	{
		int32 W = 0;
		int32 H = 0;
		Player->GetViewportSize(W, H);
		if (W > 0 && H > 0)
		{
			Aspect = static_cast<float>(W) / static_cast<float>(H);
		}
	}
	Camera->SetFieldOfView(FMath::RadiansToDegrees(2.f * FMath::Atan(FMath::Tan(FMath::DegreesToRadians(Fov) * 0.5f) * Aspect)));
}
