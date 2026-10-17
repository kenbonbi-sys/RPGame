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

#include "RttHero.generated.h"

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

	double DodgeDownAt = -1.0;
	double InvulnerableUntil = 0.0;
	bool bSprinting = false;
	bool bParagonBody = false;
	// -RttDemoRun: runs on by itself (screenshots of the run)
	bool bDemoRun = false;

	// the camera rig: where it looks, how far back, how wide (vertical degrees)
	FVector Focus = FVector::ZeroVector;
	double LastLookInput = -100.0;
	float ArmLength = 440.f;
	float Fov = 62.f;
	bool bCameraPlaced = false;

	void TakeParagonBody();
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
