using System;
using Cairo;

namespace SpellsAndRunes.GUI;

public static class LoreTheme
{
    public static (double r, double g, double b) GetAccent(string? theme) => theme switch
    {
        "ashveil"      => (0.78, 0.18, 0.10),
        "fieldmanual"  => (0.52, 0.42, 0.22),
        "aldenmoor"    => (0.45, 0.62, 0.78),
        "voss"         => (0.50, 0.62, 0.72),
        "journal"      => (0.72, 0.58, 0.28),
        "journal_fire" => (0.92, 0.58, 0.18),
        "journal_air"  => (0.52, 0.75, 0.90),
        _              => (0.55, 0.42, 0.22),
    };

    // Dark background tint per theme — replaces the neutral near-black
    public static (double r, double g, double b) GetBackground(string? theme) => theme switch
    {
        "ashveil"      => (0.09, 0.02, 0.02),
        "fieldmanual"  => (0.07, 0.06, 0.03),
        "aldenmoor"    => (0.02, 0.05, 0.10),
        "voss"         => (0.03, 0.05, 0.08),
        "journal"      => (0.08, 0.06, 0.03),
        "journal_fire" => (0.11, 0.03, 0.01),
        "journal_air"  => (0.02, 0.07, 0.10),
        _              => (0.05, 0.04, 0.03),
    };

    // Inner parchment / body area tint
    public static (double r, double g, double b) GetParchment(string? theme) => theme switch
    {
        "ashveil"      => (0.18, 0.06, 0.05),
        "fieldmanual"  => (0.14, 0.12, 0.07),
        "aldenmoor"    => (0.06, 0.10, 0.18),
        "voss"         => (0.07, 0.10, 0.15),
        "journal"      => (0.16, 0.12, 0.06),
        "journal_fire" => (0.20, 0.08, 0.02),
        "journal_air"  => (0.05, 0.12, 0.18),
        _              => (0.12, 0.10, 0.07),
    };

    // Title text color — prominent, on-brand
    public static (double r, double g, double b) GetTitleColor(string? theme) => theme switch
    {
        "ashveil"      => (0.95, 0.55, 0.42),
        "fieldmanual"  => (0.85, 0.78, 0.52),
        "aldenmoor"    => (0.68, 0.88, 1.00),
        "voss"         => (0.72, 0.90, 1.00),
        "journal"      => (0.98, 0.88, 0.58),
        "journal_fire" => (1.00, 0.72, 0.22),
        "journal_air"  => (0.72, 0.94, 1.00),
        _              => (0.92, 0.86, 0.72),
    };

    // Group / faction label shown in the header strip
    public static string GetGroupLabel(string? theme) => theme switch
    {
        "ashveil"      => "ASHVEIL BROTHERHOOD",
        "fieldmanual"  => "FIELD MANUAL",
        "aldenmoor"    => "R. ALDENMOOR",
        "voss"         => "E. VOSS",
        "journal"      => "PERSONAL JOURNAL",
        "journal_fire" => "PERSONAL JOURNAL · FIRE",
        "journal_air"  => "PERSONAL JOURNAL · AIR",
        _              => "",
    };

    public static void DrawIllustration(Context ctx, string? theme, double cx, double cy, double size, double alpha)
    {
        switch (theme)
        {
            case "ashveil":      DrawAshveil(ctx, cx, cy, size, alpha);     break;
            case "fieldmanual":  DrawFieldManual(ctx, cx, cy, size, alpha); break;
            case "aldenmoor":    DrawAldenmoor(ctx, cx, cy, size, alpha);   break;
            case "voss":         DrawVoss(ctx, cx, cy, size, alpha);        break;
            case "journal":      DrawJournal(ctx, cx, cy, size, alpha);     break;
            case "journal_fire": DrawJournalFire(ctx, cx, cy, size, alpha); break;
            case "journal_air":  DrawJournalAir(ctx, cx, cy, size, alpha);  break;
        }
    }

