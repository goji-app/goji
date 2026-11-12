package xyz.gojihub.vpn.ui.plans

import xyz.gojihub.vpn.ui.theme.MonoFamily
import androidx.compose.ui.unit.em
import xyz.gojihub.vpn.ui.theme.RefreshPill
import androidx.compose.ui.semantics.Role
import android.content.Intent
import android.net.Uri
import androidx.browser.customtabs.CustomTabsIntent
import xyz.gojihub.vpn.BuildConfig
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.CubicBezierEasing
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.keyframes
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.spring
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Chat
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Groups
import androidx.compose.material.icons.filled.LocalOffer
import androidx.compose.material.icons.filled.NorthEast
import androidx.compose.material.icons.filled.QrCode2
import xyz.gojihub.vpn.ui.util.QrDialog
import xyz.gojihub.vpn.ui.util.RichBlock
import xyz.gojihub.vpn.ui.util.parseRichContent
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.*
import androidx.compose.runtime.*
import kotlinx.coroutines.launch
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.draw.scale
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import xyz.gojihub.vpn.i18n.Loc
import xyz.gojihub.vpn.ui.theme.ActiveBadge
import xyz.gojihub.vpn.ui.theme.GodjiColors
import xyz.gojihub.vpn.ui.theme.HoloBadge
import xyz.gojihub.vpn.ui.theme.SpaceGroteskFamily
import xyz.gojihub.vpn.ui.theme.godjiCard
import xyz.gojihub.vpn.ui.theme.godjiGlassPill
import xyz.gojihub.vpn.ui.util.RichContent
import xyz.gojihub.vpn.ui.util.rememberPressScale


/** Разметка — по handoff-1.0.77/reference/GojiGlassFull.dc.html (isPlans). */
/** Личный кабинет на сайте — оплата и продление подписки (только сборка site). */
private val CABINET_URL = BuildConfig.SITE_URL + "/#/my-dashboard"

