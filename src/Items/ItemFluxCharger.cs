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
    private const float CrazyStart  = 5f;
    private const int   DebuffMs    = 30_000;

    internal const string AttrDebuffEnd = "snr:fluxoverload_end";

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, bool firstEvent,
        ref EnumHandHandling handling)
    {
        if (!firstEvent) return;
        handling = EnumHandHandling.PreventDefault;

        var data = PlayerSpellData.For(byEntity);
        var flux = byEntity.GetBehavior<EntityBehaviorFlux>();
        if (data == null || !data.IsFluxUnlocked) return;
        if (flux == null || flux.GetFluxAlignmentLevel() >= EntityBehaviorFlux.MaxAlignmentLevel) return;

        Spells.SpellAnimations.Play(byEntity, "alignment_amplifier");

        if (byEntity.World.Side != EnumAppSide.Client) return;
        byEntity.World.PlaySoundAt(
            new AssetLocation("game:sounds/effect/deepbreath"),
            byEntity, null, false, 16f, 0.6f);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot,
        EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.World.Side == EnumAppSide.Client)
        {
            float crazyP = secondsUsed < CrazyStart
                ? 0f
                : (secondsUsed - CrazyStart) / (UseDuration - CrazyStart);

            if (crazyP > 0f)
            {
                float t = secondsUsed - CrazyStart;
                SpawnOrbitRings(byEntity, t, crazyP);
                SpawnSphereShell(byEntity, t, crazyP);
                byEntity.Pos.Motion.Y = 0.018f + crazyP * 0.035f;

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
        Spells.SpellAnimations.Stop(byEntity, "alignment_amplifier");

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

        ApplyDebuff(byEntity, maxFlux, DebuffMs);

        slot.TakeOut(1);
        slot.MarkDirty();
    }

    // Called from SpellsAndRunesMod on player join to handle reconnect
    internal static void ApplyDebuff(EntityAgent entity, float maxFlux, int durationMs)
    {
        long endTime = entity.World.ElapsedMilliseconds + durationMs;
        entity.WatchedAttributes.SetLong(AttrDebuffEnd, endTime);
        entity.WatchedAttributes.SetFloat("snr:fluxoverload_maxflux", maxFlux);
        entity.WatchedAttributes.MarkPathDirty(AttrDebuffEnd);

        entity.Stats.Set("walkspeed",          "fluxoverload", 0.40f, false);
        entity.Stats.Set("healingeffectivness", "fluxoverload", 0.00f, false);
        entity.WatchedAttributes.SetFloat("intoxication", 0.9f);
        entity.WatchedAttributes.MarkPathDirty("intoxication");

        entity.World.RegisterCallback(_ => RemoveDebuff(entity), durationMs);
    }

    internal static void RemoveDebuff(EntityAgent entity)
    {
        entity.Stats.Remove("walkspeed",           "fluxoverload");
        entity.Stats.Remove("healingeffectivness",  "fluxoverload");
        entity.WatchedAttributes.SetFloat("intoxication", 0f);
        entity.WatchedAttributes.MarkPathDirty("intoxication");

        float maxFlux = entity.WatchedAttributes.GetFloat("snr:fluxoverload_maxflux", 0f);
        if (maxFlux > 0f)
        {
            entity.WatchedAttributes.SetFloat("spellsandrunes:flux", maxFlux);
            entity.WatchedAttributes.MarkPathDirty("spellsandrunes:flux");
        }

        entity.WatchedAttributes.RemoveAttribute(AttrDebuffEnd);
        entity.WatchedAttributes.RemoveAttribute("snr:fluxoverload_maxflux");
        entity.WatchedAttributes.MarkPathDirty(AttrDebuffEnd);
    }

    // ── Effects ──────────────────────────────────────────────────────────────

    private static void SpawnOrbitRings(EntityAgent entity, float t, float progress)
    {
        var pos = entity.Pos;
        for (int ring = 0; ring < 2; ring++)
        {
            float radius    = 0.85f + ring * 0.42f;
            float height    = 0.75f + ring * 0.65f;
            float rotDir    = ring == 0 ? 1f : -1f;
            float speed     = 2.2f + progress * 2.8f;
            float baseAngle = t * speed * rotDir;
            int   pCount    = 5 + (int)(progress * 5);

            for (int i = 0; i < pCount; i++)
            {
                float a  = baseAngle + i * (MathF.PI * 2f / pCount);
                float px = (float)(pos.X + Math.Cos(a) * radius);
                float pz = (float)(pos.Z + Math.Sin(a) * radius);
                float py = (float)(pos.Y + height + Math.Sin(t * 3.5f + i) * 0.10f);
                float vx = -MathF.Sin(a) * rotDir * (1.6f + progress * 2f);
                float vz =  MathF.Cos(a) * rotDir * (1.6f + progress * 2f);

                int r, g, b, al;
                if (ring == 0) { r = (int)(30+progress*50); g = (int)(100+progress*100); b = 255; al = (int)(150+progress*105); }
                else           { r = (int)(160+progress*95); g = (int)(100+progress*120); b = 255; al = (int)(130+progress*110); }

                entity.World.SpawnParticles(1, (al<<24)|(r<<16)|(g<<8)|b,
                    new Vec3d(px, py, pz), new Vec3d(px, py, pz),
                    new Vec3f(vx, 0.06f + progress * 0.10f, vz),
                    new Vec3f(vx, 0.06f + progress * 0.10f, vz),
                    0.22f + progress * 0.22f, -0.01f,
                    0.055f + progress * 0.065f);
            }
        }
    }

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
            int   al    = (int)(80  + shellP * 175);
            int   r     = (int)(80  + shellP * 170);
            int   g     = (int)(120 + shellP * 130);

            entity.World.SpawnParticles(1, (al<<24)|(r<<16)|(g<<8)|255,
                new Vec3d(pos.X + fx * radius, pos.Y + 1.0 + fy * radius, pos.Z + fz * radius),
                new Vec3d(pos.X + fx * radius, pos.Y + 1.0 + fy * radius, pos.Z + fz * radius),
                new Vec3f(-fx * 0.10f, -fy * 0.05f, -fz * 0.10f),
                new Vec3f(-fx * 0.10f, -fy * 0.05f, -fz * 0.10f),
                0.18f + shellP * 0.28f, 0f, 0.04f + shellP * 0.055f);
        }
    }

    private static void SpawnSphereBurst(EntityAgent entity)
    {
        var   pos   = entity.Pos;
        var   rng   = entity.World.Rand;
        const int N = 120;
        float ga    = MathF.PI * (3f - MathF.Sqrt(5f));

        for (int i = 0; i < N; i++)
        {
            float y   = 1f - (i / (float)(N - 1)) * 2f;
            float r   = MathF.Sqrt(1f - y * y);
            float th  = ga * i;
            float x   = MathF.Cos(th) * r;
            float z   = MathF.Sin(th) * r;
            float spd = 3.2f + (float)(rng.NextDouble() * 2.2);
            float t   = (float)i / N;
            int   cr  = (int)(255 - t * 180);
            int   cg  = (int)(255 - t * 130);

            entity.World.SpawnParticles(1, (245<<24)|(cr<<16)|(cg<<8)|255,
                new Vec3d(pos.X, pos.Y + 1.0, pos.Z), new Vec3d(pos.X, pos.Y + 1.0, pos.Z),
                new Vec3f(x*spd, y*spd + 0.4f, z*spd), new Vec3f(x*spd, y*spd + 0.4f, z*spd),
                0.65f + (float)(rng.NextDouble() * 0.45), -0.07f,
                0.09f + (float)(rng.NextDouble() * 0.10));
        }
    }

    private static void SpawnGroundRing(EntityAgent entity)
    {
        var pos = entity.Pos;
        for (int i = 0; i < 52; i++)
        {
            float a   = i * (MathF.PI * 2f / 52);
            float t   = (float)i / 52;
            int   cr  = (int)(60  + t * 100);
            int   cg  = (int)(140 + t * 90);
            entity.World.SpawnParticles(1, (215<<24)|(cr<<16)|(cg<<8)|255,
                new Vec3d(pos.X, pos.Y + 0.04, pos.Z), new Vec3d(pos.X, pos.Y + 0.04, pos.Z),
                new Vec3f(MathF.Cos(a)*4.5f, 0.12f, MathF.Sin(a)*4.5f),
                new Vec3f(MathF.Cos(a)*4.5f, 0.12f, MathF.Sin(a)*4.5f),
                0.55f, 0f, 0.08f);
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        => new[] { new WorldInteraction { ActionLangCode = "heldhelp-use", MouseButton = EnumMouseButton.Right } };
}
