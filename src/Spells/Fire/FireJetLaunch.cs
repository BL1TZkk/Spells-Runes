using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace SpellsAndRunes.Spells.Fire;

public class FireJetLaunch : Spell
{
    public override string Id => "fire_jet_launch";
    public override string Name => "Fire Jet";
    public override string Description => "Launches upward on firepower, then lets you hover while held in the air.";

    public override SpellTier Tier => SpellTier.Adept;
    public override SpellElement Element => SpellElement.Fire;
    public override SpellType Type => SpellType.Enchantment;

    public override float FluxCost => 33f;
    public override float CastTime => 0.5f;

    public override string? AnimationCode => "fire_jet_launch";
    public override bool AnimationTakesOverBody => true;

    public override IReadOnlyList<string> Prerequisites => ["fire_mine"];
    public override (int col, int row) TreePosition => (0, 6);

    public const float LaunchUpForce = 0.5f;
    public const float LaunchAnimationDelaySeconds = 10f / 30f;
    public const float HoverLift = 0.040f;
    public const float HoverRiseForce = 0.105f;
    public const float HoverSinkForce = -0.075f;
    public const float HoverForwardForce = 0.03f;

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
    }

    public override void OnTick(EntityAgent caster, IWorldAccessor world, float deltaTime, int spellLevel = 1)
    {
        if (caster.OnGround) return;

        var lookDir = caster.Pos.GetViewVector().ToVec3d().Normalize();
        double vertical = caster.Controls.ShiftKey ? HoverSinkForce : caster.Controls.Jump ? HoverRiseForce : HoverLift;
        float rangeMul = GetRangeMultiplier(spellLevel);

        caster.Pos.Motion.Set(
            lookDir.X * HoverForwardForce * rangeMul,
            GameMath.Clamp(vertical, -0.16, 0.18),
            lookDir.Z * HoverForwardForce * rangeMul);
    }

    public static void SpawnFx(IWorldAccessor world, Vec3d origin, Vec3d lookDir, int spellLevel)
    {
        var rng = world.Rand;
        int mult = 1 + (spellLevel - 1) / 4;
        Vec3d down = new Vec3d(0, -1, 0);

        for (int i = 0; i < 34 * mult; i++)
        {
            double a = rng.NextDouble() * GameMath.TWOPI;
            double r = rng.NextDouble() * 0.45;
            Vec3d pos = origin.AddCopy(Math.Cos(a) * r, 0.05, Math.Sin(a) * r);
            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = pos,
                AddPos = new Vec3d(0.04, 0.04, 0.04),
                MinVelocity = new Vec3f((float)(Math.Cos(a) * 0.8), -2.8f - (float)rng.NextDouble() * 2.2f, (float)(Math.Sin(a) * 0.8)),
                AddVelocity = new Vec3f(0.3f, 0.2f, 0.3f),
                LifeLength = 0.22f + (float)rng.NextDouble() * 0.18f,
                MinSize = 0.12f,
                MaxSize = 0.36f,
                GravityEffect = -0.15f,
                Color = ColorUtil.ColorFromRgba(15 + rng.Next(70), 95 + rng.Next(130), 255, 220),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true
            });
        }
    }
}
