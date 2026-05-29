using System.Collections.Generic;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using SpellsAndRunes.Entities;

namespace SpellsAndRunes.Spells.Fire;

public class FireMine : Spell
{
    public override string Id => "fire_mine";
    public override string Name => "Fire Mine";
    public override string Description => "Plants a volatile ember charge at the caster's feet.";

    public override SpellTier Tier => SpellTier.Apprentice;
    public override SpellElement Element => SpellElement.Fire;
    public override SpellType Type => SpellType.Offense;

    public override float FluxCost => 24f;
    public override float CastTime => 0.8f;

    public override string? AnimationCode => "fire_mine";
    public override bool AnimationTakesOverBody => true;

    public override IReadOnlyList<string> Prerequisites => ["fire_wisp"];
    public override (int col, int row) TreePosition => (0, 5);

    public const float Radius = 1.45f;
    public const float Damage = 10f;

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
        if (world.Api == null) return;

        var center = caster.Pos.XYZ.Add(caster.Pos.GetViewVector().ToVec3d().Normalize() * 1.4).Add(0, 0.03, 0);
        center = ClampToSurface(world, center, 0.03);
        SpawnMineEntity(caster, world, center, spellLevel);
        FireOrb.BroadcastFx(world, "fire_mine_arm", center, new Vec3d(0, 1, 0), spellLevel);
    }

    private void SpawnMineEntity(EntityAgent caster, IWorldAccessor world, Vec3d center, int spellLevel)
    {
        var entityType = world.GetEntityType(new AssetLocation("spellsandrunes:fire-mine"));
        if (entityType == null)
        {
            world.Api?.Logger.Warning("[SnR] Could not spawn fire mine: entity type spellsandrunes:fire-mine was not loaded.");
            return;
        }

        var entity = world.ClassRegistry.CreateEntity(entityType);
        if (entity == null)
        {
            world.Api?.Logger.Warning("[SnR] Could not spawn fire mine: class registry returned null for EntityFireMine.");
            return;
        }

        entity.Pos.SetPos(center);
        entity.Pos.Motion.Set(0, 0, 0);
        entity.WatchedAttributes.SetLong(EntityFireMine.OwnerEntityIdAttribute, caster.EntityId);
        entity.WatchedAttributes.SetInt(EntityFireMine.SpellLevelAttribute, spellLevel);
        entity.WatchedAttributes.SetFloat(EntityFireMine.RadiusAttribute, Radius * GetRangeMultiplier(spellLevel));
        entity.WatchedAttributes.SetFloat(EntityFireMine.DamageAttribute, Damage * GetDamageMultiplier(spellLevel));

        world.SpawnEntity(entity);
#if DEBUG
        world.Api?.Logger.Notification("[SnR] Spawned fire mine entity {0} at {1:0.00}, {2:0.00}, {3:0.00}.", entity.EntityId, center.X, center.Y, center.Z);
#endif
    }

    public static void SpawnArmFx(IWorldAccessor world, Vec3d center, int spellLevel)
    {
        var rng = world.Rand;
        int mult = 1 + (spellLevel - 1) / 4;
        for (int i = 0; i < 22 * mult; i++)
        {
            double a = rng.NextDouble() * Math.PI * 2;
            double r = rng.NextDouble() * 0.7;
            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = new Vec3d(center.X + Math.Cos(a) * r, center.Y + 0.04, center.Z + Math.Sin(a) * r),
                AddPos = new Vec3d(0.02, 0.02, 0.02),
                MinVelocity = new Vec3f(0, 0.18f, 0),
                AddVelocity = new Vec3f(0.12f, 0.12f, 0.12f),
                LifeLength = 0.35f + (float)rng.NextDouble() * 0.2f,
                MinSize = 0.06f,
                MaxSize = 0.16f,
                GravityEffect = -0.05f,
                Color = ColorUtil.ColorFromRgba(20 + rng.Next(80), 105 + rng.Next(110), 255, 180),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true
            });
        }
    }

    public static void SpawnBurstFx(IWorldAccessor world, Vec3d center, int spellLevel)
    {
        var rng = world.Rand;
        int mult = 1 + (spellLevel - 1) / 4;
        for (int i = 0; i < 82 * mult; i++)
        {
            double a = rng.NextDouble() * Math.PI * 2;
            double speed = 2.0 + rng.NextDouble() * 3.2;
            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = center,
                AddPos = new Vec3d(0.18, 0.12, 0.18),
                MinVelocity = new Vec3f((float)(Math.Cos(a) * speed), 0.45f + (float)rng.NextDouble() * 1.0f, (float)(Math.Sin(a) * speed)),
                AddVelocity = new Vec3f(0.3f, 0.25f, 0.3f),
                LifeLength = 0.28f + (float)rng.NextDouble() * 0.24f,
                MinSize = 0.10f,
                MaxSize = 0.34f,
                GravityEffect = -0.08f,
                Color = ColorUtil.ColorFromRgba(10 + rng.Next(80), 95 + rng.Next(130), 255, 220),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true
            });
        }
    }
}
