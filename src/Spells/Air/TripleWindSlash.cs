using System.Collections.Generic;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using SpellsAndRunes.Network;

namespace SpellsAndRunes.Spells.Air;

public class TripleWindSlash : Spell
{
    private const int FirstReleaseFrame = 9;
    private static readonly int[] ReleaseFrames = { 9, 17, 25 };

    public override string Id          => "air_triple_wind_slash";
    public override string Name        => "Triple Wind Slash";
    public override string Description => "Shapes three wind blades in quick succession and launches them forward, each following the last.";

    public override SpellTier    Tier    => SpellTier.Apprentice;
    public override SpellElement Element => SpellElement.Air;
    public override SpellType    Type    => SpellType.Offense;

    public override float FluxCost => 45f;
    public override float CastTime => FirstReleaseFrame / 30f;
    public override string? AnimationCode => "air_triple_wind_slash";
    public override bool AnimationTakesOverBody => true;

    public override IReadOnlyList<string> Prerequisites => ["air_wind_slash"];

    public override (int col, int row) TreePosition => (2, 4);

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
        var api = world.Api;
        if (api == null) return;

        float range = WindSlash.Range * GetRangeMultiplier(spellLevel) * 1.1f;
        float damage = WindSlash.Damage * GetDamageMultiplier(spellLevel) * 0.8f;
        float castTimeMultiplier = GetCastTimeMultiplier(spellLevel);

        for (int i = 0; i < ReleaseFrames.Length; i++)
        {
            int delayMs = FrameToMs(ReleaseFrames[i] - FirstReleaseFrame, castTimeMultiplier);
            api.Event.RegisterCallback(_ =>
            {
                if (!caster.Alive) return;
                var lookDir = caster.Pos.GetViewVector().ToVec3d().Normalize();
                var fxOrigin = caster.Pos.XYZ.Add(0, 0, 0);
                var hitOrigin = caster.Pos.XYZ.Add(0, caster.LocalEyePos.Y - 0.1, 0);
                var origin = fxOrigin;
                if (world.Side == EnumAppSide.Server && world.Api != null)
                {
                    WindSlash.StartDamageSweep(caster, world, hitOrigin, lookDir, range, WindSlash.HitRadius, damage, replaceExisting: false);
                    if (WindSlash.DebugHitboxEnabled)
                    {
                        WindSlash.StartHitboxSweepDebug(caster, world, hitOrigin, lookDir, range, WindSlash.HitRadius, replaceExisting: false);
                    }

                    if (world.Api is ICoreServerAPI sapi)
                    {
                        var fxMsg = new MsgSpellFx
                        {
                            SpellId = "air_wind_slash",
                            OriginX = (float)origin.X,
                            OriginY = (float)origin.Y,
                            OriginZ = (float)origin.Z,
                            LookDirX = (float)lookDir.X,
                            LookDirY = (float)lookDir.Y,
                            LookDirZ = (float)lookDir.Z,
                            SpellLevel = spellLevel
                        };
                        var channel = sapi.Network.GetChannel("spellsandrunes");
                        foreach (var p in sapi.World.AllOnlinePlayers)
                            channel.SendPacket(fxMsg, p as IServerPlayer);
                    }
                }

            }, delayMs);
        }
    }

    private static int FrameToMs(int frame, float castTimeMultiplier)
        => (int)Math.Round(frame / 30.0 * 1000.0 * castTimeMultiplier);
}
