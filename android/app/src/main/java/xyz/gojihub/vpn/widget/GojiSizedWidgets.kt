package xyz.gojihub.vpn.widget

import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.widget.RemoteViews
import androidx.compose.ui.graphics.toArgb
import androidx.core.graphics.ColorUtils
import xyz.gojihub.vpn.R
import xyz.gojihub.vpn.i18n.Loc
import xyz.gojihub.vpn.ui.theme.GodjiColors
import xyz.gojihub.vpn.ui.theme.Palettes
import xyz.gojihub.vpn.ui.theme.ThemeMode
import xyz.gojihub.vpn.ui.theme.dark
import xyz.gojihub.vpn.ui.theme.isSystemInDarkMode
import xyz.gojihub.vpn.ui.theme.light
import xyz.gojihub.vpn.util.formatDate
import xyz.gojihub.vpn.util.stripLeadingFlag
import xyz.gojihub.vpn.util.subscriptionBars
import xyz.gojihub.vpn.vpn.GodjiVpnService

/**
 * Общие данные виджетов: состояние VPN, сервер, срок подписки и цвета. Цвета берутся из
 * выбранной в приложении палитры и темы (светлая/тёмная/системная), а не из статичных ресурсов,
 * поэтому при смене палитры виджеты перекрашиваются (см. [GojiWidgetProvider.refresh]).
 */
internal class WidgetState(context: Context) {
    val running = GodjiVpnService.isRunning.value
    private val node = context.widgetEntryPoint().subscriptionRepository().selectedNode()
    val place: String = node?.let { it.geo?.displayCityCountry(Loc.lang) ?: stripLeadingFlag(it.name) } ?: Loc.s.widgetNotSelected
    val city: String = node?.let { it.geo?.displayCity(Loc.lang) ?: stripLeadingFlag(it.name) } ?: Loc.s.widgetNotSelected
    val code: String = node?.geo?.code
        ?: node?.let { Regex("[A-Z]{2,3}$").find(stripLeadingFlag(it.name).trim())?.value }
        ?: "VPN"
    val status = if (running) Loc.f.widgetProtected else Loc.f.widgetUnprotected
    val button = if (running) Loc.s.widgetDisconnect else Loc.s.widgetConnect

    private val days = WidgetSubscription.daysLeft(context)
    private val expiryIso = WidgetSubscription.expiryIso(context)
    val subLine: String = if (days != null && expiryIso != null) "$days ${Loc.f.widgetDaysLine(days, formatDate(expiryIso))}" else Loc.f.widgetNoSubscription
    private val bars = if (days == null) 0 else subscriptionBars(days)

    private val tones = run {
        val dark = when (GodjiColors.themeMode) {
            ThemeMode.DARK -> true
            ThemeMode.LIGHT -> false
            ThemeMode.SYSTEM -> isSystemInDarkMode(context)
        }
        Palettes.byId(GodjiColors.paletteId).let { if (dark) it.dark() else it.light() }
    }

    // Выключено — спокойная поверхность палитры; включено — контейнер основного цвета.
    val card = (if (running) tones.primaryC else tones.cHigh).toArgb()
    val ink = (if (running) tones.onPrimaryC else tones.onSurface).toArgb()
    val mut = (if (running) tones.onPrimaryC else tones.onSurfaceVariant).toArgb().let { if (running) ColorUtils.setAlphaComponent(it, 0xCC) else it }
    val dot = (if (running) tones.onPrimaryC else tones.outline).toArgb()
    val buttonBg = (if (running) tones.onPrimaryC else tones.primary).toArgb()
    val buttonText = (if (running) tones.primaryC else tones.onPrimary).toArgb()
    val chip = ColorUtils.blendARGB(card, ink, 0.12f)
    private val barOn = (if (running) tones.onPrimaryC else tones.primary).toArgb()
    private val barOff = ColorUtils.blendARGB(card, ink, 0.2f)

    /** Цвет i-й полоски срока подписки. */
    fun bar(i: Int) = if (i < bars) barOn else barOff
}

