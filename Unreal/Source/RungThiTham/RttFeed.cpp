#include "RttFeed.h"

#include "Engine/World.h"

URttFeed* URttFeed::Get(const UObject* WorldContext)
{
	const UWorld* World = WorldContext ? WorldContext->GetWorld() : nullptr;
	return World ? World->GetSubsystem<URttFeed>() : nullptr;
}

double URttFeed::Now() const
{
	return GetWorld() ? GetWorld()->GetRealTimeSeconds() : 0.0;
}

void URttFeed::Number(const FVector& At, float Amount, ERttNumber Kind, bool bBig)
{
	FNumber N;
	// a little scatter so a flurry of hits does not stack into one number
	N.At = At + FVector(FMath::FRandRange(-30.f, 30.f), FMath::FRandRange(-30.f, 30.f), FMath::FRandRange(0.f, 40.f));
	N.Text = FString::FromInt(FMath::RoundToInt(Amount));
	N.Kind = Kind;
	N.Born = Now();
	N.bBig = bBig;
	Numbers.Add(N);
}

void URttFeed::Word(const FString& Text, const FLinearColor& Color, float Seconds)
{
	FWord W;
	W.Text = Text;
	W.Color = Color;
	W.Born = Now();
	W.Seconds = Seconds;
	Words.Add(W);
}

void URttFeed::Expire()
{
	const double T = Now();
	Numbers.RemoveAll([T](const FNumber& N) { return T - N.Born > 1.1; });
	Words.RemoveAll([T](const FWord& W) { return T - W.Born > W.Seconds; });
}
