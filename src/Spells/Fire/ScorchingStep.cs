using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using SpellsAndRunes.Network;

namespace SpellsAndRunes.Spells.Fire;

public class ScorchingStep : Spell
{
    private const string StatId = "spellsandrunes:fire_scorching_step";

    public override string Id => "fire_scorching_step";
    public override string Name => "Scorching Step";
    public override string Description => "Boosts your movement speed for a short time.";

    public override SpellTier Tier => SpellTier.Apprentice;
    public override SpellElement Element => SpellElement.Fire;
    public override SpellType Type => SpellType.Enchantment;

    public override float FluxCost => 24f;
    public override float CastTime => 0.35f;

    public override string? AnimationCode => "fire_scorching_step";
    public override bool AnimationTakesOverBody => true;

    public override IReadOnlyList<string> Prerequisites => ["fire_fist", "fire_orb"];
    public override (int col, int row) TreePosition => (1, 3);

    public const float DurationSeconds = 7f;
    public const float SpeedMultiplier = 1.45f;

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
        if (world.Api == null) return;

        float duration = DurationSeconds + 0.5f * (spellLevel - 1);
        float multiplier = SpeedMultiplier + 0.04f * (spellLevel - 1);
        caster.Stats.Set("walkspeed", StatId, multiplier, true);

        world.Api.Event.RegisterCallback(_ =>
        {
            if (caster.Alive) caster.Stats.Remove("walkspeed", StatId);
        }, (int)(duration * 1000));

        float elapsed = 0f;
        long fxListenerId = 0;
        fxListenerId = world.Api.Event.RegisterGameTickListener(dt =>
        {
            elapsed += dt;
            if (elapsed >= duration || !caster.Alive)
            {
                world.Api.Event.UnregisterGameTickListener(fxListenerId);
                return;
            }

            FireOrb.BroadcastFx(world, Id, caster.Pos.XYZ.Add(0, 0.04, 0), caster.Pos.GetViewVector().ToVec3d().Normalize(), spellLevel);
        }, 120);

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

        FireOrb.BroadcastFx(world, Id, caster.Pos.XYZ.Add(0, 0.1, 0), caster.Pos.GetViewVector().ToVec3d().Normalize(), spellLevel);
    }

    public static void SpawnFx(IWorldAccessor world, Vec3d origin, Vec3d lookDir, int spellLevel)
    {
        var rng = world.Rand;
        int mult = 1 + (spellLevel - 1) / 4;
        Vec3d forward = new Vec3d(lookDir.X, 0, lookDir.Z);
        if (forward.LengthSq() < 0.001) forward = new Vec3d(0, 0, 1);
        forward.Normalize();
        Vec3d right = forward.Cross(new Vec3d(0, 1, 0)).Normalize();

        for (int foot = -1; foot <= 1; foot += 2)
        {
            Vec3d footBase = origin + right * (foot * 0.22) - forward * 0.08;
            for (int i = 0; i < 10 * mult; i++)
            {
                double trail = rng.NextDouble() * 0.55;
                double side = (rng.NextDouble() - 0.5) * 0.18;
                Vec3d pos = footBase - forward * trail + right * side + new Vec3d(0, rng.NextDouble() * 0.18, 0);
                world.SpawnParticles(new SimpleParticleProperties
                {
                    MinQuantity = 1,
                    AddQuantity = 0,
                    MinPos = pos,
                    AddPos = new Vec3d(0.035, 0.025, 0.035),
                    MinVelocity = new Vec3f((float)(-forward.X * 0.35), 0.16f, (float)(-forward.Z * 0.35)),
                    AddVelocity = new Vec3f(0.18f, 0.28f, 0.18f),
                    LifeLength = 0.18f + (float)rng.NextDouble() * 0.18f,
                    MinSize = 0.055f,
                    MaxSize = 0.16f,
                    GravityEffect = -0.12f,
                    Color = ColorUtil.ColorFromRgba(20 + rng.Next(65), 100 + rng.Next(120), 255, 215),
                    ParticleModel = EnumParticleModel.Quad,
                    WithTerrainCollision = false,
                    ShouldDieInLiquid = true
                });
            }
        }
    }
}
