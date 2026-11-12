package xyz.gojihub.vpn.ui.servers

import xyz.gojihub.vpn.ui.theme.RefreshPill
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.StarBorder
import androidx.compose.foundation.selection.toggleable
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.foundation.layout.heightIn
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.PriorityHigh
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Bolt
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.spring
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import xyz.gojihub.vpn.i18n.Loc
import xyz.gojihub.vpn.settings.ServerSort
import xyz.gojihub.vpn.ui.theme.GodjiColors
import xyz.gojihub.vpn.ui.theme.SpaceGroteskFamily
import xyz.gojihub.vpn.ui.theme.MonoFamily
import androidx.compose.ui.unit.em
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.layout.layout
import xyz.gojihub.vpn.ui.theme.godjiCard
import xyz.gojihub.vpn.ui.theme.godjiGlassFlat
import xyz.gojihub.vpn.ui.theme.godjiGlassPill

@Composable
fun ServersScreen(viewModel: ServersViewModel = hiltViewModel(), onServerPicked: () -> Unit = {}) {
    val state by viewModel.state.collectAsState()
    val refreshingSubscription by viewModel.refreshingSubscription.collectAsState()
    val refreshMessage by viewModel.refreshMessage.collectAsState()

    // Авто-пинг при каждом заходе на вкладку — composable пересоздаётся при переключении вкладок
    // (NavHost saveState/restoreState держит только ViewModel, не Compose-дерево), поэтому
    // LaunchedEffect(Unit) срабатывает ровно при каждом открытии "Серверов", а не один раз за
    // всё время жизни приложения. Раньше нужно было нажимать "⚡" вручную — выбор шёл вслепую.
    // PingRepository.pingAllInternal() сам не даст двум проверкам наложиться друг на друга.
    LaunchedEffect(Unit) { viewModel.pingAllIfStale() }

    // Макет 9a «Куда выйти»: заголовок 30/600, чипы сортировки, строки с большим кодом страны и шкалой пинга.
    val mono = MonoFamily
    val connectVm: xyz.gojihub.vpn.ui.connect.ConnectViewModel = hiltViewModel()
    val cs by connectVm.state.collectAsState()
    val toggle = xyz.gojihub.vpn.ui.connect.rememberVpnToggle(connectVm, cs.connected, cs.connecting)
    Column(Modifier.fillMaxSize()) {
    Column(Modifier.weight(1f).verticalScroll(rememberScrollState()).padding(horizontal = 20.dp)) {
        Text(
            Loc.s.serversTitle, color = GodjiColors.Ink, fontFamily = SpaceGroteskFamily, fontWeight = FontWeight.SemiBold,
            fontSize = 30.sp, lineHeight = 32.sp, letterSpacing = (-0.02).em, modifier = Modifier.padding(top = 24.dp)
        )
        Row(Modifier.fillMaxWidth().padding(top = 12.dp, bottom = 12.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            listOf(
                ServerSort.PING to Loc.f.sortPing,
                ServerSort.FAVORITES to Loc.f.sortFavorites,
                ServerSort.NAME to Loc.f.sortName
            ).forEach { (sort, label) ->
                val on = sort == state.sort
                Text(
                    label.uppercase() + if (on && sort == ServerSort.PING) " ↑" else "",
                    color = if (on) GodjiColors.SurfaceBase else GodjiColors.TextSecondary,
                    fontFamily = mono, fontSize = 11.sp, maxLines = 1,
                    modifier = Modifier
                        .heightIn(min = 32.dp)
                        .then(if (on) Modifier.background(GodjiColors.Ink) else Modifier.border(1.dp, GodjiColors.OutlineVariant))
                        .clickable(role = Role.Button) { viewModel.setSort(sort) }
                        .padding(horizontal = 10.dp, vertical = 8.dp)
                        .wrapContentHeight(Alignment.CenterVertically)
                )
            }
            Spacer(Modifier.weight(1f))
            Text(
                Loc.s.serversPing.uppercase(), color = GodjiColors.Ink, fontFamily = mono, fontSize = 11.sp,
                modifier = Modifier.clickable(role = Role.Button) { viewModel.pingAll() }.padding(horizontal = 6.dp, vertical = 12.dp)
            )
            Text(
                (if (refreshingSubscription) "…" else Loc.s.serversRefresh.uppercase()), color = GodjiColors.Ink, fontFamily = mono, fontSize = 11.sp,
                modifier = Modifier.clickable(role = Role.Button) { viewModel.refreshSubscription() }.padding(start = 6.dp, top = 12.dp, bottom = 12.dp)
            )
        }

        AnimatedContent(
            targetState = refreshMessage,
            transitionSpec = { fadeIn(tween(200)) togetherWith fadeOut(tween(150)) },
            label = "refreshBanner"
        ) { msg ->
            if (msg != null) RefreshBanner(msg, onDismiss = viewModel::dismissRefreshMessage)
        }

        state.servers.forEach { node ->
            val selected = node.id == state.selectedId
            val ink = GodjiColors.Ink
            Column {
                Box(Modifier.fillMaxWidth().height(1.dp).background(GodjiColors.OutlineVariant))
                Row(
                    Modifier
                        .fillMaxWidth()
                        .layout { m, c ->
                            // Выделение выходит за поля на 10dp (макет: margin 0 -10).
                            val ext = 20.dp.roundToPx()
                            val p = m.measure(c.copy(minWidth = c.maxWidth + ext, maxWidth = c.maxWidth + ext))
                            layout(c.maxWidth, p.height) { p.place(-ext / 2, 0) }
                        }
                        .background(if (selected) GodjiColors.Glass else Color.Transparent)
                        .drawBehind { if (selected) drawRect(ink, size = androidx.compose.ui.geometry.Size(3.dp.toPx(), size.height)) }
                        .clickable(role = Role.Button) { viewModel.select(node.id); onServerPicked() }
                        .padding(horizontal = 10.dp, vertical = 4.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Text(
                        node.code.uppercase().take(3), color = ink, fontFamily = mono, fontWeight = FontWeight.Bold,
                        fontSize = 30.sp, letterSpacing = (-0.06).em, maxLines = 1, softWrap = false, modifier = Modifier.width(66.dp)
                    )
                    Text(node.name, color = ink, fontWeight = FontWeight.SemiBold, fontSize = 15.sp, modifier = Modifier.weight(1f), maxLines = 2)
                    // Избранное в макете не нарисовано — оставлено рабочей звёздочкой, цель 44dp.
                    Icon(
                        if (node.isFavorite) Icons.Filled.Star else Icons.Filled.StarBorder,
                        contentDescription = Loc.f.a11yFavorite,
                        tint = if (node.isFavorite) ink else GodjiColors.TextMuted,
                        modifier = Modifier
                            .size(44.dp)
                            .toggleable(value = node.isFavorite, role = Role.Checkbox) { viewModel.toggleFavorite(node.id) }
                            .padding(13.dp)
                    )
                    Column(horizontalAlignment = Alignment.End, verticalArrangement = Arrangement.spacedBy(5.dp)) {
                        Text(
                            when {
                                state.checkingId == node.id || (state.checkingAll && node.pingMs == -2) -> "…"
                                node.pingMs == -2 -> "—"
                                node.pingMs == -1 -> "×"
                                else -> node.pingMs.toString()
                            },
                            color = ink, fontFamily = mono, fontWeight = FontWeight.SemiBold, fontSize = 13.sp,
                            modifier = Modifier.clickable { viewModel.pingOne(node.id) }
                        )
                        PingBar(node.pingMs.takeIf { it >= 0 }, ink)
                    }
                }
            }
        }
        Box(Modifier.fillMaxWidth().height(1.dp).background(GodjiColors.OutlineVariant))
        Spacer(Modifier.height(16.dp))
    }
        Box(
            Modifier
                .padding(horizontal = 20.dp, vertical = 12.dp)
                .fillMaxWidth().heightIn(min = 56.dp)
                .clip(RoundedCornerShape(2.dp))
                .background(GodjiColors.InkBtn)
                .clickable(role = Role.Button) { toggle() },
            contentAlignment = Alignment.Center
        ) {
            Text(
                when {
                    cs.connected -> when (Loc.lang) { xyz.gojihub.vpn.i18n.AppLanguage.RU -> "ОТКЛЮЧИТЬ"; xyz.gojihub.vpn.i18n.AppLanguage.EN -> "DISCONNECT"; xyz.gojihub.vpn.i18n.AppLanguage.ZH -> "断开" }
                    cs.connecting -> when (Loc.lang) { xyz.gojihub.vpn.i18n.AppLanguage.RU -> "ОТМЕНА"; xyz.gojihub.vpn.i18n.AppLanguage.EN -> "CANCEL"; xyz.gojihub.vpn.i18n.AppLanguage.ZH -> "取消" }
                    else -> when (Loc.lang) { xyz.gojihub.vpn.i18n.AppLanguage.RU -> "ВКЛЮЧИТЬ"; xyz.gojihub.vpn.i18n.AppLanguage.EN -> "CONNECT"; xyz.gojihub.vpn.i18n.AppLanguage.ZH -> "连接" }
                },
                color = GodjiColors.Surface, fontFamily = mono, fontWeight = FontWeight.Bold, fontSize = 14.sp, letterSpacing = 0.06.em
            )
        }
    }
}

/** Шкала пинга 48×4: три сегмента (<500 · <1000 · ≥1000 мс) и метка. Цвет не единственный носитель — рядом число. */
@Composable
private fun PingBar(ms: Int?, ink: Color) {
    androidx.compose.foundation.Canvas(Modifier.size(width = 48.dp, height = 10.dp)) {
        val gap = 1.dp.toPx(); val h = 4.dp.toPx(); val y = (size.height - h) / 2
        val seg = (size.width - 2 * gap) / 3
        listOf(Color(0xFF6E8B79), Color(0xFFB39A62), Color(0xFFA5655E)).forEachIndexed { i, c ->
            drawRect(c, androidx.compose.ui.geometry.Offset(i * (seg + gap), y), androidx.compose.ui.geometry.Size(seg, h))
        }
        if (ms != null) {
            val x = minOf(0.97f, ms / 1000f * 0.97f) * size.width
            drawRect(ink, androidx.compose.ui.geometry.Offset(x, 0f), androidx.compose.ui.geometry.Size(2.dp.toPx(), size.height))
        }
    }
}

/** Круглая стеклянная кнопка 40dp с подписью 9.5sp под ней (gap 3) — "Обновить"/"Пинг". */
/** Эталон: padding 11/13, радиус 20, круг 24dp с галочкой, текст 12sp, крестик 22dp на hair. */
@Composable
private fun RefreshBanner(message: RefreshMessage, onDismiss: () -> Unit) {
    // Заметный баннер: полоса слева (primary / danger), подложка по смыслу, крестик справа.
    val bar = if (!message.isError) GodjiColors.Teal else GodjiColors.Danger
    val bg = if (!message.isError) GodjiColors.TealTint else GodjiColors.ErrorContainer
    Row(
        Modifier
            .fillMaxWidth()
            .background(bg)
            .drawBehind { drawRect(bar, size = androidx.compose.ui.geometry.Size(4.dp.toPx(), size.height)) }
            .padding(start = 18.dp, top = 4.dp, bottom = 4.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            if (message.isError) Loc.s.refreshFail else Loc.s.refreshOk, color = GodjiColors.TextPrimary,
            fontWeight = FontWeight.SemiBold, fontSize = 13.sp, modifier = Modifier.weight(1f).padding(vertical = 8.dp)
        )
        Box(
            Modifier
                .size(48.dp)
                .clickable(role = Role.Button, onClickLabel = Loc.f.qrClose) { onDismiss() }
                .semantics { contentDescription = Loc.f.qrClose },
            contentAlignment = Alignment.Center
        ) { Text("×", color = GodjiColors.TextPrimary, fontSize = 20.sp, fontWeight = FontWeight.Bold) }
    }
}

/** Цвета пинга: не проверен — sub, недоступен/медленный — danger, быстрый — accentInk,
 *  средний — #C98B12. Пинг меряется через туннель до узла в Европе: обычные значения 300–1800 мс, поэтому пороги
 *  500/1000 мс (как на Windows, macOS и iOS); при прежних 40/90 мс красным был каждый узел. */
private const val PING_GOOD_MS = 500
private const val PING_MID_MS = 1000

private fun pingColor(pingMs: Int): Color = when {
    pingMs == -2 -> GodjiColors.TextSecondary
    pingMs < 0 -> GodjiColors.Danger
    pingMs < PING_GOOD_MS -> GodjiColors.TealDeep
    pingMs < PING_MID_MS -> Color(0xFFC98B12)
    else -> GodjiColors.Danger
}
