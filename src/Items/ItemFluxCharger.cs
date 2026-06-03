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
    private const float UseDuration = 8f;
    private const float CrazyStart  = 5f;  // crazy phase begins here
    private const int   DebuffMs    = 30_000;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, bool firstEvent,
        ref EnumHandHandling handling)
    {
        if (!firstEvent) return;
        handling = EnumHandHandling.PreventDefault;

        if (byEntity.World.Side != EnumAppSide.Client) return;
        var data = PlayerSpellData.For(byEntity);
        var flux = byEntity.GetBehavior<EntityBehaviorFlux>();
        if (data == null || !data.IsFluxUnlocked) return;
        if (flux == null || flux.GetFluxAlignmentLevel() >= EntityBehaviorFlux.MaxAlignmentLevel) return;

        byEntity.World.PlaySoundAt(
            new AssetLocation("game:sounds/effect/deepbreath"),
            byEntity, null, false, 16f, 0.6f);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot,
        EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.World.Side == EnumAppSide.Client)
        {
            // Quiet anticipation pulse — subtle glow near player in the last second before crazy
            if (secondsUsed > CrazyStart - 1f && secondsUsed < CrazyStart)
            {
                float anticipation = (secondsUsed - (CrazyStart - 1f));
                SpawnAnticipatePulse(byEntity, secondsUsed, anticipation);
            }

            // Crazy phase
            float crazyP = secondsUsed < CrazyStart
                ? 0f
                : (secondsUsed - CrazyStart) / (UseDuration - CrazyStart);

            if (crazyP > 0f)
            {
                float t = secondsUsed - CrazyStart;
                SpawnOrbitRings(byEntity, t, crazyP);
                SpawnEnergyInflow(byEntity, t, crazyP);
                SpawnSphereShell(byEntity, t, crazyP);

                byEntity.Pos.Motion.Y = 0.018f + crazyP * 0.035f;

                // Sound cue at start of crazy phase
                if (secondsUsed > CrazyStart && secondsUsed < CrazyStart + 0.1f)
                    byEntity.World.PlaySoundAt(
                        new AssetLocation("game:sounds/effect/deepbreath"),
                        byEntity, null, false, 16f, 0.8f);
            }
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
                byEntity, null, false, 48f, 1.0f);
            return;
        }

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

    // ── Charging effects ─────────────────────────────────────────────────────

    // Subtle close-range pulse during the last second of quiet phase
    private static void SpawnAnticipatePulse(EntityAgent entity, float t, float localP)
    {
        var   pos  = entity.Pos;
        var   rng  = entity.World.Rand;
        float beat = 0.5f + 0.5f * MathF.Sin(t * MathF.PI * 4f); // 2 Hz pulse

        int count = (int)(beat * 4);
        for (int i = 0; i < count; i++)
        {
            double angle  = rng.NextDouble() * Math.PI * 2;
            double radius = rng.NextDouble() * 0.5 + 0.2;
            float  vy     = (float)(rng.NextDouble() * 0.3 + 0.1);
            int    al     = (int)(40 + localP * beat * 100);
            int    color  = (al << 24) | (60 << 16) | (100 << 8) | 220;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + Math.Cos(angle) * radius, pos.Y + 1.0, pos.Z + Math.Sin(angle) * radius),
                new Vec3d(pos.X + Math.Cos(angle) * radius, pos.Y + 1.0, pos.Z + Math.Sin(angle) * radius),
                new Vec3f(0, vy, 0), new Vec3f(0, vy, 0),
                0.3f, -0.02f, 0.04f);
        }
    }

    // 2 contra-rotating rings — electric blue + soft violet/white
    private static void SpawnOrbitRings(EntityAgent entity, float t, float progress)
    {
        var pos = entity.Pos;
        for (int ring = 0; ring < 2; ring++)
        {
            float ringRadius = 0.85f + ring * 0.40f;
            float ringHeight = 0.75f + ring * 0.65f;
            float rotDir     = ring == 0 ? 1f : -1f;
            float speed      = 2.2f + progress * 2.8f;
            float baseAngle  = t * speed * rotDir;
            int   pCount     = 5 + (int)(progress * 5);

            for (int i = 0; i < pCount; i++)
            {
                float a  = baseAngle + i * (MathF.PI * 2f / pCount);
                float px = (float)(pos.X + Math.Cos(a) * ringRadius);
                float pz = (float)(pos.Z + Math.Sin(a) * ringRadius);
                float py = (float)(pos.Y + ringHeight + Math.Sin(t * 3.5f + i) * 0.10f);

                float vx = -MathF.Sin(a) * rotDir * (1.6f + progress * 2f);
                float vz =  MathF.Cos(a) * rotDir * (1.6f + progress * 2f);
                float vy =  0.06f + progress * 0.10f;

                int r, g, b, al;
                if (ring == 0)
                {   // electric blue
                    r = (int)(30  + progress * 50);
                    g = (int)(100 + progress * 100);
                    b = 255;
                    al = (int)(150 + progress * 105);
                }
                else
                {   // soft violet-white
                    r = (int)(160 + progress * 95);
                    g = (int)(100 + progress * 120);
                    b = 255;
                    al = (int)(130 + progress * 110);
                }

                int color = (al << 24) | (r << 16) | (g << 8) | b;
                entity.World.SpawnParticles(1, color,
                    new Vec3d(px, py, pz), new Vec3d(px, py, pz),
                    new Vec3f(vx, vy, vz), new Vec3f(vx, vy, vz),
                    0.22f + progress * 0.22f, -0.01f,
                    0.055f + progress * 0.065f);
            }
        }
    }

    // Cyan-white streaks flowing inward from surroundings
    private static void SpawnEnergyInflow(EntityAgent entity, float t, float progress)
    {
        if (progress < 0.2f) return;
        var pos  = entity.Pos;
        var rng  = entity.World.Rand;
        int count = 2 + (int)(progress * 5);

        for (int i = 0; i < count; i++)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            double dist  = rng.NextDouble() * 3.0 + 1.5;
            double h     = rng.NextDouble() * 1.0;
            float  spd   = 2.5f + progress * 3f;
            float  vx    = (float)(-Math.Cos(angle) * spd);
            float  vz    = (float)(-Math.Sin(angle) * spd);
            int    al    = (int)(60 + progress * 140);
            int    color = (al << 24) | (180 << 16) | (230 << 8) | 255;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + Math.Cos(angle) * dist, pos.Y + h, pos.Z + Math.Sin(angle) * dist),
                new Vec3d(pos.X + Math.Cos(angle) * dist, pos.Y + h, pos.Z + Math.Sin(angle) * dist),
                new Vec3f(vx, 0.6f + progress * 0.8f, vz), new Vec3f(vx, 0.6f + progress * 0.8f, vz),
                0.12f + progress * 0.12f, -0.03f,
                0.035f + progress * 0.03f);
        }
    }

    // Sphere shell building around the player
    private static void SpawnSphereShell(EntityAgent entity, float t, float progress)
    {
        if (progress < 0.3f) return;
        float shellP  = (progress - 0.3f) / 0.7f;
        var   pos     = entity.Pos;
        float radius  = 1.5f + MathF.Sin(t * 5f) * 0.14f;
        int   N       = (int)(shellP * 20) + 3;
        float goldenA = MathF.PI * (3f - MathF.Sqrt(5f));
        int   offset  = (int)(t * 22);

        for (int i = 0; i < N; i++)
        {
            float fy    = 1f - ((i + offset) % 42) / 20.5f;
            float fr    = MathF.Sqrt(Math.Max(0f, 1f - fy * fy));
            float theta = goldenA * (i + offset);
            float fx    = MathF.Cos(theta) * fr;
            float fz    = MathF.Sin(theta) * fr;
            float vx    = -fx * 0.10f, vy = -fy * 0.05f, vz = -fz * 0.10f;

            // Blue-white, brightening with progress
            int al = (int)(80  + shellP * 175);
            int r  = (int)(80  + shellP * 170);
            int g  = (int)(120 + shellP * 130);
            int color = (al << 24) | (r << 16) | (g << 8) | 255;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + fx * radius, pos.Y + 1.0 + fy * radius, pos.Z + fz * radius),
                new Vec3d(pos.X + fx * radius, pos.Y + 1.0 + fy * radius, pos.Z + fz * radius),
                new Vec3f(vx, vy, vz), new Vec3f(vx, vy, vz),
                0.18f + shellP * 0.28f, 0f,
                0.04f + shellP * 0.055f);
        }
    }

    // ── Completion effects ────────────────────────────────────────────────────

    // Clean sphere burst — white core fading to blue
    private static void SpawnSphereBurst(EntityAgent entity)
    {
        var   pos         = entity.Pos;
        var   rng         = entity.World.Rand;
        const int N       = 120;
        float goldenAngle = MathF.PI * (3f - MathF.Sqrt(5f));

        for (int i = 0; i < N; i++)
        {
            float y   = 1f - (i / (float)(N - 1)) * 2f;
            float r   = MathF.Sqrt(1f - y * y);
            float th  = goldenAngle * i;
            float x   = MathF.Cos(th) * r;
            float z   = MathF.Sin(th) * r;
            float spd = 3.2f + (float)(rng.NextDouble() * 2.2);

            // White → blue gradient from center outward
            float t   = (float)i / N;
            int   cr  = (int)(255 - t * 180);
            int   cg  = (int)(255 - t * 130);
            int   color = (245 << 24) | (cr << 16) | (cg << 8) | 255;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X, pos.Y + 1.0, pos.Z),
                new Vec3d(pos.X, pos.Y + 1.0, pos.Z),
                new Vec3f(x * spd, y * spd + 0.4f, z * spd),
                new Vec3f(x * spd, y * spd + 0.4f, z * spd),
                0.65f + (float)(rng.NextDouble() * 0.45), -0.07f,
                0.09f + (float)(rng.NextDouble() * 0.10));
        }
    }

    // Single clean ground ring
    private static void SpawnGroundRing(EntityAgent entity)
    {
        var pos = entity.Pos;
        const int N = 52;
        for (int i = 0; i < N; i++)
        {
            float a     = i * (MathF.PI * 2f / N);
            float spd   = 4.5f;
            float vx    = MathF.Cos(a) * spd;
            float vz    = MathF.Sin(a) * spd;
            float t     = (float)i / N;
            int   cr    = (int)(60  + t * 100);
            int   cg    = (int)(140 + t * 90);
            int   color = (215 << 24) | (cr << 16) | (cg << 8) | 255;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X, pos.Y + 0.04, pos.Z),
                new Vec3d(pos.X, pos.Y + 0.04, pos.Z),
                new Vec3f(vx, 0.12f, vz), new Vec3f(vx, 0.12f, vz),
                0.55f, 0f, 0.08f);
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        => new[] { new WorldInteraction { ActionLangCode = "heldhelp-use", MouseButton = EnumMouseButton.Right } };
}
