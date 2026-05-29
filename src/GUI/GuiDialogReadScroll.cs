using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using SpellsAndRunes.Lore;

namespace SpellsAndRunes.GUI;

public class GuiDialogReadScroll : GuiDialog
{
    private readonly SpellbookLoreEntry _entry;
    private double _scrollOffset;
    private double _maxScroll;
    private double _contentHeight;

    private const double PadX = 36, PadY = 28;
    private const double TitleSize = 15, AuthorSize = 10.5, BodySize = 11;
    private const double LineH = 16;

    public GuiDialogReadScroll(ICoreClientAPI capi, SpellbookLoreEntry entry) : base(capi)
    {
        _entry = entry;
        ComposeDialog();
    }

    public override string ToggleKeyCombinationCode => null!;

    private void ComposeDialog()
    {
        double sc = GuiElement.scaled(1.0);
        double vw = capi.Render.FrameWidth / sc;
        double vh = capi.Render.FrameHeight / sc;
        double w = Math.Clamp(vw * 0.52, 420, 620);
        double h = Math.Clamp(vh * 0.78, 380, 580);

        var db = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, 0, w, h);
        var cb = ElementBounds.Fixed(0, 0, w, h);

        SingleComposer = capi.Gui
            .CreateCompo("spellsandrunes:readscroll", db)
            .AddDynamicCustomDraw(cb, OnDraw, "canvas")
            .Compose();
    }

    private void OnDraw(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        double W = bounds.InnerWidth, H = bounds.InnerHeight;

        var (ar, ag, ab)   = LoreTheme.GetAccent(_entry.Theme);
        var (bgr, bgg, bgb) = LoreTheme.GetBackground(_entry.Theme);
        var (pr, pg, pb)   = LoreTheme.GetParchment(_entry.Theme);
        var (tr, tg, tb)   = LoreTheme.GetTitleColor(_entry.Theme);
        string groupLabel  = LoreTheme.GetGroupLabel(_entry.Theme);

        // ── Background ────────────────────────────────────────────────────────
        ctx.SetSourceRGBA(bgr, bgg, bgb, 0.97);
        RoundedRect(ctx, 0, 0, W, H, 6); ctx.Fill();

        // Outer border — theme accent color
        ctx.SetSourceRGBA(ar, ag, ab, 0.70);
        ctx.LineWidth = 1.5;
        RoundedRect(ctx, 0.75, 0.75, W - 1.5, H - 1.5, 6); ctx.Stroke();

        // Inner bright line just inside border
        ctx.SetSourceRGBA(ar, ag, ab, 0.18);
        ctx.LineWidth = 1;
        RoundedRect(ctx, 3, 3, W - 6, H - 6, 5); ctx.Stroke();

        // ── Header strip ──────────────────────────────────────────────────────
        const double HeaderH = 52;
        // Gradient-like header: two filled rects
        ctx.SetSourceRGBA(ar, ag, ab, 0.22);
        RoundedRect(ctx, 0, 0, W, HeaderH, 6); ctx.Fill();
        ctx.SetSourceRGBA(ar, ag, ab, 0.08);
        ctx.Rectangle(0, HeaderH - 8, W, 8); ctx.Fill();

        // Header bottom edge line
        ctx.SetSourceRGBA(ar, ag, ab, 0.55);
        ctx.LineWidth = 1;
        ctx.MoveTo(0, HeaderH); ctx.LineTo(W, HeaderH); ctx.Stroke();

        // Group label in header (left side)
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(9);
        ctx.SetSourceRGBA(ar, ag, ab, 0.90);
        ctx.MoveTo(PadX - 4, HeaderH * 0.58);
        ctx.ShowText(groupLabel);

        // Illustration in header (right side) — visible, not a watermark
        double illH = 38;
        LoreTheme.DrawIllustration(ctx, _entry.Theme, W - PadX - illH * 0.45, HeaderH * 0.50, illH, 0.85);

        // ── Body parchment area ───────────────────────────────────────────────
        double bodyTop = HeaderH + 4;
        ctx.SetSourceRGBA(pr, pg, pb, 0.42);
        RoundedRect(ctx, PadX - 12, bodyTop, W - (PadX - 12) * 2, H - bodyTop - 8, 3); ctx.Fill();

        // Large centered watermark illustration
        double wmSize = Math.Min(W, H - HeaderH) * 0.46;
        LoreTheme.DrawIllustration(ctx, _entry.Theme, W * 0.5, bodyTop + (H - bodyTop) * 0.48, wmSize, 0.09);

        // ── Clip region for scrollable text ───────────────────────────────────
        double clipY = bodyTop;
        double clipH = H - clipY - 24;
        ctx.Rectangle(PadX - 12, clipY, W - (PadX - 12) * 2, clipH);
        ctx.Clip();

        double cx = PadX;
        double cy = bodyTop + 14 - _scrollOffset;

        // Left accent bar — thick, theme colored
        ctx.SetSourceRGBA(ar, ag, ab, 0.70);
        ctx.Rectangle(cx - 16, cy, 3.5, TitleSize + AuthorSize + 22); ctx.Fill();

        // Title
        ctx.SelectFontFace("Serif", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(TitleSize + 1);
        ctx.SetSourceRGBA(tr, tg, tb, 0.98);
        ctx.MoveTo(cx, cy + TitleSize + 2);
        ctx.ShowText(_entry.Title);
        cy += TitleSize + 8;

        // Author
        ctx.SelectFontFace("Serif", FontSlant.Italic, FontWeight.Normal);
        ctx.SetFontSize(AuthorSize);
        ctx.SetSourceRGBA(ar, ag, ab, 0.80);
        ctx.MoveTo(cx, cy + AuthorSize);
        if (!string.IsNullOrEmpty(_entry.Author))
            ctx.ShowText("— " + _entry.Author);
        cy += AuthorSize + 10;

        // Divider — theme colored
        ctx.SetSourceRGBA(ar, ag, ab, 0.50);
        ctx.LineWidth = 0.8;
        ctx.MoveTo(cx, cy); ctx.LineTo(W - cx, cy); ctx.Stroke();
        cy += 14;

        // Body text
        ctx.SelectFontFace("Serif", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(BodySize);
        ctx.SetSourceRGBA(0.88, 0.84, 0.74, 0.90);

        double textW = W - cx * 2;
        cy = DrawWrappedText(ctx, string.Join("\n\n", _entry.Body), cx, cy, textW, LineH);
        cy += 20;

        _contentHeight = cy + _scrollOffset;
        _maxScroll = Math.Max(0, _contentHeight - (H - PadY));

        ctx.ResetClip();

        // ── Scroll indicator ──────────────────────────────────────────────────
        if (_maxScroll > 0)
        {
            double trackH = H - bodyTop - 28;
            double thumbH = Math.Max(24, trackH * ((H - bodyTop) / _contentHeight));
            double thumbY = bodyTop + 4 + (_scrollOffset / _maxScroll) * (trackH - thumbH);
            ctx.SetSourceRGBA(ar, ag, ab, 0.15);
            ctx.Rectangle(W - 10, bodyTop + 4, 4, trackH); ctx.Fill();
            ctx.SetSourceRGBA(ar, ag, ab, 0.55);
            ctx.Rectangle(W - 10, thumbY, 4, thumbH); ctx.Fill();
        }

        // ── Footer hint ───────────────────────────────────────────────────────
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(9.5);
        ctx.SetSourceRGBA(ar, ag, ab, 0.45);
        string hint = _maxScroll > 0 ? "Scroll to read more   ·   [Esc] or right-click to close" : "[Esc] or right-click to close";
        var te = ctx.TextExtents(hint);
        ctx.MoveTo(W / 2 - te.Width / 2, H - 7);
        ctx.ShowText(hint);
    }

    private static double DrawWrappedText(Context ctx, string text, double x, double y, double maxW, double lineH)
    {
        double cy = y;
        foreach (var para in text.Split('\n'))
        {
            if (string.IsNullOrEmpty(para))
            {
                cy += lineH * 0.6;
                continue;
            }

            string line = "";
            foreach (var word in para.Split(' '))
            {
                string test = line.Length == 0 ? word : line + " " + word;
                if (ctx.TextExtents(test).Width > maxW && line.Length > 0)
                {
                    ctx.MoveTo(x, cy + lineH * 0.8);
                    ctx.ShowText(line);
                    cy += lineH;
                    line = word;
                }
                else line = test;
            }
            if (line.Length > 0)
            {
                ctx.MoveTo(x, cy + lineH * 0.8);
                ctx.ShowText(line);
                cy += lineH;
            }
        }
        return cy;
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

    public override void OnMouseDown(MouseEvent args)
    {
        if (args.Button == EnumMouseButton.Right || !IsInsideDialog(args.X, args.Y))
        {
            TryClose();
            args.Handled = true;
            return;
        }
        base.OnMouseDown(args);
    }

    private bool IsInsideDialog(int mx, int my)
    {
        var b = SingleComposer?.Bounds;
        if (b == null) return true;
        return mx >= b.absX && mx <= b.absX + b.OuterWidth &&
               my >= b.absY && my <= b.absY + b.OuterHeight;
    }

    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        _scrollOffset = Math.Clamp(_scrollOffset - args.deltaPrecise * 24, 0, _maxScroll);
        (SingleComposer?.GetElement("canvas") as GuiElementCustomDraw)?.Redraw();
        args.SetHandled(true);
    }

    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
}
