using System;
using SpellsAndRunes.Spells.Air;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace SpellsAndRunes.Entities;

public class EntityWindClone : EntityAgent
{
    public const string OwnerEntityIdAttribute = "snr:ownerEntityId";
    public const string SpellLevelAttribute    = "snr:spellLevel";

    private const float AggroInterval = 3f;
    private const float AggroRadius   = 15f;

    private bool  dispersed;
    private float particleAccum;
    private float aggroAccum;
    private bool  animStarted;

    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);

        if (Api?.Side == EnumAppSide.Client)
        {
            // Start idle animation once on client
            if (!animStarted && AnimManager != null)
            {
                animStarted = true;
                AnimManager.StartAnimation("idle");
            }

            // Ambient wind particles
            particleAccum += dt;
            if (particleAccum < 0.12f) return;
            particleAccum = 0f;

            var rng = World.Rand;
            for (int i = 0; i < 3; i++)
            {
                double angle  = rng.NextDouble() * Math.PI * 2;
                double radius = 0.25 + rng.NextDouble() * 0.35;
                World.SpawnParticles(new SimpleParticleProperties
                {
                    MinQuantity = 1,
                    AddQuantity = 0,
                    Color       = ColorUtil.ColorFromRgba(195, 220, 245, 30 + rng.Next(55)),
                    MinPos      = new Vec3d(
                        Pos.X + Math.Cos(angle) * radius,
                        Pos.Y + rng.NextDouble() * 1.85,
                        Pos.Z + Math.Sin(angle) * radius),
                    AddPos      = new Vec3d(0.04, 0.04, 0.04),
                    MinVelocity = new Vec3f(
                        (float)((rng.NextDouble() - 0.5) * 0.12),
                        (float)(0.04 + rng.NextDouble() * 0.09),
                        (float)((rng.NextDouble() - 0.5) * 0.12)),
                    AddVelocity        = new Vec3f(0.04f, 0.04f, 0.04f),
                    LifeLength         = 0.7f + (float)rng.NextDouble() * 0.5f,
                    MinSize            = 0.08f,
                    MaxSize            = 0.22f,
                    GravityEffect      = -0.015f,
                    ParticleModel      = EnumParticleModel.Quad,
                    WithTerrainCollision = false,
                    ShouldDieInLiquid  = false
                });
            }
            return;
        }

        // Server: taunt nearby hostile mobs via small damage — the only reliable way
        // to force mob AI retargeting in VS (same mechanic as wolf retargeting on hit)
        aggroAccum += dt;
        if (aggroAccum < AggroInterval) return;
        aggroAccum = 0f;

        var tauntSource = new DamageSource
        {
            Source       = EnumDamageSource.Entity,
            SourceEntity = this,
            Type         = EnumDamageType.BluntAttack,
            DamageTier   = 0
        };

        var mobs = World.GetEntitiesAround(Pos.XYZ, AggroRadius, AggroRadius,
            e => e is EntityAgent && e is not EntityPlayer && e != this && e.Alive);
        foreach (var mob in mobs)
            mob.ReceiveDamage(tauntSource, 0f);
    }

    public override bool IsInteractable => true;

    public override bool ReceiveDamage(DamageSource damageSource, float damage)
    {
        if (Api?.Side == EnumAppSide.Server && !dispersed)
            Disperse();
        return true;
    }

    private void Disperse()
    {
        if (dispersed) return;
        dispersed = true;

        int spellLevel = WatchedAttributes.GetInt(SpellLevelAttribute, 1);
        Vec3d feet   = Pos.XYZ;
        Vec3d center = feet.Add(0, 0.9, 0);

        WindClone.SpawnBlindingSmoke(World, feet, spellLevel);
        WindClone.SpawnFx(World, center, spellLevel);

        Die(EnumDespawnReason.Removed);
    }
}
