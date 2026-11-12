import SwiftUI

/// Цветовые палитры. Палитра — три оттенка (основной, тёплый акцент, нейтраль), из которых выводятся все
/// роли светлой и тёмной темы. Таблица и формулы — 1:1 с Android ui/theme/Palettes.kt и
/// Windows/macOS Services/Palettes.cs; менять синхронно.
struct GojiPalette: Identifiable, Equatable {
    let id: String
    let ru: String
    let en: String
    let hue: Double
    let sat: Double
    let accentHue: Double
    let neutralSat: Double
    var lightL: Double = 0.32

    var name: String { AppLanguage.isRussian ? ru : en }
}

/// Роли M3 одной темы (ARGB).
struct ToneSet {
    let surface, cLow, c, cHigh, cHighest: UInt32
    let onSurface, onSurfaceVariant, outline, outlineVariant: UInt32
    let primary, onPrimary, primaryC, onPrimaryC: UInt32
    let secondaryC, onSecondaryC: UInt32
    let tertiary, tertiaryDeep, tertiaryC, onTertiaryC: UInt32
}

enum Palettes {
    static let defaultId = "sage"

    static let all: [GojiPalette] = [
        GojiPalette(id: "sage", ru: "Шалфей", en: "Sage", hue: 150, sat: 0.17, accentHue: 38, neutralSat: 0.06),
        GojiPalette(id: "teal", ru: "Бирюза", en: "Teal", hue: 175, sat: 0.90, accentHue: 16, neutralSat: 0.10, lightL: 0.21),
        GojiPalette(id: "ocean", ru: "Океан", en: "Ocean", hue: 205, sat: 0.75, accentHue: 30, neutralSat: 0.10, lightL: 0.30),
        GojiPalette(id: "indigo", ru: "Индиго", en: "Indigo", hue: 232, sat: 0.55, accentHue: 40, neutralSat: 0.09, lightL: 0.40),
        GojiPalette(id: "violet", ru: "Фиалка", en: "Violet", hue: 264, sat: 0.50, accentHue: 22, neutralSat: 0.09, lightL: 0.40),
        GojiPalette(id: "orchid", ru: "Орхидея", en: "Orchid", hue: 305, sat: 0.40, accentHue: 140, neutralSat: 0.09, lightL: 0.38),
        GojiPalette(id: "rose", ru: "Роза", en: "Rose", hue: 340, sat: 0.55, accentHue: 160, neutralSat: 0.10, lightL: 0.40),
        GojiPalette(id: "cherry", ru: "Вишня", en: "Cherry", hue: 355, sat: 0.62, accentHue: 190, neutralSat: 0.09, lightL: 0.38),
        GojiPalette(id: "terracotta", ru: "Терракота", en: "Terracotta", hue: 14, sat: 0.58, accentHue: 190, neutralSat: 0.10, lightL: 0.38),
        GojiPalette(id: "amber", ru: "Янтарь", en: "Amber", hue: 32, sat: 0.85, accentHue: 200, neutralSat: 0.12, lightL: 0.27),
        GojiPalette(id: "sand", ru: "Песок", en: "Sand", hue: 42, sat: 0.38, accentHue: 210, neutralSat: 0.13, lightL: 0.30),
        GojiPalette(id: "olive", ru: "Олива", en: "Olive", hue: 72, sat: 0.55, accentHue: 330, neutralSat: 0.12, lightL: 0.27),
        GojiPalette(id: "lime", ru: "Лайм", en: "Lime", hue: 95, sat: 0.60, accentHue: 320, neutralSat: 0.11, lightL: 0.26),
        GojiPalette(id: "moss", ru: "Мох", en: "Moss", hue: 125, sat: 0.45, accentHue: 20, neutralSat: 0.10, lightL: 0.28),
        GojiPalette(id: "mint", ru: "Мята", en: "Mint", hue: 160, sat: 0.65, accentHue: 12, neutralSat: 0.10, lightL: 0.25),
        GojiPalette(id: "slate", ru: "Сланец", en: "Slate", hue: 215, sat: 0.18, accentHue: 35, neutralSat: 0.07, lightL: 0.34),
        GojiPalette(id: "graphite", ru: "Графит", en: "Graphite", hue: 220, sat: 0.04, accentHue: 30, neutralSat: 0.02, lightL: 0.30)
    ]

    static func byId(_ id: String?) -> GojiPalette { all.first { $0.id == id } ?? all[0] }

    /// Выбранная палитра. Меняет её AppSettings.paletteId; кэш тонов пересчитывается сразу.
    static var currentId: String = defaultId {
        didSet { light = tones(byId(currentId), dark: false); dark = tones(byId(currentId), dark: true) }
    }
    static var current: GojiPalette { byId(currentId) }
    private(set) static var light: ToneSet = tones(byId(defaultId), dark: false)
    private(set) static var dark: ToneSet = tones(byId(defaultId), dark: true)

