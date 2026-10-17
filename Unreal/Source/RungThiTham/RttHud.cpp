#include "RttHud.h"

#include "Engine/Canvas.h"
#include "Engine/Engine.h"
#include "Engine/Font.h"
#include "EngineUtils.h"
#include "GameFramework/PlayerController.h"
#include "RttFeed.h"
#include "RttBoss.h"
#include "RttHero.h"
#include "RttText.h"

namespace
{
	FLinearColor NumberColour(ERttNumber Kind)
	{
		switch (Kind)
		{
		case ERttNumber::Part: return FLinearColor(1.f, 0.82f, 0.3f);
		case ERttNumber::Staggered: return FLinearColor(1.f, 0.55f, 0.15f);
		case ERttNumber::Hero: return FLinearColor(1.f, 0.3f, 0.25f);
		case ERttNumber::Crit: return FLinearColor(1.f, 0.95f, 0.6f);
		case ERttNumber::Heal: return FLinearColor(0.5f, 1.f, 0.5f);
		default: return FLinearColor(0.8f, 0.9f, 1.f);
		}
	}
}

IRttBoss* ARttHud::Boss() const
{
	IRttBoss* Best = nullptr;
	for (const TPair<AActor*, IRttBoss*>& Each : IRttBoss::All(this))
	{
		IRttBoss* B = Each.Value;
		if (B->IsEngaged() || B->IsDefeated() || B->IsStaggered())
		{
			return B;
		}
		Best = B;
	}
	return Best && Best->IsEngaged() ? Best : nullptr;
}

void ARttHud::Bar(float X, float Y, float W, float H, float Fill, float GhostFill, const FLinearColor& Colour)
{
	DrawRect(FLinearColor(0.f, 0.f, 0.f, 0.55f), X - 2.f, Y - 2.f, W + 4.f, H + 4.f);
	DrawRect(FLinearColor(1.f, 0.95f, 0.85f, 0.35f), X, Y, W * FMath::Clamp(GhostFill, 0.f, 1.f), H);
	DrawRect(Colour, X, Y, W * FMath::Clamp(Fill, 0.f, 1.f), H);
}

void ARttHud::Text(const FString& S, float X, float Y, UFont* Font, float Scale, const FLinearColor& Colour, bool bCentre)
{
	float W = 0.f;
	float H = 0.f;
	GetTextSize(S, W, H, Font, Scale);
	const float Left = bCentre ? X - W * 0.5f : X;
	DrawText(S, FLinearColor(0.f, 0.f, 0.f, Colour.A * 0.7f), Left + 2.f * Scale, Y + 2.f * Scale, Font, Scale);
	DrawText(S, Colour, Left, Y, Font, Scale);
}

