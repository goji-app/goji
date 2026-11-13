package xyz.gojihub.vpn.ui.connect

import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.LiveRegionMode
import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.VpnService
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.animation.core.CubicBezierEasing
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.StartOffset
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.core.content.ContextCompat
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.ArrowDownward
import androidx.compose.material.icons.filled.ArrowUpward
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.PowerSettingsNew
import androidx.compose.material.icons.filled.Shield
import androidx.compose.material.icons.filled.PriorityHigh
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.draw.scale
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.layout.layout
import androidx.compose.ui.layout.layoutId
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.zIndex
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.spring
import androidx.compose.ui.graphics.graphicsLayer
import xyz.gojihub.vpn.ui.theme.CookieShape
import xyz.gojihub.vpn.util.subscriptionBars
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import xyz.gojihub.vpn.R
import xyz.gojihub.vpn.i18n.AppLanguage
import xyz.gojihub.vpn.i18n.Loc
import xyz.gojihub.vpn.network.NetState
import xyz.gojihub.vpn.ui.globe.GojiGlobe
import xyz.gojihub.vpn.ui.theme.GodjiColors
import xyz.gojihub.vpn.ui.theme.SantaHat
import xyz.gojihub.vpn.ui.theme.SpaceGroteskFamily
import xyz.gojihub.vpn.ui.theme.CAP_HEIGHT
import xyz.gojihub.vpn.ui.theme.winterDecorOn
import xyz.gojihub.vpn.ui.theme.snowCaps
import xyz.gojihub.vpn.ui.theme.snowColorFor
import xyz.gojihub.vpn.ui.theme.JetBrainsMonoFamily
import androidx.compose.ui.unit.em
import xyz.gojihub.vpn.ui.theme.godjiCard
import xyz.gojihub.vpn.ui.theme.godjiGlassPill
import xyz.gojihub.vpn.ui.theme.godjiGlassStrong
import xyz.gojihub.vpn.ui.util.rememberPressScale

/** Снег на заголовке голубоватый: белый на белых буквах не виден. */
private fun titleSnow(bg: Color): Color = if (bg.luminance() > 0.5f) Color(0xFF6F8FB0) else Color(0xFFA9CBEF)

