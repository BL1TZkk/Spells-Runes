using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace SpellsAndRunes.Spells.Fire;

public class FireBlast : Spell
{
    public override string Id => "fire_blast";
    public override string Name => "Fire Blast";
    public override string Description => "Shoots a compacted ball of fire which pierces almost anything.";

    public override SpellTier Tier => SpellTier.Apprentice;
    public override SpellElement Element => SpellElement.Fire;
    public override SpellType Type => SpellType.Offense;

    public override float FluxCost => 32f;
    public override float CastTime => 0.75f;

    public override string? AnimationCode => "fire_blast";
    public override bool AnimationTakesOverBody => true;

    public override IReadOnlyList<string> Prerequisites => ["fire_mine", "fire_dance"];
    public override (int col, int row) TreePosition => (1, 5);

    public const float Range = 18f;
    public const float Speed = 100f;
    public const float HitRadius = 0.75f;
    public const float Damage = 16f;

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
        if (world.Api == null) return;

        var lookDir = caster.Pos.GetViewVector().ToVec3d().Normalize();
        var origin = caster.Pos.XYZ.Add(lookDir * 0.9).Add(0, caster.LocalEyePos.Y - 0.25, 0);
        Vec3d right = lookDir.Cross(new Vec3d(0, 1, 0)).Normalize();
        FireOrb.BroadcastFx(world, "fire_blast_hand_smoke", origin.AddCopy(right * 0.22), lookDir, spellLevel);
        float range = Range * GetRangeMultiplier(spellLevel);
        float damage = Damage * GetDamageMultiplier(spellLevel);
        float traveled = 0f;
        Vec3d pos = origin.Clone();
        HashSet<long> hit = new();
        long listenerId = 0;
        const float stepDt = 0.02f;
        float stepDist = Speed * stepDt;