    // ── Ashveil Brotherhood — eye with flame rays ──────────────────────────────
    private static void DrawAshveil(Context ctx, double cx, double cy, double size, double alpha)
    {
        var (r, g, b) = GetAccent("ashveil");
        double er = size * 0.46;

        // Eye almond shape
        ctx.SetSourceRGBA(r, g, b, 0.55 * alpha);
        ctx.LineWidth = size * 0.045;
        ctx.MoveTo(cx - er, cy);
        ctx.CurveTo(cx - er * 0.3, cy - er * 0.48, cx + er * 0.3, cy - er * 0.48, cx + er, cy);
        ctx.CurveTo(cx + er * 0.3, cy + er * 0.48, cx - er * 0.3, cy + er * 0.48, cx - er, cy);
        ctx.Stroke();

        // Iris
        ctx.SetSourceRGBA(r, g, b, 0.38 * alpha);
        ctx.Arc(cx, cy, er * 0.30, 0, Math.PI * 2); ctx.Fill();

        // Pupil
        ctx.SetSourceRGBA(r * 0.5, g * 0.5, b * 0.5, 0.55 * alpha);
        ctx.Arc(cx, cy, er * 0.13, 0, Math.PI * 2); ctx.Fill();

        // Flame rays above
        ctx.LineWidth = size * 0.032;
        for (int i = -2; i <= 2; i++)
        {
            double angle = -Math.PI / 2 + i * 0.28;
            double len = size * (i == 0 ? 0.36 : 0.24);
            ctx.SetSourceRGBA(r, g, b, (i == 0 ? 0.55 : 0.35) * alpha);
            ctx.MoveTo(cx, cy - er * 0.28);
            ctx.LineTo(cx + Math.Cos(angle) * len, cy + Math.Sin(angle) * len - er * 0.06);
            ctx.Stroke();
        }
    }

    // ── Field Manual — crossed tactical marks ─────────────────────────────────
    private static void DrawFieldManual(Context ctx, double cx, double cy, double size, double alpha)
    {
        var (r, g, b) = GetAccent("fieldmanual");
        double er = size * 0.40;

        ctx.LineWidth = size * 0.048;

        // Cross diagonals
        ctx.SetSourceRGBA(r, g, b, 0.62 * alpha);
        ctx.MoveTo(cx - er, cy - er * 0.72); ctx.LineTo(cx + er, cy + er * 0.72); ctx.Stroke();
        ctx.MoveTo(cx + er, cy - er * 0.72); ctx.LineTo(cx - er, cy + er * 0.72); ctx.Stroke();

        // Endpoint caps
        ctx.LineWidth = size * 0.028;
        ctx.SetSourceRGBA(r, g, b, 0.45 * alpha);
        double[] ex = { cx - er, cx + er, cx + er, cx - er };
        double[] ey = { cy - er * 0.72, cy + er * 0.72, cy - er * 0.72, cy + er * 0.72 };
        double[] ea = { Math.PI * 0.85, Math.PI * -0.15, Math.PI * 1.35, Math.PI * 0.35 };
        for (int i = 0; i < 4; i++)
        {
            ctx.MoveTo(ex[i] + Math.Cos(ea[i] + 0.45) * size * 0.10, ey[i] + Math.Sin(ea[i] + 0.45) * size * 0.10);
            ctx.LineTo(ex[i], ey[i]);
            ctx.LineTo(ex[i] + Math.Cos(ea[i] - 0.45) * size * 0.10, ey[i] + Math.Sin(ea[i] - 0.45) * size * 0.10);
            ctx.Stroke();
        }

        // Center circle
        ctx.SetSourceRGBA(r, g, b, 0.72 * alpha);
        ctx.Arc(cx, cy, size * 0.075, 0, Math.PI * 2); ctx.Fill();
        ctx.SetSourceRGBA(r * 0.4, g * 0.4, b * 0.4, 0.55 * alpha);
        ctx.Arc(cx, cy, size * 0.035, 0, Math.PI * 2); ctx.Fill();
    }