@Composable
fun ConnectScreen(viewModel: ConnectViewModel = hiltViewModel(), onOpenPlans: () -> Unit = {}, onOpenServers: () -> Unit = {}, onOpenJournal: () -> Unit = {}) {
    val state by viewModel.state.collectAsState()
    val context = LocalContext.current

    val toggle = rememberVpnToggle(viewModel, state.connected, state.connecting)
    val counter by viewModel.serverCounter.collectAsState()

    val target = when { state.connected -> 2; state.connecting -> 1; else -> 0 }
    // «Шалфей» — «песок/шалфей» из макета 9a; остальные палитры красятся в собственный оттенок:
    // подключение — полутон между фоном и акцентным контейнером, в сети — сам акцентный контейнер.
    val sage = GodjiColors.paletteId == "sage"
    val busyFill = if (sage) GodjiColors.TertiaryContainer else androidx.compose.ui.graphics.lerp(GodjiColors.SurfaceBase, GodjiColors.PrimaryContainer, 0.28f)
    val onFill = if (sage) GodjiColors.SecondaryContainer else androidx.compose.ui.graphics.lerp(GodjiColors.SurfaceBase, GodjiColors.PrimaryContainer, 0.55f)
    val busyInk = if (sage) GodjiColors.OnTertiaryContainer else GodjiColors.OnPrimaryContainer
    val onInk = if (sage) GodjiColors.OnSecondaryContainer else GodjiColors.OnPrimaryContainer
    val bg by animateColorAsState(
        when (target) { 2 -> onFill; 1 -> busyFill; else -> GodjiColors.SurfaceBase },
        tween(500), label = "homeBg"
    )
    val ink by animateColorAsState(
        when (target) { 2 -> onInk; 1 -> busyInk; else -> GodjiColors.TextPrimary },
        tween(500), label = "homeInk"
    )
    SideEffect { GodjiColors.navBg = bg }
    DisposableEffect(Unit) { onDispose { GodjiColors.navBg = null } }
    val mut = if (target == 0) GodjiColors.TextSecondary else ink.copy(alpha = 0.78f)
    val line = if (target == 0) GodjiColors.OutlineVariant else ink.copy(alpha = 0.22f)
    val lang = Loc.lang
    fun t(ru: String, en: String, zh: String) = when (lang) { AppLanguage.RU -> ru; AppLanguage.EN -> en; AppLanguage.ZH -> zh }
    val mono = JetBrainsMonoFamily
    val code = (state.currentGeo?.code ?: Regex("[A-Z]{2,3}$").find(state.currentNodeName.trim())?.value ?: state.currentCode).uppercase().take(3)

    Column(Modifier.fillMaxSize().background(bg)) {
        Column(Modifier.weight(1f).verticalScroll(rememberScrollState()).padding(horizontal = 20.dp)) {
            Row(Modifier.fillMaxWidth().padding(top = 22.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                Text(
                    when (target) {
                        2 -> t("В ТУННЕЛЕ", "IN TUNNEL", "已加密")
                        1 -> t("ПОДКЛЮЧЕНИЕ", "CONNECTING", "连接中")
                        else -> t("НЕТ ЗАЩИТЫ", "NO PROTECTION", "未保护")
                    },
                    color = mut, fontFamily = mono, fontSize = 11.sp
                )
                Text(counter, color = mut, fontFamily = mono, fontSize = 11.sp)
            }
            // Код страны выхода: контур в выключенном состоянии, заливка — когда подключаемся/в сети.
            // Зимой сверху сидит шапка Деда Мороза — под неё отдаётся лишняя высота.
            BoxWithConstraints(Modifier.fillMaxWidth().padding(top = 6.dp)) {
                val size = (maxWidth.value * 0.77f).coerceAtMost(262f)
                val winter = winterDecorOn()
                val snow = snowColorFor(bg)
                var codeLayout by remember { mutableStateOf<TextLayoutResult?>(null) }
                val hatW = if (winter) (size * 0.22f).dp.coerceIn(44.dp, 110.dp) else 0.dp
                val hatH = hatW * 0.87f
                val hatGap = hatH * 0.85f
                Box(
                    Modifier.then(
                        if (winter) Modifier.height((size * 0.89f).dp + hatGap) else Modifier.fillMaxWidth()
                    )
                ) {
                    Text(
                        code,
                        color = if (target == 0) GodjiColors.OutlineVariant else ink,
                        style = androidx.compose.ui.text.TextStyle(
                            fontFamily = SpaceGroteskFamily, fontWeight = FontWeight.Bold,
                            fontSize = size.sp, lineHeight = (size * 0.89f).sp, letterSpacing = (-0.07).em,
                            drawStyle = if (target == 0) Stroke(width = 4f) else null
                        ),
                        maxLines = 1, softWrap = false,
                        modifier = Modifier
                            .offset(x = (-10).dp, y = hatGap)
                            .clickable(role = Role.Button, onClick = viewModel::nextServer)
                            .semantics { contentDescription = state.currentGeo?.displayName(lang) ?: state.currentNodeName }
                            .snowCaps(codeLayout, code, snow, perGlyph = false),
                        onTextLayout = { codeLayout = it }
                    )
                }
                if (winter) {
                    // Шапка садится на верхнюю кромку первой буквы: кромка = базовая линия − высота заглавной.
                    val density = LocalDensity.current.density
                    val base = codeLayout?.getLineBaseline(0)
                    val capTopDp = if (base != null) hatGap.value + (base - CAP_HEIGHT * size * LocalDensity.current.fontScale * density) / density
                        else size * 0.30f
                    SantaHat(
                        Modifier
                            .align(Alignment.TopStart)
                            .offset(x = (size * 0.035f).dp, y = (capTopDp - hatH.value * 0.80f).dp)
                            .rotate(-7f)
                            .size(width = hatW, height = hatW * 0.87f)
                    )
                }
            }
            val titleText = homeTitle(state, ::t)
            var titleLayout by remember { mutableStateOf<TextLayoutResult?>(null) }
            Text(
                titleText, color = ink, fontFamily = SpaceGroteskFamily, fontWeight = FontWeight.SemiBold,
                fontSize = 30.sp, lineHeight = 32.sp, letterSpacing = (-0.02).em,
                onTextLayout = { titleLayout = it },
                modifier = Modifier.padding(top = 4.dp).semantics { liveRegion = LiveRegionMode.Polite }
                    .snowCaps(titleLayout, titleText, titleSnow(bg), perGlyph = true, depthEm = 0.13f, liftEm = 0.07f)
            )
            Text(homeSub(state, ::t), color = mut, fontSize = 14.sp, modifier = Modifier.padding(top = 6.dp).heightIn(min = 40.dp))

            state.error?.let { message ->
                // Заметный блок ошибки: красная полоса слева, подложка ошибки, одно действие следующего шага.
                val hint = when (state.errorAction) {
                    ErrorAction.OPEN_PLANS -> Loc.f.errorHintSubscription
                    ErrorAction.CHANGE_SERVER -> Loc.f.errorHintServer
                    null -> null
                }
                Column(
                    Modifier
                        .fillMaxWidth()
                        .padding(top = 12.dp)
                        .background(GodjiColors.ErrorContainer)
                        .drawBehind { drawRect(GodjiColors.Danger, size = Size(4.dp.toPx(), size.height)) }
                        .padding(start = 18.dp, end = 14.dp, top = 12.dp, bottom = 12.dp)
                ) {
                    Text(if (lang == AppLanguage.RU) "ОШИБКА" else "ERROR", color = GodjiColors.Danger, fontFamily = mono, fontWeight = FontWeight.Bold, fontSize = 11.sp)
                    hint?.let { Text(it, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 15.sp, modifier = Modifier.padding(top = 6.dp)) }
                    Text(message, color = GodjiColors.TextPrimary, fontSize = 13.sp, modifier = Modifier.padding(top = 6.dp))
                    val (label, action) = when (state.errorAction) {
                        ErrorAction.OPEN_PLANS -> Loc.f.errorActionPlans to onOpenPlans
                        ErrorAction.CHANGE_SERVER -> Loc.f.errorActionServer to onOpenServers
                        null -> null to null
                    }
                    if (label != null && action != null) {
                        Box(
                            Modifier.padding(top = 12.dp).height(40.dp).background(GodjiColors.Danger)
                                .clickable(role = Role.Button, onClick = action).padding(horizontal = 14.dp),
                            contentAlignment = Alignment.Center
                        ) { Text(label.uppercase(), color = GodjiColors.Surface, fontFamily = mono, fontWeight = FontWeight.Bold, fontSize = 12.sp) }
                    }
                }
            }

            state.trialDays?.let { days ->
                Box(Modifier.padding(top = 10.dp)) { TrialCard(days, activating = state.trialActivating, onActivate = viewModel::activateTrial) }
            }
            AnimatedContent(
                targetState = state.banner to state.bannerKind,
                transitionSpec = { fadeIn(tween(200)) togetherWith fadeOut(tween(150)) },
                label = "banner"
            ) { (banner, kind) ->
                if (banner != null) Box(Modifier.padding(top = 10.dp)) { BannerCard(banner, kind, onDismiss = viewModel::dismissBanner) }
            }

            // Таблица-прибор: узел, пинг со шкалой, время в сети и скорость.
            Column(Modifier.padding(top = 14.dp)) {
                InstrumentRow(t("УЗЕЛ", "NODE", "节点"), line, mut) {
                    Text(state.currentGeo?.displayCity(lang) ?: state.currentNodeName.ifBlank { Loc.s.defaultNodeName }, color = ink, fontFamily = mono, fontSize = 13.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                }
                InstrumentRow(t("ПИНГ", "PING", "延迟"), line, mut) {
                    val ms = state.qualityPingMs
                    PingScale(ms, ink, Modifier.width(96.dp))
                    Spacer(Modifier.width(10.dp))
                    Text(
                        ms?.toString() ?: "—",
                        color = ink, fontFamily = mono, fontWeight = FontWeight.SemiBold, fontSize = 13.sp,
                        modifier = Modifier.semantics {
                            contentDescription = if (ms == null) "" else t(
                                "Пинг $ms миллисекунд, ${pingWord(ms, 0)}", "Ping $ms ms, ${pingWord(ms, 1)}", "延迟 $ms 毫秒"
                            )
                        }
                    )
                }
                InstrumentRow(t("В СЕТИ", "ONLINE", "时长"), line, mut) {
                    Text(if (state.connected) state.connectedTimeLabel else "—", color = ink, fontFamily = mono, fontSize = 13.sp)
                }
                InstrumentRow(t("СКОРОСТЬ", "SPEED", "速度"), line, mut) {
                    Text(
                        if (state.connected) "↓ %.1f  ↑ %.1f %s".format(state.downSpeedMbps, state.upSpeedMbps, Loc.s.speedUnit) else "—",
                        color = ink, fontFamily = mono, fontSize = 13.sp, maxLines = 1
                    )
                }
            }
            Spacer(Modifier.height(16.dp))
        }

        Box(
            Modifier
                .padding(horizontal = 20.dp, vertical = 12.dp)
                .fillMaxWidth().heightIn(min = 56.dp)
                .clip(RoundedCornerShape(2.dp))
                .background(if (target == 1 && sage) GodjiColors.Terracotta else GodjiColors.InkBtn)
                .clickable(role = Role.Button) { toggle() }
                .semantics {
                    contentDescription = if (state.connected || state.connecting) Loc.f.a11yDisconnect else Loc.f.a11yConnect
                    stateDescription = when {
                        state.connected -> Loc.f.a11yStateOn
                        state.connecting -> Loc.f.a11yStateConnecting
                        else -> Loc.f.a11yStateOff
                    }
                },
            contentAlignment = Alignment.Center
        ) {
            Text(
                when (target) {
                    2 -> t("ОТКЛЮЧИТЬ", "DISCONNECT", "断开")
                    1 -> t("ОТМЕНА", "CANCEL", "取消")
                    else -> t("ВКЛЮЧИТЬ", "CONNECT", "连接")
                },
                color = GodjiColors.Surface, fontFamily = mono, fontWeight = FontWeight.Bold,
                fontSize = 14.sp, letterSpacing = 0.06.em
            )
        }
    }
}

private fun pingWord(ms: Int, l: Int) = when {
    ms < 500 -> if (l == 0) "хороший" else "good"
    ms < 1000 -> if (l == 0) "терпимый" else "fair"
    else -> if (l == 0) "плохой" else "bad"
}

@Composable
private fun InstrumentRow(label: String, line: Color, mut: Color, value: @Composable RowScope.() -> Unit) {
    Column {
        Box(Modifier.fillMaxWidth().height(1.dp).background(line))
        Row(Modifier.fillMaxWidth().padding(vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
            Text(label, color = mut, fontFamily = JetBrainsMonoFamily, fontSize = 13.sp, modifier = Modifier.width(96.dp))
            Row(verticalAlignment = Alignment.CenterVertically, content = value)
        }
    }
}

/** Шкала пинга: три сегмента (<500 · <1000 · ≥1000 мс) и маркер. Цвет не единственный носитель — рядом число. */
@Composable
private fun PingScale(ms: Int?, ink: Color, modifier: Modifier = Modifier) {
    Canvas(modifier.height(14.dp)) {
        val gap = 2.dp.toPx(); val h = 6.dp.toPx(); val y = (size.height - h) / 2
        val seg = (size.width - 2 * gap) / 3
        listOf(Color(0xFF6E8B79), Color(0xFFB39A62), Color(0xFFA5655E)).forEachIndexed { i, c ->
            drawRect(c, Offset(i * (seg + gap), y), Size(seg, h))
        }
        if (ms != null) {
            val x = (minOf(0.97f, ms / 1000f * 0.97f)) * size.width
            drawRect(ink, Offset(x, 0f), Size(2.5.dp.toPx(), size.height))
        }
    }
}

private const val GLOBE_ID = "globe"
private val HOME_PAD_TOP = 14.dp
private val HOME_PAD_BOTTOM = 8.dp
private val HOME_GAP = 8.dp
// Слот глобуса в раскладке (сам холст ещё заходит на 44dp под шапку и на 42dp под кнопку).
// 344 — как было в эталоне; меньше 150 сфера становится мелкой — тогда уже прокрутка.
private val GLOBE_SLOT_MIN = 150.dp
private val GLOBE_SLOT_MAX = 344.dp

/** Колонка главного экрана: блоки по порядку с промежутком [HOME_GAP] (пустые — без
 *  промежутка, например скрытый баннер), глобус (layoutId [GLOBE_ID]) получает остаток
 *  высоты [available] в пределах [GLOBE_SLOT_MIN]..[GLOBE_SLOT_MAX]. */
@Composable
private fun HomeFitColumn(available: Dp, modifier: Modifier, content: @Composable () -> Unit) {
    Layout(content, modifier) { measurables, constraints ->
        val width = constraints.maxWidth
        val gap = HOME_GAP.roundToPx()
        val loose = Constraints(maxWidth = width)
        val globeIdx = measurables.indexOfFirst { it.layoutId == GLOBE_ID }
        val fixed = measurables.mapIndexed { i, m -> if (i == globeIdx) null else m.measure(loose) }
        val shown = fixed.count { it == null || it.height > 0 }
        val gaps = gap * (shown - 1).coerceAtLeast(0)
        val fixedSum = fixed.sumOf { it?.height ?: 0 }
        val globeH = (available.roundToPx() - fixedSum - gaps)
            .coerceIn(GLOBE_SLOT_MIN.roundToPx(), GLOBE_SLOT_MAX.roundToPx())
        val globe = if (globeIdx >= 0) measurables[globeIdx].measure(Constraints.fixed(width, globeH)) else null
        val total = fixedSum + gaps + (globe?.height ?: 0)
        layout(width, total) {
            var y = 0
            fixed.forEachIndexed { i, f ->
                val p = if (i == globeIdx) globe!! else f!!
                if (i != globeIdx && p.height == 0) return@forEachIndexed
                p.place((width - p.width) / 2, y)
                y += p.height + gap
            }
        }
    }
}

private fun countryPhrase(geo: xyz.gojihub.vpn.geo.CountryGeo?) = when (Loc.lang) {
    AppLanguage.RU -> geo?.ruPrep ?: Loc.s.connectFallbackPlace
    else -> geo?.displayName(Loc.lang) ?: Loc.s.connectFallbackPlace
}

private fun headline(s: ConnectUiState) = when {
    s.connected -> Loc.s.connectHeadlineProtected(countryPhrase(s.currentGeo))
    s.connecting -> Loc.s.connectHeadlineConnecting
    else -> Loc.s.connectHeadlineOff
}

private fun subline(s: ConnectUiState): String {
    val nodeName = s.currentNodeName.ifBlank { Loc.s.defaultNodeName }
    return when {
        s.connected -> Loc.s.connectSublineConnected(nodeName)
        s.connecting -> Loc.s.connectSublineConnecting(nodeName)
        else -> Loc.s.connectSublineOff
    }
}

private fun dotColor(s: ConnectUiState) = when {
    s.connected -> GodjiColors.Teal
    s.connecting -> GodjiColors.Warning
    else -> GodjiColors.Outline
}

@Composable
private fun GlobeCard(state: ConnectUiState, modifier: Modifier = Modifier) {
    // Эталон: height:430px; margin:-24px -60px -20px. Высоту слота задаёт HomeFitColumn (до
    // 344dp — тогда холст 430dp, как в эталоне); холст заходит на 44dp под шапку и на 42dp под
    // кнопку, по ширине — +60dp с каждой стороны. Прозрачный холст: вокруг сферы общий фон.
    BoxWithConstraints(
        modifier
            .fillMaxWidth()
            .layout { measurable, constraints ->
                val slot = constraints.maxHeight
                val top = 44.dp.roundToPx()
                val bottom = 42.dp.roundToPx()
                val full = slot + top + bottom
                val placeable = measurable.measure(constraints.copy(minHeight = full, maxHeight = full))
                layout(placeable.width, slot) { placeable.place(0, -top) }
            }
    ) {
        GojiGlobe(
            status = state.globeStatus,
            node = state.globeNode,
            label = "",
            modifier = Modifier
                .align(Alignment.Center)
                .requiredWidth(maxWidth + 120.dp)
                .fillMaxHeight()
        )

        if (state.connected) {
            Column(
                Modifier
                    .align(Alignment.TopEnd)
                    .padding(top = 50.dp, end = 14.dp)
                    .widthIn(max = 190.dp)
                    .godjiGlassPill(RoundedCornerShape(2.dp))
                    .padding(horizontal = 9.dp, vertical = 13.dp),
                horizontalAlignment = Alignment.End
            ) {
                Text(state.greetingHi, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 17.sp)
                Text(state.greetingSub, color = GodjiColors.TextSecondary, fontWeight = FontWeight.SemiBold, fontSize = 9.sp)
            }
        }
    }
}

/** Кнопка подключения в стиле Material 3 Expressive — «печенье» (8 мягких волн) 64dp.
 *  Выключено — primaryContainer; подключение — печенье вращается (как LoadingIndicator M3E);
 *  включено — заливка primary, волны сглаживаются почти в круг. Нажатие «вдавливает» волны —
 *  форма морфится пружиной, а не переключается рывком. Без колец и теней: ничего не
 *  перерисовывается на каждом кадре, кроме поворота во время подключения. */
@Composable
private fun ConnectButton(state: ConnectUiState, onClick: () -> Unit) {
    val (pressInteraction, pressScale) = rememberPressScale()
    val pressed by pressInteraction.collectIsPressedAsState()
    val depth by animateFloatAsState(
        when {
            pressed -> 0.09f
            state.connected -> 0.025f
            else -> 0.055f
        },
        spring(dampingRatio = 0.45f, stiffness = 380f), label = "cookieDepth"
    )
    val fill by animateColorAsState(if (state.connected) GodjiColors.Teal else GodjiColors.PrimaryContainer, label = "cookieFill")
    val iconTint by animateColorAsState(if (state.connected) GodjiColors.Surface else GodjiColors.OnPrimaryContainer, label = "cookieIcon")
    val spin: State<Float> = if (state.connecting) {
        rememberInfiniteTransition(label = "cookieSpin").animateFloat(
            0f, 360f, infiniteRepeatable(tween(1800, easing = LinearEasing)), label = "spin"
        )
    } else remember { mutableFloatStateOf(0f) }
    val shape = CookieShape(8, depth)
    Box(
        Modifier
            .size(64.dp)
            .graphicsLayer {
                rotationZ = spin.value
                scaleX = pressScale.value
                scaleY = pressScale.value
            }
            .background(fill, shape)
            .clip(shape)
            .clickable(interactionSource = pressInteraction, indication = null, role = Role.Button, onClick = onClick)
            // Главная кнопка приложения: TalkBack называет действие и текущее состояние защиты.
            .semantics {
                contentDescription = if (state.connected || state.connecting) Loc.f.a11yDisconnect else Loc.f.a11yConnect
                stateDescription = when {
                    state.connected -> Loc.f.a11yStateOn
                    state.connecting -> Loc.f.a11yStateConnecting
                    else -> Loc.f.a11yStateOff
                }
            },
        contentAlignment = Alignment.Center
    ) {
        Icon(
            Icons.Filled.PowerSettingsNew,
            contentDescription = null,
            tint = iconTint,
            modifier = Modifier.size(26.dp).graphicsLayer { rotationZ = -spin.value }
        )
    }
}

@Composable
private fun NetworkPill(net: NetState) {
    val (label, color) = when (net) {
        NetState.WIFI -> Loc.s.netWifi to GodjiColors.TealDeep
        NetState.CELLULAR -> Loc.s.netCellular to GodjiColors.TextSecondary
        NetState.JAMMED -> Loc.s.netJammed to GodjiColors.JamText
    }
    // netMeta из эталона: Wi-Fi/мобильная — на обычном стекле, глушение — на тёплой подложке.
    Row(
        Modifier
            .height(34.dp)
            .godjiGlassPill(tint = if (net == NetState.JAMMED) GodjiColors.JamBg else null)
            .padding(horizontal = 13.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(7.dp)
    ) {
        Box(Modifier.size(8.dp).clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp)).background(color))
        Text(label, color = color, fontWeight = FontWeight.Bold, fontSize = 12.sp)
    }
}

// Цвет отдачи — tertiary, приглушённый по насыщенности (~15%), чтобы приём доминировал;
// полный акцент GodjiColors.Terracotta остался у остальных элементов.
@Composable
private fun uploadMuted(): Color = if (GodjiColors.isDark) Color(0xFFF2BCA2) else Color(0xFF94553A)

/** Плашка скорости (концепт Goji 2.0): текущие ↓/↑ и мини-график скачивания за последние
 *  ~30 с (downHistory/upHistory из ConnectViewModel; отдача — тонкой линией под ним). */
@Composable
private fun SpeedCard(state: ConnectUiState) {
    Row(
        Modifier
            .fillMaxWidth()
            .godjiCard()
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
            Text(Loc.f.speedNow, color = GodjiColors.TextSecondary, fontWeight = FontWeight.SemiBold, fontSize = 12.sp)
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                Icon(Icons.Filled.ArrowDownward, contentDescription = null, tint = GodjiColors.Teal, modifier = Modifier.size(16.dp))
                Text(
                    "%.1f".format(state.downSpeedMbps), color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold,
                    fontSize = 22.sp, lineHeight = 24.sp, letterSpacing = (-0.4).sp
                )
                Spacer(Modifier.width(6.dp))
                Icon(Icons.Filled.ArrowUpward, contentDescription = null, tint = uploadMuted(), modifier = Modifier.size(13.dp))
                Text(
                    "%.1f ${Loc.s.speedUnit}".format(state.upSpeedMbps), color = GodjiColors.TextSecondary,
                    fontWeight = FontWeight.SemiBold, fontSize = 13.sp, maxLines = 1
                )
            }
        }
        SpeedSparkline(state.downHistory, state.upHistory, Modifier.size(width = 108.dp, height = 40.dp))
    }
}

