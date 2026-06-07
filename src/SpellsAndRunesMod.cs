using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using SpellsAndRunes.Blocks;
using SpellsAndRunes.Commands;
using SpellsAndRunes.Flux;
using SpellsAndRunes.HUD;
using SpellsAndRunes.GUI;
using SpellsAndRunes.Lore;
using SpellsAndRunes.Spells;
using SpellsAndRunes.Network;
using SpellsAndRunes.Render;

namespace SpellsAndRunes;

public class SpellsAndRunesMod : ModSystem
{
    public static bool DebugHitboxesEnabled;
    private sealed class ActiveChannelSpell
    {
        public string SpellId { get; init; } = "";
        public int SpellLevel { get; init; }
        public float DrainTimer { get; set; }
        public bool FxEnabled { get; set; } = true;
    }

    private sealed class PendingCast
    {
        public string SpellId { get; init; } = "";
        public long TaskId { get; init; }
    }

    private HudFlux? hudFlux;
    private HudCastBar? castBar;
    private HudRadialMenu? radialMenu;
    private HudChickenCounter? hudChicken;
    private GuiDialogSpellbook? spellbookDialog;
    private SpellConeRenderer?  coneRenderer;
    private FireGlowRenderer?      fireGlow;
    public  SylphweedGlowRenderer? SylphGlow { get; private set; }
    public  IdleAnimatedBlockRenderer? IdleAnim { get; private set; }
    private long clientSpellDataListenerEntityId = -1;

    private IClientNetworkChannel?  clientChannel;
    private IServerNetworkChannel? serverChannel;
    private readonly Dictionary<long, ActiveChannelSpell> activeChannelSpells = new();
    private readonly Dictionary<long, PendingCast> pendingCasts = new();
    private readonly Dictionary<long, float> ignisPasteDrinkHold = new();
    private const float IgnisPasteDrinkTime = 4f;
    private readonly Dictionary<string, long> recentFxReceipts = new();
    private readonly Dictionary<string, long> recentSpellReceipts = new();
    private readonly Dictionary<string, long> recentSpellSoundReceipts = new();
    private readonly Dictionary<string, ILoadedSound> activeChannelSounds = new();
    private readonly Dictionary<string, long> channelSoundLastMs = new();
    private string? heldChannelSpellId;
    private bool lmbHoldActive;
    private string? lmbHoldSpellId;
    private string? activeCastSpellId;
    private long clientMovementLockUntilMs;
    private long clientMovementLockTickId = -1;
    private bool clientMovementLockVertical;

    private const string ChannelName = "spellsandrunes";
    private const string ChickenKillFileName = "chicken-kills.json";
    private const string CastMovementLockStatId = "spellsandrunes:castmovementlock";
    private const string CastMovementSlowStatId = "spellsandrunes:castmovementslow";
    private int chickenKills;
    private string? chickenKillPath;
    private bool chickenKillsLoaded;

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass("fluxBehavior", typeof(EntityBehaviorFlux));
        api.RegisterBlockClass("IgnisFragment", typeof(Blocks.BlockIgnisFragment));
        api.RegisterBlockEntityClass("ignisfragment", typeof(Blocks.BlockEntityIgnisFragment));
        api.RegisterBlockEntityClass("sylphweed", typeof(Blocks.BlockEntitySylphweed));
        api.RegisterItemClass("SylphweedBong", typeof(Blocks.ItemSylphweedBong));
        api.RegisterItemClass("IgnisGemCore", typeof(Blocks.ItemIgnisGemCore));
        api.RegisterItemClass("IgnisPaste", typeof(Blocks.ItemIgnisPaste));
        api.RegisterItemClass("Scroll", typeof(Items.ItemScroll));
        api.RegisterItemClass("FluxCharger", typeof(Items.ItemFluxCharger));
        api.RegisterEntity("EntityWindSpear", typeof(Entities.EntityWindSpear));
        api.RegisterEntity("EntityFireMine", typeof(Entities.EntityFireMine));
        api.RegisterEntity("EntityWindClone", typeof(Entities.EntityWindClone));
        api.RegisterCollectibleBehaviorClass("ExtractGemCore", typeof(CollBehaviorExtractGemCore));
        SpellRegistry.RegisterAll();
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
#if DEBUG
        api.Logger.Notification("[Spells & Runes] Server side loaded.");
#endif
        SpellbookLoreRegistry.Load(api);
        DebugCommands.Register(api);
        InitChickenKillCounter(api);
        api.Event.OnEntityDespawn += (entity, despawnData) =>
        {
            if (entity == null) return;
            bool isDeath = despawnData?.Reason == EnumDespawnReason.Death;
            if (!isDeath && despawnData?.DamageSourceForDeath == null && (entity as EntityAgent)?.Alive != false)
                return;
            CountChickenDeath(api, entity);
        };

