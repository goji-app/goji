package xyz.gojihub.vpn.widget

import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.widget.RemoteViews
import androidx.core.content.ContextCompat
import xyz.gojihub.vpn.R
import xyz.gojihub.vpn.i18n.AppLanguage
import xyz.gojihub.vpn.i18n.Loc
import xyz.gojihub.vpn.util.stripLeadingFlag
import xyz.gojihub.vpn.vpn.GodjiVpnService

/** Общие данные виджетов разных размеров (макет 9a): состояние, код страны, город/узел, цвета по состоянию. */
internal class WidgetState(context: Context) {
    val running = GodjiVpnService.isRunning.value
    private val node = context.widgetEntryPoint().subscriptionRepository().selectedNode()
    val city: String = node?.let { it.geo?.displayCity(Loc.lang) ?: stripLeadingFlag(it.name) } ?: Loc.s.widgetNotSelected
    val code: String = node?.geo?.code
        ?: node?.let { Regex("[A-Z]{2,3}$").find(stripLeadingFlag(it.name).trim())?.value }
        ?: "VPN"
    val ru = Loc.lang == AppLanguage.RU
    val tag = if (running) (if (ru) "В ТУННЕЛЕ" else "IN TUNNEL") else (if (ru) "НЕТ ЗАЩИТЫ" else "NO PROTECTION")
    val button = if (running) (if (ru) "ОТКЛЮЧИТЬ" else "DISCONNECT") else (if (ru) "ВКЛЮЧИТЬ" else "CONNECT")
    val short = if (running) (if (ru) "ВЫКЛ" else "OFF") else (if (ru) "ВКЛ" else "ON")
    private val ctx = context
    fun color(id: Int) = ContextCompat.getColor(ctx, id)
    val ink get() = color(if (running) R.color.widget_on_text else R.color.widget_text_primary)
    val mut get() = color(if (running) R.color.widget_on_text_secondary else R.color.widget_text_secondary)
    val bg get() = if (running) R.drawable.widget_status_on_bg else R.drawable.widget_background
    val buttonBg get() = if (running) R.drawable.widget_on_button else R.drawable.widget_button_teal
    val buttonText get() = color(if (running) R.color.widget_on_button_text else R.color.widget_button_text)
}

private fun refreshAll(context: Context, cls: Class<*>, build: (Context) -> RemoteViews) {
    val manager = AppWidgetManager.getInstance(context)
    val ids = manager.getAppWidgetIds(ComponentName(context, cls))
    if (ids.isEmpty()) return
    val views = build(context)
    ids.forEach { manager.updateAppWidget(it, views) }
}

/** 1×1: только кнопка подключения. */
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
                setTextViewText(R.id.wt_button, st.short)
                setInt(R.id.wt_button, "setBackgroundResource", st.buttonBg)
                setTextColor(R.id.wt_button, st.buttonText)
                setOnClickPendingIntent(R.id.wt_button, GojiWidgetProvider.connectToggleIntent(context, st.running))
            }
        }
    }
}

/** 4×1: состояние и узел слева, кнопка справа. */
class GojiStripWidgetProvider : AppWidgetProvider() {
    override fun onUpdate(context: Context, appWidgetManager: AppWidgetManager, appWidgetIds: IntArray) {
        val views = build(context)
        appWidgetIds.forEach { appWidgetManager.updateAppWidget(it, views) }
    }

    companion object {
        fun refresh(context: Context) = refreshAll(context, GojiStripWidgetProvider::class.java, ::build)

        private fun build(context: Context): RemoteViews {
            val st = WidgetState(context)
            return RemoteViews(context.packageName, R.layout.widget_strip).apply {
                setInt(R.id.wst_root, "setBackgroundResource", st.bg)
                setTextViewText(R.id.wst_tag, st.tag)
                setTextViewText(R.id.wst_node, "${st.code} · ${st.city}")
                setTextColor(R.id.wst_tag, st.mut)
                setTextColor(R.id.wst_node, st.ink)
                setTextViewText(R.id.wst_button, st.button)
                setInt(R.id.wst_button, "setBackgroundResource", st.buttonBg)
                setTextColor(R.id.wst_button, st.buttonText)
                setOnClickPendingIntent(R.id.wst_button, GojiWidgetProvider.connectToggleIntent(context, st.running))
                setOnClickPendingIntent(R.id.wst_root, GojiWidgetProvider.openAppIntent(context, requestCode = 13))
            }
        }
    }
}

/** 4×2: код страны крупно, состояние, город, кнопка. */
class GojiWideWidgetProvider : AppWidgetProvider() {
    override fun onUpdate(context: Context, appWidgetManager: AppWidgetManager, appWidgetIds: IntArray) {
        val views = build(context)
        appWidgetIds.forEach { appWidgetManager.updateAppWidget(it, views) }
    }

    companion object {
        fun refresh(context: Context) = refreshAll(context, GojiWideWidgetProvider::class.java, ::build)

        private fun build(context: Context): RemoteViews {
            val st = WidgetState(context)
            return RemoteViews(context.packageName, R.layout.widget_wide).apply {
                setInt(R.id.wd_root, "setBackgroundResource", st.bg)
                setTextViewText(R.id.wd_tag, st.tag)
                setTextViewText(R.id.wd_code, st.code)
                setTextViewText(R.id.wd_node, st.city)
                setTextColor(R.id.wd_tag, st.mut)
                setTextColor(R.id.wd_code, st.ink)
                setTextColor(R.id.wd_node, st.ink)
                setTextViewText(R.id.wd_button, st.button)
                setInt(R.id.wd_button, "setBackgroundResource", st.buttonBg)
                setTextColor(R.id.wd_button, st.buttonText)
                setOnClickPendingIntent(R.id.wd_button, GojiWidgetProvider.connectToggleIntent(context, st.running))
                setOnClickPendingIntent(R.id.wd_root, GojiWidgetProvider.openAppIntent(context, requestCode = 14))
            }
        }
    }
}
