using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using SpellsAndRunes.HUD;

namespace SpellsAndRunes.Render;

/// <summary>
/// Renders a rotating glowing ring between a player's hands when the spell wheel is active.
/// Visible in 3rd-person for all players with spellwheel_idle animation running.
/// </summary>
public class RadialCircleRenderer : IRenderer
{
    public double RenderOrder => 0.95;
    public int    RenderRange => 64;

    private readonly ICoreClientAPI capi;
    private readonly HudRadialMenu  radialMenu;

    private IShaderProgram? shader;
    private MeshRef?        meshRef;

    private float rotationAngle;
    private float frozenFwdX = 0f, frozenFwdZ = -1f;

    public void SetFrozenForward(float yaw)
    {
        frozenFwdX = MathF.Sin(yaw);
        frozenFwdZ = -MathF.Cos(yaw);
    }

    // One point light per active player ring
    private readonly List<RingLight> lights = new();
    private bool localLightRegistered = false;
    private readonly RingLight localLight = new();

    private const int   RingSegments  = 48;
    private const float RingRadius    = 0.18f;   // radius of the ring
    private const float RingWidth     = 0.03f;   // half-width of the ring band
    private const float RotateSpeed   = 0f;      // static ring, no spin
    private const float RingY         = 1.35f;   // height above entity feet (chest level)
    private const float RingForward   = 0.55f;   // offset forward in entity facing direction

    private class RingLight : IPointLight
    {
        // Cyan-blue glow: BGRA stored as Vec3f(b,g,r)
        public Vec3f Color { get; set; } = new Vec3f(1.0f, 0.6f, 0.1f);
        public Vec3d Pos   { get; set; } = new Vec3d();
    }

    public RadialCircleRenderer(ICoreClientAPI capi, HudRadialMenu radialMenu)
    {
        this.capi       = capi;
        this.radialMenu = radialMenu;
        InitShader();
        capi.Event.ReloadShader += OnReloadShader;
    }

    private bool OnReloadShader() { InitShader(); return true; }

    private bool InitShader()
    {
        var prog = capi.Shader.NewShaderProgram();
        prog.AssetDomain = "spellsandrunes";
        prog.VertexShader   = capi.Shader.NewShader(EnumShaderType.VertexShader);
        prog.FragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);

        prog.VertexShader.Code = @"
            #version 330 core
            layout(location = 0) in vec3 position;
            layout(location = 1) in vec2 uv;
            layout(location = 2) in vec4 color;
            uniform mat4 projectionMatrix;
            uniform mat4 modelViewMatrix;
            out vec4 vColor;
            out vec2 vUV;
            void main() {
                vColor = color;
                vUV    = uv;
                gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
            }";

        prog.FragmentShader.Code = @"
            #version 330 core
            in vec4 vColor;
            in vec2 vUV;
            out vec4 outColor;
            void main() {
                float a = vColor.a;
                outColor = vec4(vColor.rgb * a, a);
            }";

