// The hero: run, sprint, jump and Lướt (a quick dash with a short invulnerability) under the
// Shadow of the Colossus camera of the Godot port (sotc_camera.gd): it hangs back behind the hero,
// follows with a little lag so movement has weight, keeps the hero low in the frame, drifts round
// behind the hero while running and widens a little at a sprint. The numbers are the Godot hero's
// (hero.gd, controls.gd) in centimetres; Shift tapped is Lướt, held past DodgeTapTime it sprints.
//
// The body is the Paragon hero named in Config/DefaultGame.ini (Tools/Unreal/ue.cmd hero finds it
// in Content and writes it there) with its own animation blueprint; without one, a stand-in.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "RttHealth.h"
#include "Rules/RttAbility.h"

#include "RttHero.generated.h"

class ARttGauMa;
class UAnimMontage;
class UAnimSequenceBase;
class UCameraComponent;
class UInputAction;
class UInputMappingContext;
class USpringArmComponent;
class UStaticMeshComponent;
struct FInputActionValue;

UCLASS(Config = Game)
class RUNGTHITHAM_API ARttHero : public ACharacter
{
	GENERATED_BODY()

public:
	ARttHero();

	UPROPERTY(EditAnywhere, Category = "Movement")
	float RunSpeed = 540.f;
	UPROPERTY(EditAnywhere, Category = "Movement")
	float SprintSpeed = 820.f;
	UPROPERTY(EditAnywhere, Category = "Movement")
	float DashSpeed = 1000.f;
	UPROPERTY(EditAnywhere, Category = "Movement")
	float DashInvulnerable = 0.28f;
	// Seconds Shift must be held before a tap (Lướt) turns into sprinting.
	UPROPERTY(EditAnywhere, Category = "Movement")
	float DodgeTapTime = 0.22f;

	// sotc_camera.gd, in centimetres and degrees (FovBase is vertical, as Godot's)
	UPROPERTY(EditAnywhere, Category = "Camera")
	float CameraDistance = 440.f;
	// the point the camera orbits, above the hero's feet
	UPROPERTY(EditAnywhere, Category = "Camera")
	float LookHeight = 175.f;
	UPROPERTY(EditAnywhere, Category = "Camera")
	float MinPitch = -72.f;
	UPROPERTY(EditAnywhere, Category = "Camera")
	float MaxPitch = 52.f;
	UPROPERTY(EditAnywhere, Category = "Camera")
	float DefaultPitch = -8.f;
	// higher = tighter follow
	UPROPERTY(EditAnywhere, Category = "Camera")
	float FollowLag = 6.f;
	UPROPERTY(EditAnywhere, Category = "Camera")
	float VerticalLag = 3.5f;
	UPROPERTY(EditAnywhere, Category = "Camera")
	float FovBase = 62.f;
	// seconds without camera input before it drifts round behind the running hero
	UPROPERTY(EditAnywhere, Category = "Camera")
	float RecenterDelay = 1.3f;
	UPROPERTY(EditAnywhere, Category = "Camera")
	float RecenterSpeed = 1.5f;

	// The Paragon hero (Config/DefaultGame.ini): its skeletal mesh, animation blueprint class and
	// the animation Lướt plays; empty = the stand-in body.
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath HeroMesh;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftClassPath HeroAnimClass;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath DodgeAnimation;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	float HeroScale = 1.f;
	// Chém Gió's three swings, the heavy move's swing, being hit and falling (the Paragon hero's)
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	TArray<FSoftObjectPath> AttackMontages;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath HeavyMontage;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath HitReactFront;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath HitReactBack;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath DeathAnimation;

	// the weapon in hand (Content/Data/weapons.json) and the hero's sheet (classes.json, races.json)
	UPROPERTY(EditAnywhere, Category = "Combat")
	FString WeaponItem = TEXT("w_kiem_hong_anh");
	UPROPERTY(EditAnywhere, Category = "Combat")
	FString ClassId = TEXT("fighter");
	UPROPERTY(EditAnywhere, Category = "Combat")
	FString RaceId = TEXT("human");
	// how fast the Paragon swings play (they are slower than Chém Gió's 0.42 s)
	UPROPERTY(EditAnywhere, Category = "Combat")
	float SwingRate = 1.3f;
	// the big sword reaches this much past the data's reach (cm)
	UPROPERTY(EditAnywhere, Category = "Combat")
	float BladeReach = 60.f;

	UPROPERTY(VisibleAnywhere, Category = "Combat")
	TObjectPtr<URttHealth> Health;

