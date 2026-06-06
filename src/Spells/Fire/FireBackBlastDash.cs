using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using System;
using System.Collections.Generic;

namespace SpellsAndRunes.Spells.Fire;

public class FireBackBlastDash : Spell
{
    public override string Id => "fire_back_blast_dash";
    public override string Name => "Back Blast Dash";
    public override string Description => "Kicks fire backward to propel the caster forward.";

    public override SpellTier Tier => SpellTier.Novice;
    public override SpellElement Element => SpellElement.Fire;
    public override SpellType Type => SpellType.Offense;

    public override float FluxCost => 20f;
    public override float CastTime => 0.4f;

    public override string? AnimationCode => "fire_back_blast_dash";
    public override bool AnimationTakesOverBody => false;

    public override IReadOnlyList<string> Prerequisites => ["fire_fist", "fire_orb"];
    public override (int col, int row) TreePosition => (1, 2);

    public const float ForwardForce = 1.35f;

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
        var lookDir = caster.Pos.GetViewVector().ToVec3d().Normalize();
        caster.Pos.Motion.Add(
            lookDir.X * ForwardForce * GetRangeMultiplier(spellLevel),
            0.03,
            lookDir.Z * ForwardForce * GetRangeMultiplier(spellLevel));
    }

    public static void SpawnFx(IWorldAccessor world, Vec3d origin, Vec3d lookDir, int spellLevel)
    {
        var rng = world.Rand;
        Vec3d back = lookDir * -1;
        int mult = 1 + (spellLevel - 1) / 4;
        for (int i = 0; i < 58 * mult; i++)
        {
            double dist = rng.NextDouble() * 3.2;
            double fade = 1.0 - dist / 3.2;
            var pos = origin + back * dist + new Vec3d((rng.NextDouble() - 0.5) * 0.5, rng.NextDouble() * 0.35, (rng.NextDouble() - 0.5) * 0.5);
            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = pos,
                AddPos = new Vec3d(0.04, 0.04, 0.04),
                MinVelocity = new Vec3f((float)(back.X * (1.8 + fade)), 0.18f, (float)(back.Z * (1.8 + fade))),
                AddVelocity = new Vec3f(0.45f, 0.3f, 0.45f),
                LifeLength = 0.14f + (float)rng.NextDouble() * 0.16f,
                MinSize = 0.08f,
                MaxSize = 0.22f,
                GravityEffect = -0.12f,
                Color = ColorUtil.ColorFromRgba(10 + rng.Next(65), 110 + rng.Next(110), 255, (int)(165 + 60 * fade)),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true
            });
        }
    }
}