@Composable
fun PlansScreen(onOpenSupport: () -> Unit, viewModel: PlansViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsState()
    val context = LocalContext.current
    val clipboard = LocalClipboardManager.current

    // Эталон: колонка padding 16, gap 12. Обычная колонка в общем скролле, а не LazyColumn —
    // список короткий, а вложенный скролл внутри скролла ломает Compose.
    Column(
        Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(start = 20.dp, end = 20.dp, top = 24.dp, bottom = 12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Row(
            Modifier.fillMaxWidth().padding(horizontal = 4.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.Top
        ) {
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                Text(accountTitle(), color = GodjiColors.Ink, fontFamily = SpaceGroteskFamily, fontWeight = FontWeight.SemiBold, fontSize = 30.sp, lineHeight = 32.sp, letterSpacing = (-0.02).em)
            }
            Spacer(Modifier.width(10.dp))
            Text(
                if (state.refreshing) "…" else Loc.s.serversRefresh.uppercase(), color = GodjiColors.Ink, fontFamily = MonoFamily, fontSize = 11.sp,
                modifier = Modifier.clickable(role = Role.Button, onClick = viewModel::refresh).padding(vertical = 14.dp, horizontal = 4.dp)
            )
        }

        SubscriptionCard(state)

        if (state.subscriptionId != null) {
            DevicesSection(
                devices = state.devices,
                deviceLimit = state.deviceLimit,
                deleteSupportOnly = state.devicesDeleteSupportOnly,
                onRename = viewModel::renameDevice,
                onDelete = viewModel::deleteDevice,
                onOpenSupport = onOpenSupport
            )
        }

        // Перенос входа на другое устройство по QR — без почты и пароля.
        var transferUri by remember { mutableStateOf<String?>(null) }
        val transferScope = rememberCoroutineScope()
        TransferCard(onClick = {
            transferScope.launch {
                transferUri = viewModel.transferUri()
                if (transferUri == null) android.widget.Toast.makeText(context, Loc.f.transferFailed, android.widget.Toast.LENGTH_LONG).show()
            }
        })
        transferUri?.let { uri ->
            QrDialog(
                title = Loc.f.transferQrTitle,
                subtitle = Loc.f.transferQrSubtitle,
                content = uri,
                warning = Loc.f.transferQrWarning,
                closeLabel = Loc.f.qrClose,
                onDismiss = { transferUri = null }
            )
        }

        if (state.news.isNotEmpty()) {
            val pages = state.news.chunked(NEWS_PAGE_SIZE)
            val page = state.newsPage.coerceIn(0, (pages.size - 1).coerceAtLeast(0))
            Row(
                Modifier.fillMaxWidth().padding(start = 8.dp, top = 8.dp, end = 8.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                SectionLabelText(Loc.s.plansNewsTitle)
                if (!state.newsExpanded) {
                    if (state.news.size > NEWS_PREVIEW_COUNT) {
                        Text(
                            Loc.s.plansNewsShowAll, color = GodjiColors.TealDeep, fontWeight = FontWeight.Bold, fontSize = 12.sp,
                            modifier = Modifier.clickable { viewModel.toggleNewsExpanded() }
                        )
                    }
                } else if (pages.size > 1) {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                        PagerCircle("‹", enabled = page > 0) { viewModel.setNewsPage(page - 1) }
                        Text("${page + 1} / ${pages.size}", color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 12.sp, modifier = Modifier.widthIn(min = 38.dp), textAlign = androidx.compose.ui.text.style.TextAlign.Center)
                        PagerCircle("›", enabled = page < pages.size - 1) { viewModel.setNewsPage(page + 1) }
                    }
                }
            }
            val visible = if (state.newsExpanded) pages.getOrNull(page).orEmpty() else state.news.take(NEWS_PREVIEW_COUNT)
            visible.forEach { NewsCard(it) }
            if (state.newsExpanded) {
                Text(
                    Loc.s.plansNewsCollapse, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Bold, fontSize = 12.sp,
                    modifier = Modifier.padding(start = 8.dp).clickable { viewModel.toggleNewsExpanded() }
                )
            }
        }

        state.referral?.let { referral ->
            SectionLabel(Loc.s.plansInviteTitle)
            var showReferralQr by remember { mutableStateOf(false) }
            ReferralCard(referral, onCopy = { clipboard.setText(AnnotatedString(referral.link)) }, onShare = {
                val send = Intent(Intent.ACTION_SEND).apply {
                    type = "text/plain"
                    putExtra(Intent.EXTRA_TEXT, referral.link)
                }
                context.startActivity(Intent.createChooser(send, null))
            }, onQr = { showReferralQr = true })
            if (showReferralQr) {
                QrDialog(
                    title = Loc.f.referralQrTitle,
                    subtitle = Loc.f.referralQrSubtitle,
                    content = referral.link,
                    caption = referral.link,
                    closeLabel = Loc.f.qrClose,
                    onDismiss = { showReferralQr = false }
                )
            }
        }

        // Поддержка — эталон: padding 13/14, радиус 24, подложка warmGlass, круг 34dp warm.
        val (supportInteraction, supportScale) = rememberPressScale()
        Row(
            Modifier
                .fillMaxWidth()
                .scale(supportScale.value)
                .godjiCard()
                .clickable(interactionSource = supportInteraction, indication = null, onClick = onOpenSupport)
                .padding(horizontal = 14.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Box(Modifier.size(34.dp).clip(RoundedCornerShape(2.dp)).background(GodjiColors.InkBtn), contentAlignment = Alignment.Center) {
                Icon(Icons.AutoMirrored.Filled.Chat, contentDescription = null, tint = GodjiColors.Surface, modifier = Modifier.size(16.dp))
            }
            Text(Loc.s.plansSupportText, color = GodjiColors.TextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 12.sp, lineHeight = 16.8.sp)
        }
    }
}

private fun daysWord(n: Int): String = when (xyz.gojihub.vpn.i18n.Loc.lang) {
    xyz.gojihub.vpn.i18n.AppLanguage.RU -> {
        val m100 = n % 100; val m10 = n % 10
        "$n " + if (m100 in 11..14) "дней" else when (m10) { 1 -> "день"; 2, 3, 4 -> "дня"; else -> "дней" }
    }
    xyz.gojihub.vpn.i18n.AppLanguage.EN -> if (n == 1) "$n day" else "$n days"
    xyz.gojihub.vpn.i18n.AppLanguage.ZH -> "$n 天"
}

private fun accountTitle() = when (xyz.gojihub.vpn.i18n.Loc.lang) {
    xyz.gojihub.vpn.i18n.AppLanguage.RU -> "Аккаунт"
    xyz.gojihub.vpn.i18n.AppLanguage.EN -> "Account"
    xyz.gojihub.vpn.i18n.AppLanguage.ZH -> "账户"
}

private fun noAutoRenew() = when (xyz.gojihub.vpn.i18n.Loc.lang) {
    xyz.gojihub.vpn.i18n.AppLanguage.RU -> "Подписка не продлевается сама — списаний врасплох не будет."
    xyz.gojihub.vpn.i18n.AppLanguage.EN -> "Your subscription never renews itself — no surprise charges."
    xyz.gojihub.vpn.i18n.AppLanguage.ZH -> "订阅不会自动续费，不会有意外扣款。"
}

/** Макет 9a: «24 дня» крупно, «до 27 октября», пояснение про ручное продление, кнопка в личный кабинет. */
@Composable
private fun SubscriptionCard(state: PlansUiState) {
    val context = LocalContext.current
    Column(Modifier.fillMaxWidth().padding(top = 8.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Row(verticalAlignment = Alignment.Bottom, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(
                daysWord(state.daysLeft), color = GodjiColors.Ink, fontFamily = SpaceGroteskFamily,
                fontWeight = FontWeight.SemiBold, fontSize = 44.sp, letterSpacing = (-0.04).em, maxLines = 1
            )
            Text(Loc.s.plansUntil(state.expiryLabel), color = GodjiColors.TextSecondary, fontSize = 14.sp, modifier = Modifier.padding(bottom = 8.dp))
        }
        Text(noAutoRenew(), color = GodjiColors.TextSecondary, fontSize = 13.sp, lineHeight = 18.sp)
        // Только в сборке site (сайт загрузки / GitHub): в Google Play вести к оплате мимо
        // Google Play Billing нельзя (см. app/build.gradle.kts).
        if (BuildConfig.ENABLE_CABINET_LINK) {
            Box(
                Modifier
                    .padding(top = 8.dp)
                    .fillMaxWidth()
                    .heightIn(min = 56.dp)
                    .clip(RoundedCornerShape(2.dp))
                    .background(GodjiColors.InkBtn)
                    .clickable(role = androidx.compose.ui.semantics.Role.Button) {
                        runCatching { CustomTabsIntent.Builder().build().launchUrl(context, Uri.parse(CABINET_URL)) }
                    }
                    .padding(horizontal = 12.dp, vertical = 8.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    Loc.f.payInCabinet.uppercase(), color = GodjiColors.Surface, fontFamily = MonoFamily,
                    fontWeight = FontWeight.Bold, fontSize = 14.sp, letterSpacing = 0.06.em, textAlign = androidx.compose.ui.text.style.TextAlign.Center
                )
            }
        }
    }
}

/** Подпись секции ("НОВОСТИ", "ПРИГЛАСИ ДРУЗЕЙ"): 11sp semibold, трекинг ~1sp, TextMuted. */
@Composable
private fun SectionLabel(text: String) {
    Box(Modifier.padding(start = 8.dp, top = 8.dp, end = 8.dp)) { SectionLabelText(text) }
}

@Composable
private fun SectionLabelText(text: String) {
    Text(text.uppercase(), color = GodjiColors.TextMuted, fontFamily = MonoFamily, fontSize = 11.sp)
}

@Composable
private fun SectionLabelText(text: AnnotatedString) {
    Text(text, color = GodjiColors.TextMuted, fontFamily = MonoFamily, fontSize = 11.sp)
}

@Composable
private fun PagerCircle(glyph: String, enabled: Boolean, onClick: () -> Unit) {
    Box(
        Modifier
            .size(28.dp)
            .clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp))
            .background(GodjiColors.Chip)
            .border(1.dp, GodjiColors.CardBorder, androidx.compose.foundation.shape.RoundedCornerShape(2.dp))
            .clickable(enabled = enabled, onClick = onClick),
        contentAlignment = Alignment.Center
    ) {
        Text(glyph, color = if (enabled) GodjiColors.TextPrimary else GodjiColors.TextMuted, fontWeight = FontWeight.Bold, fontSize = 14.sp)
    }
}

/** Новость — компактная строка вместо крупной карточки: заголовок semibold + дата вторичным
 *  в одну строку, краткий текст усечён в одну строку. Тап разворачивает полный текст
 *  («Ещё» у списка сохраняется) вместе с кнопками рассылки. */
@Composable
private fun NewsCard(item: NewsUi) {
    val context = LocalContext.current
    var expanded by remember(item.id) { mutableStateOf(false) }
    // Первые два текстовых блока — заголовок и краткое содержание; всё остальное — после тапа.
    val texts = remember(item.rawContent) {
        parseRichContent(item.rawContent).blocks.filterIsInstance<RichBlock.TextBlock>().map { it.parsed.annotated.text }
    }
    val title = texts.firstOrNull()
    val preview = texts.getOrNull(1) ?: title
    Column(
        Modifier
            .fillMaxWidth()
            .godjiCard(RoundedCornerShape(2.dp))
            .clip(RoundedCornerShape(2.dp))
            .clickable { expanded = !expanded }
            .padding(horizontal = 14.dp, vertical = 9.dp),
        verticalArrangement = Arrangement.spacedBy(3.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            if (title != null) {
                Text(
                    title, color = GodjiColors.TextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 13.sp,
                    maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f, fill = false)
                )
            }
            Text(item.dateLabel, color = GodjiColors.TextSecondary, fontWeight = FontWeight.SemiBold, fontSize = 10.5.sp, maxLines = 1)
        }
        if (!expanded && preview != null) {
            Text(
                preview, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 11.5.sp,
                maxLines = 1, overflow = TextOverflow.Ellipsis
            )
        }
        if (expanded) {
            RichContent(raw = item.rawContent, collapsedBlocks = 6, readMoreLabel = Loc.s.plansNewsReadMore)
            item.buttons.forEach { btn ->
                Box(
                    Modifier
                        .height(32.dp)
                        .clip(RoundedCornerShape(2.dp))
                        .background(GodjiColors.Chip)
                        .border(1.dp, GodjiColors.CardBorder, RoundedCornerShape(2.dp))
                        .clickable { context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(btn.url))) }
                        .padding(horizontal = 14.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Text(btn.text, color = GodjiColors.TealDeep, fontWeight = FontWeight.Bold, fontSize = 12.sp)
                }
            }
        }
    }
}

