using System;
using System.Buffers;
using Benchmark.Core;
using Benchmark.Core.Algorithms;
using Benchmark.Core.Components;
using Benchmark.Core.Random;
using DCFApixels.DragonECS;
using StableHash32 = Benchmark.Core.Hash.StableHash32;

namespace Benchmark.DragonEcs
{

public class ContextDragonEcs : ContextBase
{
	private EcsPipeline?     _pipeline;
	private EcsDefaultWorld? _world;

	public ContextDragonEcs()
		: base("Dragon Ecs") {}

	protected override void DoSetup()
	{
		var world = _world = new EcsDefaultWorld(
			new EcsWorldConfig(
				entitiesCapacity: EntityCount,
				poolsCapacity: 16,
				poolComponentsCapacity: EntityCount,
				poolRecycledComponentsCapacity: EntityCount / 2,
				groupCapacity: EntityCount));

		_pipeline = EcsPipeline.New()
							   .Add(new SpawnSystem())
							   .Add(new RespawnSystem())
							   .Add(new KillSystem())
							   .Add(new RenderSystem(Framebuffer))
							   .Add(new SpriteSystem())
							   .Add(new DamageSystem())
							   .Add(new AttackSystem())
							   .Add(new MovementSystem())
							   .Add(new UpdateVelocitySystem())
							   .Add(new UpdateDataSystem())
							   .Inject(_world)
							   .Build();

		_pipeline.Init();

		var spawnPool = world.GetPool<TagSpawn>();
		var dataPool  = world.GetPool<CompData>();
		var unitPool  = world.GetPool<CompUnit>();
		for (var i = 0; i < EntityCount; ++i)
		{
			var entity = world.NewEntity();
			spawnPool.Add(entity);
			dataPool.Add(entity);
			unitPool.Add(entity) = new Unit
			{
				Id   = (uint) i,
				Seed = (uint) i,
			};
		}
	}

	protected override void DoRun(int tick) =>
		_pipeline?.Run();

	protected override void DoCleanup()
	{
		_pipeline?.Destroy();
		_pipeline = null;
		_world?.Destroy();
		_world = null;
	}

	private class SpawnSystem
		: IEcsRun,
		  IEcsInject<EcsDefaultWorld>
	{
		private EcsDefaultWorld _world = null!;

		public void Inject(EcsDefaultWorld world) =>
			_world = world;

		public void Run()
		{
			foreach (var entity in _world.Where(out Aspect a))
			{
				switch (SpawnUnit(
							in a.Datas.Get(entity)
								.V,
							ref a.Units.Get(entity)
								 .V,
							out a.Healths.Add(entity)
								 .V,
							out a.Damages.Add(entity)
								 .V,
							out a.Sprites.Add(entity)
								 .V,
							out a.Positions.Add(entity)
								 .V,
							out a.Velocities.Add(entity)
								 .V))
				{
				case UnitType.NPC:
					a.NpcUnits.Add(entity);
					break;
				case UnitType.Hero:
					a.HeroUnits.Add(entity);
					break;
				case UnitType.Monster:
					a.MonsterUnits.Add(entity);
					break;
				}

				a.Spawns.Del(entity);
			}
		}

		private class Aspect : EcsAspect
		{
			public readonly EcsPool<CompDamage>    Damages      = Opt;
			public readonly EcsPool<CompData>      Datas        = Inc;
			public readonly EcsPool<CompHealth>    Healths      = Opt;
			public readonly EcsTagPool<TagHero>    HeroUnits    = Opt;
			public readonly EcsTagPool<TagMonster> MonsterUnits = Opt;
			public readonly EcsTagPool<TagNPC>     NpcUnits     = Opt;
			public readonly EcsPool<CompPosition>  Positions    = Opt;
			public readonly EcsTagPool<TagSpawn>   Spawns       = Inc;
			public readonly EcsPool<CompSprite>    Sprites      = Opt;
			public readonly EcsPool<CompUnit>      Units        = Inc;
			public readonly EcsPool<CompVelocity>  Velocities   = Opt;
		}
	}