	URttHealth* GetHealth() const { return Health; }
	FVector GetFeet() const;
	bool IsDown() const { return Health && Health->bDead; }
	float RespawnIn() const;
	// 0..1 while gathering power for the heavy move, -1 otherwise (the HUD draws it)
	float HeavyCharge() const;
	FString HeavyName() const;
	const Rtt::Sheet* GetSheet() const { return Sheet.Get(); }
	// camera trauma, 0..1
	void Shake(float Amount);

	bool IsInvulnerable() const;
	bool IsSprinting() const { return bSprinting; }
	bool HasParagonBody() const { return bParagonBody; }
	UCameraComponent* GetCamera() const { return Camera; }

protected:
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;
	virtual void SetupPlayerInputComponent(UInputComponent* PlayerInput) override;
	virtual void PawnClientRestart() override;

private:
	UPROPERTY(VisibleAnywhere, Category = "Camera")
	TObjectPtr<USpringArmComponent> Boom;
	UPROPERTY(VisibleAnywhere, Category = "Camera")
	TObjectPtr<UCameraComponent> Camera;
	UPROPERTY(VisibleAnywhere, Category = "Body")
	TObjectPtr<UStaticMeshComponent> Body;
	UPROPERTY(VisibleAnywhere, Category = "Body")
	TObjectPtr<UStaticMeshComponent> Head;

	UPROPERTY(Transient)
	TObjectPtr<UInputMappingContext> Controls;
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> MoveAction;
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> LookAction;
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> JumpAction;
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> DodgeAction;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> Dodge;
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> AttackAction;
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> HeavyAction;
	UPROPERTY(Transient)
	TArray<TObjectPtr<UAnimMontage>> Swings;
	UPROPERTY(Transient)
	TObjectPtr<UAnimMontage> HeavySwing;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> HitFront;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> HitBack;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> Fall;
	UPROPERTY(Transient)
	TObjectPtr<UClass> BodyAnimClass;

	// combat (RttHeroCombat.cpp)
	TUniquePtr<Rtt::Sheet> Sheet;
	TUniquePtr<Rtt::PerfectDodge> Perfect;
	const Rtt::WeaponFamily* Family = nullptr;
	Rtt::Element WeaponType = Rtt::Element::Physical;
	Rtt::ComboState Combo;
	int32 SwingStage = 0;
	double SwingHitAt = -1.0;
	double NextSwingAt = 0.0;
	double SwingBonus = 1.0;
	bool bSwingQueued = false;
	bool bHeavyCharging = false;
	bool bHeavyFull = false;
	double HeavyStart = 0.0;
	double HeavyHitAt = -1.0;
	double HeavyChargeAtRelease = 0.0;
	double KnockedUntil = 0.0;
	double RespawnAt = 0.0;
	FTransform StartXf;
	float Trauma = 0.f;
	double HitStopUntil = 0.0;
	double SlowMoUntil = 0.0;

	double DodgeDownAt = -1.0;
	double InvulnerableUntil = 0.0;
	bool bSprinting = false;
	bool bParagonBody = false;
	// -RttDemoRun: runs on by itself (screenshots of the run); -RttDemoFight: goes for the colossus
	// and swings at it, with a Trảm now and then (screenshots of the fight)
	bool bDemoRun = false;
	bool bDemoFight = false;
	double DemoHeavyAt = 6.0;
	void DemoFight();

	// the camera rig: where it looks, how far back, how wide (vertical degrees)
	FVector Focus = FVector::ZeroVector;
	double LastLookInput = -100.0;
	float ArmLength = 440.f;
	float Fov = 62.f;
	bool bCameraPlaced = false;

	void TakeParagonBody();
	void SetUpCombat();
	bool CanAct() const;
	void AttackPressed();
	void HeavyPressed();
	void HeavyReleased();
	void Swing();
	void LandSwing();
	void LandHeavy();
	void FaceFoe();
	// blows every colossus in reach of the hero's front (cm, degrees); the number struck
	int32 Strike(const FRttBlow& Blow, float ReachCm, float AngleDegrees, float HitStopSeconds);
	void UpdateCombat();
	void OnHurt(const FRttBlow& Blow, float Amount);
	void OnEvaded(const FRttBlow& Blow);
	void OnFall(const FRttBlow& Blow);
	void Rise();
	void HitStop(float Seconds);
	void UpdateTime();
	void BuildControls();
	void Move(const FInputActionValue& Value);
	void Look(const FInputActionValue& Value);
	void DodgePressed();
	void DodgeReleased();
	void Dash();
	void PlaceCamera();
	void UpdateCamera(float RealDelta);
	double Now() const;
	double RealNow() const;
};