/** Сглаженная линия скорости: скачивание — акцентом с лёгкой заливкой, отдача — тонкой
 *  тёплой линией. Без данных (не подключено) — пунктирная базовая линия. */
@Composable
private fun SpeedSparkline(down: List<Float>, up: List<Float>, modifier: Modifier = Modifier) {
    val accent = GodjiColors.Teal
    val warm = uploadMuted()
    val muted = GodjiColors.OutlineVariant
    Canvas(modifier) {
        val stroke = 2.5.dp.toPx()
        val top = stroke
        val bottom = size.height - stroke
        if (down.size < 2) {
            drawLine(
                muted, Offset(0f, bottom), Offset(size.width, bottom), strokeWidth = 2.dp.toPx(),
                pathEffect = androidx.compose.ui.graphics.PathEffect.dashPathEffect(floatArrayOf(6f, 8f)),
                cap = StrokeCap.Round
            )
            return@Canvas
        }
        val max = maxOf(down.maxOrNull() ?: 0f, up.maxOrNull() ?: 0f, 0.05f)
        fun path(values: List<Float>): androidx.compose.ui.graphics.Path {
            val step = size.width / (values.size - 1)
            val pts = values.mapIndexed { i, v -> Offset(i * step, bottom - (v / max) * (bottom - top)) }
            return androidx.compose.ui.graphics.Path().apply {
                moveTo(pts[0].x, pts[0].y)
                for (i in 1 until pts.size) {
                    val mid = Offset((pts[i - 1].x + pts[i].x) / 2f, (pts[i - 1].y + pts[i].y) / 2f)
                    quadraticTo(pts[i - 1].x, pts[i - 1].y, mid.x, mid.y)
                }
                lineTo(pts.last().x, pts.last().y)
            }
        }
        if (up.size >= 2) drawPath(path(up), warm.copy(alpha = 0.7f), style = Stroke(1.5.dp.toPx(), cap = StrokeCap.Round))
        val line = path(down)
        val fill = androidx.compose.ui.graphics.Path().apply {
            addPath(line)
            lineTo(size.width, size.height)
            lineTo(0f, size.height)
            close()
        }
        drawPath(fill, Brush.verticalGradient(listOf(accent.copy(alpha = 0.22f), Color.Transparent)))
        drawPath(line, accent, style = Stroke(stroke, cap = StrokeCap.Round, join = androidx.compose.ui.graphics.StrokeJoin.Round))
    }
}

