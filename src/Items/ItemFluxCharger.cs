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

        if (byEntity.World.Side != EnumAppSide.Client) return;

        var data = PlayerSpellData.For(byEntity);
        var flux = byEntity.GetBehavior<EntityBehaviorFlux>();
        if (data == null || !data.IsFluxUnlocked) return;
        if (flux == null || flux.GetFluxAlignmentLevel() >= EntityBehaviorFlux.MaxAlignmentLevel) return;

        byEntity.World.PlaySoundAt(
            new AssetLocation("game:sounds/effect/deepbreath"),
            byEntity, null, false, 16f, 0.7f);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot,
        EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        float progress = secondsUsed / UseDuration;

        if (byEntity.World.Side == EnumAppSide.Client)
        {
            SpawnOrbitRings(byEntity, secondsUsed, progress);
            SpawnGroundFracture(byEntity, progress);
            SpawnEnergyInflow(byEntity, secondsUsed, progress);
            SpawnSphereShell(byEntity, secondsUsed, progress);
            SpawnLightningFlicker(byEntity, secondsUsed, progress);
            SpawnDarkVoidRing(byEntity, secondsUsed, progress);

            // Levitate client-side — player physics runs here
            byEntity.Pos.Motion.Y = 0.018f + progress * 0.035f;

            if (secondsUsed > 2f && secondsUsed < 2.1f)
                byEntity.World.PlaySoundAt(
                    new AssetLocation("game:sounds/effect/deepbreath"),
                    byEntity, null, false, 16f, 0.5f);
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
            SpawnGroundRings(byEntity);
            SpawnUpwardPillar(byEntity);
            SpawnStarScatter(byEntity);
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

    // 3 contra-rotating orbit rings, accelerating
    private static void SpawnOrbitRings(EntityAgent entity, float t, float progress)
    {
        var pos = entity.Pos;
        for (int ring = 0; ring < 3; ring++)
        {
            float ringRadius  = 0.65f + ring * 0.40f;
            float ringHeight  = 0.55f + ring * 0.60f;
            float rotDir      = ring % 2 == 0 ? 1f : -1f;
            float speed       = 2.5f + progress * 3.0f;
            float baseAngle   = t * speed * rotDir;
            int   pCount      = 4 + ring * 2 + (int)(progress * 4);

            for (int i = 0; i < pCount; i++)
            {
                float a  = baseAngle + i * (MathF.PI * 2f / pCount);
                float px = (float)(pos.X + Math.Cos(a) * ringRadius);
                float pz = (float)(pos.Z + Math.Sin(a) * ringRadius);
                float py = (float)(pos.Y + ringHeight + Math.Sin(t * 4f + i) * 0.10f);

                float vx = -MathF.Sin(a) * rotDir * (1.8f + progress * 2f);
                float vz =  MathF.Cos(a) * rotDir * (1.8f + progress * 2f);
                float vy =  0.05f + progress * 0.12f;

                int r, g, b, al;
                if (ring == 0) { r = (int)(30 + progress*80);  g = (int)(80 + progress*120); b = 255; al = (int)(140+progress*115); }
                else if (ring == 1) { r = (int)(160+progress*95); g = (int)(50+progress*80);  b = 255; al = (int)(130+progress*125); }
                else               { r = 255; g = (int)(220+progress*35); b = (int)(100+progress*155); al = (int)(100+progress*120); }

                int color = (al << 24) | (r << 16) | (g << 8) | b;
                entity.World.SpawnParticles(1, color,
                    new Vec3d(px, py, pz), new Vec3d(px, py, pz),
                    new Vec3f(vx, vy, vz), new Vec3f(vx, vy, vz),
                    0.20f + progress * 0.25f, -0.01f,
                    0.05f + progress * 0.07f);
            }
        }
    }

    // Dark ember particles radiating from feet (ground fracture)
    private static void SpawnGroundFracture(EntityAgent entity, float progress)
    {
        if (progress < 0.15f) return;
        var pos = entity.Pos;
        var rng = entity.World.Rand;
        int count = (int)(progress * 5);

        for (int i = 0; i < count; i++)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            float spd = (float)(0.4 + progress * 1.2);
            float vx  = (float)(Math.Cos(angle) * spd);
            float vz  = (float)(Math.Sin(angle) * spd);
            int al = (int)(120 + progress * 100);
            int r  = (int)(60  + progress * 80);
            int color = (al << 24) | (r << 16) | (20 << 8) | 200;
            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X, pos.Y + 0.03, pos.Z),
                new Vec3d(pos.X, pos.Y + 0.03, pos.Z),
                new Vec3f(vx, 0.04f, vz), new Vec3f(vx, 0.04f, vz),
                0.25f + progress * 0.35f, 0f, 0.04f + progress * 0.03f);
        }
    }

    // Energy streaks converging from the surroundings inward
    private static void SpawnEnergyInflow(EntityAgent entity, float t, float progress)
    {
        if (progress < 0.25f) return;
        var pos = entity.Pos;
        var rng = entity.World.Rand;
        int count = (int)(progress * 6);

        for (int i = 0; i < count; i++)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            double dist  = rng.NextDouble() * 3.0 + 1.5;
            double h     = rng.NextDouble() * 0.8;
            float spd    = 2.5f + progress * 3f;
            float vx = (float)(-Math.Cos(angle) * spd);
            float vz = (float)(-Math.Sin(angle) * spd);
            int al = (int)(70 + progress * 130);
            int color = (al << 24) | (200 << 16) | (230 << 8) | 255;
            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + Math.Cos(angle) * dist, pos.Y + h, pos.Z + Math.Sin(angle) * dist),
                new Vec3d(pos.X + Math.Cos(angle) * dist, pos.Y + h, pos.Z + Math.Sin(angle) * dist),
                new Vec3f(vx, 0.8f + progress, vz), new Vec3f(vx, 0.8f + progress, vz),
                0.12f + progress * 0.12f, -0.04f, 0.03f + progress * 0.03f);
        }
    }

    // Sphere shell building up around the player
    private static void SpawnSphereShell(EntityAgent entity, float t, float progress)
    {
        if (progress < 0.35f) return;
        float shellP  = (progress - 0.35f) / 0.65f;
        var   pos     = entity.Pos;
        float radius  = 1.6f + MathF.Sin(t * 6f) * 0.18f;
        int   N       = (int)(shellP * 22) + 3;
        float goldenA = MathF.PI * (3f - MathF.Sqrt(5f));
        int   offset  = (int)(t * 25);

        for (int i = 0; i < N; i++)
        {
            float fy    = 1f - ((i + offset) % 44) / 21.5f;
            float fr    = MathF.Sqrt(Math.Max(0f, 1f - fy * fy));
            float theta = goldenA * (i + offset);
            float fx    = MathF.Cos(theta) * fr;
            float fz    = MathF.Sin(theta) * fr;
            float vx    = -fx * 0.12f, vy = -fy * 0.06f, vz = -fz * 0.12f;

            int al = (int)(90  + shellP * 165);
            int r  = (int)(100 + shellP * 155);
            int g  = (int)(40  + shellP * 80);
            int color = (al << 24) | (r << 16) | (g << 8) | 255;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + fx * radius, pos.Y + 1.0 + fy * radius, pos.Z + fz * radius),
                new Vec3d(pos.X + fx * radius, pos.Y + 1.0 + fy * radius, pos.Z + fz * radius),
                new Vec3f(vx, vy, vz), new Vec3f(vx, vy, vz),
                0.18f + shellP * 0.28f, 0f, 0.04f + shellP * 0.06f);
        }
    }

    // Vertical lightning flickers above the player
    private static void SpawnLightningFlicker(EntityAgent entity, float t, float progress)
    {
        if (progress < 0.55f) return;
        if ((int)(t * 10) % 4 != 0) return; // flickers

        var pos = entity.Pos;
        var rng = entity.World.Rand;
        float ox = (float)(rng.NextDouble() - 0.5) * 0.5f;
        float oz = (float)(rng.NextDouble() - 0.5) * 0.5f;

        for (int seg = 0; seg < 10; seg++)
        {
            float segH  = 2.0f + seg * 0.35f;
            float jx    = (float)(rng.NextDouble() - 0.5) * 0.2f;
            float jz    = (float)(rng.NextDouble() - 0.5) * 0.2f;
            int   color = (240 << 24) | (255 << 16) | (255 << 8) | 200;
            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + ox + jx, pos.Y + segH, pos.Z + oz + jz),
                new Vec3d(pos.X + ox + jx, pos.Y + segH, pos.Z + oz + jz),
                new Vec3f(0f, -3f, 0f), new Vec3f(0f, -3f, 0f),
                0.07f, 0f, 0.025f);
        }
    }

    // Dark void ring contracting inward before explosion
    private static void SpawnDarkVoidRing(EntityAgent entity, float t, float progress)
    {
        if (progress < 0.70f) return;
        float voidP  = (progress - 0.70f) / 0.30f;
        var   pos    = entity.Pos;
        float radius = 2.5f - voidP * 1.8f; // contracts from 2.5 to 0.7
        int   N      = 32;

        for (int i = 0; i < N; i++)
        {
            float a  = i * (MathF.PI * 2f / N) + t * 1.5f;
            float px = (float)(pos.X + Math.Cos(a) * radius);
            float pz = (float)(pos.Z + Math.Sin(a) * radius);
            float py = (float)(pos.Y + 1.0f);
            float vx = -(float)Math.Cos(a) * (1.5f + voidP * 2f);
            float vz = -(float)Math.Sin(a) * (1.5f + voidP * 2f);

            int al = (int)(180 + voidP * 75);
            int r  = (int)(20  + voidP * 30);
            int g  = (int)(0   + voidP * 20);
            int color = (al << 24) | (r << 16) | (g << 8) | 180;

            entity.World.SpawnParticles(1, color,
                new Vec3d(px, py, pz), new Vec3d(px, py, pz),
                new Vec3f(vx, 0.02f, vz), new Vec3f(vx, 0.02f, vz),
                0.18f, 0f, 0.06f);
        }
    }

    // ── Completion effects ────────────────────────────────────────────────────

    // 150 particles in a perfect sphere — white core to purple edge
    private static void SpawnSphereBurst(EntityAgent entity)
    {
        var   pos         = entity.Pos;
        var   rng         = entity.World.Rand;
        const int N       = 150;
        float goldenAngle = MathF.PI * (3f - MathF.Sqrt(5f));

        for (int i = 0; i < N; i++)
        {
            float y   = 1f - (i / (float)(N - 1)) * 2f;
            float r   = MathF.Sqrt(1f - y * y);
            float th  = goldenAngle * i;
            float x   = MathF.Cos(th) * r;
            float z   = MathF.Sin(th) * r;
            float spd = 3.5f + (float)(rng.NextDouble() * 2.5);

            float t   = (float)rng.NextDouble();
            int cr    = (int)(255 - t * 100);
            int cg    = (int)(255 - t * 220);
            int color = (245 << 24) | (cr << 16) | (cg << 8) | 255;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X, pos.Y + 1.0, pos.Z),
                new Vec3d(pos.X, pos.Y + 1.0, pos.Z),
                new Vec3f(x * spd, y * spd + 0.5f, z * spd),
                new Vec3f(x * spd, y * spd + 0.5f, z * spd),
                0.7f + (float)(rng.NextDouble() * 0.5), -0.08f,
                0.10f + (float)(rng.NextDouble() * 0.12));
        }
    }

    // 3 expanding ground rings at different radii
    private static void SpawnGroundRings(EntityAgent entity)
    {
        var pos = entity.Pos;
        for (int ring = 0; ring < 3; ring++)
        {
            float speedMult = 3.5f + ring * 1.5f;
            float height    = 0.04f + ring * 0.12f;
            int   N         = 56 - ring * 8;

            for (int i = 0; i < N; i++)
            {
                float a  = i * (MathF.PI * 2f / N);
                float vx = MathF.Cos(a) * speedMult;
                float vz = MathF.Sin(a) * speedMult;

                float t   = (float)i / N;
                int   cr  = (int)(60  + t * 140 + ring * 40);
                int   cg  = (int)(80  + t * 100 + ring * 30);
                int   color = (210 << 24) | (cr << 16) | (cg << 8) | 255;

                entity.World.SpawnParticles(1, color,
                    new Vec3d(pos.X, pos.Y + height, pos.Z),
                    new Vec3d(pos.X, pos.Y + height, pos.Z),
                    new Vec3f(vx, 0.15f, vz), new Vec3f(vx, 0.15f, vz),
                    0.55f - ring * 0.1f, 0f, 0.08f + ring * 0.02f);
            }
        }
    }

    // Pillar of particles shooting straight up
    private static void SpawnUpwardPillar(EntityAgent entity)
    {
        var pos = entity.Pos;
        var rng = entity.World.Rand;
        const int N = 40;

        for (int i = 0; i < N; i++)
        {
            float spd = 4f + (float)(rng.NextDouble() * 4f);
            float ox  = (float)(rng.NextDouble() - 0.5) * 0.3f;
            float oz  = (float)(rng.NextDouble() - 0.5) * 0.3f;
            float t   = (float)i / N;
            int   cr  = (int)(200 + t * 55);
            int   cg  = (int)(160 + t * 95);
            int   color = (230 << 24) | (cr << 16) | (cg << 8) | 255;

            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + ox, pos.Y + 0.5, pos.Z + oz),
                new Vec3d(pos.X + ox, pos.Y + 0.5, pos.Z + oz),
                new Vec3f(ox * 0.5f, spd, oz * 0.5f),
                new Vec3f(ox * 0.5f, spd, oz * 0.5f),
                0.8f + (float)(rng.NextDouble() * 0.4), -0.04f,
                0.08f + (float)(rng.NextDouble() * 0.08));
        }
    }

    // Large slow-fading star particles scattered around
    private static void SpawnStarScatter(EntityAgent entity)
    {
        var pos = entity.Pos;
        var rng = entity.World.Rand;
        const int N = 25;

        for (int i = 0; i < N; i++)
        {
            double angle  = rng.NextDouble() * Math.PI * 2;
            double dist   = rng.NextDouble() * 2.0;
            double height = rng.NextDouble() * 2.5 + 0.5;
            float  spd    = (float)(rng.NextDouble() * 0.6 + 0.2);
            float  vx     = (float)(Math.Cos(angle) * spd);
            float  vz     = (float)(Math.Sin(angle) * spd);

            int color = (220 << 24) | (255 << 16) | (240 << 8) | 255;
            entity.World.SpawnParticles(1, color,
                new Vec3d(pos.X + Math.Cos(angle) * dist, pos.Y + height, pos.Z + Math.Sin(angle) * dist),
                new Vec3d(pos.X + Math.Cos(angle) * dist, pos.Y + height, pos.Z + Math.Sin(angle) * dist),
                new Vec3f(vx, 0.1f, vz), new Vec3f(vx, 0.1f, vz),
                1.5f + (float)(rng.NextDouble() * 0.8), -0.02f,
                0.18f + (float)(rng.NextDouble() * 0.14));
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        => new[] { new WorldInteraction { ActionLangCode = "heldhelp-use", MouseButton = EnumMouseButton.Right } };
}
