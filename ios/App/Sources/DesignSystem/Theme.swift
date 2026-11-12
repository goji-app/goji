import SwiftUI
import UIKit

/// Токены «Goji Expressive» — Material 3 Expressive, как в Android (ui/theme/Color.kt, 163f5ab) и
/// Windows/macOS-клиентах: плотные тональные поверхности вместо стекла, без размытия, бликов и
/// теней. Тональная схема построена от фирменного бирюзового, тёплый акцент — терракотовый
/// (tertiary). Каждый цвет динамический: светлый/тёмный вариант по текущей теме.
///
/// Имена токенов v5 «Стекло» сохранены — экраны продолжают их читать, а значения теперь роли M3:
/// textPrimary = onSurface, teal/tealDeep = primary, tealTint = primaryContainer,
/// chip = surfaceContainerHigh, selection = secondaryContainer и т. д. Токены, которые больше
/// ничего не рисуют (блики, кромки, пятна фона, тени), прозрачные.
enum GojiColor {
    /// Цвет роли из текущей палитры (см. Palettes.swift): светлый и тёмный вариант выбираются по теме.
    private static func pair(_ key: KeyPath<ToneSet, UInt32>) -> Color {
        Color(light: Palettes.light[keyPath: key], dark: Palettes.dark[keyPath: key])
    }

    /// Заливка главной по состоянию: «Шалфей» — «песок/шалфей» макета 9a, остальные палитры — свой оттенок.
    private static var isSage: Bool { Palettes.currentId == "sage" }
    static var homeBusyFill: Color {
        isSage ? pair(\.tertiaryC)
            : Color(light: Palettes.mix(Palettes.light.surface, Palettes.light.primaryC, 0.28), dark: Palettes.mix(Palettes.dark.surface, Palettes.dark.primaryC, 0.28))
    }
    static var homeOnFill: Color {
        isSage ? pair(\.secondaryC)
            : Color(light: Palettes.mix(Palettes.light.surface, Palettes.light.primaryC, 0.55), dark: Palettes.mix(Palettes.dark.surface, Palettes.dark.primaryC, 0.55))
    }
    static var homeBusyInk: Color { isSage ? pair(\.onTertiaryC) : pair(\.onPrimaryC) }
    static var homeOnInk: Color { isSage ? pair(\.onSecondaryC) : pair(\.onPrimaryC) }
    static var homeBusyButton: Color { isSage ? pair(\.tertiary) : pair(\.primary) }

    // ── роли Material 3 ─────────────────────────────────────
    static var surface: Color { pair(\.surface) }
    static var surfaceContainerLow: Color { pair(\.cLow) }
    static var surfaceContainer: Color { pair(\.c) }
    static var surfaceContainerHigh: Color { pair(\.cHigh) }
    static var surfaceContainerHighest: Color { pair(\.cHighest) }
    static var primary: Color { pair(\.primary) }
    static var onPrimary: Color { pair(\.onPrimary) }
    static var primaryContainer: Color { pair(\.primaryC) }
    static var onPrimaryContainer: Color { pair(\.onPrimaryC) }
    static var secondaryContainer: Color { pair(\.secondaryC) }
    static var onSecondaryContainer: Color { pair(\.onSecondaryC) }
    static var tertiaryContainer: Color { pair(\.tertiaryC) }
    static var onTertiaryContainer: Color { pair(\.onTertiaryC) }
    static var errorContainer: Color { Color(light: 0xFFFFDAD6, dark: 0xFF93000A) }
    static var outline: Color { pair(\.outline) }
    static var outlineVariant: Color { pair(\.outlineVariant) }

    // ── поля v5 (значения — роли M3) ────────────────────────
    static var textPrimary: Color { pair(\.onSurface) }
    static var textSecondary: Color { pair(\.onSurfaceVariant) }
    static var textMuted: Color { outline }

    static var teal: Color { primary }
    static var tealDeep: Color { primary }
    static var tealTint: Color { primaryContainer }
    static var tealTintBorder: Color { primary }

    // Терракотовый приглушён на ~10–15% насыщенности (1.0.132): тёплый акцент — график отдачи,
    // полоски остатка подписки — не должен перетягивать внимание на карточке скорости.
    static var terracotta: Color { pair(\.tertiary) }
    static var terracottaTint: Color { tertiaryContainer }
    static var warning: Color { Color(light: 0xFF8A6100, dark: 0xFFF0BF48) }
    static var danger: Color { Color(light: 0xFFBA1A1A, dark: 0xFFFFB4AB) }
    static var jamBg: Color { errorContainer }
    static var jamText: Color { danger }

