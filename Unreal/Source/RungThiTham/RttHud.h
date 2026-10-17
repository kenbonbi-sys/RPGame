// The fight's HUD, drawn on the canvas for now (the Dauntless HUD of the Godot port comes with the
// UI stage): the colossus's name, life and stagger bars at the top, the hero's life at the bottom
// left, the heavy move's gathering power, damage numbers where blows land (yellow part damage,
// white-blue core damage, orange while staggered, red on the hero) and the words of the fight.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/HUD.h"

#include "RttHud.generated.h"

class ARttGauMa;
class UFont;

UCLASS()
class RUNGTHITHAM_API ARttHud : public AHUD
{
	GENERATED_BODY()

public:
	virtual void DrawHUD() override;

private:
	float Ghost = 1.f;
	float HeroGhost = 1.f;
	double LastDraw = 0.0;

	void Bar(float X, float Y, float W, float H, float Fill, float GhostFill, const FLinearColor& Colour);
	void Text(const FString& S, float X, float Y, UFont* Font, float Scale, const FLinearColor& Colour, bool bCentre);
	ARttGauMa* Boss() const;
};
