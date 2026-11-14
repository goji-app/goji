package xyz.gojihub.vpn.widget

import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.widget.RemoteViews
import xyz.gojihub.vpn.R

/**
 * Виджет 2×2: статус защиты, сервер и кнопка подключения. Цвета — из выбранной палитры
 * ([WidgetState]). Перерисовывается из [GojiWidgetProvider.refresh] — его уже зовут сервис
 * (подключение/отключение), SubscriptionRepository (смена узла) и настройки (смена палитры).
 */
class GojiStatusWidgetProvider : AppWidgetProvider() {

    override fun onUpdate(context: Context, appWidgetManager: AppWidgetManager, appWidgetIds: IntArray) {
        val views = build(context)
        appWidgetIds.forEach { appWidgetManager.updateAppWidget(it, views) }
    }

    companion object {
        fun refresh(context: Context) {
            val manager = AppWidgetManager.getInstance(context)
            val ids = manager.getAppWidgetIds(ComponentName(context, GojiStatusWidgetProvider::class.java))
            if (ids.isEmpty()) return
            val views = build(context)
            ids.forEach { manager.updateAppWidget(it, views) }
        }

        private fun build(context: Context): RemoteViews {
            val st = WidgetState(context)
            return RemoteViews(context.packageName, R.layout.widget_status).apply {
                tint(R.id.ws_bg, st.card)
                tint(R.id.ws_dot, st.dot)
                setTextColor(R.id.ws_brand, st.mut)
                setTextViewText(R.id.ws_status, st.status)
                setTextColor(R.id.ws_status, st.ink)
                setTextViewText(R.id.ws_node, st.place)
                setTextColor(R.id.ws_node, st.mut)
                tint(R.id.ws_button_bg, st.buttonBg)
                setTextViewText(R.id.ws_button, st.button)
                setTextColor(R.id.ws_button, st.buttonText)
                setOnClickPendingIntent(R.id.ws_button_box, GojiWidgetProvider.connectToggleIntent(context, st.running))
                setOnClickPendingIntent(R.id.ws_root, GojiWidgetProvider.openAppIntent(context, requestCode = 11))
            }
        }
    }
}