/** Предложение бесплатного пробного периода для аккаунта без подписки. */
@Composable
private fun TrialCard(days: Int, activating: Boolean, onActivate: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().godjiCard(RoundedCornerShape(2.dp), tint = GodjiColors.TealTint).padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(Loc.f.trialTitle(days), color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 14.sp)
            Text(Loc.f.trialDesc, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 11.5.sp, lineHeight = 15.sp)
        }
        Box(
            Modifier
                .height(38.dp)
                .clip(RoundedCornerShape(2.dp))
                .background(GodjiColors.Teal)
                .clickable(enabled = !activating, onClick = onActivate)
                .padding(horizontal = 16.dp),
            contentAlignment = Alignment.Center
        ) {
            if (activating) CircularProgressIndicator(Modifier.size(16.dp), strokeWidth = 2.dp, color = GodjiColors.Surface)
            else Text(Loc.f.trialActivate, color = GodjiColors.Surface, fontWeight = FontWeight.Bold, fontSize = 13.sp)
        }
    }
}

/** Эталон: padding 13/14, радиус 22, подложка bannerBg, круг 28dp цвета accent/warm с белым
 *  значком, текст 12sp, крестик — круг 24dp на подложке hair. */
@Composable
private fun BannerCard(text: String, kind: BannerKind, onDismiss: () -> Unit) {
    val ok = kind == BannerKind.SUCCESS
    val bg = if (ok) GodjiColors.TealTint else GodjiColors.TerracottaTint
    val iconBg = if (ok) GodjiColors.Teal else GodjiColors.Terracotta
    val icon = when (kind) { BannerKind.SUCCESS -> Icons.Filled.Check; BannerKind.WARNING -> Icons.Filled.PriorityHigh; BannerKind.INFO -> Icons.Filled.Info }
    Row(
        Modifier.fillMaxWidth().godjiGlassPill(RoundedCornerShape(2.dp), tint = bg).padding(horizontal = 12.dp, vertical = 10.dp),
        verticalAlignment = Alignment.Top,
        horizontalArrangement = Arrangement.spacedBy(11.dp)
    ) {
        Box(Modifier.size(28.dp).clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp)).background(iconBg), contentAlignment = Alignment.Center) {
            Icon(icon, contentDescription = null, tint = GodjiColors.Surface, modifier = Modifier.size(15.dp))
        }
        Text(text, color = GodjiColors.TextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 12.sp, lineHeight = 17.sp, modifier = Modifier.weight(1f))
        Box(
            Modifier.size(24.dp).clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp)).background(GodjiColors.Hair).clickable { onDismiss() },
            contentAlignment = Alignment.Center
        ) {
            Text("×", color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 13.sp)
        }
    }
}