        serverChannel = ServerChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<MsgUnlockSpell>()
            .RegisterMessageType<MsgSetHotbarSlot>()
            .RegisterMessageType<MsgCastSpell>()
            .RegisterMessageType<MsgChannelSpell>()
            .RegisterMessageType<MsgStartCast>()
            .RegisterMessageType<MsgSpellFx>()
            .RegisterMessageType<MsgFreezeMotion>()
            .RegisterMessageType<MsgMovementLock>()
            .RegisterMessageType<MsgLaunchPlayer>()
            .RegisterMessageType<MsgMovementBoost>()
            .RegisterMessageType<MsgPlayAnimation>()
            .RegisterMessageType<MsgCancelCast>()
            .RegisterMessageType<MsgChickenKills>()
            .RegisterMessageType<MsgReadScroll>()
            .SetMessageHandler<MsgReadScroll>((player, msg) =>
            {
                if (string.IsNullOrEmpty(msg.ScrollId)) return;
                var data = PlayerSpellData.For(player.Entity);
                bool isFirst = !data.GetUnlockedLoreEntryIds().Any();
                data.UnlockLoreEntry(msg.ScrollId);
                if (isFirst)
                    data.UnlockLoreEntry("journal-general-1");
            })
            .SetMessageHandler<MsgUnlockSpell>((player, msg) =>
            {
                var entity = player.Entity;
                if (entity == null) return;
                var data = PlayerSpellData.For(entity);
                SpellTree.TryUnlock(msg.SpellId, data);
            })
            .SetMessageHandler<MsgSetHotbarSlot>((player, msg) =>
            {
                var entity = player.Entity;
                if (entity == null) return;
                var data = PlayerSpellData.For(entity);
                data.SetHotbarSlot(msg.Slot, string.IsNullOrEmpty(msg.SpellId) ? null : msg.SpellId);
            })
            .SetMessageHandler<MsgChannelSpell>((player, msg) =>
            {
                var entity = player.Entity;
                if (entity == null) return;

                var spell = SpellRegistry.Get(msg.SpellId);
                if (spell == null || !IsChannelSpell(msg.SpellId)) return;

                if (msg.IsActive)
                {
                    var data = PlayerSpellData.For(entity);
                    if (!data.IsUnlocked(spell.Id) || !SpellTree.HasRequiredFluxAlignment(spell, data)) return;
                    StartChannelSpell(api, player, entity, spell, msg.SpellLevel);
                }
                else StopChannelSpell(api, entity, msg.SpellId, collapse: true);
            })
            .SetMessageHandler<MsgCastSpell>((player, msg) =>
            {
                var entity = player.Entity;
                if (entity == null) return;

                var spell = SpellRegistry.Get(msg.SpellId);
                if (spell == null) return;

                var data = PlayerSpellData.For(entity);
                if (!data.IsUnlocked(spell.Id) || !SpellTree.HasRequiredFluxAlignment(spell, data)) return;

                int spellLevel = data.GetSpellLevel(msg.SpellId);
                float scaledFluxCost = spell.FluxCost * spell.GetFluxCostMultiplier(spellLevel);

                float currentFlux = entity.WatchedAttributes.GetFloat("spellsandrunes:flux", 0f);
#if DEBUG
                api.Logger.Notification($"[SnR] Cast '{msg.SpellId}' lvl {spellLevel}: flux={currentFlux} cost={scaledFluxCost:F1}");
#endif
                if (currentFlux < scaledFluxCost)
                {
#if DEBUG
                    api.Logger.Notification($"[SnR] Flux check FAILED ({currentFlux} < {scaledFluxCost:F1})");
#endif
                    return;
                }
                entity.WatchedAttributes.SetFloat("spellsandrunes:flux", currentFlux - scaledFluxCost);
                entity.WatchedAttributes.MarkPathDirty("spellsandrunes:flux");

                // Notify casting client of cast start with scaled cast time
                float castTimeMultiplier = spell.GetCastTimeMultiplier(spellLevel);
                float scaledCastTime = spell.CastTime * castTimeMultiplier;
                serverChannel?.SendPacket(new MsgStartCast { SpellId = msg.SpellId, CastTime = scaledCastTime }, player);
                ApplyCastMovementControl(player, msg.SpellId, (int)(scaledCastTime * 1000));

                // Broadcast animation to all clients
                if (spell.AnimationCode != null)
                {
                    var animMsg = new MsgPlayAnimation
                    {
                        EntityId       = entity.EntityId,
                        AnimationCode  = spell.AnimationCode,
                        TakesOverBody  = spell.AnimationTakesOverBody,
                        AnimationSpeed = castTimeMultiplier > 0f ? spell.AnimationSpeed / castTimeMultiplier : spell.AnimationSpeed,
                    };
                    foreach (var p in api.World.AllOnlinePlayers)
                        serverChannel?.SendPacket(animMsg, p as IServerPlayer);
                }

                // Schedule execution after cast time completes (in milliseconds)
                int delayMs = (int)(scaledCastTime * 1000);
                if (pendingCasts.TryGetValue(entity.EntityId, out var pending))
                {
                    api.Event.UnregisterGameTickListener(pending.TaskId);
                    pendingCasts.Remove(entity.EntityId);
                }
                long taskId = 0;
                taskId = api.Event.RegisterGameTickListener(dt =>
                {
                    api.Event.UnregisterGameTickListener(taskId);

                    if (!entity.Alive)
                    {
                        pendingCasts.Remove(entity.EntityId);
                        return;
                    }

                    if (pendingCasts.TryGetValue(entity.EntityId, out var pendingNow) && pendingNow.TaskId != taskId)
                    {
                        return;
                    }
                    pendingCasts.Remove(entity.EntityId);

                    bool hit = spell.TryCast(entity, api.World, spellLevel);

                    // Broadcast FX packet with spell level for scaling
                    var fxMsg = new MsgSpellFx
                    {
                        SpellId     = msg.SpellId,
                        OriginX     = entity.Pos.X,
                        OriginY     = entity.Pos.Y,
                        OriginZ     = entity.Pos.Z,
                        LookDirX    = entity.Pos.GetViewVector().X,
                        LookDirY    = entity.Pos.GetViewVector().Y,
                        LookDirZ    = entity.Pos.GetViewVector().Z,
                        SpellLevel  = spellLevel,
                    };
                    if (ShouldBroadcastCastFx(msg.SpellId))
                    {
                        foreach (var p in api.World.AllOnlinePlayers)
                            serverChannel?.SendPacket(fxMsg, p as IServerPlayer);
                    }

                    // Base XP always, hit bonus if spell connected
                    data.AddSpellXp(msg.SpellId, spell.XpPerCast + (hit ? spell.XpPerCast / 2 : 0));
                    data.AddElementXp(spell.Element, spell.ElementXpPerCast + (hit ? 2 : 0));

                    // Spell-specific packets to the casting player only
                    if (msg.SpellId == "air_feather_fall")
                        serverChannel?.SendPacket(new MsgFreezeMotion { NudgeY = 0.06f }, player);

                    if (msg.SpellId == "air_windy_dash")
                    {
                        var look = entity.Pos.GetViewVector();
                        serverChannel?.SendPacket(new MsgLaunchPlayer
                        {
                            UpForce = 0f,
                            ForwardForce = Spells.Air.WindyDash.ForwardForce * spell.GetRangeMultiplier(spellLevel),
                            LookDirX = look.X,
                            LookDirY = look.Y,
                            LookDirZ = look.Z,
                            UseLookY = true
                        }, player);
                    }

                    if (msg.SpellId == "fire_back_blast_dash")
                    {
                        var look = entity.Pos.GetViewVector();
                        serverChannel?.SendPacket(new MsgLaunchPlayer
                        {
                            UpForce = 0.03f,
                            ForwardForce = Spells.Fire.FireBackBlastDash.ForwardForce * spell.GetRangeMultiplier(spellLevel),
                            LookDirX = look.X,
                            LookDirY = 0f,
                            LookDirZ = look.Z,
                            UseLookY = false
                        }, player);
                    }

                    if (msg.SpellId == "air_updraft")
                    {
                        var look = entity.Pos.GetViewVector();
                        serverChannel?.SendPacket(new MsgLaunchPlayer
                        {
                            UpForce = Spells.Air.Updraft.UpForce,
                            ForwardForce = Spells.Air.Updraft.ForwardForce * spell.GetRangeMultiplier(spellLevel),
                            LookDirX = look.X,
                            LookDirY = 0f,
                            LookDirZ = look.Z,
                            UseLookY = false
                        }, player);
                    }

                    if (msg.SpellId is "air_wind_step" or "air_cloning_wind_step")
                    {
                        var look = entity.Pos.GetViewVector();
                        serverChannel?.SendPacket(new MsgLaunchPlayer
                        {
                            UpForce = Math.Max((float)entity.Pos.Motion.Y, 0.05f),
                            ForwardForce = Spells.Air.WindStep.ForwardForce * spell.GetRangeMultiplier(spellLevel),
                            LookDirX = look.X,
                            LookDirY = 0f,
                            LookDirZ = look.Z,
                            UseLookY = false
                        }, player);
                    }

                    if (msg.SpellId == "air_air_kick")
                    {
                        var look = entity.Pos.GetViewVector();
                        serverChannel?.SendPacket(new MsgLaunchPlayer
                        {
                            UpForce      = Spells.Air.AirKick.UpForce,
                            ForwardForce = Spells.Air.AirKick.ForwardForce,
                            LookDirX     = look.X,
                            LookDirY     = 0f,
                            LookDirZ     = look.Z,
                            UseLookY     = false
                        }, player);

                        // Play windball follow-up animation after a short delay (player in air)
                        api.Event.RegisterCallback(_ =>
                        {
                            if (!entity.Alive) return;
                            var windballAnim = new MsgPlayAnimation
                            {
                                EntityId      = entity.EntityId,
                                AnimationCode = "air_wind_kick_windball",
                                TakesOverBody = true,
                                AnimationSpeed = 1f,
                            };
                            foreach (var p in api.World.AllOnlinePlayers)
                                serverChannel?.SendPacket(windballAnim, p as IServerPlayer);
                        }, 300);

                        // Delay projectile until player reaches apex (~0.5s after launch)
                        Vec3d projLook = look.ToVec3d().Normalize();
                        Vec3d projPos  = null!;
                        float delay    = 0.5f;
                        float delayAcc = 0f;
                        bool  started  = false;

                        long delayId = 0;
                        delayId = api.Event.RegisterGameTickListener(ddt =>
                        {
                            delayAcc += ddt;
                            if (delayAcc < delay) return;
                            api.Event.UnregisterGameTickListener(delayId);
                            // capture look at launch time
                            projLook = entity.Pos.GetViewVector().ToVec3d().Normalize();
                            projPos = entity.Pos.XYZ.Add(0, 1.8, 0); // spawn at head level
                            started = true;
                        }, 50);

                        // Server-side projectile simulation (starts after delay)
                        float traveled = 0f;
                        hit      = false;
                        long  lid      = 0;
                        const float stepDt   = 0.02f;
                        float rangeMul = spell.GetRangeMultiplier(spellLevel);
                        float stepDist = Spells.Air.AirKick.ProjectileSpeed * stepDt * rangeMul;
                        float hitR     = Spells.Air.AirKick.ProjectileRadius * rangeMul;
                        float armingDist = 0f; // allow immediate hits

                        lid = api.Event.RegisterGameTickListener(dt =>
                        {
                            if (!started) return;
                            if (hit) { api.Event.UnregisterGameTickListener(lid); return; }

                            Vec3d prevPos = projPos;
                            projPos    = projPos.Add(projLook.X * stepDist, projLook.Y * stepDist, projLook.Z * stepDist);
                            traveled  += stepDist;

                            if (traveled > Spells.Air.AirKick.MaxRange * rangeMul)
                            {
                                api.Event.UnregisterGameTickListener(lid); return;
                            }

                            // Terrain collision
                            var block = api.World.BlockAccessor.GetBlock(new BlockPos((int)projPos.X, (int)projPos.Y, (int)projPos.Z, 0));
                            if (block != null && block.BlockId != 0 && !block.IsLiquid())
                            {
                                api.Event.UnregisterGameTickListener(lid); return;
                            }

                            // Broadcast projectile position FX each tick so clients see it moving
                            var trailFx = new MsgSpellFx
                            {
                                SpellId  = "air_air_kick_trail",
                                OriginX  = projPos.X, OriginY = projPos.Y, OriginZ = projPos.Z,
                                LookDirX = (float)projLook.X, LookDirY = (float)projLook.Y, LookDirZ = (float)projLook.Z,
                            };
                            foreach (var p in api.World.AllOnlinePlayers)
                                serverChannel?.SendPacket(trailFx, p as IServerPlayer);

                            var hitboxFx = new MsgSpellFx
                            {
                                SpellId = "air_air_kick_hitbox",
                                OriginX = projPos.X,
                                OriginY = projPos.Y,
                                OriginZ = projPos.Z,
                                LookDirX = (float)projLook.X,
                                LookDirY = (float)projLook.Y,
                                LookDirZ = (float)projLook.Z
                            };
                            foreach (var p in api.World.AllOnlinePlayers)
                                serverChannel?.SendPacket(hitboxFx, p as IServerPlayer);
                            // segment hit test (same idea as slash sweep)
                            api.World.GetEntitiesAround(projPos, hitR + 0.5f, hitR + 0.5f, e =>
                            {
                                if (hit) return false;
                                if (traveled < armingDist) return false;
                                if (e.EntityId == entity.EntityId) return false;
                                if (e is not EntityAgent) return false;
                                Vec3d targetPos = e.Pos.XYZ.Add(0, e.LocalEyePos.Y * 0.5, 0);
                                // quick sphere hit (fallback)
                                if (targetPos.DistanceTo(projPos) <= hitR || targetPos.DistanceTo(prevPos) <= hitR)
                                {
                                    hit = true;
                                    api.Event.UnregisterGameTickListener(lid);

                                    e.ReceiveDamage(new DamageSource
                                    {
                                        Source       = EnumDamageSource.Entity,
                                        SourceEntity = entity,
                                        Type         = EnumDamageType.BluntAttack,
                                    }, Spells.Air.AirKick.ImpactDamage);

                                    var impFxx = new MsgSpellFx
                                    {
                                        SpellId  = "air_air_kick",
                                        OriginX  = projPos.X, OriginY = projPos.Y, OriginZ = projPos.Z,
                                        LookDirX = (float)projLook.X, LookDirY = (float)projLook.Y, LookDirZ = (float)projLook.Z,
                                    };
                                    foreach (var p in api.World.AllOnlinePlayers)
                                        serverChannel?.SendPacket(impFxx, p as IServerPlayer);

                                    return false;
                                }

                                // closest point on segment
                                Vec3d toTarget = targetPos - prevPos;
                                Vec3d seg = projPos - prevPos;
                                double segLenSq = seg.LengthSq();
                                if (segLenSq < 0.0001) return false;
                                double t = toTarget.Dot(seg) / segLenSq;
                                if (t < 0 || t > 1) return false;
                                Vec3d closest = prevPos + seg * t;
                                if (targetPos.DistanceTo(closest) > hitR) return false;
                                hit = true;
                                api.Event.UnregisterGameTickListener(lid);

                                e.ReceiveDamage(new DamageSource
                                {
                                    Source       = EnumDamageSource.Entity,
                                    SourceEntity = entity,
                                    Type         = EnumDamageType.BluntAttack,
                                }, Spells.Air.AirKick.ImpactDamage);

                                var impFx = new MsgSpellFx
                                {
                                    SpellId  = "air_air_kick",
                                    OriginX  = projPos.X, OriginY = projPos.Y, OriginZ = projPos.Z,
                                    LookDirX = (float)projLook.X, LookDirY = (float)projLook.Y, LookDirZ = (float)projLook.Z,
                                };
                                foreach (var p in api.World.AllOnlinePlayers)
                                    serverChannel?.SendPacket(impFx, p as IServerPlayer);

                                return false;
                            });
                        }, (int)(stepDt * 1000));
                    }

                    // Broadcast FX to all nearby clients
                    var origin  = GetSpellFxOrigin(entity, msg.SpellId);
                    var lookDir = entity.Pos.GetViewVector();
                    var fx = new MsgSpellFx
                    {
                        SpellId  = msg.SpellId,
                        OriginX  = origin.X, OriginY = origin.Y, OriginZ = origin.Z,
                        LookDirX = lookDir.X, LookDirY = lookDir.Y, LookDirZ = lookDir.Z,
                    };
                    foreach (var p in api.World.AllOnlinePlayers)
                        serverChannel?.SendPacket(fx, p as IServerPlayer);
                }, delayMs);

                pendingCasts[entity.EntityId] = new PendingCast
                {
                    SpellId = msg.SpellId,
                    TaskId = taskId
                };
            })
            .SetMessageHandler<MsgCancelCast>((player, msg) =>
            {
                var entity = player.Entity;
                if (entity == null) return;
                if (!pendingCasts.TryGetValue(entity.EntityId, out var pending)) return;
                if (!string.IsNullOrEmpty(msg.SpellId) && pending.SpellId != msg.SpellId) return;

                api.Event.UnregisterGameTickListener(pending.TaskId);
                pendingCasts.Remove(entity.EntityId);
                ClearCastMovementControl(player);

                var cancel = new MsgCancelCast
                {
                    EntityId = entity.EntityId,
                    SpellId = pending.SpellId
                };
                foreach (var p in api.World.AllOnlinePlayers)
                    serverChannel?.SendPacket(cancel, p as IServerPlayer);
            });

