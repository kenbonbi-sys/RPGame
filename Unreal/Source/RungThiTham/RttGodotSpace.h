// Godot's space, kept for code ported line by line from the Godot reference (the colossi move by
// procedural maths there): +Y up, −Z forward, metres, right-handed bases with column axes. Unreal's
// is +Z up, +X forward, centimetres. ToU / ToG convert points; XfToU turns a Godot transform into
// an Unreal one for a mesh imported from a glTF made in that Godot space (Tools/Sculpt), whose
// axes the glTF importer turns into (x, z, y).
#pragma once

#include "CoreMinimal.h"

namespace RttG
{
	// A basis by its column axes.
	struct FBasis
	{
		FVector X = FVector(1.0, 0.0, 0.0);
		FVector Y = FVector(0.0, 1.0, 0.0);
		FVector Z = FVector(0.0, 0.0, 1.0);

		FVector operator*(const FVector& V) const { return X * V.X + Y * V.Y + Z * V.Z; }
		FBasis operator*(const FBasis& O) const { return {(*this) * O.X, (*this) * O.Y, (*this) * O.Z}; }
		// the inverse of a rotation
		FBasis Transposed() const
		{
			return {FVector(X.X, Y.X, Z.X), FVector(X.Y, Y.Y, Z.Y), FVector(X.Z, Y.Z, Z.Z)};
		}
		FBasis Orthonormalized() const
		{
			const FVector NX = X.GetSafeNormal();
			const FVector NZ = FVector::CrossProduct(NX, Y).GetSafeNormal();
			const FVector NY = FVector::CrossProduct(NZ, NX);
			return {NX, NY, NZ};
		}
	};

	struct FXf
	{
		FBasis B;
		FVector O = FVector::ZeroVector;

		FVector operator*(const FVector& V) const { return B * V + O; }
		FXf operator*(const FXf& Other) const { return {B * Other.B, B * Other.O + O}; }
		// the inverse of a rotation and translation
		FXf Inverse() const
		{
			const FBasis T = B.Transposed();
			return {T, -(T * O)};
		}
	};

	inline FBasis RotX(double A)
	{
		const double C = FMath::Cos(A), S = FMath::Sin(A);
		return {FVector(1, 0, 0), FVector(0, C, S), FVector(0, -S, C)};
	}

	inline FBasis RotY(double A)
	{
		const double C = FMath::Cos(A), S = FMath::Sin(A);
		return {FVector(C, 0, -S), FVector(0, 1, 0), FVector(S, 0, C)};
	}

	inline FBasis RotZ(double A)
	{
		const double C = FMath::Cos(A), S = FMath::Sin(A);
		return {FVector(C, S, 0), FVector(-S, C, 0), FVector(0, 0, 1)};
	}

	// Godot's Basis.from_euler(Vector3(pitch, yaw, roll)): YXZ order.
	inline FBasis Euler(double Pitch, double Yaw, double Roll)
	{
		return RotY(Yaw) * RotX(Pitch) * RotZ(Roll);
	}

	// Godot's Basis(axis, angle).
	inline FBasis AxisAngle(const FVector& Axis, double A)
	{
		const FVector U = Axis.GetSafeNormal();
		const double C = FMath::Cos(A), S = FMath::Sin(A), K = 1.0 - C;
		auto Col = [&](const FVector& E)
		{
			// Rodrigues: v cos + (u × v) sin + u (u·v)(1 − cos)
			return E * C + FVector::CrossProduct(U, E) * S + U * FVector::DotProduct(U, E) * K;
		};
		return {Col(FVector(1, 0, 0)), Col(FVector(0, 1, 0)), Col(FVector(0, 0, 1))};
	}

	// gau_ma.gd's _axis_basis: a basis whose Y is `Y`.
	inline FBasis AxisBasis(const FVector& InY)
	{
		const FVector Y = InY.GetSafeNormal();
		const FVector Ref = FMath::Abs(Y.X) < 0.9 ? FVector(1, 0, 0) : FVector(0, 0, -1);
		const FVector Z = FVector::CrossProduct(Ref, Y).GetSafeNormal();
		const FVector X = FVector::CrossProduct(Y, Z).GetSafeNormal();
		return {X, Y, Z};
	}

	// Godot's angle_difference(from, to): to − from wrapped to [−π, π].
	inline double AngleDifference(double From, double To)
	{
		double D = FMath::Fmod(To - From + PI, 2.0 * PI);
		if (D < 0.0)
		{
			D += 2.0 * PI;
		}
		return D - PI;
	}

	inline double MoveToward(double From, double To, double Delta)
	{
		return FMath::Abs(To - From) <= Delta ? To : From + FMath::Sign(To - From) * Delta;
	}

	// Godot point (m) -> Unreal point (cm), and back.
	inline FVector DirToU(const FVector& G) { return FVector(-G.Z, G.X, G.Y); }
	inline FVector ToU(const FVector& G) { return DirToU(G) * 100.0; }
	inline FVector ToG(const FVector& U) { return FVector(U.Y, U.Z, -U.X) / 100.0; }

	// A Godot transform (with a uniform `Scale` on top) for a mesh or collider made in Godot space.
	inline FTransform XfToU(const FXf& Xf, double Scale = 1.0)
	{
		const FMatrix M(DirToU(Xf.B.X).GetSafeNormal(), DirToU(Xf.B.Z).GetSafeNormal(), DirToU(Xf.B.Y).GetSafeNormal(), FVector::ZeroVector);
		return FTransform(M.ToQuat(), ToU(Xf.O), FVector(Scale));
	}
}
