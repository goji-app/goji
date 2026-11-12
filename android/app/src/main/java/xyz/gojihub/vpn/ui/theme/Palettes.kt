package xyz.gojihub.vpn.ui.theme

import androidx.compose.ui.graphics.Color
import xyz.gojihub.vpn.i18n.AppLanguage
import xyz.gojihub.vpn.i18n.Loc

/**
 * Цветовые палитры. Палитра — это три оттенка (основной, тёплый акцент, нейтраль), из которых
 * [GodjiColors] выводит все тональные роли для светлой и тёмной темы. Новую палитру добавляет
 * одна строка в [Palettes.all]; та же таблица (id, оттенки) лежит в Windows/macOS/iOS.
 *
 * Светлоты подобраны под WCAG AA: текст ≥ 4.5:1 на своих поверхностях в обеих темах.
 */
data class GojiPalette(
    val id: String,
    val ru: String, val en: String, val zh: String,
    /** Оттенок основного цвета (0..360) и его насыщенность (0..1). */
    val hue: Float, val sat: Float,
    /** Оттенок тёплого акцента (tertiary). */
    val accentHue: Float,
    /** Насыщенность нейтральных поверхностей: 0 — серый, 0.2 — заметно окрашенный. */
    val neutralSat: Float,
    /** Светлота основного цвета в светлой теме (ниже — темнее, у жёлтых/лаймовых нужна темнее). */
    val lightL: Float = 0.32f
) {
    fun name(lang: AppLanguage) = when (lang) { AppLanguage.RU -> ru; AppLanguage.EN -> en; AppLanguage.ZH -> zh }
}

object Palettes {
    const val DEFAULT_ID = "sage"

    val all = listOf(
        GojiPalette("sage", "Шалфей", "Sage", "鼠尾草", 150f, 0.17f, 38f, 0.06f),
        GojiPalette("teal", "Бирюза", "Teal", "青绿", 175f, 0.90f, 16f, 0.10f, 0.21f),
        GojiPalette("ocean", "Океан", "Ocean", "海洋", 205f, 0.75f, 30f, 0.10f, 0.30f),
        GojiPalette("indigo", "Индиго", "Indigo", "靛蓝", 232f, 0.55f, 40f, 0.09f, 0.40f),
        GojiPalette("violet", "Фиалка", "Violet", "紫罗兰", 264f, 0.50f, 22f, 0.09f, 0.40f),
        GojiPalette("orchid", "Орхидея", "Orchid", "兰花", 305f, 0.40f, 140f, 0.09f, 0.38f),
        GojiPalette("rose", "Роза", "Rose", "玫瑰", 340f, 0.55f, 160f, 0.10f, 0.40f),
        GojiPalette("cherry", "Вишня", "Cherry", "樱桃", 355f, 0.62f, 190f, 0.09f, 0.38f),
        GojiPalette("terracotta", "Терракота", "Terracotta", "赤陶", 14f, 0.58f, 190f, 0.10f, 0.38f),
        GojiPalette("amber", "Янтарь", "Amber", "琥珀", 32f, 0.85f, 200f, 0.12f, 0.27f),
        GojiPalette("sand", "Песок", "Sand", "沙", 42f, 0.38f, 210f, 0.13f, 0.30f),
        GojiPalette("olive", "Олива", "Olive", "橄榄", 72f, 0.55f, 330f, 0.12f, 0.27f),
        GojiPalette("lime", "Лайм", "Lime", "青柠", 95f, 0.60f, 320f, 0.11f, 0.26f),
        GojiPalette("moss", "Мох", "Moss", "苔藓", 125f, 0.45f, 20f, 0.10f, 0.28f),
        GojiPalette("mint", "Мята", "Mint", "薄荷", 160f, 0.65f, 12f, 0.10f, 0.25f),
        GojiPalette("slate", "Сланец", "Slate", "石板", 215f, 0.18f, 35f, 0.07f, 0.34f),
        GojiPalette("graphite", "Графит", "Graphite", "石墨", 220f, 0.04f, 30f, 0.02f, 0.30f),
    )

    fun byId(id: String?): GojiPalette = all.firstOrNull { it.id == id } ?: all.first()
}

/** Все роли для [GodjiColors.apply] одной темы. */
internal class ToneSet(
    val surface: Color, val cLow: Color, val c: Color, val cHigh: Color, val cHighest: Color,
    val onSurface: Color, val onSurfaceVariant: Color, val outline: Color, val outlineVariant: Color,
    val primary: Color, val onPrimary: Color, val primaryC: Color, val onPrimaryC: Color,
    val secondaryC: Color, val onSecondaryC: Color,
    val tertiary: Color, val tertiaryDeep: Color, val tertiaryC: Color, val onTertiaryC: Color
)