/** "Перенести на другое устройство" — в стиле карточки партнёрской программы: круг с иконкой,
 *  заголовок и пояснение, стрелка. */
@Composable
private fun TransferCard(onClick: () -> Unit) {
    val (interaction, scale) = rememberPressScale()
    Row(
        Modifier
            .fillMaxWidth()
            .scale(scale.value)
            .godjiCard(RoundedCornerShape(2.dp))
            .clickable(interactionSource = interaction, indication = null, onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 11.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Box(Modifier.size(36.dp).clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp)).background(GodjiColors.TealTint), contentAlignment = Alignment.Center) {
            Icon(Icons.Filled.QrCode2, contentDescription = null, tint = GodjiColors.TealDeep, modifier = Modifier.size(18.dp))
        }
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(Loc.f.transferTitle, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 13.5.sp)
            Text(Loc.f.transferDesc, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 11.sp, lineHeight = 15.4.sp)
        }
        Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, contentDescription = null, tint = GodjiColors.TextSecondary, modifier = Modifier.size(16.dp))
    }
}

/** Приглашения: ссылка-капсула с кнопкой "Копировать", три счётчика через разделители, список
 *  приглашённых с инициалом и статусом. Нажатие на саму ссылку — системное "Поделиться". */
@Composable
private fun ReferralCard(referral: ReferralUi, onCopy: () -> Unit, onShare: () -> Unit, onQr: () -> Unit) {
    Column(
        Modifier.fillMaxWidth().godjiCard().padding(horizontal = 14.dp, vertical = 11.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Row(
            Modifier
                .fillMaxWidth()
                .height(44.dp)
                .clip(RoundedCornerShape(2.dp))
                .background(GodjiColors.Chip)
                .border(1.dp, GodjiColors.CardBorder, RoundedCornerShape(2.dp))
                .padding(start = 14.dp, end = 6.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                referral.link, color = GodjiColors.TextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 12.5.sp,
                maxLines = 1, overflow = TextOverflow.Ellipsis,
                modifier = Modifier.weight(1f).clickable(onClick = onShare)
            )
            // QR-код ссылки — показать другу с экрана.
            val (qrInteraction, qrScale) = rememberPressScale()
            Box(
                Modifier
                    .size(32.dp)
                    .scale(qrScale.value)
                    .clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp))
                    .background(GodjiColors.Thumb)
                    .border(1.dp, GodjiColors.CardBorder, androidx.compose.foundation.shape.RoundedCornerShape(2.dp))
                    .clickable(interactionSource = qrInteraction, indication = null, onClick = onQr),
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Filled.QrCode2, contentDescription = Loc.f.referralQrTitle, tint = GodjiColors.TealDeep, modifier = Modifier.size(17.dp))
            }
            val (copyInteraction, copyScale) = rememberPressScale()
            Box(
                Modifier
                    .height(32.dp)
                    .scale(copyScale.value)
                    .clip(RoundedCornerShape(2.dp))
                    .background(Brush.verticalGradient(0f to GodjiColors.AccentGradTop, 0.55f to GodjiColors.AccentGradMid, 1f to GodjiColors.AccentGradBottom))
                    .clickable(interactionSource = copyInteraction, indication = null, onClick = onCopy)
                    .padding(horizontal = 12.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(Loc.s.plansReferralCopyShort, color = GodjiColors.Surface, fontWeight = FontWeight.Bold, fontSize = 11.5.sp)
            }
        }
        Row(Modifier.fillMaxWidth().height(IntrinsicSize.Min)) {
            ReferralStat(Loc.s.plansReferralInvited, "${referral.totalReferrals}", GodjiColors.TextPrimary, Modifier.weight(1f))
            Box(Modifier.width(1.dp).fillMaxHeight().background(GodjiColors.Hair))
            ReferralStat(Loc.s.plansReferralActive, "${referral.activeReferrals}", GodjiColors.TextPrimary, Modifier.weight(1f))
            Box(Modifier.width(1.dp).fillMaxHeight().background(GodjiColors.Hair))
            ReferralStat(Loc.s.plansReferralBonusDays, "+${referral.totalBonusDays}", GodjiColors.TealDeep, Modifier.weight(1f))
        }
        Column {
            HorizontalDivider(thickness = 1.dp, color = GodjiColors.Hair)
            Spacer(Modifier.height(4.dp))
            if (referral.entries.isEmpty()) {
                Text(Loc.s.plansReferralEmpty, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 12.sp, modifier = Modifier.padding(vertical = 7.dp))
            }
            referral.entries.forEach { e -> ReferralRow(e) }
        }
    }
}