    static var glass: Color { surfaceContainer }
    static var glassStrong: Color { surfaceContainerHigh }
    static var glassFaint: Color { surfaceContainerHighest }
    static var stroke: Color { Color.clear }
    /// Разделитель внутри карточек — «щель» цвета фона (M3 Expressive), а не тонкая линия.
    static var hair: Color { surface }
    static var highlight: Color { Color.clear }
    static var chip: Color { surfaceContainerHigh }
    static var track: Color { surfaceContainerHighest }
    static var selection: Color { secondaryContainer }
    static var glassShadow: Color { Color.clear }

    // Градиенты v5 сведены к плоскому primary — в M3E заливки однотонные.
    static var accentTop: Color { primary }
    static var accentMid: Color { primary }
    static var accentBottom: Color { primary }
    static var accentGlow: Color { Color.clear }

    static var inkButton: Color { primary }
    static var inkButtonText: Color { onPrimary }
    static var ringCore: Color { surfaceContainer }

    static var baseTop: Color { surface }
    static var baseMid: Color { surface }
    static var baseBottom: Color { surface }

    static var accentGradient: LinearGradient {
        LinearGradient(colors: [accentTop, accentBottom], startPoint: .top, endPoint: .bottom)
    }
}

extension Color {
    /// ARGB-константы (0xAARRGGBB), как в Android-токенах.
    init(light: UInt32, dark: UInt32) {
        self.init(UIColor { traits in
            UIColor(argb: traits.userInterfaceStyle == .dark ? dark : light)
        })
    }
}

extension UIColor {
    convenience init(argb: UInt32) {
        self.init(
            red: CGFloat((argb >> 16) & 0xFF) / 255,
            green: CGFloat((argb >> 8) & 0xFF) / 255,
            blue: CGFloat(argb & 0xFF) / 255,
            alpha: CGFloat((argb >> 24) & 0xFF) / 255
        )
    }
}

/// Шрифты: Manrope во всём приложении, как в Android (Type.kt); Instrument Serif остался только
/// для приветствий на других письменностях. Размеры масштабируются системной настройкой
/// «Размер текста» (Dynamic Type) через relativeTo.
enum GojiFont {
    enum Weight: String {
        // Макет 9a: Golos Text вместо Manrope. Имя `manrope` у функций осталось ради вызовов по всему приложению.
        case regular = "GolosText-Regular"
        case medium = "GolosText-Medium"
        case semibold = "GolosText-SemiBold"
        case bold = "GolosText-Bold"
        case extraBold = "GolosText-Bold"
    }

    /// Общий масштаб текста (2026-10-16): шрифт во всех приложениях уменьшен на ступень, как
    /// «Обычный» ×0.9 на Android и компьютерах. Dynamic Type по-прежнему действует поверх.
    static let scale: CGFloat = 0.9

    static func manrope(_ size: CGFloat, _ weight: Weight = .regular, relativeTo style: Font.TextStyle = .body) -> Font {
        .custom(weight.rawValue, size: (size * scale).rounded(.toNearestOrEven), relativeTo: style)
    }

    /// Моноширинные подписи, цифры, кнопки и навигация (макет 9a): JetBrains Mono.
    static func mono(_ size: CGFloat, _ weight: Weight = .regular, relativeTo style: Font.TextStyle = .body) -> Font {
        let name: String
        switch weight {
        case .regular, .medium: name = "JetBrainsMono-Regular"
        case .semibold: name = "JetBrainsMono-SemiBold"
        case .bold, .extraBold: name = "JetBrainsMono-Bold"
        }
        return .custom(name, size: (size * scale).rounded(.toNearestOrEven), relativeTo: style)
    }

    static func serif(_ size: CGFloat, italic: Bool = false, relativeTo style: Font.TextStyle = .largeTitle) -> Font {
        .custom(italic ? "InstrumentSerif-Italic" : "InstrumentSerif-Regular", size: (size * scale).rounded(.toNearestOrEven), relativeTo: style)
    }

    /// Заголовки экранов: Golos SemiBold 30 (макет 9a).
    static func title(_ size: CGFloat = 30) -> Font { manrope(size, .semibold, relativeTo: .largeTitle) }
    static func section() -> Font { manrope(17, .bold, relativeTo: .headline) }
    static func body() -> Font { manrope(14, .medium) }
    static func caption() -> Font { manrope(12, .medium, relativeTo: .caption) }
    static func button() -> Font { manrope(15, .bold, relativeTo: .headline) }
}

enum GojiMetrics {
    static let screenPadding: CGFloat = 16
    static let cardRadius: CGFloat = 2
    static let buttonRadius: CGFloat = 2
}
