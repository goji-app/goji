using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace GodjiVpn.Services;

/// <summary>Палитра: три оттенка (основной, тёплый акцент, нейтраль), из которых выводятся все роли обеих тем.
/// Таблица и формулы — 1:1 с Android ui/theme/Palettes.kt; менять синхронно.</summary>
public sealed record GojiPalette(string Id, string Ru, string En, string Zh, double Hue, double Sat, double AccentHue, double NeutralSat, double LightL = 0.32)
{
    public string Name(string lang) => lang switch { "en" => En, "zh" => Zh, _ => Ru };
}

/// <summary>Роли M3 одной темы (как ToneSet в Android).</summary>
public sealed record ToneSet(
    Color Surface, Color CLow, Color C, Color CHigh, Color CHighest,
    Color OnSurface, Color OnSurfaceVariant, Color Outline, Color OutlineVariant,
    Color Primary, Color OnPrimary, Color PrimaryC, Color OnPrimaryC,
    Color SecondaryC, Color OnSecondaryC,
    Color Tertiary, Color TertiaryDeep, Color TertiaryC, Color OnTertiaryC);

public static class Palettes
{
    public const string DefaultId = "sage";

    public static readonly IReadOnlyList<GojiPalette> All = new GojiPalette[]
    {
        new("sage", "Шалфей", "Sage", "鼠尾草", 150, 0.17, 38, 0.06),
        new("teal", "Бирюза", "Teal", "青绿", 175, 0.90, 16, 0.10, 0.21),
        new("ocean", "Океан", "Ocean", "海洋", 205, 0.75, 30, 0.10, 0.30),
        new("indigo", "Индиго", "Indigo", "靛蓝", 232, 0.55, 40, 0.09, 0.40),
        new("violet", "Фиалка", "Violet", "紫罗兰", 264, 0.50, 22, 0.09, 0.40),
        new("orchid", "Орхидея", "Orchid", "兰花", 305, 0.40, 140, 0.09, 0.38),
        new("rose", "Роза", "Rose", "玫瑰", 340, 0.55, 160, 0.10, 0.40),
        new("cherry", "Вишня", "Cherry", "樱桃", 355, 0.62, 190, 0.09, 0.38),
        new("terracotta", "Терракота", "Terracotta", "赤陶", 14, 0.58, 190, 0.10, 0.38),
        new("amber", "Янтарь", "Amber", "琥珀", 32, 0.85, 200, 0.12, 0.27),
        new("sand", "Песок", "Sand", "沙", 42, 0.38, 210, 0.13, 0.30),
        new("olive", "Олива", "Olive", "橄榄", 72, 0.55, 330, 0.12, 0.27),
        new("lime", "Лайм", "Lime", "青柠", 95, 0.60, 320, 0.11, 0.26),
        new("moss", "Мох", "Moss", "苔藓", 125, 0.45, 20, 0.10, 0.28),
        new("mint", "Мята", "Mint", "薄荷", 160, 0.65, 12, 0.10, 0.25),
        new("slate", "Сланец", "Slate", "石板", 215, 0.18, 35, 0.07, 0.34),
        new("graphite", "Графит", "Graphite", "石墨", 220, 0.04, 30, 0.02, 0.30),
    };

    public static GojiPalette ById(string? id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];

    private static Color H(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static Color Hsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360; s = Math.Clamp(s, 0, 1);
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;
        var (r, g, b) = h switch { < 60 => (c, x, 0.0), < 120 => (x, c, 0.0), < 180 => (0.0, c, x), < 240 => (0.0, x, c), < 300 => (x, 0.0, c), _ => (c, 0.0, x) };
        byte B(double v) => (byte)Math.Round(Math.Clamp(v + m, 0, 1) * 255);
        return Color.FromRgb(B(r), B(g), B(b));
    }

    // «Шалфей» — токены макета 9a без пересчёта (mut чуть темнее #5F6A65 ради AA).
    private static readonly ToneSet SageLight = new(
        H(0xF4F5F2), H(0xFAFAF8), H(0xFFFFFF), H(0xEDEFEA), H(0xDADFDA),
        H(0x2A3430), H(0x4A5651), H(0x5F6A65), H(0xDADFDA),
        H(0x4F6358), H(0xFFFFFF), H(0xDCE8DF), H(0x24362C),
        H(0xDCE8DF), H(0x24362C),
        H(0x6B6444), H(0x4D4830), H(0xEFE9D6), H(0x3A3626));
    private static readonly ToneSet SageDark = new(
        H(0x1C2120), H(0x202625), H(0x242A28), H(0x2B3230), H(0x333B38),
        H(0xEEF2EF), H(0xB8C2BD), H(0x9AA6A0), H(0x333B38),
        H(0xD9E1DC), H(0x1C2120), H(0x2F4038), H(0xEAF3EE),
        H(0x2F4038), H(0xEAF3EE),
        H(0xE8E2CC), H(0xF4F0E2), H(0x3A372B), H(0xF4F0E2));

    public static ToneSet Light(GojiPalette p)
    {
        if (p.Id == "sage") return SageLight;
        var n = p.NeutralSat; var h = p.Hue; var s = p.Sat;
        return new ToneSet(
            Hsl(h, n, 0.965), Hsl(h, n, 0.94), Hsl(h, n, 0.915), Hsl(h, n, 0.89), Hsl(h, n, 0.86),
            Hsl(h, n * 1.5, 0.11), Hsl(h, n * 1.2, 0.27), Hsl(h, n, 0.36), Hsl(h, n, 0.76),
            Hsl(h, s, p.LightL), Colors.White, Hsl(h, s * 0.85, 0.85), Hsl(h, s, 0.12),
            Hsl(h, s * 0.45, 0.87), Hsl(h, s * 0.6, 0.13),
            Hsl(p.AccentHue, 0.55, 0.36), Hsl(p.AccentHue, 0.6, 0.27), Hsl(p.AccentHue, 0.7, 0.9), Hsl(p.AccentHue, 0.8, 0.12));
    }