        if (!prog.Compile())
        {
            capi.Logger.Error($"[RadialCircleRenderer] Shader compile error: {prog.LoadError}");
            return false;
        }
        shader = prog;
        return true;
    }

    public void OnRenderFrame(float dt, EnumRenderStage stage)
    {
        if (shader == null) return;

        rotationAngle += RotateSpeed * dt;
        if (rotationAngle > MathF.PI * 2f) rotationAngle -= MathF.PI * 2f;

        // Collect entities that should show a ring
        var camPos  = capi.World.Player.Entity.CameraPos;
        var players = capi.World.AllOnlinePlayers;

        // (pos, fwdX, fwdZ) — forward from BodyYaw (visual body direction)
        var ringEntities = new List<(Vec3d pos, float fwdX, float fwdZ)>();

        var localEntity = capi.World.Player?.Entity;
        if (localEntity != null && radialMenu.IsOpen)
        {
            if (capi.World.Player?.CameraMode != EnumCameraMode.FirstPerson)
            {
                // Camera is behind the player in 3rd-person; camera→player = body forward
                float dx = (float)(localEntity.Pos.X - camPos.X);
                float dz = (float)(localEntity.Pos.Z - camPos.Z);
                float len = MathF.Sqrt(dx * dx + dz * dz);
                if (len > 0.001f) { frozenFwdX = dx / len; frozenFwdZ = dz / len; }
                ringEntities.Add((localEntity.Pos.XYZ, frozenFwdX, frozenFwdZ));
            }
        }

        foreach (var player in players)
        {
            var entity = player.Entity;
            if (entity == null) continue;
            if (entity.EntityId == capi.World.Player?.Entity?.EntityId) continue;
            if (entity.AnimManager != null && entity.AnimManager.IsAnimationActive("spellwheel_idle"))
            {
                double dist = entity.Pos.DistanceTo(capi.World.Player?.Entity?.Pos ?? entity.Pos);
                if (dist < RenderRange)
                {
                    float yaw = entity.BodyYaw;
                    ringEntities.Add((entity.Pos.XYZ, MathF.Sin(yaw), -MathF.Cos(yaw)));
                }
            }
        }

        // Update local point light
        if (ringEntities.Count > 0 && localEntity != null && radialMenu.IsOpen)
        {
            localLight.Pos = localEntity.Pos.XYZ.Add(0, RingY, 0);
            if (!localLightRegistered) { capi.Render.AddPointLight(localLight); localLightRegistered = true; }
        }
        else if (localLightRegistered)
        {
            capi.Render.RemovePointLight(localLight);
            localLightRegistered = false;
        }

        if (ringEntities.Count == 0) return;

        // Sigil style — matches HudRadialMenu palette
        double t      = capi.ElapsedMilliseconds / 1000.0;
        float  sigA   = (float)((0.5 + 0.5 * Math.Sin(t * 0.4)) * 0.35 + 0.22);
        float  lineAng = (float)(t * 0.04);   // slow hex rotation

        // Default sigil color (r=0.57, g=0.50, b=0.78) → BGRA: slot0=B, slot2=R
        byte sB = (byte)(0.78f * 255);
        byte sG = (byte)(0.50f * 255);
        byte sR = (byte)(0.57f * 255);

        // Per entity: 2 rings (outer glow band + inner ring) + 6 hexagram line quads
        const int lineQuads = 6;
        int perEntity = (RingSegments * 3 + lineQuads) * 4;
        var mesh = new MeshData(ringEntities.Count * perEntity, ringEntities.Count * perEntity / 4 * 6, false, true, true, false);
        mesh.mode = EnumDrawMode.Triangles;

        int vi = 0, ii = 0;

        float angleStep = MathF.PI * 2f / RingSegments;
        const float lineWidth = 0.006f;

        void AddQuad(float ax, float ay, float az, float bx, float by, float bz,
                     float cx2, float cy2, float cz2, float dx, float dy, float dz,
                     byte rb, byte rg, byte rr, byte ra)
        {
            int b = vi;
            mesh.xyz[vi*3+0]=ax; mesh.xyz[vi*3+1]=ay; mesh.xyz[vi*3+2]=az;
            mesh.Rgba[vi*4+0]=rb; mesh.Rgba[vi*4+1]=rg; mesh.Rgba[vi*4+2]=rr; mesh.Rgba[vi*4+3]=ra; vi++;
            mesh.xyz[vi*3+0]=bx; mesh.xyz[vi*3+1]=by; mesh.xyz[vi*3+2]=bz;
            mesh.Rgba[vi*4+0]=rb; mesh.Rgba[vi*4+1]=rg; mesh.Rgba[vi*4+2]=rr; mesh.Rgba[vi*4+3]=ra; vi++;
            mesh.xyz[vi*3+0]=cx2; mesh.xyz[vi*3+1]=cy2; mesh.xyz[vi*3+2]=cz2;
            mesh.Rgba[vi*4+0]=rb; mesh.Rgba[vi*4+1]=rg; mesh.Rgba[vi*4+2]=rr; mesh.Rgba[vi*4+3]=ra; vi++;
            mesh.xyz[vi*3+0]=dx; mesh.xyz[vi*3+1]=dy; mesh.xyz[vi*3+2]=dz;
            mesh.Rgba[vi*4+0]=rb; mesh.Rgba[vi*4+1]=rg; mesh.Rgba[vi*4+2]=rr; mesh.Rgba[vi*4+3]=ra; vi++;
            mesh.Indices[ii++]=b; mesh.Indices[ii++]=b+1; mesh.Indices[ii++]=b+2;
            mesh.Indices[ii++]=b; mesh.Indices[ii++]=b+2; mesh.Indices[ii++]=b+3;
        }

        foreach (var (entityPos, fwdX, fwdZ) in ringEntities)
        {
            // Center: body-forward offset so ring is in front of chest
            float cx = (float)(entityPos.X - camPos.X) + fwdX * RingForward;
            float cy = (float)(entityPos.Y - camPos.Y) + RingY;
            float cz = (float)(entityPos.Z - camPos.Z) + fwdZ * RingForward;

            // Ring plane: (right, world_up), normal = body forward
            float rightX = -fwdZ, rightZ = fwdX;

            // Point on ring at angle a, radius r
            (float x, float y, float z) RingPt(float a, float r) =>
                (cx + rightX * MathF.Cos(a) * r,
                 cy +          MathF.Sin(a) * r,
                 cz + rightZ * MathF.Cos(a) * r);

            // ── Outer ring: thick glow band (alpha 35%) + thin bright edge ──────
            float ri  = RingRadius - RingWidth;
            float ro  = RingRadius + RingWidth;
            float riT = RingRadius - 0.004f;   // thin inner edge
            float roT = RingRadius + 0.004f;   // thin outer edge

            byte glowA  = (byte)(sigA * 0.35f * 255);
            byte edgeA  = (byte)(sigA         * 255);

            for (int s = 0; s < RingSegments; s++)
            {
                float a0 = s * angleStep, a1 = (s + 1) * angleStep;
                var (x0i, y0i, z0i) = RingPt(a0, ri);
                var (x0o, y0o, z0o) = RingPt(a0, ro);
                var (x1o, y1o, z1o) = RingPt(a1, ro);
                var (x1i, y1i, z1i) = RingPt(a1, ri);
                AddQuad(x0i,y0i,z0i, x0o,y0o,z0o, x1o,y1o,z1o, x1i,y1i,z1i, sB,sG,sR,glowA);

                var (x0ti, y0ti, z0ti) = RingPt(a0, riT);
                var (x0to, y0to, z0to) = RingPt(a0, roT);
                var (x1to, y1to, z1to) = RingPt(a1, roT);
                var (x1ti, y1ti, z1ti) = RingPt(a1, riT);
                AddQuad(x0ti,y0ti,z0ti, x0to,y0to,z0to, x1to,y1to,z1to, x1ti,y1ti,z1ti, sB,sG,sR,edgeA);
            }

            // ── Inner ring at 0.62× radius ────────────────────────────────────
            float rInner = RingRadius * 0.62f;
            float riIn   = rInner - 0.004f;
            float roIn   = rInner + 0.004f;
            byte  innerA = (byte)(sigA * 0.55f * 255);

            for (int s = 0; s < RingSegments; s++)
            {
                float a0 = s * angleStep, a1 = (s + 1) * angleStep;
                var (x0i, y0i, z0i) = RingPt(a0, riIn);
                var (x0o, y0o, z0o) = RingPt(a0, roIn);
                var (x1o, y1o, z1o) = RingPt(a1, roIn);
                var (x1i, y1i, z1i) = RingPt(a1, riIn);
                AddQuad(x0i,y0i,z0i, x0o,y0o,z0o, x1o,y1o,z1o, x1i,y1i,z1i, sB,sG,sR,innerA);
            }

            // ── Hexagram: 6 lines connecting every-other hexagon vertex ─────
            byte hexA = (byte)(sigA * 0.80f * 255);
            for (int k = 0; k < 6; k++)
            {
                float a1 = k          * MathF.PI / 3f + lineAng;
                float a2 = ((k + 2) % 6) * MathF.PI / 3f + lineAng;

                var (px1, py1, pz1) = RingPt(a1, RingRadius);
                var (px2, py2, pz2) = RingPt(a2, RingRadius);

                // Perpendicular to line in ring plane (right, up)
                float dx = px2 - px1, dy = py2 - py1, dz = pz2 - pz1;
                float len = MathF.Sqrt(dx*dx + dy*dy + dz*dz);
                if (len < 0.0001f) continue;
                float dirR = dx * rightX + dz * rightZ;  // component along right
                float dirU = dy;                           // component along up
                float pw = lineWidth / len;
                float perpX = (-dirU * rightX) * pw;
                float perpY = ( dirR          ) * pw;
                float perpZ = (-dirU * rightZ ) * pw;

                AddQuad(px1+perpX, py1+perpY, pz1+perpZ,
                        px1-perpX, py1-perpY, pz1-perpZ,
                        px2-perpX, py2-perpY, pz2-perpZ,
                        px2+perpX, py2+perpY, pz2+perpZ,
                        sB, sG, sR, hexA);
            }
        }

        mesh.VerticesCount = vi;
        mesh.IndicesCount  = ii;

        meshRef?.Dispose();
        meshRef = capi.Render.UploadMesh(mesh);

        var rapi = capi.Render;
        shader.Use();
        shader.UniformMatrix("projectionMatrix", rapi.CurrentProjectionMatrix);
        shader.UniformMatrix("modelViewMatrix",  rapi.CameraMatrixOriginf);

        rapi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);
        rapi.GLDepthMask(false);
        rapi.GlDisableCullFace();

        rapi.RenderMesh(meshRef);

        rapi.GLDepthMask(true);
        rapi.GlToggleBlend(true);
        rapi.GlEnableCullFace();

        shader.Stop();
    }

    public void Dispose()
    {
        capi.Event.ReloadShader -= OnReloadShader;
        if (localLightRegistered) capi.Render.RemovePointLight(localLight);
        shader?.Dispose();
        meshRef?.Dispose();
    }
}
