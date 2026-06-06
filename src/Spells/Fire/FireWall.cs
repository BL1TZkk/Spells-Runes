using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace SpellsAndRunes.Spells.Fire;

public class FireWall : Spell
{
    public override string Id => "fire_wall";
    public override string Name => "Fire Wall";
    public override string Description => "Raises a wall of fire that pushes enemies away and burns them.";

    public override SpellTier Tier => SpellTier.Adept;
    public override SpellElement Element => SpellElement.Fire;
    public override SpellType Type => SpellType.Defense;

    public override float FluxCost => 40f;
    public override float CastTime => 1.0f;

    public override string? AnimationCode => "fire_wall";
    public override bool AnimationTakesOverBody => false;

    public override IReadOnlyList<string> Prerequisites => ["fire_dance"];
    public override (int col, int row) TreePosition => (2, 6);

    public const float DurationSeconds = 8f;
    public const float Width = 5.5f;
    public const float Height = 2.4f;
    public const float Thickness = 0.9f;
    public const float DamagePerSecond = 10f;
    public const float PushForce = 0.22f;
    public const float StartDistance = 1.35f;
    public const float EndDistance = 4.0f;
    public const float TravelSeconds = 0.65f;

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
        if (world.Api == null) return;

        Vec3d lookDir = caster.Pos.GetViewVector().ToVec3d();
        lookDir.Y = 0;
        if (lookDir.LengthSq() < 0.001) lookDir = new Vec3d(0, 0, 1);
        lookDir.Normalize();

        Vec3d right = lookDir.Cross(new Vec3d(0, 1, 0)).Normalize();
        Vec3d castOrigin = caster.Pos.XYZ.Clone();
        float elapsed = 0f;
        long listenerId = 0;