@Composable
private fun ReferralStat(label: String, value: String, valueColor: Color, modifier: Modifier) {
    Column(modifier, horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(2.dp)) {
        Text(value, color = valueColor, fontWeight = FontWeight.Bold, fontSize = 20.sp)
        Text(label, color = GodjiColors.TextSecondary, fontWeight = FontWeight.SemiBold, fontSize = 10.5.sp)
    }
}

@Composable
private fun ReferralRow(e: ReferralEntryUi) {
    Row(
        Modifier.fillMaxWidth().padding(vertical = 7.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Box(
            Modifier.size(28.dp).clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp)).background(GodjiColors.Chip).border(1.dp, GodjiColors.CardBorder, androidx.compose.foundation.shape.RoundedCornerShape(2.dp)),
            contentAlignment = Alignment.Center
        ) {
            Text(e.displayName.take(1).uppercase(), color = GodjiColors.TextSecondary, fontWeight = FontWeight.Bold, fontSize = 11.sp)
        }
        Text(e.displayName, color = GodjiColors.TextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 12.5.sp, maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f))
        if (e.bonusDays > 0) {
            Text(Loc.s.plansReferralBonusSuffix(e.bonusDays), color = GodjiColors.TealDeep, fontWeight = FontWeight.Bold, fontSize = 10.5.sp)
        }
        // Статус: активен — okBg + сплошная обводка accent + точка; иначе — пунктир цвета sub.
        val active = e.isActive
        val line = if (active) GodjiColors.Teal else GodjiColors.TextSecondary
        Row(
            Modifier
                .height(22.dp)
                .clip(RoundedCornerShape(2.dp))
                .background(if (active) GodjiColors.TealTint else Color.Transparent)
                .drawWithContent {
                    drawContent()
                    val sw = 1.dp.toPx()
                    drawRoundRect(
                        color = line,
                        topLeft = Offset(sw / 2, sw / 2),
                        size = androidx.compose.ui.geometry.Size(size.width - sw, size.height - sw),
                        cornerRadius = androidx.compose.ui.geometry.CornerRadius(size.height / 2),
                        style = Stroke(sw, pathEffect = if (active) null else PathEffect.dashPathEffect(floatArrayOf(3.dp.toPx(), 3.dp.toPx())))
                    )
                }
                .padding(start = 7.dp, end = 9.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(5.dp)
        ) {
            Box(Modifier.size(6.dp).clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp)).background(if (active) GodjiColors.Teal else Color.Transparent))
            Text(
                if (active) Loc.s.plansReferralActiveBadge else Loc.s.plansReferralInactiveBadge,
                color = if (active) GodjiColors.TealDeep else GodjiColors.TextSecondary,
                fontWeight = FontWeight.ExtraBold, fontSize = 10.sp
            )
        }
    }
}

