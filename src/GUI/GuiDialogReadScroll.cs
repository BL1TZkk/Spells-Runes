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

        // ── Background ────────────────────────────────────────────────────────
        ctx.SetSourceRGBA(0.05, 0.04, 0.03, 0.97);
        RoundedRect(ctx, 0, 0, W, H, 4); ctx.Fill();

        ctx.SetSourceRGBA(0.42, 0.34, 0.22, 0.6);
        ctx.LineWidth = 1;
        RoundedRect(ctx, 0.5, 0.5, W - 1, H - 1, 4); ctx.Stroke();

        // Subtle inner parchment area
        ctx.SetSourceRGBA(0.12, 0.10, 0.07, 0.5);
        RoundedRect(ctx, PadX - 12, PadY - 10, W - (PadX - 12) * 2, H - (PadY - 10) * 2, 2); ctx.Fill();

        // ── Clip region for scrollable text ───────────────────────────────────
        double clipY = PadY - 10;
        double clipH = H - clipY * 2;
        ctx.Rectangle(PadX - 12, clipY, W - (PadX - 12) * 2, clipH);
        ctx.Clip();

        double cx = PadX;
        double cy = PadY - _scrollOffset;

        // Element accent bar
        uint elemColor = _entry.Element switch
        {
            "Fire" => 0xFF3050E0,
            "Air"  => 0xFFEBDCD7,
            _      => 0xFF6b5c45
        };
        ctx.SetSourceRGBA(
            ((elemColor >> 16) & 0xFF) / 255.0,
            ((elemColor >> 8)  & 0xFF) / 255.0,
            ((elemColor)       & 0xFF) / 255.0,
            0.55);
        ctx.Rectangle(cx - 14, cy, 2, 56); ctx.Fill();

        // Title
        ctx.SelectFontFace("Serif", FontSlant.Normal, FontWeight.Bold);
        ctx.SetFontSize(TitleSize);
        ctx.SetSourceRGBA(0.92, 0.86, 0.72, 0.95);
        ctx.MoveTo(cx, cy + TitleSize + 2);
        ctx.ShowText(_entry.Title);
        cy += TitleSize + 8;

        // Author
        ctx.SelectFontFace("Serif", FontSlant.Italic, FontWeight.Normal);
        ctx.SetFontSize(AuthorSize);
        ctx.SetSourceRGBA(0.62, 0.54, 0.40, 0.75);
        ctx.MoveTo(cx, cy + AuthorSize);
        if (!string.IsNullOrEmpty(_entry.Author))
        {
            ctx.ShowText("— " + _entry.Author);
        }
        cy += AuthorSize + 12;

        // Divider
        ctx.SetSourceRGBA(0.42, 0.34, 0.22, 0.4);
        ctx.LineWidth = 0.7;
        ctx.MoveTo(cx, cy); ctx.LineTo(W - cx, cy); ctx.Stroke();
        cy += 12;

        // Body text
        ctx.SelectFontFace("Serif", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(BodySize);
        ctx.SetSourceRGBA(0.82, 0.76, 0.62, 0.88);

        double textW = W - cx * 2;
        cy = DrawWrappedText(ctx, string.Join("\n\n", _entry.Body), cx, cy, textW, LineH);
        cy += 20;

        _contentHeight = cy + _scrollOffset;
        _maxScroll = Math.Max(0, _contentHeight - (H - PadY));

        ctx.ResetClip();

        // ── Scroll indicator ──────────────────────────────────────────────────
        if (_maxScroll > 0)
        {
            double trackH = H - PadY * 2;
            double thumbH = Math.Max(24, trackH * (H / _contentHeight));
            double thumbY = PadY + (_scrollOffset / _maxScroll) * (trackH - thumbH);
            ctx.SetSourceRGBA(0.42, 0.34, 0.22, 0.25);
            ctx.Rectangle(W - 10, PadY, 4, trackH); ctx.Fill();
            ctx.SetSourceRGBA(0.72, 0.60, 0.40, 0.5);
            ctx.Rectangle(W - 10, thumbY, 4, thumbH); ctx.Fill();
        }

        // ── Footer hint ───────────────────────────────────────────────────────
        ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(9.5);
        ctx.SetSourceRGBA(0.42, 0.38, 0.30, 0.5);
        string hint = _maxScroll > 0 ? "Scroll to read more   ·   [Esc] to close" : "[Esc] to close";
        var te = ctx.TextExtents(hint);
        ctx.MoveTo(W / 2 - te.Width / 2, H - 8);
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