        listenerId = world.Api.Event.RegisterGameTickListener(dt =>
        {
            elapsed += dt;
            if (elapsed >= DurationSeconds || !caster.Alive)
            {
                world.Api.Event.UnregisterGameTickListener(listenerId);
                return;
            }

            double travelT = GameMath.Clamp(elapsed / TravelSeconds, 0f, 1f);
            travelT = travelT * travelT * (3 - 2 * travelT);
            double distance = StartDistance + (EndDistance - StartDistance) * travelT;
            Vec3d center = ClampToSurface(world, castOrigin.AddCopy(lookDir * distance), 0.05);

            ApplyWall(caster, world, center, lookDir, right, dt, spellLevel);
            FireOrb.BroadcastFx(world, Id, center, lookDir, spellLevel);
            FireOrb.BroadcastFx(world, "fire_wall_hitbox", center, lookDir, spellLevel);
        }, 125);
    }

    private static void ApplyWall(EntityAgent caster, IWorldAccessor world, Vec3d center, Vec3d forward, Vec3d right, float dt, int spellLevel)
    {
        float halfWidth = Width * 0.5f * (1f + 0.08f * (spellLevel - 1));
        float damage = DamagePerSecond * dt * (1f + 0.15f * (spellLevel - 1));

        world.GetEntitiesAround(center.AddCopy(0, Height * 0.5, 0), halfWidth + 1.5f, Height + 1f, e =>
        {
            if (e.EntityId == caster.EntityId || e is not EntityAgent) return false;
            Vec3d rel = e.Pos.XYZ - center;
            double along = rel.Dot(right);
            double depth = rel.Dot(forward);
            if (Math.Abs(along) > halfWidth || Math.Abs(depth) > Thickness || rel.Y < -0.2 || rel.Y > Height) return false;

            e.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = caster,
                Type = EnumDamageType.Fire,
            }, damage);

            double side = Math.Sign(depth);
            if (side == 0) side = 1;
            e.Pos.Motion.Add(forward.X * PushForce * side, 0.08, forward.Z * PushForce * side);
            return false;
        });
    }

    public static void SpawnFx(IWorldAccessor world, Vec3d center, Vec3d lookDir, int spellLevel)
    {
        var rng = world.Rand;
        Vec3d forward = new Vec3d(lookDir.X, 0, lookDir.Z);
        if (forward.LengthSq() < 0.001) forward = new Vec3d(0, 0, 1);
        forward.Normalize();
        Vec3d right = forward.Cross(new Vec3d(0, 1, 0)).Normalize();
        int mult = 1 + (spellLevel - 1) / 4;
        int columns = 15;
        double time = world.ElapsedMilliseconds * 0.004;

        for (int c = 0; c < columns; c++)
        {
            double t = columns == 1 ? 0.5 : c / (double)(columns - 1);
            double baseAlong = (t - 0.5) * Width;
            double wave = Math.Sin(time + c * 0.72) * 0.16;
            double pulse = 0.5 + 0.5 * Math.Sin(time * 0.7 + c * 1.13);
            double tongueHeight = Height * (0.78 + 0.16 * pulse + 0.06 * rng.NextDouble());
            int particles = (7 + rng.Next(3)) * mult;

            for (int i = 0; i < particles; i++)
            {
                double yNorm = Math.Pow(rng.NextDouble(), 1.35);
                double y = yNorm * tongueHeight;
                double taper = 1.0 - yNorm * 0.72;
                double along = baseAlong + wave * yNorm + (rng.NextDouble() - 0.5) * 0.34 * taper;
                double depth = (rng.NextDouble() - 0.5) * Thickness * (0.9 - yNorm * 0.35);
                Vec3d pos = center + right * along + forward * depth + new Vec3d(0, y, 0);
                float lift = 0.42f + (float)(1.1 * (1.0 - yNorm)) + (float)rng.NextDouble() * 0.35f;

                world.SpawnParticles(new SimpleParticleProperties
                {
                    MinQuantity = 1,
                    AddQuantity = 0,
                    MinPos = pos,
                    AddPos = new Vec3d(0.06 * taper, 0.05, 0.06 * taper),
                    MinVelocity = new Vec3f((float)(forward.X * 0.08 + right.X * wave * 0.55), lift, (float)(forward.Z * 0.08 + right.Z * wave * 0.55)),
                    AddVelocity = new Vec3f(0.22f, 0.45f, 0.22f),
                    LifeLength = 0.28f + (float)rng.NextDouble() * 0.34f,
                    MinSize = 0.10f + (float)(0.10 * taper),
                    MaxSize = 0.30f + (float)(0.24 * taper),
                    GravityEffect = -0.25f,
                    Color = yNorm < 0.38
                        ? ColorUtil.ColorFromRgba(5 + rng.Next(55), 125 + rng.Next(105), 255, 235)
                        : ColorUtil.ColorFromRgba(35 + rng.Next(75), 75 + rng.Next(120), 255, 205),
                    ParticleModel = EnumParticleModel.Quad,
                    WithTerrainCollision = false,
                    ShouldDieInLiquid = true
                });
            }

            int coreChunks = 2 + rng.Next(4);
            for (int core = 0; core < coreChunks; core++)
            {
                double coreT = (core + rng.NextDouble() * 0.35) / coreChunks;
                double coreY = tongueHeight * (0.10 + coreT * 0.82);
                double coreSway = Math.Sin(time * 1.15 + c * 0.9 + core * 0.65) * 0.11;
                Vec3d corePos = center
                    + right * (baseAlong + wave * 0.45 + coreSway)
                    + forward * ((rng.NextDouble() - 0.5) * Thickness * 0.22)
                    + new Vec3d(0, coreY, 0);

                world.SpawnParticles(new SimpleParticleProperties
                {
                    MinQuantity = 1,
                    AddQuantity = 0,
                    MinPos = corePos,
                    AddPos = new Vec3d(0.16, 0.10, 0.16),
                    MinVelocity = new Vec3f((float)(right.X * (wave + coreSway) * 0.24), 0.38f + (float)(coreT * 0.18), (float)(right.Z * (wave + coreSway) * 0.24)),
                    AddVelocity = new Vec3f(0.22f, 0.30f, 0.22f),
                    LifeLength = 0.30f + (float)rng.NextDouble() * 0.18f,
                    MinSize = 0.18f,
                    MaxSize = 0.38f,
                    GravityEffect = -0.30f,
                    Color = ColorUtil.ColorFromRgba(5 + rng.Next(45), 135 + rng.Next(85), 255, 205),
                    ParticleModel = EnumParticleModel.Quad,
                    WithTerrainCollision = false,
                    ShouldDieInLiquid = true
                });
            }
        }

        for (int i = 0; i < 18 * mult; i++)
        {
            double along = (rng.NextDouble() - 0.5) * Width;
            double depth = (rng.NextDouble() - 0.5) * Thickness;
            Vec3d pos = center + right * along + forward * depth + new Vec3d(0, 0.04 + rng.NextDouble() * 0.12, 0);
            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = pos,
                AddPos = new Vec3d(0.08, 0.03, 0.08),
                MinVelocity = new Vec3f((float)(forward.X * 0.05), 0.18f, (float)(forward.Z * 0.05)),
                AddVelocity = new Vec3f(0.18f, 0.18f, 0.18f),
                LifeLength = 0.16f + (float)rng.NextDouble() * 0.16f,
                MinSize = 0.06f,
                MaxSize = 0.16f,
                GravityEffect = -0.12f,
                Color = ColorUtil.ColorFromRgba(20 + rng.Next(60), 100 + rng.Next(120), 255, 215),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true
            });
        }
    }

    public static void SpawnHitboxDebug(IWorldAccessor world, Vec3d center, Vec3d lookDir)
    {
        Vec3d forward = new Vec3d(lookDir.X, 0, lookDir.Z);
        if (forward.LengthSq() < 0.001) forward = new Vec3d(0, 0, 1);
        forward.Normalize();
        Vec3d right = forward.Cross(new Vec3d(0, 1, 0)).Normalize();

        var p = new SimpleParticleProperties
        {
            MinQuantity = 1,
            AddQuantity = 0,
            AddPos = new Vec3d(0, 0, 0),
            MinVelocity = new Vec3f(0, 0, 0),
            AddVelocity = new Vec3f(0, 0, 0),
            LifeLength = 0.25f,
            MinSize = 0.10f,
            MaxSize = 0.10f,
            GravityEffect = 0f,
            ParticleModel = EnumParticleModel.Quad,
            WithTerrainCollision = false,
            ShouldDieInLiquid = false,
            Color = ColorUtil.ColorFromRgba(255, 70, 70, 220)
        };

        for (int i = 0; i <= 16; i++)
        {
            double along = -Width * 0.5 + Width * i / 16.0;
            for (int j = 0; j <= 4; j++)
            {
                double y = Height * j / 4.0;
                p.MinPos = center + right * along + new Vec3d(0, y, 0);
                world.SpawnParticles(p);
            }
        }
    }
}