/** Устройства подписки — в эталоне этого блока нет (там нет данных), но это рабочая функция
 *  приложения; оформлено тем же языком: подпись секции + карточка со строками через hair. */
@Composable
private fun DevicesSection(
    devices: List<DeviceUi>,
    deviceLimit: Int,
    deleteSupportOnly: Boolean,
    onRename: (String, String) -> Unit,
    onDelete: (String) -> Unit,
    onOpenSupport: () -> Unit
) {
    var renameTarget by remember { mutableStateOf<DeviceUi?>(null) }
    var deleteTarget by remember { mutableStateOf<DeviceUi?>(null) }

    // Заголовок секции устройств: счётчик «N из M» рядом; на пределе лимита — цветом warning.
    if (deviceLimit > 0) {
        Box(Modifier.padding(start = 8.dp, top = 8.dp, end = 8.dp)) {
            SectionLabelText(buildAnnotatedString {
                append(Loc.s.plansDevicesTitle.uppercase() + " · ")
                withStyle(SpanStyle(color = if (devices.size >= deviceLimit) GodjiColors.Warning else GodjiColors.TextMuted)) {
                    append(Loc.s.plansDevicesCountLabel(devices.size, deviceLimit).uppercase())
                }
            })
        }
    } else {
        SectionLabel(Loc.s.plansDevicesTitle)
    }
    Column(Modifier.fillMaxWidth().godjiCard().clip(RoundedCornerShape(2.dp))) {
        if (devices.isEmpty()) {
            Text(Loc.s.plansDevicesEmpty, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 12.sp, modifier = Modifier.padding(horizontal = 14.dp, vertical = 11.dp))
        }
        devices.forEachIndexed { index, device ->
            if (index > 0) HorizontalDivider(thickness = 1.dp, color = GodjiColors.Hair)
            Row(Modifier.fillMaxWidth().padding(start = 14.dp, end = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f).padding(vertical = 10.dp), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text(device.name, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 13.5.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    val subtitle = listOfNotNull(device.details.takeIf { it.isNotEmpty() }, device.createdAtLabel).joinToString(" · ")
                    if (subtitle.isNotEmpty()) {
                        Text(subtitle, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 11.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    }
                }
                if (device.busy) {
                    Box(Modifier.size(48.dp), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(Modifier.size(16.dp), strokeWidth = 2.dp, color = GodjiColors.Teal, trackColor = GodjiColors.TrackBg)
                    }
                } else {
                    // Зона нажатия 48dp (норма Material), значок прежнего размера.
                    var menu by remember { mutableStateOf(false) }
                    Box {
                        Box(
                            Modifier.size(48.dp).clickable(role = Role.Button) { menu = true },
                            contentAlignment = Alignment.Center
                        ) { Text("···", color = GodjiColors.TextMuted, fontSize = 18.sp, letterSpacing = 2.sp) }
                        androidx.compose.material3.DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
                            androidx.compose.material3.DropdownMenuItem(
                                text = { Text(Loc.s.plansDevicesRenameTitle) },
                                onClick = { menu = false; renameTarget = device }
                            )
                            androidx.compose.material3.DropdownMenuItem(
                                text = { Text(Loc.s.plansDevicesDelete, color = GodjiColors.Danger) },
                                onClick = { menu = false; deleteTarget = device }
                            )
                        }
                    }
                }
            }
        }
    }

    renameTarget?.let { device ->
        var name by remember(device.hwid) { mutableStateOf(device.name) }
        AlertDialog(
            onDismissRequest = { renameTarget = null },
            title = { Text(Loc.s.plansDevicesRenameTitle) },
            text = {
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text(Loc.s.plansDevicesRenameLabel) },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
            },
            confirmButton = { TextButton(onClick = { onRename(device.hwid, name); renameTarget = null }) { Text(Loc.s.plansDevicesSave) } },
            dismissButton = { TextButton(onClick = { renameTarget = null }) { Text(Loc.s.plansDevicesCancel) } }
        )
    }

    deleteTarget?.let { device ->
        AlertDialog(
            onDismissRequest = { deleteTarget = null },
            title = { Text(Loc.s.plansDevicesDeleteTitle) },
            text = { Text(if (deleteSupportOnly) Loc.s.plansDevicesDeleteSupportOnly else Loc.s.plansDevicesDeleteConfirm) },
            confirmButton = {
                if (deleteSupportOnly) {
                    TextButton(onClick = { onOpenSupport(); deleteTarget = null }) { Text(Loc.s.plansDevicesContactSupport) }
                } else {
                    TextButton(onClick = { onDelete(device.hwid); deleteTarget = null }) { Text(Loc.s.plansDevicesDelete, color = GodjiColors.Danger) }
                }
            },
            dismissButton = { TextButton(onClick = { deleteTarget = null }) { Text(Loc.s.plansDevicesCancel) } }
        )
    }
}

