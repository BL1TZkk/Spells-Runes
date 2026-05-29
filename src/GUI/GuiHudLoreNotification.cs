using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SpellsAndRunes.GUI;

public class GuiHudLoreNotification : HudElement
{
    private readonly string _title;
    private readonly string _category;
    private float _elapsed;
    private const float Duration = 5f;
    private const float FadeIn = 0.35f;
    private const float FadeOut = 0.9f;
    private long _tickId;

    public GuiHudLoreNotification(ICoreClientAPI capi, string title, string category = "lore") : base(capi)
    {
        _title = title;
        _category = category;
        ComposeGui();
        TryOpen();
        _tickId = capi.Event.RegisterGameTickListener(Tick, 40);
        capi.World.PlaySoundAt(
            new AssetLocation("game:sounds/player/chalkdraw1"),
            capi.World.Player.Entity, null, false, 16f, 0.65f);
    }

    private void ComposeGui()
    {
        double w = 260, h = 58;
        var bounds = ElementBounds.Fixed(EnumDialogArea.RightBottom, -18, -80, w, h);
        SingleComposer = capi.Gui
            .CreateCompo("spellsandrunes:lorenotif", bounds)
            .AddDynamicCustomDraw(ElementBounds.Fixed(0, 0, w, h), Draw, "canvas")
            .Compose();
    }

    private void Draw(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        float t = Math.Clamp(_elapsed / Duration, 0f, 1f);
        double alpha = t < FadeIn / Duration
            ? t / (FadeIn / Duration)
            : t > 1f - FadeOut / Duration
                ? (1f - t) / (FadeOut / Duration)
                : 1.0;
        alpha = Math.Clamp(alpha, 0, 1);

        double W = bounds.InnerWidth, H = bounds.InnerHeight;

        // Background
        ctx.SetSourceRGBA(0.06, 0.05, 0.04, 0.93 * alpha);
        RoundedRect(ctx, 0, 0, W, H, 4); ctx.Fill();

        // Left accent bar
        ctx.SetSourceRGBA(0.55, 0.42, 0.22, 0.85 * alpha);
        ctx.Rectangle(0, 0, 3, H); ctx.Fill();

        // Border
        ctx.SetSourceRGBA(0.42, 0.34, 0.22, 0.5 * alpha);
        ctx.LineWidth = 1;
        RoundedRect(ctx, 0.5, 0.5, W - 1, H - 1, 4); ctx.Stroke();

        // "NEW ENTRY" label
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(8.5);
        ctx.SetSourceRGBA(0.72, 0.55, 0.25, 0.75 * alpha);
        ctx.MoveTo(12, 16);
        ctx.ShowText(_category == "journal" ? "NEW JOURNAL ENTRY" : "NEW LORE ENTRY");

        // Title
        ctx.SelectFontFace("Serif", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(13);
        ctx.SetSourceRGBA(0.90, 0.84, 0.68, 0.95 * alpha);
        string display = _title.Length > 34 ? _title[..31] + "…" : _title;
        ctx.MoveTo(12, 38);
        ctx.ShowText(display);
    }

    private void Tick(float dt)
    {
        _elapsed += dt;
        (SingleComposer?.GetElement("canvas") as GuiElementCustomDraw)?.Redraw();
        if (_elapsed >= Duration)
        {
            capi.Event.UnregisterGameTickListener(_tickId);
            TryClose();
        }
    }

    private static void RoundedRect(Context ctx, double x, double y, double w, double h, double r)
    {
        ctx.MoveTo(x + r, y);
        ctx.LineTo(x + w - r, y);
        ctx.Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
        ctx.LineTo(x + w, y + h - r);
        ctx.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
        ctx.LineTo(x + r, y + h);
        ctx.Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
        ctx.LineTo(x, y + r);
        ctx.Arc(x + r, y + r, r, Math.PI, Math.PI * 3 / 2);
        ctx.ClosePath();
    }

    public override bool ShouldReceiveMouseEvents() => false;
    public override bool Focusable => false;
}