void ARttHud::DrawHUD()
{
	Super::DrawHUD();
	if (!Canvas || !GEngine)
	{
		return;
	}
	const float Sw = Canvas->ClipX;
	const float Sh = Canvas->ClipY;
	const float U = Sh / 1080.f;
	UFont* Large = GEngine->GetLargeFont();
	UFont* Medium = GEngine->GetMediumFont();
	UFont* Small = GEngine->GetSmallFont();
	const double Real = GetWorld()->GetRealTimeSeconds();
	const float Dt = LastDraw > 0.0 ? static_cast<float>(FMath::Min(Real - LastDraw, 0.1)) : 0.f;
	LastDraw = Real;

	// the colossus: name, life (with the ghost of what it just lost) and the stagger bar
	if (IRttBoss* Bear = Boss())
	{
		const URttHealth* Life = Bear->GetBossHealth();
		const float W = 720.f * U;
		const float X = (Sw - W) * 0.5f;
		const float Y = 58.f * U;
		const float Fill = Life ? Life->Fraction() : 0.f;
		Ghost = Fill > Ghost ? Fill : FMath::FInterpTo(Ghost, Fill, Dt, 2.5f);
		Text(Bear->GetBossName(), Sw * 0.5f, Y - 46.f * U, Large, 1.6f * U, FLinearColor(1.f, 0.95f, 0.85f), true);
		if (Bear->IsDefeated())
		{
			Text(FString(RttText::BearBack).Replace(TEXT("%d"), *FString::FromInt(FMath::CeilToInt(Bear->RespawnIn()))), Sw * 0.5f, Y, Medium, U, FLinearColor(1.f, 0.86f, 0.55f), true);
		}
		else
		{
			Bar(X, Y, W, 14.f * U, Fill, Ghost, Bear->IsRaging() ? FLinearColor(0.85f, 0.12f, 0.1f) : FLinearColor(0.72f, 0.16f, 0.13f));
			const bool bStaggered = Bear->IsStaggered();
			const float Pulse = 0.6f + 0.4f * FMath::Sin(static_cast<float>(Real) * 10.f);
			Bar(X, Y + 22.f * U, W, 6.f * U, Bear->StaggerFraction(), 0.f,
				bStaggered ? FLinearColor(1.f, 0.55f * Pulse, 0.1f) : FLinearColor(0.95f, 0.78f, 0.3f));
			if (bStaggered)
			{
				Text(RttText::StaggerBar, Sw * 0.5f, Y + 32.f * U, Small, 1.2f * U, FLinearColor(1.f, 0.6f, 0.2f), true);
			}
		}
	}

	ARttHero* Hero = Cast<ARttHero>(GetOwningPawn());
	if (Hero && Hero->GetHealth())
	{
		const URttHealth* Life = Hero->GetHealth();
		const float W = 380.f * U;
		const float X = 48.f * U;
		const float Y = Sh - 86.f * U;
		const float Fill = Life->Fraction();
		HeroGhost = Fill > HeroGhost ? Fill : FMath::FInterpTo(HeroGhost, Fill, Dt, 2.5f);
		const FLinearColor Colour = Fill > 0.5f ? FLinearColor(0.35f, 0.78f, 0.3f) : (Fill > 0.25f ? FLinearColor(0.95f, 0.65f, 0.15f) : FLinearColor(0.9f, 0.2f, 0.15f));
		Bar(X, Y, W, 16.f * U, Fill, HeroGhost, Colour);
		Text(FString::Printf(TEXT("%d / %d"), FMath::RoundToInt(Life->Hp), FMath::RoundToInt(Life->MaxHp)), X, Y - 34.f * U, Large, U, FLinearColor(1.f, 1.f, 1.f, 0.9f), false);
		const float Charge = Hero->HeavyCharge();
		if (Charge >= 0.f)
		{
			const float CW = 180.f * U;
			const bool bFull = Charge >= 1.f;
			Bar((Sw - CW) * 0.5f, Sh * 0.64f, CW, 6.f * U, Charge, 0.f, bFull ? FLinearColor(1.f, 0.95f, 0.6f) : FLinearColor(0.95f, 0.75f, 0.3f));
			Text(Hero->HeavyName(), Sw * 0.5f, Sh * 0.64f - 34.f * U, Large, 1.2f * U, FLinearColor(1.f, 0.9f, 0.6f), true);
		}
		if (Hero->IsDown())
		{
			Text(RttText::YouFell, Sw * 0.5f, Sh * 0.42f, Large, 2.4f * U, FLinearColor(1.f, 0.35f, 0.3f), true);
			Text(FString(RttText::RiseIn).Replace(TEXT("%d"), *FString::FromInt(FMath::CeilToInt(Hero->RespawnIn()))), Sw * 0.5f, Sh * 0.42f + 64.f * U, Large, 1.2f * U, FLinearColor::White, true);
		}
	}

	if (URttFeed* Feed = URttFeed::Get(this))
	{
		Feed->Expire();
		APlayerController* Player = GetOwningPlayerController();
		for (const URttFeed::FNumber& N : Feed->Numbers)
		{
			const float Age = static_cast<float>(Real - N.Born);
			FVector2D Screen;
			if (!Player || !Player->ProjectWorldLocationToScreen(N.At, Screen))
			{
				continue;
			}
			const float A = FMath::Clamp(1.4f - Age * 1.4f, 0.f, 1.f);
			const float Pop = 1.f + FMath::Max(0.f, 0.35f - Age) * 1.6f;
			FLinearColor C = NumberColour(N.Kind);
			C.A = A;
			Text(N.Text, Screen.X, Screen.Y - Age * 70.f * U, Large, (N.bBig ? 1.9f : 1.4f) * Pop * U, C, true);
		}
		for (int32 i = 0; i < Feed->Words.Num(); ++i)
		{
			const URttFeed::FWord& W = Feed->Words[i];
			const float Age = static_cast<float>(Real - W.Born);
			FLinearColor C = W.Color;
			C.A = FMath::Clamp((W.Seconds - Age) / 0.5f, 0.f, 1.f);
			const float Pop = 1.f + FMath::Max(0.f, 0.2f - Age) * 2.f;
			Text(W.Text, Sw * 0.5f, Sh * 0.22f + i * 60.f * U, Large, 2.1f * Pop * U, C, true);
		}
	}

	Text(RttText::Controls, Sw * 0.5f, Sh - 38.f * U, Medium, 1.1f * U, FLinearColor(1.f, 1.f, 1.f, 0.55f), true);
}
