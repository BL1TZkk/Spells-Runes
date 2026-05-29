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
        double sc = GuiElement.scaled(1.0);

        double padX     = sc * 36;
        double padY     = sc * 28;
        double titleSz  = sc * 15;
        double authorSz = sc * 10.5;
        double bodySz   = sc * 11;
        double lineH    = sc * 16;
        double headerH  = sc * 50;
        double round    = sc * 6;

        var (ar, ag, ab)    = LoreTheme.GetAccent(_entry.Theme);
        var (bgr, bgg, bgb) = LoreTheme.GetBackground(_entry.Theme);
        var (pr, pg, pb)    = LoreTheme.GetParchment(_entry.Theme);
        var (tr, tg, tb)    = LoreTheme.GetTitleColor(_entry.Theme);
        string groupLabel   = LoreTheme.GetGroupLabel(_entry.Theme);

        // ── Background ────────────────────────────────────────────────────────
        ctx.SetSourceRGBA(bgr, bgg, bgb, 0.97);
        RoundedRect(ctx, 0, 0, W, H, round); ctx.Fill();

        // Outer border — theme accent color
        ctx.SetSourceRGBA(ar, ag, ab, 0.70);
        ctx.LineWidth = sc * 1.5;
        RoundedRect(ctx, sc * 0.75, sc * 0.75, W - sc * 1.5, H - sc * 1.5, round); ctx.Stroke();

        // Inner highlight line
        ctx.SetSourceRGBA(ar, ag, ab, 0.18);
        ctx.LineWidth = sc;
        RoundedRect(ctx, sc * 3, sc * 3, W - sc * 6, H - sc * 6, round - sc); ctx.Stroke();

        // ── Header strip ──────────────────────────────────────────────────────
        ctx.SetSourceRGBA(ar, ag, ab, 0.22);
        RoundedRect(ctx, 0, 0, W, headerH, round); ctx.Fill();
        ctx.SetSourceRGBA(ar, ag, ab, 0.08);
        ctx.Rectangle(0, headerH - sc * 8, W, sc * 8); ctx.Fill();

        ctx.SetSourceRGBA(ar, ag, ab, 0.55);
        ctx.LineWidth = sc;
        ctx.MoveTo(0, headerH); ctx.LineTo(W, headerH); ctx.Stroke();

        // Group label (left)
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(sc * 9);
        ctx.SetSourceRGBA(ar, ag, ab, 0.90);
        ctx.MoveTo(padX - sc * 4, headerH * 0.60);
        ctx.ShowText(groupLabel);

        // Illustration in header (right) — clear path first to avoid Cairo arc line artifact
        double illH = sc * 36;
        LoreTheme.DrawIllustration(ctx, _entry.Theme, W - padX - illH * 0.45, headerH * 0.50, illH, 0.85);

        // ── Body parchment area ───────────────────────────────────────────────
        double bodyTop = headerH + sc * 4;
        ctx.SetSourceRGBA(pr, pg, pb, 0.42);
        RoundedRect(ctx, padX - sc * 12, bodyTop, W - (padX - sc * 12) * 2, H - bodyTop - sc * 8, sc * 3); ctx.Fill();

        // Centered watermark
        double wmSize = Math.Min(W, H - headerH) * 0.46;
        LoreTheme.DrawIllustration(ctx, _entry.Theme, W * 0.5, bodyTop + (H - bodyTop) * 0.48, wmSize, 0.09);

        // ── Clip for scrollable text ──────────────────────────────────────────
        double clipH = H - bodyTop - sc * 24;
        ctx.Rectangle(padX - sc * 12, bodyTop, W - (padX - sc * 12) * 2, clipH);
        ctx.Clip();

        double cx = padX;
        double cy = bodyTop + sc * 14 - _scrollOffset;

        // Left accent bar
        ctx.SetSourceRGBA(ar, ag, ab, 0.70);
        ctx.Rectangle(cx - sc * 16, cy, sc * 3.5, titleSz + authorSz + sc * 22); ctx.Fill();

        // Title
        ctx.SelectFontFace("Serif", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(titleSz + sc);
        ctx.SetSourceRGBA(tr, tg, tb, 0.98);
        ctx.MoveTo(cx, cy + titleSz + sc * 2);
        ctx.ShowText(_entry.Title);
        cy += titleSz + sc * 8;

        // Author
        ctx.SelectFontFace("Serif", FontSlant.Italic, FontWeight.Normal);
        ctx.SetFontSize(authorSz);
        ctx.SetSourceRGBA(ar, ag, ab, 0.80);
        ctx.MoveTo(cx, cy + authorSz);
        if (!string.IsNullOrEmpty(_entry.Author))
            ctx.ShowText("— " + _entry.Author);
        cy += authorSz + sc * 10;

        // Divider
        ctx.SetSourceRGBA(ar, ag, ab, 0.50);
        ctx.LineWidth = sc * 0.8;
        ctx.MoveTo(cx, cy); ctx.LineTo(W - cx, cy); ctx.Stroke();
        cy += sc * 14;

        // Body text
        ctx.SelectFontFace("Serif", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(bodySz);
        ctx.SetSourceRGBA(0.88, 0.84, 0.74, 0.90);
        double textW = W - cx * 2;
        cy = DrawWrappedText(ctx, string.Join("\n\n", _entry.Body), cx, cy, textW, lineH);
        cy += sc * 20;

        _contentHeight = cy + _scrollOffset;
        _maxScroll = Math.Max(0, _contentHeight - (H - padY));

        ctx.ResetClip();

        // ── Scrollbar ─────────────────────────────────────────────────────────
        if (_maxScroll > 0)
        {
            double trackH = H - bodyTop - sc * 28;
            double thumbH = Math.Max(sc * 24, trackH * ((H - bodyTop) / _contentHeight));
            double thumbY = bodyTop + sc * 4 + (_scrollOffset / _maxScroll) * (trackH - thumbH);
            ctx.SetSourceRGBA(ar, ag, ab, 0.15);
            ctx.Rectangle(W - sc * 10, bodyTop + sc * 4, sc * 4, trackH); ctx.Fill();
            ctx.SetSourceRGBA(ar, ag, ab, 0.55);
            ctx.Rectangle(W - sc * 10, thumbY, sc * 4, thumbH); ctx.Fill();
        }

        // ── Footer hint ───────────────────────────────────────────────────────
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(sc * 9.5);
        ctx.SetSourceRGBA(ar, ag, ab, 0.45);
        string hint = _maxScroll > 0 ? "Scroll to read more   ·   [Esc] or right-click to close" : "[Esc] or right-click to close";
        var te = ctx.TextExtents(hint);
        ctx.MoveTo(W / 2 - te.Width / 2, H - sc * 7);
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
        _scrollOffset = Math.Clamp(_scrollOffset - args.deltaPrecise * GuiElement.scaled(24), 0, _maxScroll);
        (SingleComposer?.GetElement("canvas") as GuiElementCustomDraw)?.Redraw();
        args.SetHandled(true);
    }

    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
}