	private class UpdateDataSystem
		: IEcsRun,
		  IEcsInject<EcsDefaultWorld>
	{
		private EcsDefaultWorld _world = null!;

		public void Inject(EcsDefaultWorld world) =>
			_world = world;

		public void Run()
		{
			foreach (var entity in _world.Where(out Aspect a))
				UpdateDataSystemForEach(
					ref a.Datas.Get(entity)
						 .V);
		}

		private class Aspect : EcsAspect
		{
			public readonly EcsPool<CompData> Datas = Inc;
		}
	}

	private class UpdateVelocitySystem
		: IEcsRun,
		  IEcsInject<EcsDefaultWorld>
	{
		private EcsDefaultWorld _world = null!;

		public void Inject(EcsDefaultWorld world) =>
			_world = world;

		public void Run()
		{
			foreach (var entity in _world.Where(out Aspect a))
				UpdateVelocitySystemForEach(
					ref a.Velocities.Get(entity)
						 .V,
					ref a.Units.Get(entity)
						 .V,
					in a.Datas.Get(entity)
						.V,
					in a.Positions.Get(entity)
						.V);
		}

		private class Aspect : EcsAspect
		{
			public readonly EcsPool<CompData>     Datas      = Inc;
			public readonly EcsTagPool<TagDead>   Deads      = Exc;
			public readonly EcsPool<CompPosition> Positions  = Inc;
			public readonly EcsPool<CompUnit>     Units      = Inc;
			public readonly EcsPool<CompVelocity> Velocities = Inc;
		}
	}

	private class MovementSystem
		: IEcsRun,
		  IEcsInject<EcsDefaultWorld>
	{
		private EcsDefaultWorld _world = null!;

		public void Inject(EcsDefaultWorld world) =>
			_world = world;

		public void Run()
		{
			foreach (var entity in _world.Where(out Aspect a))
				MovementSystemForEach(
					ref a.Positions.Get(entity)
						 .V,
					in a.Velocities.Get(entity)
						.V);
		}

		private class Aspect : EcsAspect
		{
			public readonly EcsTagPool<TagDead>   Deads      = Exc;
			public readonly EcsPool<CompPosition> Positions  = Inc;
			public readonly EcsPool<CompVelocity> Velocities = Inc;
		}
	}

	private class AttackSystem
		: IEcsRun,
		  IEcsInject<EcsDefaultWorld>
	{
		private EcsDefaultWorld _world = null!;

		public void Inject(EcsDefaultWorld world) =>
			_world = world;

		public void Run()
		{
			var entities    = _world.Where(out Aspect a);
			var count       = entities.Count;
			var keys        = ArrayPool<uint>.Shared.Rent(count);
			var indirection = ArrayPool<int>.Shared.Rent(count);
			var targets     = ArrayPool<Target<int>>.Shared.Rent(count);
			FillTargets(
				entities,
				a,
				keys,
				targets);
			RadixSort.SortWithIndirection(keys, indirection, count);
			ArrayPool<uint>.Shared.Return(keys);
			CreateAttacks(
				entities,
				a,
				indirection,
				targets.AsSpan(0, count));
			ArrayPool<int>.Shared.Return(indirection);
			ArrayPool<Target<int>>.Shared.Return(targets);
		}

		private static void FillTargets(
			EcsSpan entities,
			Aspect a,
			Span<uint> keys,
			Span<Target<int>> targets)
		{
			var i = 0;
			foreach (var entity in entities)
			{
				var index = i++;
				keys[index] = a.Units.Get(entity)
							   .V.Id;
				targets[index] = new Target<int>(
					entity,
					a.Positions.Get(entity)
					 .V);
			}
		}

		private void CreateAttacks(
			EcsSpan entities,
			Aspect a,
			ReadOnlySpan<int> indirection,
			ReadOnlySpan<Target<int>> targets)
		{
			var count = targets.Length;
			foreach (var entity in entities)
			{
				ref readonly var damage = ref a.Damages.Get(entity)
											   .V;
				if (damage.Cooldown <= 0)
					continue;

				ref var unit = ref a.Units.Get(entity)
									.V;
				ref readonly var data = ref a.Datas.Get(entity)
											 .V;
				var tick = data.Tick - unit.SpawnTick;
				if (tick % damage.Cooldown != 0)
					continue;

				ref readonly var position = ref a.Positions.Get(entity)
												 .V;
				var generator    = new RandomGenerator(unit.Seed);
				var index        = generator.Random(ref unit.Counter, count);
				var target       = targets[indirection[index]];
				var attackEntity = _world.NewEntity();
				a.Attacks.Add(attackEntity) = new AttackEntity
				{
					Target = _world.GetEntityLong(target.Entity),
					Damage = damage.Attack,
					Ticks  = Common.AttackTicks(position.V, target.Position),
				};
			}
		}

		private class Aspect : EcsAspect
		{
			public readonly EcsPool<AttackEntity> Attacks   = Opt;
			public readonly EcsPool<CompDamage>   Damages   = Inc;
			public readonly EcsPool<CompData>     Datas     = Inc;
			public readonly EcsTagPool<TagDead>   Deads     = Exc;
			public readonly EcsPool<CompPosition> Positions = Inc;
			public readonly EcsTagPool<TagSpawn>  Spawns    = Exc;
			public readonly EcsPool<CompUnit>     Units     = Inc;
		}
	}