    // ── R. Aldenmoor — compass rose ───────────────────────────────────────────
    private static void DrawAldenmoor(Context ctx, double cx, double cy, double size, double alpha)
    {
        var (r, g, b) = GetAccent("aldenmoor");
        double er = size * 0.44;

        // Outer ring
        ctx.SetSourceRGBA(r, g, b, 0.25 * alpha);
        ctx.LineWidth = size * 0.025;
        ctx.Arc(cx, cy, er * 0.88, 0, Math.PI * 2); ctx.Stroke();

        // 4 main arms with pointed tips
        ctx.LineWidth = size * 0.032;
        for (int i = 0; i < 4; i++)
        {
            double angle = i * Math.PI / 2;
            ctx.SetSourceRGBA(r, g, b, 0.58 * alpha);
            ctx.MoveTo(cx, cy);
            ctx.LineTo(cx + Math.Cos(angle) * er, cy + Math.Sin(angle) * er);
            ctx.Stroke();

            ctx.SetSourceRGBA(r, g, b, 0.38 * alpha);
            ctx.LineWidth = size * 0.022;
            ctx.MoveTo(cx + Math.Cos(angle) * er, cy + Math.Sin(angle) * er);
            ctx.LineTo(cx + Math.Cos(angle + 0.30) * er * 0.65, cy + Math.Sin(angle + 0.30) * er * 0.65);
            ctx.MoveTo(cx + Math.Cos(angle) * er, cy + Math.Sin(angle) * er);
            ctx.LineTo(cx + Math.Cos(angle - 0.30) * er * 0.65, cy + Math.Sin(angle - 0.30) * er * 0.65);
            ctx.Stroke();
            ctx.LineWidth = size * 0.032;
        }

        // 4 shorter diagonal arms
        ctx.LineWidth = size * 0.022;
        ctx.SetSourceRGBA(r, g, b, 0.35 * alpha);
        for (int i = 0; i < 4; i++)
        {
            double angle = i * Math.PI / 2 + Math.PI / 4;
            ctx.MoveTo(cx, cy);
            ctx.LineTo(cx + Math.Cos(angle) * er * 0.58, cy + Math.Sin(angle) * er * 0.58);
            ctx.Stroke();
        }

        // Center
        ctx.SetSourceRGBA(r, g, b, 0.65 * alpha);
        ctx.Arc(cx, cy, size * 0.075, 0, Math.PI * 2); ctx.Fill();
        ctx.SetSourceRGBA(r * 0.5, g * 0.5, b * 0.5, 0.50 * alpha);
        ctx.Arc(cx, cy, size * 0.032, 0, Math.PI * 2); ctx.Fill();
    }

    // ── E. Voss — scientific measurement dial ────────────────────────────────
    private static void DrawVoss(Context ctx, double cx, double cy, double size, double alpha)
    {
        var (r, g, b) = GetAccent("voss");
        double er = size * 0.44;

        // Outer circle
        ctx.SetSourceRGBA(r, g, b, 0.42 * alpha);
        ctx.LineWidth = size * 0.028;
        ctx.Arc(cx, cy, er, 0, Math.PI * 2); ctx.Stroke();

        // Inner circle
        ctx.SetSourceRGBA(r, g, b, 0.22 * alpha);
        ctx.Arc(cx, cy, er * 0.62, 0, Math.PI * 2); ctx.Stroke();

        // Tick marks
        ctx.LineWidth = size * 0.025;
        for (int i = 0; i < 12; i++)
        {
            double angle = i * Math.PI / 6;
            bool major = i % 3 == 0;
            double inner = major ? 0.78 : 0.86;
            ctx.SetSourceRGBA(r, g, b, (major ? 0.65 : 0.38) * alpha);
            ctx.MoveTo(cx + Math.Cos(angle) * er * inner, cy + Math.Sin(angle) * er * inner);
            ctx.LineTo(cx + Math.Cos(angle) * er, cy + Math.Sin(angle) * er);
            ctx.Stroke();
        }

        // Pointer
        ctx.SetSourceRGBA(r, g, b, 0.72 * alpha);
        ctx.LineWidth = size * 0.038;
        double pAngle = -Math.PI * 0.28;
        ctx.MoveTo(cx, cy);
        ctx.LineTo(cx + Math.Cos(pAngle) * er * 0.70, cy + Math.Sin(pAngle) * er * 0.70);
        ctx.Stroke();

        // Counter-pointer (shorter)
        ctx.SetSourceRGBA(r, g, b, 0.38 * alpha);
        ctx.LineWidth = size * 0.028;
        ctx.MoveTo(cx, cy);
        ctx.LineTo(cx + Math.Cos(pAngle + Math.PI) * er * 0.28, cy + Math.Sin(pAngle + Math.PI) * er * 0.28);
        ctx.Stroke();

        // Center pivot
        ctx.SetSourceRGBA(r, g, b, 0.68 * alpha);
        ctx.Arc(cx, cy, size * 0.068, 0, Math.PI * 2); ctx.Fill();
    }