        listenerId = world.Api.Event.RegisterGameTickListener(dt =>
        {
            if (!caster.Alive)
            {
                world.Api.Event.UnregisterGameTickListener(listenerId);
                return;
            }

            Vec3d prev = pos.Clone();
            pos.Add(lookDir.X * stepDist, lookDir.Y * stepDist, lookDir.Z * stepDist);
            traveled += stepDist;

            FireOrb.BroadcastFx(world, "fire_blast_trail", pos, lookDir, spellLevel);
            FireOrb.BroadcastFx(world, "fire_blast_hitbox", pos, lookDir, spellLevel);

            world.GetEntitiesAround(pos, HitRadius + 0.5f, HitRadius + 0.5f, e =>
            {
                if (e.EntityId == caster.EntityId || e is not EntityAgent || hit.Contains(e.EntityId)) return false;
                Vec3d target = e.Pos.XYZ.Add(0, e.LocalEyePos.Y * 0.5, 0);
                if (DistanceToSegment(target, prev, pos) > HitRadius) return false;

                hit.Add(e.EntityId);
                e.ReceiveDamage(new DamageSource
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = caster,
                    Type = EnumDamageType.Fire,
                }, damage);
                e.Pos.Motion.Add(lookDir.X * 0.12, 0.04, lookDir.Z * 0.12);
                FireOrb.BroadcastFx(world, "fire_blast_impact", target, lookDir, spellLevel);
                return false;
            });

            if (HitSolidBlock(world, prev, pos) || traveled >= range)
            {
                FireOrb.BroadcastFx(world, "fire_blast_impact", pos, lookDir, spellLevel);
                world.Api.Event.UnregisterGameTickListener(listenerId);
            }
        }, (int)(stepDt * 1000));
    }

    private static bool HitSolidBlock(IWorldAccessor world, Vec3d from, Vec3d to)
    {
        BlockSelection? bsel = null;
        EntitySelection? esel = null;
        world.RayTraceForSelection(from, to, ref bsel, ref esel);
        return bsel != null;
    }

    private static double DistanceToSegment(Vec3d point, Vec3d from, Vec3d to)
    {
        Vec3d seg = to - from;
        double lenSq = seg.LengthSq();
        if (lenSq < 0.0001) return point.DistanceTo(from);
        double t = GameMath.Clamp((point - from).Dot(seg) / lenSq, 0, 1);
        return point.DistanceTo(from + seg * t);
    }

    public static void SpawnTrailFx(IWorldAccessor world, Vec3d origin, Vec3d lookDir, int spellLevel)
    {
        var rng = world.Rand;
        int mult = 1 + (spellLevel - 1) / 4;
        for (int i = 0; i < 12 * mult; i++)
        {
            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = origin,
                AddPos = new Vec3d(0.14, 0.14, 0.14),
                MinVelocity = new Vec3f((float)(-lookDir.X * 0.9), 0.12f, (float)(-lookDir.Z * 0.9)),
                AddVelocity = new Vec3f(0.35f, 0.35f, 0.35f),
                LifeLength = 0.18f + (float)rng.NextDouble() * 0.12f,
                MinSize = 0.10f,
                MaxSize = 0.24f,
                GravityEffect = -0.08f,
                Color = ColorUtil.ColorFromRgba(10 + rng.Next(70), 100 + rng.Next(130), 255, 230),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true
            });
        }
    }

    public static void SpawnImpactFx(IWorldAccessor world, Vec3d origin, int spellLevel)
    {
        var rng = world.Rand;
        int mult = 1 + (spellLevel - 1) / 4;
        for (int i = 0; i < 54 * mult; i++)
        {
            double a = rng.NextDouble() * Math.PI * 2;
            double speed = 1.4 + rng.NextDouble() * 2.8;
            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = origin,
                AddPos = new Vec3d(0.15, 0.15, 0.15),
                MinVelocity = new Vec3f((float)(Math.Cos(a) * speed), 0.35f + (float)rng.NextDouble() * 0.6f, (float)(Math.Sin(a) * speed)),
                AddVelocity = new Vec3f(0.25f, 0.2f, 0.25f),
                LifeLength = 0.25f + (float)rng.NextDouble() * 0.2f,
                MinSize = 0.10f,
                MaxSize = 0.32f,
                GravityEffect = -0.08f,
                Color = ColorUtil.ColorFromRgba(10 + rng.Next(80), 90 + rng.Next(140), 255, 225),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true
            });
        }
    }

    public static void SpawnHandSmokeFx(IWorldAccessor world, Vec3d origin, Vec3d lookDir, int spellLevel)
    {
        var rng = world.Rand;
        int mult = 1 + (spellLevel - 1) / 4;
        for (int i = 0; i < 16 * mult; i++)
        {
            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = origin,
                AddPos = new Vec3d(0.12, 0.10, 0.12),
                MinVelocity = new Vec3f((float)(-lookDir.X * 0.18), 0.12f, (float)(-lookDir.Z * 0.18)),
                AddVelocity = new Vec3f(0.16f, 0.18f, 0.16f),
                LifeLength = 0.45f + (float)rng.NextDouble() * 0.28f,
                MinSize = 0.12f,
                MaxSize = 0.30f,
                SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 1.5f),
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -16f),
                GravityEffect = -0.02f,
                Color = ColorUtil.ColorFromRgba(85 + rng.Next(45), 80 + rng.Next(45), 75 + rng.Next(45), 150),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true,
                SelfPropelled = true,
                WindAffectednes = 0.7f
            });
        }
    }

    public static void SpawnHitboxDebug(IWorldAccessor world, Vec3d center, float radius)
    {
        var p = new SimpleParticleProperties
        {
            MinQuantity = 1,
            AddQuantity = 0,
            AddPos = new Vec3d(0, 0, 0),
            MinVelocity = new Vec3f(0, 0, 0),
            AddVelocity = new Vec3f(0, 0, 0),
            LifeLength = 0.18f,
            MinSize = 0.09f,
            MaxSize = 0.09f,
            GravityEffect = 0f,
            ParticleModel = EnumParticleModel.Quad,
            WithTerrainCollision = false,
            ShouldDieInLiquid = false,
            Color = ColorUtil.ColorFromRgba(255, 70, 70, 220)
        };

        for (int i = 0; i < 18; i++)
        {
            double a = i * Math.PI * 2 / 18;
            p.MinPos = new Vec3d(center.X + Math.Cos(a) * radius, center.Y, center.Z + Math.Sin(a) * radius);
            world.SpawnParticles(p);
        }
    }
}
