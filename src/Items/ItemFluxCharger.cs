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
                byEntity, null, false, 16f, 0.6f);
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot,
        EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        float progress = secondsUsed / UseDuration;

        if (byEntity.World.Side == EnumAppSide.Client)
        {
            SpawnOrbitRings(byEntity, secondsUsed, progress);
            SpawnEnergyStreaks(byEntity, progress);
            SpawnSphereShell(byEntity, secondsUsed, progress);

            // Mid-point pulse sound
            if (secondsUsed > 2f && secondsUsed < 2.08f)
                byEntity.World.PlaySoundAt(
                    new AssetLocation("game:sounds/effect/deepbreath"),
                    byEntity, null, false, 16f, 0.4f);
        }

        if (byEntity.World.Side == EnumAppSide.Server)
        {
            // Override motion.Y each tick — overwrites gravity, produces steady lift
            byEntity.Pos.Motion.Y = 0.02f + progress * 0.04f;
        }

        return secondsUsed < UseDuration;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot,
        EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (secondsUsed < UseDuration - 0.1f) return;

        if (byEntity.World.Side == EnumAppSide.Client)
        {
            SpawnSphereBurst(byEntity);
            SpawnGroundRing(byEntity);
            byEntity.World.PlaySoundAt(
                new AssetLocation("game:sounds/effect/thunder"),
                byEntity, null, false, 32f, 0.8f);
            return;
        }

        // Server side
        var data = PlayerSpellData.For(byEntity);
        var flux = byEntity.GetBehavior<EntityBehaviorFlux>();
        if (data == null || flux == null || !data.IsFluxUnlocked) return;

        int currentLevel = flux.GetFluxAlignmentLevel();
        if (currentLevel >= EntityBehaviorFlux.MaxAlignmentLevel) return;

        int newLevel = currentLevel + 1;

        var activators = byEntity.WatchedAttributes.GetOrAddTreeAttribute("snr:activators");
        if (newLevel >= 2) activators.SetInt(EntityBehaviorFlux.FluxAlignmentLevel2Activator, 1);
        if (newLevel >= 3) activators.SetInt(EntityBehaviorFlux.FluxAlignmentLevel3Activator, 1);
        if (newLevel >= 4) activators.SetInt(EntityBehaviorFlux.FluxAlignmentLevel4Activator, 1);
        byEntity.WatchedAttributes.MarkPathDirty("snr:activators");

        flux.SetFluxAlignmentLevel(newLevel);

        float maxFlux = flux.GetMaxFluxForLevel(newLevel);
        byEntity.WatchedAttributes.SetFloat("spellsandrunes:flux", 0f);
        byEntity.WatchedAttributes.MarkPathDirty("spellsandrunes:flux");

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
            byEntity.WatchedAttributes.SetFloat("spellsandrunes:flux", maxFlux);
            byEntity.WatchedAttributes.MarkPathDirty("spellsandrunes:flux");
        }, DebuffMs);

        slot.TakeOut(1);
        slot.MarkDirty();
    }

    // Two contra-rotating rings orbiting the player
    private static void SpawnOrbitRings(EntityAgent entity, float t, float progress)
    {
        var pos = entity.Pos;
        int particlesPerRing = 4 + (int)(progress * 4);

        for (int ring = 0; ring < 2; ring++)
        {
            float ringRadius  = 0.85f + ring * 0.35f;
            float ringHeight  = 0.8f  + ring * 0.55f;
            float rotDir      = ring == 0 ? 1f : -1f;
            float baseAngle   = t * 2.8f * rotDir;

            for (int i = 0; i < particlesPerRing; i++)
            {
                float a = baseAngle + i * (MathF.PI * 2f / particlesPerRing);

                float px = (float)(pos.X + Math.Cos(a) * ringRadius);
                float pz = (float)(pos.Z + Math.Sin(a) * ringRadius);
                float py = (float)(pos.Y + ringHeight + Math.Sin(t * 3f + i) * 0.12f);

                // Tangential velocity (following the ring rotation)
                float vx = -MathF.Sin(a) * rotDir * (1.5f + progress);
                float vz =  MathF.Cos(a) * rotDir * (1.5f + progress);
                float vy =  0.1f + progress * 0.15f;

                // Ring 0: electric blue, Ring 1: white-purple
                int r = ring == 0 ? (int)(40  + progress * 60)  : (int)(180 + progress * 75);
                int g = ring == 0 ? (int)(120 + progress * 80)  : (int)(80  + progress * 80);
                int b = 255;
                int al = (int)(160 + progress * 95);
                int color = (al << 24) | (r << 16) | (g << 8) | b;

                entity.World.SpawnParticles(1, color,
                    new Vec3d(px, py, pz), new Vec3d(px, py, pz),
                    new Vec3f(vx, vy, vz), new Vec3f(vx, vy, vz),
                    0.25f + progress * 0.2f, -0.02f,
                    0.06f + progress * 0.06f);
            }
        }
    }

    // Upward energy streaks converging on the player
    private static void SpawnEnergyStreaks(EntityAgent entity, float progress)
    {
        if (progress < 0.3f) return;
        var pos = entity.Pos;
        var rng = entity.World.Rand;
        int count = (int)(progress * 5);

        for (int i = 0; i < count; i++)
        {
            double angle  = rng.NextDouble() * Math.PI * 2;
            double dist   = rng.NextDouble() * 2.5 + 1.0;
            double height = rng.NextDouble() * 0.5;

            float vx = (float)(-Math.Cos(angle) * (2.0 + progress * 2));
            float vz = (float)(-Math.Sin(angle) * (2.0 + progress * 2));
            float vy = (float)(1.5 + progress * 2);

            int al = (int)(80 + progress * 120);
            int color = (al << 24) | (220 << 16) | (240 << 8) | 255; // bright cyan-white
            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + Math.Cos(angle) * dist, pos.Y + height, pos.Z + Math.Sin(angle) * dist),
                new Vec3d(pos.X + Math.Cos(angle) * dist, pos.Y + height, pos.Z + Math.Sin(angle) * dist),
                new Vec3f(vx, vy, vz), new Vec3f(vx, vy, vz),
                0.15f + progress * 0.15f, -0.05f,
                0.04f + progress * 0.04f);
        }
    }

    // Sphere shell that forms around the player in the second half
    private static void SpawnSphereShell(EntityAgent entity, float t, float progress)
    {
        if (progress < 0.4f) return;
        float shellP = (progress - 0.4f) / 0.6f; // 0→1 over last 60% of duration

        var pos = entity.Pos;
        float radius  = 1.4f + MathF.Sin(t * 5f) * 0.12f; // pulsating
        int   N       = (int)(shellP * 18) + 2;
        float goldenA = MathF.PI * (3f - MathF.Sqrt(5f));
        int   offset  = (int)(t * 30); // slowly rotates sampling offset

        for (int i = 0; i < N; i++)
        {
            float fy     = 1f - ((i + offset) % 40) / 19.5f;
            float fr     = MathF.Sqrt(Math.Max(0f, 1f - fy * fy));
            float theta  = goldenA * (i + offset);
            float fx     = MathF.Cos(theta) * fr;
            float fz     = MathF.Sin(theta) * fr;

            // Slight inward drift so they look like they're converging
            float vx = -fx * 0.15f, vy = -fy * 0.08f, vz = -fz * 0.15f;

            int al = (int)(100 + shellP * 155);
            int r  = (int)(120 + shellP * 80);
            int g  = (int)(60  + shellP * 60);
            int color = (al << 24) | (r << 16) | (g << 8) | 255;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + fx * radius, pos.Y + 1.0 + fy * radius, pos.Z + fz * radius),
                new Vec3d(pos.X + fx * radius, pos.Y + 1.0 + fy * radius, pos.Z + fz * radius),
                new Vec3f(vx, vy, vz), new Vec3f(vx, vy, vz),
                0.15f + shellP * 0.25f, 0f,
                0.04f + shellP * 0.05f);
        }
    }

    // 80 particles exploding outward in a perfect sphere
    private static void SpawnSphereBurst(EntityAgent entity)
    {
        var pos = entity.Pos;
        const int N = 80;
        // Fibonacci sphere distribution for even coverage
        float goldenAngle = MathF.PI * (3f - MathF.Sqrt(5f));

        for (int i = 0; i < N; i++)
        {
            float y     = 1f - (i / (float)(N - 1)) * 2f;
            float r     = MathF.Sqrt(1f - y * y);
            float theta = goldenAngle * i;
            float x     = MathF.Cos(theta) * r;
            float z     = MathF.Sin(theta) * r;

            float speed = 2.8f + (float)(entity.World.Rand.NextDouble() * 1.8);
            float vx = x * speed, vy = y * speed + 0.3f, vz = z * speed;

            // White core fading to purple
            float t = (float)entity.World.Rand.NextDouble();
            int cr = (int)(255 - t * 120);
            int cg = (int)(255 - t * 200);
            int cb = 255;
            int color = (240 << 24) | (cr << 16) | (cg << 8) | cb;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X, pos.Y + 1.0, pos.Z),
                new Vec3d(pos.X, pos.Y + 1.0, pos.Z),
                new Vec3f(vx, vy, vz), new Vec3f(vx, vy, vz),
                0.6f + (float)(entity.World.Rand.NextDouble() * 0.4), -0.08f,
                0.12f + (float)(entity.World.Rand.NextDouble() * 0.1));
        }
    }

    // Ring of particles expanding outward along the ground
    private static void SpawnGroundRing(EntityAgent entity)
    {
        var pos = entity.Pos;
        const int N = 48;

        for (int i = 0; i < N; i++)
        {
            float angle = i * (MathF.PI * 2f / N);
            float vx = MathF.Cos(angle) * 4.5f;
            float vz = MathF.Sin(angle) * 4.5f;

            float t = (float)i / N;
            int cr = (int)(60  + t * 120);
            int cg = (int)(100 + t * 80);
            int color = (200 << 24) | (cr << 16) | (cg << 8) | 255;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X, pos.Y + 0.05, pos.Z),
                new Vec3d(pos.X, pos.Y + 0.05, pos.Z),
                new Vec3f(vx, 0.1f, vz), new Vec3f(vx, 0.1f, vz),
                0.5f, 0.0f, 0.10f);
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        => new[] { new WorldInteraction { ActionLangCode = "heldhelp-use", MouseButton = EnumMouseButton.Right } };
}