    public static ToneSet Dark(GojiPalette p)
    {
        if (p.Id == "sage") return SageDark;
        var n = p.NeutralSat; var h = p.Hue; var s = p.Sat;
        return new ToneSet(
            Hsl(h, n, 0.095), Hsl(h, n, 0.125), Hsl(h, n, 0.15), Hsl(h, n, 0.185), Hsl(h, n, 0.22),
            Hsl(h, n, 0.90), Hsl(h, n, 0.76), Hsl(h, n, 0.62), Hsl(h, n, 0.28),
            Hsl(h, Math.Min(s * 0.8, 0.7), 0.74), Hsl(h, s, 0.13), Hsl(h, s * 0.7, 0.25), Hsl(h, s * 0.8, 0.88),
            Hsl(h, s * 0.35, 0.26), Hsl(h, s * 0.5, 0.88),
            Hsl(p.AccentHue, 0.8, 0.78), Hsl(p.AccentHue, 0.8, 0.88), Hsl(p.AccentHue, 0.55, 0.26), Hsl(p.AccentHue, 0.8, 0.88));
    }

    /// <summary>Записывает роли выбранной палитры в тематические словари приложения (Light и Dark): они
    /// имеют приоритет над Themes/Tokens.axaml, DynamicResource перекрашивается сам.</summary>
    public static void Apply(GojiPalette p)
    {
        var app = Application.Current;
        if (app == null) return;
        foreach (var dark in new[] { false, true })
        {
            var t = dark ? Dark(p) : Light(p);
            var d = new ResourceDictionary();
            void B(string key, Color c) => d[key + "Brush"] = new SolidColorBrush(c);
            void C(string key, Color c) => d[key + "Color"] = c;

            B("SurfaceBase", t.Surface); B("SurfaceContainerLow", t.CLow); B("SurfaceContainer", t.C);
            B("SurfaceContainerHigh", t.CHigh); B("SurfaceContainerHighest", t.CHighest);
            B("Primary", t.Primary); B("OnPrimary", t.OnPrimary); B("PrimaryContainer", t.PrimaryC); B("OnPrimaryContainer", t.OnPrimaryC);
            B("SecondaryContainer", t.SecondaryC); B("OnSecondaryContainer", t.OnSecondaryC);
            B("TertiaryContainer", t.TertiaryC); B("OnTertiaryContainer", t.OnTertiaryC);
            B("Outline", t.Outline); B("OutlineVariant", t.OutlineVariant);
            B("BgSolid", t.Surface); B("Surface", t.OnPrimary); B("SurfaceGlass", t.CHigh);
            B("Ink", t.OnSurface); B("TextPrimary", t.OnSurface); B("TextSecondary", t.OnSurfaceVariant); B("TextMuted", t.Outline);
            B("Teal", t.Primary); B("TealBright", t.Primary); B("TealDeep", t.Primary); B("TealTint", t.PrimaryC); B("TealTintBorder", t.Primary);
            B("Terracotta", t.Tertiary); B("TerracottaDeep", t.TertiaryDeep); B("TerracottaTint", t.TertiaryC);
            B("ButtonBorder", t.Outline); B("Chip", t.CHigh); B("TrackBg", t.CHighest); B("BorderTeal", t.Primary);
            B("Hair", t.CHighest); B("SelBg", t.SecondaryC); B("Thumb", t.SecondaryC); B("Lens", t.SecondaryC); B("RingCore", t.C);
            B("InkBtn", t.Primary); B("InkBtnText", t.OnPrimary); B("BannerOk", t.PrimaryC); B("BannerWarn", t.TertiaryC);
            B("OffDot", t.Outline); B("Panel", t.C); B("PanelAlt", t.CHigh); B("Border", t.OutlineVariant);
            B("Accent", t.Primary); B("AccentDim", t.Primary); B("Text", t.OnSurface);
            // Заливка главной по состоянию: «Шалфей» — «песок/шалфей» макета, остальные — оттенок своей палитры.
            var sage = p.Id == "sage";
            static Color Mix(Color a, Color b, double k) => Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * k), (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k));
            B("HomeBusyFill", sage ? t.TertiaryC : Mix(t.Surface, t.PrimaryC, 0.28));
            B("HomeOnFill", sage ? t.SecondaryC : Mix(t.Surface, t.PrimaryC, 0.55));
            B("HomeBusyInk", sage ? t.OnTertiaryC : t.OnPrimaryC);
            B("HomeOnInk", sage ? t.OnSecondaryC : t.OnPrimaryC);
            B("HomeBusyBtn", sage ? t.Tertiary : t.Primary);
            C("Teal", t.Primary); C("Primary", t.Primary); C("OnPrimary", t.OnPrimary); C("PrimaryContainer", t.PrimaryC);
            C("Tertiary", t.Tertiary); C("OutlineVariant", t.OutlineVariant); C("TrackBg", t.CHighest);
            C("Glass", t.C); C("GlassStrong", t.CHigh); C("GlassFaint", t.CHighest);
            C("BaseTop", t.Surface); C("BaseMid", t.Surface); C("BaseBottom", t.Surface);
            app.Resources.ThemeDictionaries[dark ? ThemeVariant.Dark : ThemeVariant.Light] = d;
        }
    }
}
