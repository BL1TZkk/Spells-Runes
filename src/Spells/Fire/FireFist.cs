using System.Collections.Generic;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace SpellsAndRunes.Spells.Fire;

public class FireFist : Spell
{
    public override string Id => "fire_fist";
    public override string Name => "Fire Fist";
    public override string Description => "Ignites the caster's fist and drives a close-range burst of fire forward.";

    public override SpellTier Tier => SpellTier.Novice;
    public override SpellElement Element => SpellElement.Fire;
    public override SpellType Type => SpellType.Offense;

    public override float FluxCost => 18f;
    private const int FirstHitFrame = 13;
    private static readonly int[] HitFrames = { 13, 21, 29, 36 };

    public override float CastTime => FirstHitFrame / 30f;

    public override string? AnimationCode => "fire_fist";
    public override bool AnimationTakesOverBody => false;

    public override IReadOnlyList<string> Prerequisites => ["fire_hot_skin"];
    public override (int col, int row) TreePosition => (0, 2);

    public const float Range = 2.2f;
    public const float Damage = 6f;

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
        if (world.Api == null) return;

        for (int i = 0; i < HitFrames.Length; i++)
        {
            int hitIndex = i;
            int delayMs = FrameToMs(HitFrames[i] - FirstHitFrame);
            world.Api.Event.RegisterCallback(_ =>
            {
                if (!caster.Alive) return;
                DoHit(caster, world, spellLevel, hitIndex);
            }, delayMs);
        }
    }

    private static int FrameToMs(int frame) => (int)Math.Round(frame / 30.0 * 1000.0);

    private static void DoHit(EntityAgent caster, IWorldAccessor world, int spellLevel, int hitIndex)
    {
        var origin = caster.Pos.XYZ.Add(0, caster.LocalEyePos.Y - 0.25, 0);
        var lookDir = caster.Pos.GetViewVector().ToVec3d().Normalize();
        float range = Range * (1f + 0.10f * (spellLevel - 1));
        float damage = Damage * (1f + 0.15f * (spellLevel - 1)) / HitFrames.Length;

        world.GetEntitiesAround(origin, range + 0.6f, range + 0.6f, e =>
        {
            if (e.EntityId == caster.EntityId || e is not EntityAgent) return false;
            Vec3d target = e.Pos.XYZ.Add(0, e.LocalEyePos.Y * 0.5, 0);
            Vec3d toTarget = target - origin;
            double along = toTarget.Dot(lookDir);
            if (along < 0 || along > range) return false;
            Vec3d closest = origin + lookDir * along;
            if (target.DistanceTo(closest) > 0.85) return false;

            e.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = caster,
                Type = EnumDamageType.Fire,
            }, damage);
            return false;
        });

        Vec3d right = lookDir.Cross(new Vec3d(0, 1, 0)).Normalize();
        int side = hitIndex % 2 == 0 ? 1 : -1;
        Vec3d fistOrigin = origin.AddCopy(lookDir * 0.35).Add(right * (0.22 * side));
        FireOrb.BroadcastFx(world, "fire_fist", fistOrigin, lookDir, spellLevel);
    }

    public static void SpawnFx(IWorldAccessor world, Vec3d origin, Vec3d lookDir, int spellLevel)
    {
        var rng = world.Rand;
        int mult = 1 + (spellLevel - 1) / 4;
        for (int i = 0; i < 36 * mult; i++)
        {
            double dist = rng.NextDouble() * Range;
            var pos = origin.AddCopy(lookDir.X * dist, lookDir.Y * dist, lookDir.Z * dist);
            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = pos,
                AddPos = new Vec3d(0.25, 0.25, 0.25),
                MinVelocity = new Vec3f((float)(lookDir.X * 2.8), 0.4f, (float)(lookDir.Z * 2.8)),
                AddVelocity = new Vec3f(0.65f, 0.45f, 0.65f),
                LifeLength = 0.22f + (float)rng.NextDouble() * 0.18f,
                MinSize = 0.10f,
                MaxSize = 0.26f,
                GravityEffect = -0.15f,
                Color = ColorUtil.ColorFromRgba(15 + rng.Next(65), 95 + rng.Next(120), 255, 210),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true
            });
        }
    }
}