internal fun RemoteViews.tint(id: Int, color: Int) = setInt(id, "setColorFilter", color)

private fun refreshAll(context: Context, cls: Class<*>, build: (Context) -> RemoteViews) {
    val manager = AppWidgetManager.getInstance(context)
    val ids = manager.getAppWidgetIds(ComponentName(context, cls))
    if (ids.isEmpty()) return
    val views = build(context)
    ids.forEach { manager.updateAppWidget(it, views) }
}

/** 1×1: кнопка питания. */
class GojiTinyWidgetProvider : AppWidgetProvider() {
    override fun onUpdate(context: Context, appWidgetManager: AppWidgetManager, appWidgetIds: IntArray) {
        val views = build(context)
        appWidgetIds.forEach { appWidgetManager.updateAppWidget(it, views) }
    }

    companion object {
        fun refresh(context: Context) = refreshAll(context, GojiTinyWidgetProvider::class.java, ::build)

        private fun build(context: Context): RemoteViews {
            val st = WidgetState(context)
            return RemoteViews(context.packageName, R.layout.widget_tiny).apply {
                tint(R.id.wt_bg, st.buttonBg)
                tint(R.id.wt_icon, st.buttonText)
                setOnClickPendingIntent(R.id.wt_root, GojiWidgetProvider.connectToggleIntent(context, st.running))
            }
        }
    }
}

/** 4×2: страна, сервер, срок подписки, смена сервера и кнопка. */
class GojiWideWidgetProvider : AppWidgetProvider() {
    override fun onUpdate(context: Context, appWidgetManager: AppWidgetManager, appWidgetIds: IntArray) {
        val views = build(context)
        appWidgetIds.forEach { appWidgetManager.updateAppWidget(it, views) }
    }

    companion object {
        private val BARS = intArrayOf(R.id.wd_bar0, R.id.wd_bar1, R.id.wd_bar2, R.id.wd_bar3, R.id.wd_bar4)

        fun refresh(context: Context) = refreshAll(context, GojiWideWidgetProvider::class.java, ::build)

        private fun build(context: Context): RemoteViews {
            val st = WidgetState(context)
            return RemoteViews(context.packageName, R.layout.widget_wide).apply {
                tint(R.id.wd_bg, st.card)
                tint(R.id.wd_dot, st.dot)
                setTextViewText(R.id.wd_tag, st.status)
                setTextColor(R.id.wd_tag, st.mut)
                setTextViewText(R.id.wd_code, st.code)
                setTextColor(R.id.wd_code, st.ink)
                setTextViewText(R.id.wd_node, st.city)
                setTextColor(R.id.wd_node, st.ink)
                setTextViewText(R.id.wd_sub, st.subLine)
                setTextColor(R.id.wd_sub, st.mut)
                BARS.forEachIndexed { i, id -> tint(id, st.bar(i)) }
                listOf(R.id.wd_prev_bg, R.id.wd_next_bg).forEach { tint(it, st.chip) }
                listOf(R.id.wd_prev_t, R.id.wd_next_t).forEach { setTextColor(it, st.ink) }
                tint(R.id.wd_button_bg, st.buttonBg)
                setTextViewText(R.id.wd_button, st.button)
                setTextColor(R.id.wd_button, st.buttonText)
                setOnClickPendingIntent(R.id.wd_button_box, GojiWidgetProvider.connectToggleIntent(context, st.running))
                setOnClickPendingIntent(R.id.wd_prev, nodeStep(context, GojiWidgetActionReceiver.ACTION_PREV_NODE, 2))
                setOnClickPendingIntent(R.id.wd_next, nodeStep(context, GojiWidgetActionReceiver.ACTION_NEXT_NODE, 3))
                setOnClickPendingIntent(R.id.wd_root, GojiWidgetProvider.openAppIntent(context, requestCode = 14))
            }
        }

        private fun nodeStep(context: Context, action: String, requestCode: Int) =
            GojiWidgetProvider.nodeStepIntent(context, action, requestCode)
    }
}
