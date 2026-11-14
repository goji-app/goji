package xyz.gojihub.vpn.widget

import android.content.Context
import java.time.Instant

/**
 * Дата окончания подписки для виджета 4×2. Сохраняется при каждом обновлении подписки
 * ([saveExpiry] из GodjiApplication), а дни считаются от неё в момент отрисовки — так виджет
 * не врёт, даже если приложение несколько дней не открывали.
 */
internal object WidgetSubscription {
    private const val PREFS = "widget_subscription"
    private const val KEY_EXPIRE = "expire_at"

    fun saveExpiry(context: Context, expireAtIso: String) {
        val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        if (prefs.getString(KEY_EXPIRE, null) == expireAtIso) return
        prefs.edit().putString(KEY_EXPIRE, expireAtIso).apply()
        GojiWidgetProvider.refresh(context)
    }

    fun expiryIso(context: Context): String? =
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString(KEY_EXPIRE, null)

    fun daysLeft(context: Context): Int? = expiryIso(context)?.let {
        runCatching { ((Instant.parse(it).toEpochMilli() - System.currentTimeMillis()) / 86_400_000L).toInt().coerceAtLeast(0) }.getOrNull()
    }
}
