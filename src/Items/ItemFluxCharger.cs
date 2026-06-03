using System;
using SpellsAndRunes.Flux;
using SpellsAndRunes.Spells;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace SpellsAndRunes.Items;

public class ItemFluxCharger : Item
{
    private const float UseDuration = 4f;
    private const int   DebuffMs    = 30_000;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, bool firstEvent,
        ref EnumHandHandling handling)
    {
        if (!firstEvent) return;

        handling = EnumHandHandling.PreventDefault;

        var data = PlayerSpellData.For(byEntity);
        var flux = byEntity.GetBehavior<EntityBehaviorFlux>();

        if (byEntity.World.Side == EnumAppSide.Client)
        {
            if (data == null || !data.IsFluxUnlocked) return;
            if (flux == null || flux.GetFluxAlignmentLevel() >= EntityBehaviorFlux.MaxAlignmentLevel) return;

            byEntity.World.PlaySoundAt(
                new AssetLocation("game:sounds/effect/deepbreath"),
                byEntity, null, false, 16f, 0.5f);
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot,
        EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.World.Side == EnumAppSide.Client)
            SpawnChargerParticles(byEntity, secondsUsed / UseDuration);

        return secondsUsed < UseDuration;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot,
        EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (secondsUsed < UseDuration - 0.1f) return;

        if (byEntity.World.Side == EnumAppSide.Client)
        {
            SpawnCompletionBurst(byEntity);
            byEntity.World.PlaySoundAt(
                new AssetLocation("game:sounds/effect/thunder"),
                byEntity, null, false, 16f, 0.5f);
            return;
        }

        // Server side
        var data = PlayerSpellData.For(byEntity);
        var flux = byEntity.GetBehavior<EntityBehaviorFlux>();
        if (data == null || flux == null || !data.IsFluxUnlocked) return;

        int currentLevel = flux.GetFluxAlignmentLevel();
        if (currentLevel >= EntityBehaviorFlux.MaxAlignmentLevel) return;

        int newLevel = currentLevel + 1;

        // Set activators so level persists across reloads
        var activators = byEntity.WatchedAttributes.GetOrAddTreeAttribute("snr:activators");
        if (newLevel >= 2) activators.SetInt(EntityBehaviorFlux.FluxAlignmentLevel2Activator, 1);
        if (newLevel >= 3) activators.SetInt(EntityBehaviorFlux.FluxAlignmentLevel3Activator, 1);
        if (newLevel >= 4) activators.SetInt(EntityBehaviorFlux.FluxAlignmentLevel4Activator, 1);
        byEntity.WatchedAttributes.MarkPathDirty("snr:activators");

        flux.SetFluxAlignmentLevel(newLevel);

        // Debuffs for 30s
        byEntity.Stats.Set("walkspeed",          "fluxoverload", 0.40f, false);
        byEntity.Stats.Set("healingeffectivness", "fluxoverload", 0.00f, false);
        byEntity.WatchedAttributes.SetFloat("intoxication", 0.9f);
        byEntity.WatchedAttributes.MarkPathDirty("intoxication");

        byEntity.World.RegisterCallback(_ =>
        {
            byEntity.Stats.Remove("walkspeed",           "fluxoverload");
            byEntity.Stats.Remove("healingeffectivness",  "fluxoverload");
            byEntity.WatchedAttributes.SetFloat("intoxication", 0f);
            byEntity.WatchedAttributes.MarkPathDirty("intoxication");
        }, DebuffMs);

        slot.TakeOut(1);
        slot.MarkDirty();
    }

    private static void SpawnChargerParticles(EntityAgent entity, float progress)
    {
        var pos = entity.Pos;
        var rng = entity.World.Rand;
        int count = 1 + (int)(progress * 4);

        for (int i = 0; i < count; i++)
        {
            double angle  = rng.NextDouble() * Math.PI * 2;
            double radius = rng.NextDouble() * 0.5 + 0.1;
            double height = rng.NextDouble() * 1.6 + 0.3;

            int a = (int)(120 + progress * 135);
            int r = (int)(80  + progress * 80);
            int g = (int)(20  + progress * 30);
            int b = 255;
            int color = (a << 24) | (r << 16) | (g << 8) | b;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + Math.Cos(angle) * radius, pos.Y + height, pos.Z + Math.Sin(angle) * radius),
                new Vec3d(pos.X + Math.Cos(angle) * radius, pos.Y + height, pos.Z + Math.Sin(angle) * radius),
                new Vec3f((float)(rng.NextDouble() - 0.5) * 0.3f, (float)(rng.NextDouble() * 0.4 + 0.2), (float)(rng.NextDouble() - 0.5) * 0.3f),
                new Vec3f((float)(rng.NextDouble() - 0.5) * 0.3f, (float)(rng.NextDouble() * 0.4 + 0.2), (float)(rng.NextDouble() - 0.5) * 0.3f),
                (float)(0.3 + progress * 0.4), -0.05f,
                (float)(0.12 + progress * 0.15));
        }
    }

    private static void SpawnCompletionBurst(EntityAgent entity)
    {
        var pos = entity.Pos;
        var rng = entity.World.Rand;

        for (int i = 0; i < 40; i++)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            double tilt  = rng.NextDouble() * Math.PI;
            float  speed = (float)(rng.NextDouble() * 2.0 + 0.5);
            float  vx    = (float)(Math.Sin(tilt) * Math.Cos(angle)) * speed;
            float  vy    = (float)(Math.Abs(Math.Cos(tilt))) * speed + 0.5f;
            float  vz    = (float)(Math.Sin(tilt) * Math.Sin(angle)) * speed;

            int color = (220 << 24) | (140 << 16) | (60 << 8) | 255;
            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X, pos.Y + 1.0, pos.Z),
                new Vec3d(pos.X, pos.Y + 1.0, pos.Z),
                new Vec3f(vx, vy, vz), new Vec3f(vx, vy, vz),
                (float)(rng.NextDouble() * 0.6 + 0.4), -0.1f,
                0.20f);
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        => new[] { new WorldInteraction { ActionLangCode = "heldhelp-use", MouseButton = EnumMouseButton.Right } };
}
