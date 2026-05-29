using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace SpellsAndRunes.Spells.Fire;

public class FireWisp : Spell
{
    public override string Id => "fire_wisp";
    public override string Name => "Fire Wisp";
    public override string Description => "Summons a little fire wisp to light your way.";

    public override SpellTier Tier => SpellTier.Adept;
    public override SpellElement Element => SpellElement.Fire;
    public override SpellType Type => SpellType.Enchantment;

    public override float FluxCost => 28f;
    public override float CastTime => 0.8f;

    public override string? AnimationCode => "fire_wisp";
    public override bool AnimationTakesOverBody => true;

    public override IReadOnlyList<string> Prerequisites => ["fire_fist"];
    public override (int col, int row) TreePosition => (0, 4);

    public const float DurationSeconds = 45f;
    public const float OrbitRadius = 1.2f;

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
        if (world.Api == null) return;

        float duration = DurationSeconds + 5f * (spellLevel - 1);
        float elapsed = 0f;
        long listenerId = 0;
        Vec3d lookDir = caster.Pos.GetViewVector().ToVec3d().Normalize();
        Vec3d handOrigin = caster.Pos.XYZ
            .Add(lookDir * 0.55)
            .Add(0, caster.LocalEyePos.Y - 0.35, 0);
        FireOrb.BroadcastFx(world, Id, handOrigin, lookDir, spellLevel);

        listenerId = world.Api.Event.RegisterGameTickListener(dt =>
        {
            elapsed += dt;
            if (!caster.Alive || elapsed >= duration)
            {
                world.Api.Event.UnregisterGameTickListener(listenerId);
                return;
            }

            double a = elapsed * 1.8;
            Vec3d pos = caster.Pos.XYZ
                .Add(Math.Cos(a) * OrbitRadius, caster.LocalEyePos.Y + 0.35 + Math.Sin(elapsed * 1.6) * 0.12, Math.Sin(a) * OrbitRadius);
            FireOrb.BroadcastFx(world, Id, pos, new Vec3d(0, 1, 0), spellLevel);
        }, 80);
    }

    public static void SpawnFx(IWorldAccessor world, Vec3d origin, int spellLevel)
    {
        var rng = world.Rand;
        int mult = 1 + (spellLevel - 1) / 4;

        for (int i = 0; i < 5 * mult; i++)
        {
            double a = rng.NextDouble() * Math.PI * 2;
            double r = rng.NextDouble() * 0.18;
            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = origin.AddCopy(Math.Cos(a) * r, Math.Sin(a) * r * 0.6, Math.Sin(a) * r),
                AddPos = new Vec3d(0.02, 0.02, 0.02),
                MinVelocity = new Vec3f(0, 0.025f, 0),
                AddVelocity = new Vec3f(0.04f, 0.04f, 0.04f),
                LifeLength = 0.25f + (float)rng.NextDouble() * 0.16f,
                MinSize = 0.07f,
                MaxSize = 0.18f,
                GravityEffect = -0.05f,
                Color = ColorUtil.ColorFromRgba(25 + rng.Next(60), 105 + rng.Next(110), 255, 220),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true
            });
        }
    }
}