    // ── Journal General — quill ───────────────────────────────────────────────
    private static void DrawJournal(Context ctx, double cx, double cy, double size, double alpha)
    {
        var (r, g, b) = GetAccent("journal");
        double angle = -Math.PI / 4;
        double len = size * 0.72;
        double perpAngle = angle + Math.PI / 2;
        double tipX = cx + Math.Cos(angle) * len * 0.52;
        double tipY = cy + Math.Sin(angle) * len * 0.52;
        double baseX = cx - Math.Cos(angle) * len * 0.48;
        double baseY = cy - Math.Sin(angle) * len * 0.48;

        // Shaft
        ctx.SetSourceRGBA(r, g, b, 0.60 * alpha);
        ctx.LineWidth = size * 0.038;
        ctx.MoveTo(baseX, baseY); ctx.LineTo(tipX, tipY); ctx.Stroke();

        // Feather barbs
        ctx.LineWidth = size * 0.022;
        for (int i = -4; i <= 4; i++)
        {
            if (i == 0) continue;
            double t = i / 4.5;
            double px = cx + Math.Cos(angle) * len * t * 0.38;
            double py = cy + Math.Sin(angle) * len * t * 0.38;
            double blen = size * (0.18 - Math.Abs(t) * 0.03);
            ctx.SetSourceRGBA(r, g, b, (0.38 - Math.Abs(t) * 0.04) * alpha);
            ctx.MoveTo(px, py);
            ctx.LineTo(px + Math.Cos(perpAngle) * blen, py + Math.Sin(perpAngle) * blen);
            ctx.Stroke();
        }

        // Tip
        ctx.SetSourceRGBA(r, g, b, 0.72 * alpha);
        ctx.Arc(baseX, baseY, size * 0.042, 0, Math.PI * 2); ctx.Fill();
    }

    // ── Journal Fire — flame burst ────────────────────────────────────────────
    private static void DrawJournalFire(Context ctx, double cx, double cy, double size, double alpha)
    {
        var (r, g, b) = GetAccent("journal_fire");
        ctx.LineWidth = size * 0.038;

        // Side flames
        for (int side = -1; side <= 1; side += 2)
        {
            ctx.SetSourceRGBA(r, g, b, 0.32 * alpha);
            ctx.MoveTo(cx + side * size * 0.10, cy + size * 0.30);
            ctx.CurveTo(
                cx + side * size * 0.30, cy + size * 0.08,
                cx + side * size * 0.22, cy - size * 0.10,
                cx + side * size * 0.07, cy - size * 0.24);
            ctx.Stroke();
        }

        // Central flame
        ctx.SetSourceRGBA(r, g, b, 0.68 * alpha);
        ctx.MoveTo(cx, cy + size * 0.32);
        ctx.CurveTo(cx - size * 0.22, cy + size * 0.04, cx + size * 0.22, cy - size * 0.10, cx, cy - size * 0.40);
        ctx.MoveTo(cx, cy + size * 0.32);
        ctx.CurveTo(cx + size * 0.22, cy + size * 0.04, cx - size * 0.22, cy - size * 0.10, cx, cy - size * 0.40);
        ctx.Stroke();

        // Base glow
        ctx.SetSourceRGBA(r, g, b, 0.42 * alpha);
        ctx.Arc(cx, cy + size * 0.30, size * 0.09, 0, Math.PI * 2); ctx.Fill();
    }

    // ── Journal Air — wind spiral ─────────────────────────────────────────────
    private static void DrawJournalAir(Context ctx, double cx, double cy, double size, double alpha)
    {
        var (r, g, b) = GetAccent("journal_air");

        for (int i = 1; i <= 4; i++)
        {
            double er = size * i * 0.115;
            double startA = -Math.PI * 0.85 + i * 0.18;
            double endA = Math.PI * 0.55 - i * 0.12;
            ctx.SetSourceRGBA(r, g, b, (0.58 - i * 0.09) * alpha);
            ctx.LineWidth = size * (0.036 - i * 0.005);
            ctx.Arc(cx - size * 0.05, cy + size * 0.05, er, startA, endA);
            ctx.Stroke();
        }

        // Center
        ctx.SetSourceRGBA(r, g, b, 0.65 * alpha);
        ctx.Arc(cx, cy, size * 0.058, 0, Math.PI * 2); ctx.Fill();
    }
}