private fun hsl(h: Float, s: Float, l: Float): Color {
    val hh = ((h % 360f) + 360f) % 360f
    val ss = s.coerceIn(0f, 1f)
    val c = (1f - kotlin.math.abs(2f * l - 1f)) * ss
    val x = c * (1f - kotlin.math.abs((hh / 60f) % 2f - 1f))
    val m = l - c / 2f
    val (r, g, b) = when {
        hh < 60f -> Triple(c, x, 0f)
        hh < 120f -> Triple(x, c, 0f)
        hh < 180f -> Triple(0f, c, x)
        hh < 240f -> Triple(0f, x, c)
        hh < 300f -> Triple(x, 0f, c)
        else -> Triple(c, 0f, x)
    }
    return Color(r + m, g + m, b + m)
}

internal fun GojiPalette.light(): ToneSet {
    if (id == "sage") return SAGE_LIGHT
    val n = neutralSat
    return ToneSet(
        surface = hsl(hue, n, 0.965f), cLow = hsl(hue, n, 0.94f), c = hsl(hue, n, 0.915f),
        cHigh = hsl(hue, n, 0.89f), cHighest = hsl(hue, n, 0.86f),
        onSurface = hsl(hue, n * 1.5f, 0.11f), onSurfaceVariant = hsl(hue, n * 1.2f, 0.27f),
        outline = hsl(hue, n, 0.36f), outlineVariant = hsl(hue, n, 0.76f),
        primary = hsl(hue, sat, lightL), onPrimary = Color.White,
        primaryC = hsl(hue, sat * 0.85f, 0.85f), onPrimaryC = hsl(hue, sat, 0.12f),
        secondaryC = hsl(hue, sat * 0.45f, 0.87f), onSecondaryC = hsl(hue, sat * 0.6f, 0.13f),
        tertiary = hsl(accentHue, 0.55f, 0.36f), tertiaryDeep = hsl(accentHue, 0.6f, 0.27f),
        tertiaryC = hsl(accentHue, 0.7f, 0.9f), onTertiaryC = hsl(accentHue, 0.8f, 0.12f)
    )
}

internal fun GojiPalette.dark(): ToneSet {
    if (id == "sage") return SAGE_DARK
    val n = neutralSat
    return ToneSet(
        surface = hsl(hue, n, 0.095f), cLow = hsl(hue, n, 0.125f), c = hsl(hue, n, 0.15f),
        cHigh = hsl(hue, n, 0.185f), cHighest = hsl(hue, n, 0.22f),
        onSurface = hsl(hue, n, 0.90f), onSurfaceVariant = hsl(hue, n, 0.76f),
        outline = hsl(hue, n, 0.62f), outlineVariant = hsl(hue, n, 0.28f),
        primary = hsl(hue, (sat * 0.8f).coerceAtMost(0.7f), 0.74f), onPrimary = hsl(hue, sat, 0.13f),
        primaryC = hsl(hue, sat * 0.7f, 0.25f), onPrimaryC = hsl(hue, sat * 0.8f, 0.88f),
        secondaryC = hsl(hue, sat * 0.35f, 0.26f), onSecondaryC = hsl(hue, sat * 0.5f, 0.88f),
        tertiary = hsl(accentHue, 0.8f, 0.78f), tertiaryDeep = hsl(accentHue, 0.8f, 0.88f),
        tertiaryC = hsl(accentHue, 0.55f, 0.26f), onTertiaryC = hsl(accentHue, 0.8f, 0.88f)
    )
}

/** Подпись палитры на текущем языке интерфейса. */
fun GojiPalette.localName() = name(Loc.lang)

private fun c(hex: Long) = Color(0xFF000000 or hex)

/** «Шалфей» — токены макета 9a (README → Design Tokens) без пересчёта. mut чуть темнее (#5F6A65) ради AA. */
private val SAGE_LIGHT = ToneSet(
    surface = c(0xF4F5F2), cLow = c(0xFAFAF8), c = c(0xFFFFFF), cHigh = c(0xEDEFEA), cHighest = c(0xDADFDA),
    onSurface = c(0x2A3430), onSurfaceVariant = c(0x4A5651), outline = c(0x5F6A65), outlineVariant = c(0xDADFDA),
    primary = c(0x4F6358), onPrimary = c(0xFFFFFF), primaryC = c(0xDCE8DF), onPrimaryC = c(0x24362C),
    secondaryC = c(0xDCE8DF), onSecondaryC = c(0x24362C),
    tertiary = c(0x6B6444), tertiaryDeep = c(0x4D4830), tertiaryC = c(0xEFE9D6), onTertiaryC = c(0x3A3626)
)
private val SAGE_DARK = ToneSet(
    surface = c(0x1C2120), cLow = c(0x202625), c = c(0x242A28), cHigh = c(0x2B3230), cHighest = c(0x333B38),
    onSurface = c(0xEEF2EF), onSurfaceVariant = c(0xB8C2BD), outline = c(0x9AA6A0), outlineVariant = c(0x333B38),
    primary = c(0xD9E1DC), onPrimary = c(0x1C2120), primaryC = c(0x2F4038), onPrimaryC = c(0xEAF3EE),
    secondaryC = c(0x2F4038), onSecondaryC = c(0xEAF3EE),
    tertiary = c(0xE8E2CC), tertiaryDeep = c(0xF4F0E2), tertiaryC = c(0x3A372B), onTertiaryC = c(0xF4F0E2)
)