    static func tones(_ p: GojiPalette, dark: Bool) -> ToneSet {
        if p.id == "sage" { return dark ? sageDark : sageLight }
        let n = p.neutralSat, h = p.hue, s = p.sat
        func c(_ hue: Double, _ sat: Double, _ l: Double) -> UInt32 { argb(hue, sat, l) }
        if !dark {
            return ToneSet(
                surface: c(h, n, 0.965), cLow: c(h, n, 0.94), c: c(h, n, 0.915), cHigh: c(h, n, 0.89), cHighest: c(h, n, 0.86),
                onSurface: c(h, n * 1.5, 0.11), onSurfaceVariant: c(h, n * 1.2, 0.27), outline: c(h, n, 0.36), outlineVariant: c(h, n, 0.76),
                primary: c(h, s, p.lightL), onPrimary: 0xFFFFFFFF, primaryC: c(h, s * 0.85, 0.85), onPrimaryC: c(h, s, 0.12),
                secondaryC: c(h, s * 0.45, 0.87), onSecondaryC: c(h, s * 0.6, 0.13),
                tertiary: c(p.accentHue, 0.55, 0.36), tertiaryDeep: c(p.accentHue, 0.6, 0.27),
                tertiaryC: c(p.accentHue, 0.7, 0.9), onTertiaryC: c(p.accentHue, 0.8, 0.12))
        }
        return ToneSet(
            surface: c(h, n, 0.095), cLow: c(h, n, 0.125), c: c(h, n, 0.15), cHigh: c(h, n, 0.185), cHighest: c(h, n, 0.22),
            onSurface: c(h, n, 0.90), onSurfaceVariant: c(h, n, 0.76), outline: c(h, n, 0.62), outlineVariant: c(h, n, 0.28),
            primary: c(h, min(s * 0.8, 0.7), 0.74), onPrimary: c(h, s, 0.13), primaryC: c(h, s * 0.7, 0.25), onPrimaryC: c(h, s * 0.8, 0.88),
            secondaryC: c(h, s * 0.35, 0.26), onSecondaryC: c(h, s * 0.5, 0.88),
            tertiary: c(p.accentHue, 0.8, 0.78), tertiaryDeep: c(p.accentHue, 0.8, 0.88),
            tertiaryC: c(p.accentHue, 0.55, 0.26), onTertiaryC: c(p.accentHue, 0.8, 0.88))
    }

    /// «Шалфей» — токены макета 9a без пересчёта (mut чуть темнее #5F6A65 ради AA).
    private static let sageLight = ToneSet(
        surface: 0xFFF4F5F2, cLow: 0xFFFAFAF8, c: 0xFFFFFFFF, cHigh: 0xFFEDEFEA, cHighest: 0xFFDADFDA,
        onSurface: 0xFF2A3430, onSurfaceVariant: 0xFF4A5651, outline: 0xFF5F6A65, outlineVariant: 0xFFDADFDA,
        primary: 0xFF4F6358, onPrimary: 0xFFFFFFFF, primaryC: 0xFFDCE8DF, onPrimaryC: 0xFF24362C,
        secondaryC: 0xFFDCE8DF, onSecondaryC: 0xFF24362C,
        tertiary: 0xFF6B6444, tertiaryDeep: 0xFF4D4830, tertiaryC: 0xFFEFE9D6, onTertiaryC: 0xFF3A3626)
    private static let sageDark = ToneSet(
        surface: 0xFF1C2120, cLow: 0xFF202625, c: 0xFF242A28, cHigh: 0xFF2B3230, cHighest: 0xFF333B38,
        onSurface: 0xFFEEF2EF, onSurfaceVariant: 0xFFB8C2BD, outline: 0xFF9AA6A0, outlineVariant: 0xFF333B38,
        primary: 0xFFD9E1DC, onPrimary: 0xFF1C2120, primaryC: 0xFF2F4038, onPrimaryC: 0xFFEAF3EE,
        secondaryC: 0xFF2F4038, onSecondaryC: 0xFFEAF3EE,
        tertiary: 0xFFE8E2CC, tertiaryDeep: 0xFFF4F0E2, tertiaryC: 0xFF3A372B, onTertiaryC: 0xFFF4F0E2)

    /// Смесь двух цветов (для заливки главной у палитр, кроме «Шалфея»).
    static func mix(_ a: UInt32, _ b: UInt32, _ k: Double) -> UInt32 {
        func ch(_ shift: UInt32) -> UInt32 {
            let x = Double((a >> shift) & 0xFF), y = Double((b >> shift) & 0xFF)
            return UInt32(x + (y - x) * k) & 0xFF
        }
        return 0xFF000000 | (ch(16) << 16) | (ch(8) << 8) | ch(0)
    }

    private static func argb(_ hue: Double, _ sat: Double, _ l: Double) -> UInt32 {
        let hh = (hue.truncatingRemainder(dividingBy: 360) + 360).truncatingRemainder(dividingBy: 360)
        let ss = min(max(sat, 0), 1)
        let c = (1 - abs(2 * l - 1)) * ss
        let x = c * (1 - abs((hh / 60).truncatingRemainder(dividingBy: 2) - 1))
        let m = l - c / 2
        var r = 0.0, g = 0.0, b = 0.0
        if hh < 60 { r = c; g = x } else if hh < 120 { r = x; g = c } else if hh < 180 { g = c; b = x }
        else if hh < 240 { g = x; b = c } else if hh < 300 { r = x; b = c } else { r = c; b = x }
        func byte(_ v: Double) -> UInt32 { UInt32((min(max(v + m, 0), 1) * 255).rounded()) }
        return 0xFF000000 | (byte(r) << 16) | (byte(g) << 8) | byte(b)
    }
}