/** Узел + "Держит тебя в сети" — одна карточка. Строка узла — кнопка на всю ширину карточки,
 *  открывает "Серверы" для быстрого выбора другого узла (goServers в эталоне). */
@Composable
private fun NodeAndAutoSwitchCard(state: ConnectUiState, onOpenServers: () -> Unit, onOpenJournal: () -> Unit) {
    val cardShape = RoundedCornerShape(2.dp)
    Column(Modifier.fillMaxWidth().godjiCard(cardShape).clip(cardShape)) {
        val nodeInteraction = remember { MutableInteractionSource() }
        val pressed by nodeInteraction.collectIsPressedAsState()
        Row(
            Modifier
                .fillMaxWidth()
                .background(if (pressed) Color(0x147F7F7F) else Color.Transparent)
                .clickable(interactionSource = nodeInteraction, indication = null, onClick = onOpenServers)
                .padding(horizontal = 14.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Box(
                Modifier
                    .size(38.dp)
                    .clip(RoundedCornerShape(2.dp))
                    .background(GodjiColors.Chip)
                    .border(1.dp, GodjiColors.CardBorder, RoundedCornerShape(2.dp)),
                contentAlignment = Alignment.Center
            ) {
                Text(state.currentFlag, fontSize = 19.sp)
            }
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Text(state.currentNodeName.ifBlank { Loc.s.defaultNodeName }, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                val meta = state.currentGeo?.displayCityCountry(Loc.lang).orEmpty()
                if (meta.isNotBlank()) {
                    Text(meta, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 11.5.sp)
                }
            }
            Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, contentDescription = null, tint = GodjiColors.TextSecondary, modifier = Modifier.size(16.dp))
        }
        HorizontalDivider(thickness = 2.dp, color = GodjiColors.SurfaceBase)
        // Блок «Держит тебя в сети» открывает журнал сети — там видно, когда и почему
        // переключались узлы (см. journal/NetworkJournal).
        val journal by xyz.gojihub.vpn.journal.NetworkJournal.events.collectAsState()
        val todayStart = remember(journal) { xyz.gojihub.vpn.journal.NetworkJournal.dayStart(0) }
        val switchesToday = journal.count { it.at >= todayStart && xyz.gojihub.vpn.journal.NetworkJournal.isSwitch(it.kind) }
        val lastAt = journal.lastOrNull()?.at?.let { java.text.SimpleDateFormat("HH:mm", java.util.Locale.getDefault()).format(java.util.Date(it)) }
        Column(
            Modifier
                .fillMaxWidth()
                .clickable(onClick = onOpenJournal)
                .padding(start = 14.dp, top = 10.dp, end = 14.dp, bottom = 11.dp),
            verticalArrangement = Arrangement.spacedBy(3.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Icon(Icons.Filled.Shield, contentDescription = null, tint = GodjiColors.TealDeep, modifier = Modifier.size(15.dp))
                Text(Loc.s.autoSwitchTitle, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 14.sp, modifier = Modifier.weight(1f))
                xyz.gojihub.vpn.ui.theme.AutoBadge()
            }
            // Строка "сеть: Wi-Fi/мобильная" убрана ради экрана без прокрутки — сеть и так
            // показана плашкой в шапке.
            Text(Loc.s.autoSwitchDesc, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 11.sp, lineHeight = 16.sp)
            Row(Modifier.padding(top = 4.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                Text(
                    Loc.f.journalHomeLine(switchesToday, lastAt), color = GodjiColors.TealDeep, fontWeight = FontWeight.SemiBold,
                    fontSize = 11.5.sp, maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f)
                )
                Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, contentDescription = null, tint = GodjiColors.TealDeep, modifier = Modifier.size(16.dp))
            }
        }
    }
}