        api.Event.PlayerJoin += player =>
        {
            if (player is IServerPlayer sp)
                SendChickenKills(sp);

            // Restore or clear FluxCharger debuff after reconnect
            if (player.Entity is { } joinEntity)
            {
                long endTime = joinEntity.WatchedAttributes.GetLong(Items.ItemFluxCharger.AttrDebuffEnd, 0);
                if (endTime > 0)
                {
                    long remaining = endTime - api.World.ElapsedMilliseconds;
                    if (remaining <= 0)
                        Items.ItemFluxCharger.RemoveDebuff(joinEntity);
                    else
                    {
                        float maxFlux = joinEntity.WatchedAttributes.GetFloat("snr:fluxoverload_maxflux", 0f);
                        Items.ItemFluxCharger.ApplyDebuff(joinEntity, maxFlux, (int)remaining);
                    }
                }
            }
        };

        api.Event.RegisterGameTickListener(dt => TickChannelSpells(api, dt), 100);
        api.Event.RegisterGameTickListener(dt => TickIgnisPasteDrink(api, dt), 100);
        api.Event.PlayerDisconnect += player =>
        {
            if (player?.Entity != null) ignisPasteDrinkHold.Remove(player.Entity.EntityId);
        };
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
#if DEBUG
        api.Logger.Notification("[Spells & Runes] Client side loaded.");
#endif
        SpellbookLoreRegistry.Load(api);

        clientChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<MsgUnlockSpell>()
            .RegisterMessageType<MsgSetHotbarSlot>()
            .RegisterMessageType<MsgCastSpell>()
            .RegisterMessageType<MsgChannelSpell>()
            .RegisterMessageType<MsgStartCast>()
            .RegisterMessageType<MsgSpellFx>()
            .RegisterMessageType<MsgFreezeMotion>()
            .RegisterMessageType<MsgMovementLock>()
            .RegisterMessageType<MsgLaunchPlayer>()
            .RegisterMessageType<MsgMovementBoost>()
            .RegisterMessageType<MsgPlayAnimation>()
            .RegisterMessageType<MsgCancelCast>()
            .RegisterMessageType<MsgChickenKills>()
            .RegisterMessageType<MsgReadScroll>()
            .SetMessageHandler<MsgPlayAnimation>(msg =>
            {
                var entity = api.World.GetEntityById(msg.EntityId) as EntityAgent;
                if (entity == null) return;
                SpellAnimations.Play(entity, msg.AnimationCode, msg.TakesOverBody, msg.AnimationSpeed);
            })
            .SetMessageHandler<MsgCancelCast>(msg =>
            {
                var entity = api.World.GetEntityById(msg.EntityId) as EntityAgent;
                if (entity != null)
                {
                    var spell = SpellRegistry.Get(msg.SpellId);
                    if (!string.IsNullOrEmpty(spell?.AnimationCode))
                        SpellAnimations.Stop(entity, spell.AnimationCode!);
                    if (msg.SpellId == "fire_jet_launch")
                        SpellAnimations.Stop(entity, "fire_jet_flight");
                }

                if (api.World.Player?.Entity?.EntityId == msg.EntityId)
                {
                    castBar?.Cancel();
                    lmbHoldActive = false;
                    lmbHoldSpellId = null;
                    activeCastSpellId = null;
                }
            })
            .SetMessageHandler<MsgChickenKills>(msg =>
            {
                chickenKills = msg.Count;
                hudChicken?.SetCount(chickenKills);
            })
            .SetMessageHandler<MsgFreezeMotion>(msg =>
            {
                var entity = api.World.Player?.Entity;
                if (entity == null) return;
                entity.Pos.Motion.Set(0, msg.NudgeY, 0);
            })
            .SetMessageHandler<MsgMovementLock>(msg =>
            {
                if (msg.DurationMs <= 0)
                {
                    ClearClientMovementLock(api);
                    return;
                }

                var entity = api.World.Player?.Entity;
                if (entity == null) return;

                long untilMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + msg.DurationMs;
                clientMovementLockUntilMs = Math.Max(clientMovementLockUntilMs, untilMs);
                clientMovementLockVertical |= msg.LockVertical;
                entity.Stats.Set("walkspeed", CastMovementLockStatId, 0f, true);
                ApplyClientMovementLockFrame(entity);

                if (clientMovementLockTickId < 0)
                    clientMovementLockTickId = api.Event.RegisterGameTickListener(_ => TickClientMovementLock(api), 10);
            })
            .SetMessageHandler<MsgLaunchPlayer>(msg =>
            {
                var entity = api.World.Player?.Entity;
                if (entity == null) return;
                if (msg.UseLookY)
                {
                    var look = new Vec3d(msg.LookDirX, msg.LookDirY, msg.LookDirZ);
                    if (look.LengthSq() < 0.0001) look = new Vec3d(0, 0, 1);
                    look = look.Normalize();
                    var motion = new Vec3d(
                        look.X * msg.ForwardForce,
                        look.Y * msg.ForwardForce,
                        look.Z * msg.ForwardForce);
                    motion.Y += msg.UpForce;
                    entity.Pos.Motion.Set(motion);
                }
                else
                {
                    // Up burst first, then forward dash (horizontal)
                    entity.Pos.Motion.Set(
                        msg.LookDirX * msg.ForwardForce,
                        msg.UpForce,
                        msg.LookDirZ * msg.ForwardForce);
                }
            })
            .SetMessageHandler<MsgMovementBoost>(msg =>
            {
                var entity = api.World.Player?.Entity;
                if (entity == null || string.IsNullOrEmpty(msg.StatId)) return;
                if (msg.DurationMs <= 0)
                {
                    entity.Stats.Remove("walkspeed", msg.StatId);
                    return;
                }
                entity.Stats.Set("walkspeed", msg.StatId, msg.Multiplier, true);
                api.Event.RegisterCallback(_ =>
                {
                    if (api.World.Player?.Entity != null)
                        api.World.Player.Entity.Stats.Remove("walkspeed", msg.StatId);
                }, msg.DurationMs);
            })
            .SetMessageHandler<MsgSpellFx>(msg =>
            {
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (msg.SpellId == "air_wind_slash")
                {
                    if (recentSpellReceipts.TryGetValue(msg.SpellId, out long lastSpellMs) && nowMs - lastSpellMs < 150)
                    {
                        return;
                    }
                    recentSpellReceipts[msg.SpellId] = nowMs;
                }
                string fxKey = $"{msg.SpellId}|{msg.SpellLevel}|{msg.OriginX:F3}|{msg.OriginY:F3}|{msg.OriginZ:F3}|{msg.LookDirX:F3}|{msg.LookDirY:F3}|{msg.LookDirZ:F3}";
                if (recentFxReceipts.TryGetValue(fxKey, out long lastMs) && nowMs - lastMs < 120)
                {
                    return;
                }

                recentFxReceipts[fxKey] = nowMs;
                if (recentFxReceipts.Count > 64)
                {
                    string? staleKey = null;
                    foreach (var pair in recentFxReceipts)
                    {
                        if (nowMs - pair.Value > 1000)
                        {
                            staleKey = pair.Key;
                            break;
                        }
                    }
                    if (staleKey != null) recentFxReceipts.Remove(staleKey);
                }

                var origin  = new Vec3d(msg.OriginX, msg.OriginY, msg.OriginZ);
                var lookDir = new Vec3d(msg.LookDirX, msg.LookDirY, msg.LookDirZ).Normalize();
                PlaySpellSound(api, msg.SpellId, origin, msg.SpellLevel);
                switch (msg.SpellId)
                {
                    case "air_air_push":
                        Spells.Air.AirPush.SpawnWindParticles(api.World, origin, lookDir, msg.SpellLevel);
                        break;
                    case "air_windy_dash":
                        Spells.Air.WindyDash.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        break;
                    case "air_updraft":
                        Spells.Air.Updraft.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        break;
                    case "air_wind_slash":
                        Spells.Air.WindSlash.SpawnFx(api.World, origin, lookDir, msg.SpellLevel, 0.0);
                        break;
                    case "air_wind_clone":
                        Spells.Air.WindClone.SpawnFx(api.World, origin, msg.SpellLevel);
                        break;
                    case "air_storms_eye":
                        Spells.Air.StormsEye.SpawnFx(api.World, origin, msg.SpellLevel);
                        break;
                    case "air_stroms_eye":
                        Spells.Air.StormsEye.SpawnFx(api.World, origin, msg.SpellLevel);
                        break;
                    case "air_tornado":
                        Spells.Air.Tornado.SpawnFx(api.World, origin, msg.SpellLevel);
                        break;
                    case "air_wind_vortex":
                        Spells.Air.WindVortex.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        break;
                    case "air_wind_spear":
                        break;
                    case "air_feather_fall":
                        Spells.Air.FeatherFall.SpawnFx(api.World, origin);
                        break;
                    case "air_air_kick":
                        Spells.Air.AirKick.SpawnImpactFx(api.World, origin, lookDir);
                        break;
                    case "air_air_kick_trail":
                        Spells.Air.AirKick.SpawnTrailFx(api.World, origin, lookDir);
                        break;
                    case "air_air_kick_hitbox":
                        if (DebugHitboxesEnabled)
                            Spells.Air.AirKick.SpawnHitboxDebug(api.World, origin, Spells.Air.AirKick.ProjectileRadius);
                        break;
                    case "air_wind_step":
                        Spells.Air.WindyDash.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        break;
                    case "air_cloning_wind_step":
                        Spells.Air.WindyDash.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        Spells.Air.WindClone.SpawnFx(api.World, origin, msg.SpellLevel);
                        break;
                    case "fire_spark":
                        Spells.Fire.Spark.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, lookDir, Spells.Fire.Spark.Range, 18 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_flamethrower":
                        Spells.Fire.FireFlamethrower.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, lookDir, 3.6f, 12 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_hot_skin":
                        Spells.Fire.HotSkin.SpawnFx(api.World, origin, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, new Vec3d(0, 1, 0), 0.7f, 4 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_cook_in_hand":
                        Spells.Fire.CookInHand.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin.AddCopy(0, 0.7, 0), lookDir, 0.45f, 4 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_fist":
                        Spells.Fire.FireFist.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, lookDir, 1.6f, 18 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_back_blast_dash":
                        Spells.Fire.FireBackBlastDash.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, lookDir * -1, 1.6f, 14 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_mine_arm":
                        Spells.Fire.FireMine.SpawnArmFx(api.World, origin, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, new Vec3d(0, 1, 0), 0.55f, 8 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_mine_burst":
                        Spells.Fire.FireMine.SpawnBurstFx(api.World, origin, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, new Vec3d(0, 1, 0), 1.4f, 22 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_orb_trail":
                        Spells.Fire.FireOrb.SpawnTrailFx(api.World, origin, lookDir, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, lookDir, 0.45f, 5 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_orb_impact":
                        Spells.Fire.FireOrb.SpawnImpactFx(api.World, origin, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, new Vec3d(0, 1, 0), 0.8f, 16 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_orb_release_smoke":
                        Spells.Fire.FireOrb.SpawnReleaseSmokeFx(api.World, origin, lookDir, msg.SpellLevel);
                        break;
                    case "fire_dance_cone":
                        Spells.Fire.FireDance.SpawnConeFx(api.World, origin, lookDir, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, lookDir, 3.5f, 32 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_scorching_step":
                        Spells.Fire.ScorchingStep.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, lookDir * -1, 1.8f, 18 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_blast_trail":
                        Spells.Fire.FireBlast.SpawnTrailFx(api.World, origin, lookDir, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, lookDir, 0.65f, 8 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_blast_impact":
                        Spells.Fire.FireBlast.SpawnImpactFx(api.World, origin, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, new Vec3d(0, 1, 0), 1.0f, 18 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_blast_hand_smoke":
                        Spells.Fire.FireBlast.SpawnHandSmokeFx(api.World, origin, lookDir, msg.SpellLevel);
                        break;
                    case "fire_blast_hitbox":
                        if (DebugHitboxesEnabled)
                            Spells.Fire.FireBlast.SpawnHitboxDebug(api.World, origin, Spells.Fire.FireBlast.HitRadius);
                        break;
                    case "fire_wisp":
                        Spells.Fire.FireWisp.SpawnFx(api.World, origin, msg.SpellLevel);
                        fireGlow?.AddWispGlow(origin, 12 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_jet_launch":
                        Spells.Fire.FireJetLaunch.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        fireGlow?.AddFireGlow(origin, new Vec3d(0, -1, 0), 1.4f, 16 * (1 + (msg.SpellLevel - 1) / 4));
                        break;
                    case "fire_wall":
                        Spells.Fire.FireWall.SpawnFx(api.World, origin, lookDir, msg.SpellLevel);
                        AddFireWallGlow(origin, lookDir, msg.SpellLevel);
                        break;
                    case "fire_wall_hitbox":
                        if (DebugHitboxesEnabled)
                            Spells.Fire.FireWall.SpawnHitboxDebug(api.World, origin, lookDir);
                        break;
                }
            });

        // Client-side cast start notification
        clientChannel!.SetMessageHandler<MsgStartCast>(msg =>
        {
            castBar?.OnBeginCast(msg.SpellId, msg.CastTime);
            activeCastSpellId = msg.SpellId;
        });

        // Spellbook hotkey
        api.Input.RegisterHotKey("spellsandrunes.spellbook", "Open Spellbook", GlKeys.K, HotkeyType.GUIOrOtherControls);

        // Radial menu hotkey (hold R)
        api.Input.RegisterHotKey("spellsandrunes.radial", "Spell Radial Menu", GlKeys.R, HotkeyType.GUIOrOtherControls);


        radialMenu      = new HudRadialMenu(api);
        hudFlux         = new HudFlux(api, radialMenu);
        castBar         = new HudCastBar(api);
        hudChicken      = new HudChickenCounter(api);
        spellbookDialog = new GuiDialogSpellbook(api, clientChannel!);
        coneRenderer = new SpellConeRenderer(api, radialMenu);
        fireGlow     = new FireGlowRenderer(api);
        api.Event.RegisterRenderer(fireGlow, EnumRenderStage.AfterOIT, "fireglow");
        IdleAnim     = new IdleAnimatedBlockRenderer(api);

        api.Event.RegisterGameTickListener(_ => coneRenderer.OnGameTick(_), 50);
        api.Event.RegisterGameTickListener(_ => TickChannelSounds(), 250);

        // /snr debug — toggle all hitbox visualizations together
        api.Input.RegisterHotKey("spellsandrunes.debug", "Toggle Spell Debugs", GlKeys.F8, HotkeyType.GUIOrOtherControls);
        api.Input.SetHotKeyHandler("spellsandrunes.debug", _ =>
        {
            DebugHitboxesEnabled = !DebugHitboxesEnabled;
            coneRenderer.Enabled = DebugHitboxesEnabled;
            Spells.Air.WindSlash.DebugHitboxEnabled = DebugHitboxesEnabled;
            return true;
        });

        // Force HUD/spellbook redraw when WatchedAttributes arrive from server (spell data sync)
        AttachClientSpellDataListeners(api);
        api.Event.PlayerEntitySpawn += (player) =>
        {
            if (player != api.World.Player) return;
            AttachClientSpellDataListeners(api);
        };

        // Spellbook toggle
        api.Input.SetHotKeyHandler("spellsandrunes.spellbook", combo =>
        {
            if (spellbookDialog.IsOpened()) spellbookDialog.TryClose();
            else spellbookDialog.TryOpen();
            return true;
        });

        // Radial: open on press — also starts sprint-forward tick
        long sprintTickId = -1;
        var sprintHk = api.Input.GetHotKeyByCode("sprint");
        int sprintKey = sprintHk != null ? (int)sprintHk.CurrentMapping.KeyCode : (int)GlKeys.LShift;

        api.Input.SetHotKeyHandler("spellsandrunes.radial", combo =>
        {
            var entity = api.World.Player?.Entity;
            if (entity == null || !PlayerSpellData.For(entity).IsFluxUnlocked) return true;
            radialMenu.Open();
            sprintTickId = api.Event.RegisterGameTickListener(_ =>
            {
                var player = api.World.Player;
                if (player?.Entity == null) return;
                if (api.Input.KeyboardKeyState[sprintKey])
                    player.Entity.Controls.Sprint = true;
            }, 16);
            return true;
        });

        // Track mouse for radial hover
        api.Event.MouseMove += (MouseEvent e) =>
        {
            if (radialMenu.IsOpen)
                radialMenu.UpdateMouse(e.X, e.Y);
        };

        // R release — close radial and stop sprint tick
        api.Event.KeyUp += (KeyEvent e) =>
        {
            if (e.KeyCode == (int)GlKeys.R && radialMenu.IsOpen)
            {
                radialMenu.Close();
                if (sprintTickId >= 0) { api.Event.UnregisterGameTickListener(sprintTickId); sprintTickId = -1; }
            }
        };

        // RMB cancels cast
        api.Event.MouseDown += (MouseEvent e) =>
        {
            if (e.Button != EnumMouseButton.Right) return;
            if (!api.Input.MouseGrabbed || radialMenu.IsOpen) return;

            string? spellId = radialMenu.GetSelectedSpellId();
            if (spellId == "air_wind_vortex")
            {
                if (heldChannelSpellId != null) return;

                var player = api.World.Player;
                if (player?.Entity == null) return;

                var data = PlayerSpellData.For(player.Entity);
                int spellLevel = data.GetSpellLevel(spellId);
                clientChannel!.SendPacket(new MsgChannelSpell
                {
                    SpellId = spellId,
                    IsActive = true,
                    SpellLevel = spellLevel
                });
                heldChannelSpellId = spellId;
                e.Handled = true;
                return;
            }

            if (castBar!.IsCasting)
                castBar.Cancel();

            var playerCancel = api.World.Player;
            if (playerCancel?.Entity != null)
            {
                string? cancelSpellId = lmbHoldSpellId ?? activeCastSpellId;
                if (!string.IsNullOrEmpty(cancelSpellId))
                {
                    clientChannel!.SendPacket(new MsgCancelCast
                    {
                        EntityId = playerCancel.Entity.EntityId,
                        SpellId = cancelSpellId
                    });
                }
            }
        };

        // LMB — cast selected spell (radial is a separate dialog that handles its own clicks)
        api.Event.MouseDown += (MouseEvent e) =>
        {
            if (e.Button != EnumMouseButton.Left) return;
            if (!api.Input.MouseGrabbed) return; // any GUI (inventory, spellbook, etc.) has focus
            if (radialMenu.IsOpen) return; // radial handles its own LMB via OnMouseDown

            string? spellId = radialMenu.GetSelectedSpellId();
            if (spellId == null) return;

            e.Handled = true;

            if (castBar!.IsCasting) return;

            var spell = SpellRegistry.Get(spellId);
            if (spell == null) return;
            if (IsChannelSpell(spell.Id))
            {
                if (heldChannelSpellId != null) return;

                var channelPlayer = api.World.Player;
                if (channelPlayer?.Entity == null) return;

                var channelData = PlayerSpellData.For(channelPlayer.Entity);
                int channelLevel = channelData.GetSpellLevel(spell.Id);
                clientChannel!.SendPacket(new MsgChannelSpell
                {
                    SpellId = spell.Id,
                    IsActive = true,
                    SpellLevel = channelLevel
                });
                heldChannelSpellId = spell.Id;
                return;
            }

            var player = api.World.Player;
            if (player?.Entity == null) return;
            var data = PlayerSpellData.For(player.Entity);
            int spellLevel = data.GetSpellLevel(spellId);

            clientChannel!.SendPacket(new MsgCastSpell { SpellId = spellId, SpellLevel = spellLevel });
            lmbHoldActive = true;
            lmbHoldSpellId = spellId;
        };

        api.Event.MouseUp += (MouseEvent e) =>
        {
            if (e.Button == EnumMouseButton.Left && heldChannelSpellId != null && heldChannelSpellId != "air_wind_vortex")
            {
                clientChannel!.SendPacket(new MsgChannelSpell
                {
                    SpellId = heldChannelSpellId,
                    IsActive = false
                });
                heldChannelSpellId = null;
                e.Handled = true;
                return;
            }

            if (e.Button == EnumMouseButton.Left && lmbHoldActive)
            {
                var playerCancel = api.World.Player;
                if (playerCancel?.Entity != null && !string.IsNullOrEmpty(lmbHoldSpellId))
                {
                    clientChannel!.SendPacket(new MsgCancelCast
                    {
                        EntityId = playerCancel.Entity.EntityId,
                        SpellId = lmbHoldSpellId!
                    });
                }
                castBar?.Cancel();
                lmbHoldActive = false;
                lmbHoldSpellId = null;
            }

            if (e.Button != EnumMouseButton.Right) return;
            if (heldChannelSpellId == null) return;

            clientChannel!.SendPacket(new MsgChannelSpell
            {
                SpellId = heldChannelSpellId,
                IsActive = false
            });
            heldChannelSpellId = null;
            e.Handled = true;
        };
    }

    public void SendReadScroll(string scrollId)
        => clientChannel?.SendPacket(new MsgReadScroll { ScrollId = scrollId });

    public override void Dispose()
    {
        hudFlux?.Dispose();
        castBar?.Dispose();
        radialMenu?.Dispose();
        hudChicken?.Dispose();
        spellbookDialog?.Dispose();
        fireGlow?.Dispose();
        SylphGlow?.Dispose();
        IdleAnim?.Dispose();
        base.Dispose();
    }

    private void TickClientMovementLock(ICoreClientAPI api)
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMs >= clientMovementLockUntilMs)
        {
            ClearClientMovementLock(api);
            return;
        }

        var entity = api.World.Player?.Entity;
        if (entity == null) return;

        entity.Stats.Set("walkspeed", CastMovementLockStatId, 0f, true);
        ApplyClientMovementLockFrame(entity);
    }

    private void ApplyClientMovementLockFrame(EntityAgent entity)
    {
        entity.Controls.Forward = false;
        entity.Controls.Backward = false;
        entity.Controls.Left = false;
        entity.Controls.Right = false;
        entity.Controls.Sprint = false;
        entity.Controls.Jump = false;

        entity.Pos.Motion.X = 0;
        entity.Pos.Motion.Z = 0;
        if (clientMovementLockVertical)
            entity.Pos.Motion.Y = 0;
    }

    private void ClearClientMovementLock(ICoreClientAPI api)
    {
        if (clientMovementLockTickId >= 0)
        {
            api.Event.UnregisterGameTickListener(clientMovementLockTickId);
            clientMovementLockTickId = -1;
        }

        clientMovementLockUntilMs = 0;
        clientMovementLockVertical = false;
        api.World.Player?.Entity?.Stats.Remove("walkspeed", CastMovementLockStatId);
    }

    private void AttachClientSpellDataListeners(ICoreClientAPI api)
    {
        var entity = api.World.Player?.Entity;
        if (entity == null) return;
        if (clientSpellDataListenerEntityId == entity.EntityId) return;

        void RefreshSpellHud()
        {
            spellbookDialog?.ReloadData();
            radialMenu?.RefreshHud();
        }

        foreach (var key in new[]
        {
            "snr:unlocked",
            "snr:hotbar",
            "snr:activators",
            "snr:elementxp",
            "snr:elementlevel",
            "snr:elementsp",
            "snr:spellxp",
            "snr:spelllevel",
            "snr:loreunlocked",
            "spellsandrunes:fluxalignmentlevel",
        })
        {
            entity.WatchedAttributes.RegisterModifiedListener(key, RefreshSpellHud);
        }

        clientSpellDataListenerEntityId = entity.EntityId;
    }

    private void ApplyCastMovementControl(IServerPlayer player, string spellId, int durationMs)
    {
        if (durationMs <= 0) return;
        int paddedDurationMs = durationMs + 100;

        if (ShouldLockMovementWhileCasting(spellId))
        {
            serverChannel?.SendPacket(new MsgMovementLock
            {
                DurationMs = paddedDurationMs,
                LockVertical = true,
            }, player);
        }

        float castMovementSpeed = GetCastMovementSpeedMultiplier(spellId);
        if (castMovementSpeed != 0f)
        {
            serverChannel?.SendPacket(new MsgMovementBoost
            {
                StatId = CastMovementSlowStatId,
                Multiplier = castMovementSpeed,
                DurationMs = paddedDurationMs,
            }, player);
        }
    }

    private void ClearCastMovementControl(IServerPlayer player)
    {
        serverChannel?.SendPacket(new MsgMovementLock { DurationMs = 0 }, player);
        serverChannel?.SendPacket(new MsgMovementBoost
        {
            StatId = CastMovementSlowStatId,
            DurationMs = 0,
        }, player);
    }

    private static bool ShouldLockMovementWhileCasting(string spellId) => spellId is
        "fire_jet_launch" or
        "fire_blast" or
        "fire_wisp" or
        "fire_scorching_step";

    private static bool ShouldBroadcastCastFx(string spellId) => spellId is not
        "fire_fist" and not
        "fire_spark" and not
        "fire_wisp";

    private static float GetCastMovementSpeedMultiplier(string spellId) => spellId switch
    {
        "fire_wall" => -1f,
        _ => 0f,
    };

    private void StartChannelSpell(ICoreServerAPI api, IServerPlayer player, Entity entity, Spell spell, int spellLevel)
    {
        if (!PlayerSpellData.For(entity).IsFluxUnlocked) return;

        if (activeChannelSpells.ContainsKey(entity.EntityId)) return;
        spellLevel = Math.Max(1, spellLevel);

        float currentFlux = entity.WatchedAttributes.GetFloat("spellsandrunes:flux", 0f);
        float initialCost = spell.FluxCost * spell.GetFluxCostMultiplier(spellLevel) * 0.1f;
        if (currentFlux < initialCost) return;

        entity.WatchedAttributes.SetFloat("spellsandrunes:flux", currentFlux - initialCost);
        entity.WatchedAttributes.MarkPathDirty("spellsandrunes:flux");

        activeChannelSpells[entity.EntityId] = new ActiveChannelSpell
        {
            SpellId = spell.Id,
            SpellLevel = spellLevel,
            DrainTimer = 0f,
            FxEnabled = spell.Id != "fire_jet_launch" || !entity.OnGround,
        };

        if (spell.Id == "fire_jet_launch")
        {
            if (entity.OnGround)
            {
                float launchCastTime = Spells.Fire.FireJetLaunch.LaunchAnimationDelaySeconds;
                int delayMs = (int)(launchCastTime * 1000);
                serverChannel?.SendPacket(new MsgStartCast { SpellId = spell.Id, CastTime = launchCastTime }, player);
                ApplyCastMovementControl(player, spell.Id, delayMs);
                BroadcastSpellAnimation(api, entity, spell, spellLevel);

                api.Event.RegisterCallback(_ =>
                {
                    if (!entity.Alive || !activeChannelSpells.TryGetValue(entity.EntityId, out var state) || state.SpellId != spell.Id) return;

                    state.FxEnabled = true;
                    ClearCastMovementControl(player);
                    var look = entity.Pos.GetViewVector();
                    BroadcastSpellFx(api, entity, spell.Id, spellLevel);
                    serverChannel?.SendPacket(new MsgLaunchPlayer
                    {
                        UpForce = Spells.Fire.FireJetLaunch.LaunchUpForce,
                        ForwardForce = 0f,
                        LookDirX = look.X,
                        LookDirY = 0f,
                        LookDirZ = look.Z,
                        UseLookY = false
                    }, player);
                    BroadcastAnimationCode(api, entity, "fire_jet_flight", true);
                }, delayMs);
            }
            else
            {
                BroadcastAnimationCode(api, entity, "fire_jet_flight", true);
                BroadcastSpellFx(api, entity, spell.Id, spellLevel);
            }
        }
        else
        {
            BroadcastSpellAnimation(api, entity, spell, spellLevel);
            BroadcastSpellFx(api, entity, spell.Id, spellLevel);
        }
    }

    private void StopChannelSpell(ICoreServerAPI api, Entity entity, string spellId, bool collapse)
    {
        if (!activeChannelSpells.Remove(entity.EntityId, out var state)) return;
        if (state.SpellId != spellId) return;
        if (entity is EntityAgent agent)
            SpellRegistry.Get(state.SpellId)?.OnEnd(agent, api.World);
        BroadcastSpellAnimationStop(api, entity, state.SpellId);
        if (collapse && state.SpellId == "air_wind_vortex")
            ApplyWindVortexCollapse(api.World, entity, state.SpellLevel);
    }

    private void TickChannelSpells(ICoreServerAPI api, float deltaTime)
    {
        if (activeChannelSpells.Count == 0) return;

        var ended = new List<long>();
        foreach (var pair in activeChannelSpells)
        {
            var entity = api.World.GetEntityById(pair.Key);
            if (entity is not EntityAgent agent || !agent.Alive)
            {
                ended.Add(pair.Key);
                continue;
            }

            var spell = SpellRegistry.Get(pair.Value.SpellId);
            if (spell == null)
            {
                ended.Add(pair.Key);
                continue;
            }

            if (pair.Value.SpellId == "air_wind_vortex")
                ApplyWindVortexAura(api.World, agent, pair.Value.SpellLevel);
            else
                spell.OnTick(agent, api.World, deltaTime, pair.Value.SpellLevel);

            if (pair.Value.SpellId == "fire_jet_launch" && pair.Value.FxEnabled && !agent.OnGround)
                SendFireJetHover(api, agent, spell, pair.Value.SpellLevel);

            if (pair.Value.FxEnabled)
                BroadcastSpellFx(api, agent, pair.Value.SpellId, pair.Value.SpellLevel);

            pair.Value.DrainTimer += deltaTime;
            if (pair.Value.DrainTimer < 1f) continue;
            pair.Value.DrainTimer -= 1f;

            float sustainCost = spell.FluxCost * spell.GetFluxCostMultiplier(pair.Value.SpellLevel) * 0.25f;
            float currentFlux = agent.WatchedAttributes.GetFloat("spellsandrunes:flux", 0f);
            if (currentFlux < sustainCost)
            {
                ended.Add(pair.Key);
                continue;
            }

            agent.WatchedAttributes.SetFloat("spellsandrunes:flux", currentFlux - sustainCost);
            agent.WatchedAttributes.MarkPathDirty("spellsandrunes:flux");
        }

        foreach (long entityId in ended)
        {
            var entity = api.World.GetEntityById(entityId);
            if (entity != null && activeChannelSpells.TryGetValue(entityId, out var state))
                StopChannelSpell(api, entity, state.SpellId, collapse: true);
            else activeChannelSpells.Remove(entityId);
        }
    }

    private static bool IsChannelSpell(string spellId) => spellId is
        "air_wind_vortex" or
        "fire_flamethrower" or
        "fire_hot_skin" or
        "fire_cook_in_hand" or
        "fire_jet_launch";

    private static bool IsChannelSound(string spellId) => IsChannelSpell(spellId) || spellId is
        "fire_wall" or
        "fire_dance_cone";

    private void TickIgnisPasteDrink(ICoreServerAPI api, float deltaTime)
    {
        foreach (var p in api.World.AllOnlinePlayers)
        {
            if (p is not IServerPlayer sp || sp.Entity == null) continue;
            long id = sp.Entity.EntityId;

            var slot = sp.InventoryManager.ActiveHotbarSlot;
            var stack = slot?.Itemstack;

            if (stack?.Block is not BlockLiquidContainerBase container)
            {
                ignisPasteDrinkHold.Remove(id);
                continue;
            }

            var content = container.GetContent(stack);
            if (content?.Collectible?.Code?.Domain != "spellsandrunes"
                || content.Collectible.Code.Path != "ignis-paste")
            {
                ignisPasteDrinkHold.Remove(id);
                continue;
            }

            if (!sp.Entity.Controls.RightMouseDown)
            {
                ignisPasteDrinkHold.Remove(id);
                continue;
            }

            float held = ignisPasteDrinkHold.GetValueOrDefault(id, 0f) + deltaTime;

            if (held < IgnisPasteDrinkTime)
            {
                ignisPasteDrinkHold[id] = held;
                continue;
            }

            ignisPasteDrinkHold.Remove(id);

            container.SetContent(stack, null!);
            slot!.MarkDirty();

            var data = PlayerSpellData.For(sp.Entity);
            data.TriggerActivator("element_fire");
            data.UnlockLoreEntry("journal-fire-1");

            sp.Entity.WatchedAttributes.SetBool("snr:firepower", true);
            sp.Entity.WatchedAttributes.MarkPathDirty("snr:firepower");

            api.World.PlaySoundAt(
                new AssetLocation("sounds/environment/fire"),
                sp.Entity.Pos.X, sp.Entity.Pos.Y, sp.Entity.Pos.Z);

            sp.SendMessage(0, Lang.Get("spellsandrunes:message-fire-awakens"), EnumChatType.Notification);
        }
    }

    private void ApplyWindVortexAura(IWorldAccessor world, EntityAgent caster, int spellLevel)
    {
        var center = caster.Pos.XYZ.Add(0, 0.8, 0);
        var spell = SpellRegistry.Get("air_wind_vortex");
        if(spell is not Spells.Air.WindVortex) return;
        float radius = Spells.Air.WindVortex.AuraRadius * spell.GetRangeMultiplier(spellLevel);

        world.GetEntitiesAround(center, radius, radius, e =>
        {
            if (e.EntityId == caster.EntityId || e is not EntityAgent) return false;
            Vec3d away = e.Pos.XYZ - center;
            double dist = Math.Max(0.15, away.Length());
            away = away.Normalize();
            e.Pos.Motion.Add(away.X * (0.18 / dist), 0.04, away.Z * (0.18 / dist));
            return false;
        });
    }

    private void ApplyWindVortexCollapse(IWorldAccessor world, Entity entity, int spellLevel)
    {
        var center = entity.Pos.XYZ.Add(0, 0.8, 0);
        var spell = SpellRegistry.Get("air_wind_vortex");
                if(spell is not Spells.Air.WindVortex) return;
        float radius = Spells.Air.WindVortex.AuraRadius * spell.GetRangeMultiplier(spellLevel);

        world.GetEntitiesAround(center, radius + 1.2f, radius + 1.2f, e =>
        {
            if (e.EntityId == entity.EntityId || e is not EntityAgent) return false;
            Vec3d away = (e.Pos.XYZ - center).Normalize();
            e.Pos.Motion.Add(away.X *1.5, 1, away.Z * 1.5);
            return false;
        });

        serverChannel?.BroadcastPacket(new MsgSpellFx
        {
            SpellId = "air_wind_vortex",
            OriginX = center.X,
            OriginY = center.Y,
            OriginZ = center.Z,     
            LookDirX = 0,
            LookDirY = 1,
            LookDirZ = 0,
            SpellLevel = spellLevel,
        });
    }

    private void BroadcastSpellFx(ICoreServerAPI api, Entity entity, string spellId, int spellLevel)
    {
        var lookDir = entity.Pos.GetViewVector();
        var origin = GetSpellFxOrigin(entity, spellId);
        var fx = new MsgSpellFx
        {
            SpellId = spellId,
            OriginX = origin.X,
            OriginY = origin.Y,
            OriginZ = origin.Z,
            LookDirX = lookDir.X,
            LookDirY = lookDir.Y,
            LookDirZ = lookDir.Z,
            SpellLevel = spellLevel
        };

        foreach (var p in api.World.AllOnlinePlayers)
            serverChannel?.SendPacket(fx, p as IServerPlayer);
    }

    private void BroadcastSpellAnimation(ICoreServerAPI api, Entity entity, Spell spell, int spellLevel)
    {
        if (string.IsNullOrEmpty(spell.AnimationCode)) return;

        BroadcastAnimationCode(api, entity, spell.AnimationCode!, spell.AnimationTakesOverBody, spell.AnimationSpeed);
    }

    internal static void BroadcastAnimation(ICoreServerAPI api, IServerNetworkChannel channel, Entity entity, string animationCode, bool takesOverBody = false, float animationSpeed = 1f)
    {
        var msg = new Network.MsgPlayAnimation
        {
            EntityId      = entity.EntityId,
            AnimationCode = animationCode,
            TakesOverBody = takesOverBody,
            AnimationSpeed = animationSpeed,
        };
        foreach (var p in api.World.AllOnlinePlayers)
            channel.SendPacket(msg, p as IServerPlayer);
    }

    internal static IServerNetworkChannel? ServerChannel;

    private void BroadcastAnimationCode(ICoreServerAPI api, Entity entity, string animationCode, bool takesOverBody, float animationSpeed = 1f)
    {
        var msg = new MsgPlayAnimation
        {
            EntityId = entity.EntityId,
            AnimationCode = animationCode,
            TakesOverBody = takesOverBody,
            AnimationSpeed = animationSpeed,
        };

        foreach (var p in api.World.AllOnlinePlayers)
            serverChannel?.SendPacket(msg, p as IServerPlayer);
    }

    private void SendFireJetHover(ICoreServerAPI api, EntityAgent agent, Spell spell, int spellLevel)
    {
        var look = agent.Pos.GetViewVector();
        float vertical = agent.Controls.ShiftKey
            ? Spells.Fire.FireJetLaunch.HoverSinkForce
            : agent.Controls.Jump
                ? Spells.Fire.FireJetLaunch.HoverRiseForce
                : Spells.Fire.FireJetLaunch.HoverLift;

        foreach (var p in api.World.AllOnlinePlayers)
        {
            if (p is not IServerPlayer sp || sp.Entity?.EntityId != agent.EntityId) continue;
            serverChannel?.SendPacket(new MsgLaunchPlayer
            {
                UpForce = vertical,
                ForwardForce = Spells.Fire.FireJetLaunch.HoverForwardForce * spell.GetRangeMultiplier(spellLevel),
                LookDirX = look.X,
                LookDirY = 0f,
                LookDirZ = look.Z,
                UseLookY = false
            }, sp);
            break;
        }
    }

    private void PlaySpellSound(ICoreClientAPI api, string spellId, Vec3d origin, int spellLevel)
    {
        var sound = GetSpellSound(spellId);
        if (sound.Path == null) return;

        float levelBump = Math.Min(0.12f, Math.Max(0, spellLevel - 1) * 0.015f);

        if (IsChannelSound(spellId))
        {
            channelSoundLastMs[spellId] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (activeChannelSounds.TryGetValue(spellId, out var existing) && existing != null)
            {
                existing.SetPosition((float)origin.X, (float)origin.Y, (float)origin.Z);
                if (!existing.IsPlaying) existing.Start();
            }
            else
            {
                existing?.Stop();
                existing?.Dispose();
                var loaded = api.World.LoadSound(new SoundParams
                {
                    Location         = new AssetLocation(sound.Path),
                    Position         = new Vec3f((float)origin.X, (float)origin.Y, (float)origin.Z),
                    Range            = sound.Range,
                    Volume           = sound.Volume + levelBump,
                    ShouldLoop       = true,
                    RelativePosition = false,
                });
                loaded?.Start();
                if (loaded != null) activeChannelSounds[spellId] = loaded;
            }
            return;
        }

        long nowMs = api.World.ElapsedMilliseconds;
        if (recentSpellSoundReceipts.TryGetValue(spellId, out long lastMs) && nowMs - lastMs < sound.ThrottleMs)
            return;

        recentSpellSoundReceipts[spellId] = nowMs;
        if (recentSpellSoundReceipts.Count > 64)
        {
            string? staleKey = null;
            foreach (var pair in recentSpellSoundReceipts)
            {
                if (nowMs - pair.Value > 3000) { staleKey = pair.Key; break; }
            }
            if (staleKey != null) recentSpellSoundReceipts.Remove(staleKey);
        }

        api.World.PlaySoundAt(
            new AssetLocation(sound.Path),
            origin.X, origin.Y, origin.Z,
            api.World.Player,
            false,
            sound.Range,
            sound.Volume + levelBump);
    }

    private void TickChannelSounds()
    {
        if (activeChannelSounds.Count == 0) return;
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var toStop = new List<string>();
        foreach (var pair in channelSoundLastMs)
            if (nowMs - pair.Value > 600) toStop.Add(pair.Key);
        foreach (var key in toStop)
        {
            channelSoundLastMs.Remove(key);
            if (activeChannelSounds.Remove(key, out var snd))
            { snd.Stop(); snd.Dispose(); }
        }
    }

    private static (string? Path, float Range, float Volume, int ThrottleMs) GetSpellSound(string spellId) => spellId switch
    {
        "air_air_push" => ("sounds/effect/swoosh", 16f, 0.70f, 180),
        "air_windy_dash" => ("sounds/effect/gliding", 14f, 0.65f, 350),
        "air_updraft" => ("sounds/block/bellowslarge/bellowlarge-out1", 14f, 0.72f, 350),
        "air_wind_slash" => ("sounds/effect/swoosh", 18f, 0.75f, 90),
        "air_wind_clone" => ("sounds/environment/wind", 14f, 0.58f, 650),
        "air_storms_eye" => ("sounds/environment/wind", 20f, 0.68f, 500),
        "air_stroms_eye" => ("sounds/environment/wind", 20f, 0.68f, 500),
        "air_tornado" => ("sounds/environment/wind", 22f, 0.80f, 650),
        "air_wind_vortex" => ("sounds/environment/wind", 18f, 0.65f, 650),
        "air_feather_fall" => ("sounds/effect/gliding", 10f, 0.52f, 500),
        "air_air_kick" => ("sounds/player/projectilehit", 16f, 0.72f, 120),
        "air_air_kick_trail" => (null, 0f, 0f, 0),
        "air_air_kick_hitbox" => (null, 0f, 0f, 0),
        "air_spear_in_an_eye" => ("sounds/player/throw", 16f, 0.72f, 250),
        "air_wind_step" => ("sounds/effect/gliding", 16f, 0.68f, 300),
        "air_cloning_wind_step" => ("sounds/effect/gliding", 16f, 0.70f, 300),

        "fire_spark" => ("sounds/torch-ignite", 14f, 0.65f, 160),
        "fire_flamethrower" => ("sounds/environment/fire", 16f, 0.70f, 260),
        "fire_hot_skin" => ("sounds/effect/embers", 12f, 0.42f, 600),
        "fire_cook_in_hand" => ("sounds/effect/cooking", 10f, 0.40f, 600),
        "fire_fist" => ("sounds/held/torch-attack", 14f, 0.65f, 160),
        "fire_back_blast_dash" => ("sounds/effect/smallexplosion", 18f, 0.72f, 300),
        "fire_mine_arm" => ("sounds/effect/fuse", 14f, 0.45f, 600),
        "fire_mine_burst" => ("sounds/effect/mediumexplosion", 22f, 0.78f, 250),
        "fire_orb_trail" => (null, 0f, 0f, 0),
        "fire_orb_impact" => ("sounds/held/torch-attack", 16f, 0.50f, 160),
        "fire_orb_release_smoke" => ("sounds/effect/embers", 12f, 0.30f, 400),
        "fire_dance_cone" => ("sounds/environment/fire", 18f, 0.72f, 450),
        "fire_scorching_step" => ("sounds/effect/embers", 14f, 0.48f, 300),
        "fire_blast_trail" => ("sounds/effect/smallexplosion", 18f, 0.62f, 250),
        "fire_blast_impact" => ("sounds/effect/smallexplosion", 18f, 0.72f, 150),
        "fire_blast_hand_smoke" => ("sounds/effect/embers", 12f, 0.35f, 400),
        "fire_blast_hitbox" => (null, 0f, 0f, 0),
        "fire_wisp" => ("sounds/effect/embers", 12f, 0.38f, 450),
        "fire_jet_launch" => ("sounds/block/bellowslarge/bellowlarge-out2", 16f, 0.58f, 420),
        "fire_wall" => ("sounds/environment/fire", 18f, 0.62f, 550),
        "fire_wall_hitbox" => (null, 0f, 0f, 0),

        "flux_expression_1" => ("sounds/effect/portal", 12f, 0.35f, 500),
        "flux_expression_2" => ("sounds/effect/portal", 12f, 0.42f, 500),
        "flux_expression_3" => ("sounds/effect/rift", 14f, 0.45f, 500),
        "flux_expression_4" => ("sounds/effect/rift", 16f, 0.52f, 500),

        _ => spellId.StartsWith("air_", StringComparison.Ordinal)
            ? ("sounds/effect/swoosh", 14f, 0.45f, 300)
            : spellId.StartsWith("fire_", StringComparison.Ordinal)
                ? ("sounds/effect/embers", 14f, 0.45f, 300)
                : (null, 0f, 0f, 0),
    };

    private void AddFireWallGlow(Vec3d origin, Vec3d lookDir, int spellLevel)
    {
        if (fireGlow == null) return;

        Vec3d forward = new Vec3d(lookDir.X, 0, lookDir.Z);
        if (forward.LengthSq() < 0.001) forward = new Vec3d(0, 0, 1);
        forward.Normalize();

        Vec3d right = forward.Cross(new Vec3d(0, 1, 0)).Normalize();
        int mult = 1 + (spellLevel - 1) / 4;
        const int glowColumns = 7;

        for (int i = 0; i < glowColumns; i++)
        {
            double t = glowColumns == 1 ? 0.5 : i / (double)(glowColumns - 1);
            double along = (t - 0.5) * Spells.Fire.FireWall.Width;
            Vec3d glowOrigin = origin + right * along + new Vec3d(0, 0.08, 0);
            fireGlow.AddFireGlow(glowOrigin, new Vec3d(0, 1, 0), 0.75f, 3 * mult);
        }
    }

    private void BroadcastSpellAnimationStop(ICoreServerAPI api, Entity entity, string spellId)
    {
        var msg = new MsgCancelCast
        {
            EntityId = entity.EntityId,
            SpellId = spellId,
        };

        foreach (var p in api.World.AllOnlinePlayers)
            serverChannel?.SendPacket(msg, p as IServerPlayer);
    }

    private Vec3d GetSpellFxOrigin(Entity entity, string spellId)
    {
        if (entity is not EntityAgent agent)
            return entity.Pos.XYZ.Add(0, 0.5, 0);

        return spellId switch
        {
            "air_feather_fall" => entity.Pos.XYZ.Add(0, 0.1, 0),
            "air_storms_eye" => Spells.Air.StormsEye.GetCenter(agent),
            "air_stroms_eye" => Spells.Air.StormsEye.GetCenter(agent),
            "air_spear_in_an_eye" => Spells.Air.SpearInAnEye.GetCenter(agent),
            "air_tornado" => agent.Pos.XYZ.Add(agent.Pos.GetViewVector().ToVec3d().Normalize() * 9).Add(0, 0.5, 0),
            "air_wind_vortex" => entity.Pos.XYZ.Add(0, 0.8, 0),
            "fire_flamethrower" => entity.Pos.XYZ
                .Add(agent.Pos.GetViewVector().ToVec3d().Normalize() * 1.05)
                .Add(0, agent.LocalEyePos.Y - 0.32, 0),
            "fire_hot_skin" => entity.Pos.XYZ.Add(0, 0.9, 0),
            "fire_cook_in_hand" => entity.Pos.XYZ.Add(0, 0.75, 0),
            "fire_fist" => entity.Pos.XYZ.Add(0, agent.LocalEyePos.Y - 0.25, 0),
            "fire_scorching_step" => entity.Pos.XYZ.Add(0, 0.1, 0),
            "fire_jet_launch" => entity.Pos.XYZ.Add(0, 0.2, 0),
            "fire_wisp" => entity.Pos.XYZ
                .Add(agent.Pos.GetViewVector().ToVec3d().Normalize() * 0.55)
                .Add(0, agent.LocalEyePos.Y - 0.35, 0),
            _ => entity.Pos.XYZ.Add(0, 0.5, 0),
        };
    }

    private void InitChickenKillCounter(ICoreServerAPI api)
    {
        if (chickenKillsLoaded) return;
        chickenKillsLoaded = true;

        string basePath = api.GetOrCreateDataPath("spellsandrunes");
        chickenKillPath = Path.Combine(basePath, ChickenKillFileName);

        if (File.Exists(chickenKillPath))
        {
            try
            {
                var json = File.ReadAllText(chickenKillPath);
                var data = JsonSerializer.Deserialize<ChickenKillStats>(json);
                chickenKills = data?.ChickenKills ?? 0;
            }
            catch
            {
                chickenKills = 0;
            }
        }
    }

    private void CountChickenDeath(ICoreServerAPI api, Entity entity)
    {
        if (entity is not EntityAgent agent) return;
        var code = agent.Code?.Path ?? "";
        if (!code.Contains("chicken", StringComparison.OrdinalIgnoreCase)) return;

        if (!chickenKillsLoaded) InitChickenKillCounter(api);
        chickenKills++;
        SaveChickenKills();
        BroadcastChickenKills(api);
    }

    private void SaveChickenKills()
    {
        if (string.IsNullOrEmpty(chickenKillPath)) return;
        var data = new ChickenKillStats
        {
            ChickenKills = chickenKills,
            UpdatedUtc = DateTime.UtcNow.ToString("o")
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(chickenKillPath, json);
    }

    private void BroadcastChickenKills(ICoreServerAPI api)
    {
        var msg = new MsgChickenKills { Count = chickenKills };
        foreach (var p in api.World.AllOnlinePlayers)
            serverChannel?.SendPacket(msg, p as IServerPlayer);
    }

    private void SendChickenKills(IServerPlayer player)
    {
        var msg = new MsgChickenKills { Count = chickenKills };
        serverChannel?.SendPacket(msg, player);
    }

    private sealed class ChickenKillStats
    {
        public int ChickenKills { get; set; }
        public string? UpdatedUtc { get; set; }
    }
}
