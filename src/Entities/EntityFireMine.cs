using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using SpellsAndRunes.Spells.Fire;

namespace SpellsAndRunes.Entities;

public class EntityFireMine : Entity
{
    public const string OwnerEntityIdAttribute = "snr:ownerEntityId";
    public const string SpellLevelAttribute = "snr:spellLevel";
    public const string RadiusAttribute = "snr:radius";
    public const string DamageAttribute = "snr:damage";

    private const float TriggerRadius = 0.48f;
    private const float ArmDelaySeconds = 0.35f;
    private float aliveSeconds;
    private float markerSeconds;
    private bool detonated;

    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);

        if (Api?.Side != EnumAppSide.Server || detonated) return;

        aliveSeconds += dt;
        if (aliveSeconds < ArmDelaySeconds) return;

        markerSeconds += dt;
        if (markerSeconds >= 0.6f)
        {
            markerSeconds = 0f;
            FireOrb.BroadcastFx(World, "fire_mine_arm", Pos.XYZ, new Vec3d(0, 1, 0), WatchedAttributes.GetInt(SpellLevelAttribute, 1));
        }

        long ownerId = WatchedAttributes.GetLong(OwnerEntityIdAttribute);
        Vec3d center = Pos.XYZ;

        bool triggered = false;
        World.GetEntitiesAround(center, TriggerRadius, TriggerRadius, e =>
        {
            if (e.EntityId == EntityId || e.EntityId == ownerId || e is not EntityAgent) return false;
            if (e.Pos.XYZ.DistanceTo(center) > TriggerRadius) return false;
            triggered = true;
            return false;
        });

        if (triggered) Detonate();
    }

    public override bool ReceiveDamage(DamageSource damageSource, float damage)
    {
        bool handled = base.ReceiveDamage(damageSource, damage);
        if (Api?.Side == EnumAppSide.Server && !detonated) Detonate();
        return handled;
    }

    private void Detonate()
    {
        if (detonated) return;
        detonated = true;

        long ownerId = WatchedAttributes.GetLong(OwnerEntityIdAttribute);
        int spellLevel = WatchedAttributes.GetInt(SpellLevelAttribute, 1);
        float radius = WatchedAttributes.GetFloat(RadiusAttribute, FireMine.Radius);
        float damage = WatchedAttributes.GetFloat(DamageAttribute, FireMine.Damage);
        Entity? owner = World.GetEntityById(ownerId);
        Vec3d center = Pos.XYZ;

        World.GetEntitiesAround(center, radius, radius, e =>
        {
            if (e.EntityId == EntityId || e.EntityId == ownerId || e is not EntityAgent) return false;
            double dist = Math.Max(0.2, e.Pos.XYZ.DistanceTo(center));
            if (dist > radius) return false;

            float scaledDamage = damage * (float)Math.Max(0.25, 1.0 - dist / radius);
            e.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = owner,
                Type = EnumDamageType.Fire,
            }, scaledDamage);

            Vec3d away = (e.Pos.XYZ - center).Normalize();
            e.Pos.Motion.Add(away.X * 0.18, 0.12, away.Z * 0.18);
            return false;
        });

        FireOrb.BroadcastFx(World, "fire_mine_burst", center, new Vec3d(0, 1, 0), spellLevel);
        Die(EnumDespawnReason.Removed);
    }
}