@Composable
private fun DeviceAction(icon: androidx.compose.ui.graphics.vector.ImageVector, label: String, tint: Color, onClick: () -> Unit) {
    Box(
        Modifier.size(48.dp).clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp)).clickable(role = Role.Button, onClick = onClick),
        contentAlignment = Alignment.Center
    ) {
        Icon(icon, contentDescription = label, tint = tint, modifier = Modifier.size(17.dp))
    }
}

/** Кольцо дней: остаток от оплаченного периода — после продления полное и каждый день
 *  убывает (см. util/SubscriptionPeriod). Ядро RingCore 54dp. */
@Composable
private fun DaysRing(days: Int, totalDays: Int) {
    val pct = (days.toFloat() / totalDays.coerceAtLeast(1)).coerceIn(0f, 1f)
    Box(Modifier.size(68.dp), contentAlignment = Alignment.Center) {
        Canvas(Modifier.fillMaxSize()) {
            val w = 7.dp.toPx()
            val inset = w / 2
            val arcSize = androidx.compose.ui.geometry.Size(size.width - w, size.height - w)
            drawArc(GodjiColors.TrackBg, -90f, 360f, false, Offset(inset, inset), arcSize, style = Stroke(w))
            drawArc(GodjiColors.Teal, -90f, 360f * pct, false, Offset(inset, inset), arcSize, style = Stroke(w))
        }
        // Круг фиксированного размера — размеры текста в dp, чтобы крупный шрифт не обрезал «ДНЕЙ».
        val density = androidx.compose.ui.platform.LocalDensity.current
        val numberSize = with(density) { 19.dp.toSp() }
        val labelSize = with(density) { 7.5.dp.toSp() }
        Box(Modifier.size(54.dp).clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp)).background(GodjiColors.RingCore), contentAlignment = Alignment.Center) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Text("$days", color = GodjiColors.TextPrimary, fontWeight = FontWeight.ExtraBold, fontSize = numberSize, lineHeight = numberSize, maxLines = 1)
                Text(Loc.s.plansDays, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Bold, fontSize = labelSize, lineHeight = labelSize, letterSpacing = 0.6.sp, maxLines = 1)
            }
        }
    }
}
