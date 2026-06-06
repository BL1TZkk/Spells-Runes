using System;
using System.Collections.Generic;
using SpellsAndRunes.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SpellsAndRunes.Spells.Air;

public class WindyDash : Spell
{
    private const string StatId = "spellsandrunes:air_windy_dash";

    public override string Id          => "air_windy_dash";
    public override string Name        => "Windy Dash";
    public override string Description => "Wraps the caster in a burst of wind, propelling them forward in an instant and carrying their steps for a short time.";

    public override SpellTier    Tier    => SpellTier.Novice;
    public override SpellElement Element => SpellElement.Air;
    public override SpellType    Type    => SpellType.Offense;

    public override float FluxCost => 20f;
    public override float CastTime => 0.4f;

    public override string? AnimationCode        => "air_windy_dash";
    public override bool    AnimationTakesOverBody => true;

    public override IReadOnlyList<string> Prerequisites => ["air_feather_fall"];

    public override (int col, int row) TreePosition => (0, 3);

    public const float ForwardForce = 0.75f;
    public const float DurationSeconds = 3f;
    public const float SpeedMultiplier = 1.18f;

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
        var lookDir = caster.Pos.GetViewVector().ToVec3d().Normalize();
        float force = ForwardForce * GetRangeMultiplier(spellLevel);
        caster.Pos.Motion.Set(lookDir * force);

        ApplyMovementBoost(caster, world, spellLevel);
        SpawnFx(world, caster.Pos.XYZ.Add(0, 0.5, 0), lookDir, spellLevel);
    }

    private static void ApplyMovementBoost(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
        if (world.Api == null) return;

        float duration = DurationSeconds + 0.25f * (spellLevel - 1);
        float multiplier = SpeedMultiplier + 0.015f * (spellLevel - 1);
        caster.Stats.Set("walkspeed", StatId, multiplier, true);

        world.Api.Event.RegisterCallback(_ =>
        {
            if (caster.Alive) caster.Stats.Remove("walkspeed", StatId);
        }, (int)(duration * 1000));

        if (world.Api is ICoreServerAPI sapi)
        {
            var channel = sapi.Network.GetChannel("spellsandrunes");
            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp || sp.Entity?.EntityId != caster.EntityId) continue;
                channel.SendPacket(new MsgMovementBoost
                {
                    StatId = StatId,
                    Multiplier = multiplier,
                    DurationMs = (int)(duration * 1000),
                }, sp);
                break;
            }
        }
    }

    public static void SpawnFx(IWorldAccessor world, Vec3d origin, Vec3d lookDir, int spellLevel = 1)
    {
        int mult = 1 + (spellLevel - 1) / 4;
        var rng = world.Rand;
        Vec3d right = lookDir.Cross(new Vec3d(0, 1, 0)).Normalize();

        for (int i = 0; i < 5 * mult; i++)
        {
            double bladeOffset = (i - (5 * mult - 1) * 0.5) * 0.12;
            for (int s = 0; s < 26; s++)
            {
                double t = s / 25.0;
                var pos = origin + right * bladeOffset + lookDir * (t * 2.2) + new Vec3d(0, (rng.NextDouble() - 0.5) * 0.08, 0);
                world.SpawnParticles(new SimpleParticleProperties
                {
                    MinQuantity = 1,
                    AddQuantity = 0,
                    Color = ColorUtil.ColorFromRgba(215, 242, 255, 160 + rng.Next(70)),
                    MinPos = pos,
                    AddPos = new Vec3d(0.012, 0.012, 0.012),
                    MinVelocity = new Vec3f(
                        (float)(lookDir.X * (9.5 + rng.NextDouble() * 2.4)),
                        (float)((rng.NextDouble() - 0.5) * 0.12),
                        (float)(lookDir.Z * (9.5 + rng.NextDouble() * 2.4))),
                    AddVelocity = new Vec3f(0.05f, 0.05f, 0.05f),
                    LifeLength = 0.11f + (float)rng.NextDouble() * 0.05f,
                    MinSize = 0.08f,
                    MaxSize = 0.2f,
                    GravityEffect = 0f,
                    ParticleModel = EnumParticleModel.Quad,
                    WithTerrainCollision = false,
                    ShouldDieInLiquid = false
                });
            }
        }
    }
}