/** Плитка «Качество канала» (концепт Goji 2.0): оценка 0..100 по медиане задержки и потерям
 *  за последнюю минуту (см. ConnectViewModel.startQualityProbe). */
@Composable
private fun QualityTile(state: ConnectUiState, modifier: Modifier = Modifier) {
    val score = state.qualityScore
    val (label, color) = when {
        score == null -> "" to GodjiColors.TextSecondary
        score >= 85 -> Loc.f.qualityExcellent to GodjiColors.Teal
        score >= 65 -> Loc.f.qualityGood to GodjiColors.Teal
        score >= 45 -> Loc.f.qualityFair to GodjiColors.Warning
        else -> Loc.f.qualityPoor to GodjiColors.Danger
    }
    Column(
        modifier.godjiCard().padding(horizontal = 14.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(3.dp)
    ) {
        Text(Loc.f.qualityTitle, color = GodjiColors.TextSecondary, fontWeight = FontWeight.SemiBold, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
        Row(verticalAlignment = Alignment.Bottom, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            Text(
                score?.toString() ?: "—", color = color, fontWeight = FontWeight.ExtraBold,
                fontSize = 26.sp, lineHeight = 28.sp, letterSpacing = (-0.5).sp, modifier = Modifier.alignByBaseline()
            )
            if (label.isNotEmpty()) {
                Text(label, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 12.5.sp, modifier = Modifier.alignByBaseline())
            }
        }
        Text(
            when {
                state.qualityPingMs != null -> Loc.f.qualityPing(state.qualityPingMs, state.qualityLossPct)
                score != null -> Loc.f.qualityNoReply
                state.connected -> Loc.f.qualityMeasuring
                else -> Loc.f.qualityOff
            },
            // Число задержки — не мельче заголовка плитки и semibold, чтобы читалось с первого взгляда.
            color = GodjiColors.TextSecondary, fontWeight = FontWeight.SemiBold, fontSize = 12.sp, lineHeight = 15.sp
        )
    }
}

/** Плитка «Подписка» (концепт Goji 2.0): остаток дней крупно, полоски-индикатор остатка
 *  (меньше недели — тёплым цветом, меньше трёх дней — красным) и дата окончания с трафиком. */
@Composable
private fun SubscriptionTile(state: ConnectUiState, onClick: () -> Unit, modifier: Modifier = Modifier) {
    val days = state.daysLeft
    val filled = subscriptionBars(days)
    val barColor = when {
        days < 3 -> GodjiColors.Danger
        days < 7 -> GodjiColors.Terracotta
        else -> GodjiColors.Teal
    }
    val unlimited = state.isUnlimited || state.quotaGb <= 0.0
    Column(
        modifier.godjiCard().clickable(onClick = onClick).padding(horizontal = 14.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        Text(Loc.f.subTileTitle, color = GodjiColors.TextSecondary, fontWeight = FontWeight.SemiBold, fontSize = 12.sp)
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(
                Loc.f.subTileDays(days), color = GodjiColors.TextPrimary, fontWeight = FontWeight.ExtraBold,
                fontSize = 20.sp, lineHeight = 24.sp, letterSpacing = (-0.4).sp, maxLines = 1,
                modifier = Modifier.weight(1f, fill = false)
            )
            Row(horizontalArrangement = Arrangement.spacedBy(3.dp), verticalAlignment = Alignment.Bottom) {
                repeat(5) { i ->
                    Box(
                        Modifier
                            .size(width = 5.dp, height = (8 + i * 2).dp)
                            .clip(RoundedCornerShape(2.dp))
                            .background(if (i < filled) barColor else GodjiColors.TrackBg)
                    )
                }
            }
        }
        Text(
            Loc.f.subTileUntil(state.expiryLabel) + " · " +
                if (unlimited) Loc.s.trafficUnlimited("%.1f".format(state.usedGb)) else Loc.s.trafficLimited("%.1f".format(state.usedGb), state.quotaGb.toInt()),
            color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 11.sp, lineHeight = 14.sp
        )
    }
}

/** Плавно анимирует изменение доли использованного трафика (вместо мгновенного скачка) и
 *  добавляет бегущий блик по заполненной части — акцентный градиент вместо плоского тона. */
@Composable
private fun AnimatedTrafficBar(pct: Float, modifier: Modifier = Modifier) {
    val animatedPct by animateFloatAsState(
        targetValue = pct.coerceIn(0f, 1f),
        animationSpec = tween(durationMillis = 900, easing = FastOutSlowInEasing),
        label = "trafficPct"
    )
    val infiniteTransition = rememberInfiniteTransition(label = "trafficShimmer")
    // gg-shimmer: блик шириной 40% заливки, translateX −100% → 300% за 1.8с
    val shimmerT by infiniteTransition.animateFloat(
        initialValue = -1f,
        targetValue = 3f,
        animationSpec = infiniteRepeatable(tween(durationMillis = 1800, easing = LinearEasing)),
        label = "shimmerT"
    )
    BoxWithConstraints(
        modifier
            .fillMaxWidth()
            .height(8.dp)
            .clip(RoundedCornerShape(2.dp))
            .background(GodjiColors.TrackBg)
    ) {
        val fillWidth = maxWidth * animatedPct
        val shimmerWidth = fillWidth * 0.4f
        if (fillWidth > 0.dp) {
            Box(
                Modifier
                    .fillMaxHeight()
                    .width(fillWidth)
                    .clip(RoundedCornerShape(2.dp))
                    .background(Brush.verticalGradient(0f to GodjiColors.AccentGradTop, 0.55f to GodjiColors.AccentGradMid, 1f to GodjiColors.AccentGradBottom))
            ) {
                Box(
                    Modifier
                        .fillMaxHeight()
                        .width(shimmerWidth)
                        .offset(x = shimmerWidth * shimmerT)
                        .background(
                            Brush.horizontalGradient(
                                listOf(Color.Transparent, Color.White.copy(alpha = 0.55f), Color.Transparent)
                            )
                        )
                )
            }
        }
    }
}

private fun homeTitle(s: ConnectUiState, t: (String, String, String) -> String) = when {
    s.connected -> {
        val name = s.currentGeo?.displayName(Loc.lang) ?: Loc.s.connectFallbackPlace
        t("Ты в сети: $name", "You're online: $name", "已连接：$name")
    }
    s.connecting -> t("Подключаем…", "Connecting…", "连接中…")
    else -> t("Без защиты", "Not protected", "未受保护")
}

private fun homeSub(s: ConnectUiState, t: (String, String, String) -> String) = when {
    s.connected -> t("Провайдер видит только шифр. Держит тебя в сети", "Your provider sees only ciphertext", "运营商只能看到密文")
    s.connecting -> t("Ищем лучший путь до узла", "Finding the best path to the node", "正在寻找到节点的最佳路径")
    else -> t("Провайдер видит всё, что ты открываешь", "Your provider sees everything you open", "运营商能看到你访问的一切")
}


/** Включить/отключить VPN с запросами разрешений — общий для главной и «Серверов». */
@Composable
internal fun rememberVpnToggle(viewModel: ConnectViewModel, connected: Boolean, connecting: Boolean): () -> Unit {
    val context = LocalContext.current
    val vpnPermissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == android.app.Activity.RESULT_OK) viewModel.startTunnel(context)
    }
    // На Android 13+ без рантайм-запроса уведомление службы не показывается; отказ не блокирует подключение.
    val notificationPermissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { }
    return {
        if (connected || connecting) {
            viewModel.stopTunnel(context)
        } else {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
            ) {
                notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
            }
            val prepareIntent = VpnService.prepare(context)
            if (prepareIntent != null) vpnPermissionLauncher.launch(prepareIntent) else viewModel.startTunnel(context)
        }
    }
}