	private class DamageSystem
		: IEcsRun,
		  IEcsInject<EcsDefaultWorld>
	{
		private EcsDefaultWorld _world = null!;

		public void Inject(EcsDefaultWorld world) =>
			_world = world;

		public void Run()
		{
			var group = _world.WhereToGroup(out Aspect a);
			foreach (var entity in _world.Where(out AttackAspect aa))
			{
				ref var attack = ref aa.Attacks.Get(entity);
				if (attack.Ticks-- > 0)
					continue;

				if (attack.Target.TryGetID(out var targetEntity)
				 && group.Has(targetEntity))
				{
					ref var health = ref a.Healths.Get(targetEntity)
										  .V;
					ref var damage = ref a.Damages.Get(targetEntity)
										  .V;
					ApplyDamageSequential(ref health, in damage, in attack);
				}
				_world.DelEntity(entity);
			}
		}

		private class AttackAspect : EcsAspect
		{
			public readonly EcsPool<AttackEntity> Attacks = Inc;
		}

		private class Aspect : EcsAspect
		{
			public readonly EcsPool<CompDamage> Damages = Inc;
			public readonly EcsTagPool<TagDead> Deads   = Exc;
			public readonly EcsPool<CompHealth> Healths = Inc;
		}
	}

	private class KillSystem
		: IEcsRun,
		  IEcsInject<EcsDefaultWorld>
	{
		private EcsDefaultWorld _world = null!;

		public void Inject(EcsDefaultWorld world) =>
			_world = world;

		public void Run()
		{
			foreach (var entity in _world.Where(out Aspect a))
			{
				ref readonly var health = ref a.Healths.Get(entity)
											   .V;
				if (health.Hp > 0)
					continue;

				ref var unit = ref a.Units.Get(entity)
									.V;
				a.Deads.Add(entity);
				ref readonly var data = ref a.Datas.Get(entity)
											 .V;
				unit.RespawnTick = data.Tick + RespawnTicks;
			}
		}

		private class Aspect : EcsAspect
		{
			public readonly EcsPool<CompData>   Datas   = Inc;
			public readonly EcsTagPool<TagDead> Deads   = Exc;
			public readonly EcsPool<CompHealth> Healths = Inc;
			public readonly EcsPool<CompUnit>   Units   = Inc;
		}
	}

