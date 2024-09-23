using System.Runtime.CompilerServices;
using Benchmark.Core.Components;
using DCFApixels.DragonECS;

namespace Benchmark.DragonEcs
{

public struct CompPosition : IEcsComponent
{
	public Position V;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator CompPosition(Position value) =>
		new() { V = value };
}

public struct CompVelocity : IEcsComponent
{
	public Velocity V;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator CompVelocity(Velocity value) =>
		new() { V = value };
}

public struct CompSprite : IEcsComponent
{
	public Sprite V;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator CompSprite(Sprite value) =>
		new() { V = value };
}

public struct CompUnit : IEcsComponent
{
	public Unit V;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator CompUnit(Unit value) =>
		new() { V = value };
}

public struct CompData : IEcsComponent
{
	public Data V;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator CompData(Data value) =>
		new() { V = value };
}

public struct CompHealth : IEcsComponent
{
	public Health V;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator CompHealth(Health value) =>
		new() { V = value };
}

public struct CompDamage : IEcsComponent
{
	public Damage V;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator CompDamage(Damage value) =>
		new() { V = value };
}

public struct AttackEntity
	: IEcsComponent,
	  IAttack
{
	public entlong Target;
	public int     Damage;
	public int     Ticks;

	int IAttack.Damage
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Damage;
	}
}

}