	private class SpriteSystem
		: IEcsRun,
		  IEcsInject<EcsDefaultWorld>
	{
		private EcsDefaultWorld _world = null!;

		public void Inject(EcsDefaultWorld world) =>
			_world = world;

		public void Run()
		{
			ForEachSprite<StateAspect<TagSpawn>>(SpriteMask.Spawn);
			ForEachSprite<StateAspect<TagDead>>(SpriteMask.Grave);
			ForEachSprite<UnitAspect<TagNPC>>(SpriteMask.NPC);
			ForEachSprite<UnitAspect<TagHero>>(SpriteMask.Hero);
			ForEachSprite<UnitAspect<TagMonster>>(SpriteMask.Monster);
		}

		private void ForEachSprite<TAspect>(SpriteMask sprite)
			where TAspect : EcsAspect, ISpriteAspect, new()
		{
			foreach (var entity in _world.Where(out TAspect a))
				a.Sprites.Get(entity)
				 .V.Character = sprite;
		}

		private interface ISpriteAspect
		{
			public EcsPool<CompSprite> Sprites { get; }
		}

		private class StateAspect<T>
			: EcsAspect,
			  ISpriteAspect
			where T : struct, IEcsTagComponent
		{
			public readonly EcsTagPool<T>       Spawns = Inc;
			public          EcsPool<CompSprite> Sprites { get; } = Inc;
		}

		private class UnitAspect<T>
			: EcsAspect,
			  ISpriteAspect
			where T : struct, IEcsTagComponent
		{
			public readonly EcsTagPool<TagDead>  Deads  = Exc;
			public readonly EcsTagPool<TagSpawn> Spawns = Exc;
			public readonly EcsTagPool<T>        Units  = Inc;
			public          EcsPool<CompSprite>  Sprites { get; } = Inc;
		}
	}

	private class RenderSystem
		: IEcsRun,
		  IEcsInject<EcsDefaultWorld>
	{
		private readonly Framebuffer     _framebuffer;
		private          EcsDefaultWorld _world = null!;

		public RenderSystem(Framebuffer framebuffer) =>
			_framebuffer = framebuffer;

		public void Inject(EcsDefaultWorld world) =>
			_world = world;

		public void Run()
		{
			foreach (var entity in _world.Where(out Aspect a))
				RenderSystemForEach(
					_framebuffer,
					in a.Positions.Get(entity)
						.V,
					in a.Sprites.Get(entity)
						.V,
					in a.Units.Get(entity)
						.V,
					in a.Datas.Get(entity)
						.V);
		}

		private class Aspect : EcsAspect
		{
			public readonly EcsPool<CompData>     Datas     = Inc;
			public readonly EcsPool<CompPosition> Positions = Inc;
			public readonly EcsPool<CompSprite>   Sprites   = Inc;
			public readonly EcsPool<CompUnit>     Units     = Inc;
		}
	}

	private class RespawnSystem
		: IEcsRun,
		  IEcsInject<EcsDefaultWorld>
	{
		private EcsDefaultWorld _world = null!;

		public void Inject(EcsDefaultWorld world) =>
			_world = world;

		public void Run()
		{
			foreach (var entity in _world.Where(out Aspect a))
			{
				ref readonly var unit = ref a.Units.Get(entity)
											 .V;
				ref readonly var data = ref a.Datas.Get(entity)
											 .V;
				if (data.Tick < unit.RespawnTick)
					continue;

				var newEntity = _world.NewEntity();
				a.Spawns.Add(newEntity);
				a.Datas.Add(newEntity) = data;
				a.Units.Add(newEntity) = new Unit
				{
					Id   = unit.Id | (uint) data.Tick << 16,
					Seed = StableHash32.Hash(unit.Seed, unit.Counter),
				};
				_world.DelEntity(entity);
			}
		}

		private class Aspect : EcsAspect
		{
			public readonly EcsPool<CompData>    Datas  = Inc;
			public readonly EcsTagPool<TagDead>  Deads  = Inc;
			public readonly EcsTagPool<TagSpawn> Spawns = Opt;
			public readonly EcsPool<CompUnit>    Units  = Inc;
		}
	}
}

}
