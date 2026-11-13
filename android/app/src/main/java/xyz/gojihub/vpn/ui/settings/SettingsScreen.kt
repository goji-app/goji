package xyz.gojihub.vpn.ui.settings

import androidx.compose.ui.semantics.Role
import android.content.Intent
import android.provider.Settings
import xyz.gojihub.vpn.BuildConfig
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
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
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.PriorityHigh
import xyz.gojihub.vpn.network.NetworkDiagnostics
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import xyz.gojihub.vpn.i18n.AppLanguage
import xyz.gojihub.vpn.i18n.Loc
import xyz.gojihub.vpn.settings.PingMethod
import xyz.gojihub.vpn.ui.theme.FontSizePreset
import xyz.gojihub.vpn.ui.theme.isWinterSeason
import xyz.gojihub.vpn.ui.theme.NewBadge
import xyz.gojihub.vpn.ui.theme.ThemeMode
import xyz.gojihub.vpn.ui.theme.Palettes
import xyz.gojihub.vpn.ui.theme.light
import xyz.gojihub.vpn.ui.theme.dark
import xyz.gojihub.vpn.ui.theme.localName
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.selected
import xyz.gojihub.vpn.ui.theme.GodjiColors
import xyz.gojihub.vpn.ui.util.BackButton
import xyz.gojihub.vpn.ui.theme.SpaceGroteskFamily
import xyz.gojihub.vpn.ui.theme.MonoFamily
import androidx.compose.ui.unit.em
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import xyz.gojihub.vpn.ui.theme.godjiCard
import xyz.gojihub.vpn.ui.util.LogViewerDialog
import xyz.gojihub.vpn.ui.util.RichContent
import xyz.gojihub.vpn.ui.util.rememberPressScale
import xyz.gojihub.vpn.util.LogCategory

/** Подсветка строки при нажатии — style-active="background:rgba(127,127,127,.08)" в эталоне. */
private val PressedRowBg = Color(0x147F7F7F)

@Composable
fun SettingsScreen(
    onLoggedOut: () -> Unit,
    onOpenPingSettings: () -> Unit,
    onOpenLogLevel: () -> Unit,
    onOpenAppTunneling: () -> Unit,
    onOpenSupport: () -> Unit,
    onOpenNetworkRules: () -> Unit = {},
    onOpenBypassDomains: () -> Unit = {},
    onOpenJournal: () -> Unit = {},
    viewModel: SettingsViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsState()
    val context = LocalContext.current
    var logDialog by remember { mutableStateOf<Pair<LogCategory, String>?>(null) }
    logDialog?.let { (category, title) ->
        LogViewerDialog(category = category, title = title, onDismiss = { logDialog = null })
    }

    // Как в Windows-клиенте: главный экран — карточка со ссылками на разделы, каждый раздел
    // открывается своим окном (стрелка «назад» и системный «назад» возвращают к списку).
    var page by androidx.compose.runtime.saveable.rememberSaveable { mutableStateOf(SettingsPage.MAIN) }
    androidx.activity.compose.BackHandler(enabled = page != SettingsPage.MAIN) { page = SettingsPage.MAIN }

    Column(
        Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(start = 20.dp, top = 24.dp, end = 20.dp, bottom = 12.dp),
        verticalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        if (page == SettingsPage.MAIN) {
            Text(
                Loc.s.settingsTitle, color = GodjiColors.TextPrimary, fontFamily = SpaceGroteskFamily,
                fontWeight = FontWeight.SemiBold, fontSize = 30.sp, lineHeight = 32.sp, letterSpacing = (-0.02).em,
                modifier = Modifier.padding(bottom = 10.dp)
            )
            RefCard {
                LinkRow(Loc.s.settingsAppearance, Loc.f.setAppearanceDesc) { page = SettingsPage.APPEARANCE }
                Hair()
                LinkRow(Loc.s.settingsConnection, Loc.f.setConnectionDesc) { page = SettingsPage.CONNECTION }
                Hair()
                LinkRow(Loc.f.securitySection, Loc.f.setSecurityDesc) { page = SettingsPage.SECURITY }
                Hair()
                LinkRow(Loc.s.settingsNotifications, Loc.f.setNotificationsDesc) { page = SettingsPage.NOTIFICATIONS }
                Hair()
                LinkRow(Loc.s.settingsAbout, Loc.f.setAboutDesc) { page = SettingsPage.ABOUT }
            }
            Spacer(Modifier.height(4.dp))
            RefCard {
                LinkRow(Loc.s.support.settingsSupportLink, Loc.s.support.settingsSupportLinkDesc, onOpenSupport)
            }
            Spacer(Modifier.height(4.dp))
            var confirmLogout by androidx.compose.runtime.saveable.rememberSaveable { mutableStateOf(false) }
            val (outInteraction, outScale) = rememberPressScale()
            Box(
                Modifier
                    .fillMaxWidth()
                    .height(56.dp)
                    .scale(outScale.value)
                    .border(1.dp, GodjiColors.OutlineVariant, RoundedCornerShape(2.dp))
                    .clip(RoundedCornerShape(2.dp))
                    .clickable(interactionSource = outInteraction, indication = null, role = Role.Button) {
                        confirmLogout = true
                    },
                contentAlignment = Alignment.Center
            ) {
                Text(Loc.s.settingsLogout.uppercase(), color = GodjiColors.Danger, fontFamily = MonoFamily, fontWeight = FontWeight.Bold, fontSize = 14.sp, letterSpacing = 0.06.em)
            }
            if (confirmLogout) {
                androidx.compose.material3.AlertDialog(
                    onDismissRequest = { confirmLogout = false },
                    title = { Text(Loc.f.logoutConfirmTitle, fontWeight = FontWeight.Bold) },
                    text = { Text(Loc.f.logoutConfirmText, fontSize = 13.5.sp, lineHeight = 19.sp) },
                    confirmButton = {
                        androidx.compose.material3.TextButton(onClick = { confirmLogout = false; viewModel.logout(); onLoggedOut() }) {
                            Text(Loc.f.logoutConfirm, color = GodjiColors.Danger, fontWeight = FontWeight.Bold)
                        }
                    },
                    dismissButton = {
                        androidx.compose.material3.TextButton(onClick = { confirmLogout = false }) { Text(Loc.s.plansDevicesCancel) }
                    }
                )
            }
            // Удаление аккаунта — Google Play требует путь к нему внутри приложения.
            var confirmDelete by androidx.compose.runtime.saveable.rememberSaveable { mutableStateOf(false) }
            LaunchedEffect(Unit) {
                viewModel.accountDeleted.collect {
                    confirmDelete = false
                    android.widget.Toast.makeText(context, Loc.f.deleteAccountSent, android.widget.Toast.LENGTH_LONG).show()
                    onLoggedOut()
                }
            }
            Text(
                Loc.f.deleteAccount, color = GodjiColors.TextSecondary, fontWeight = FontWeight.SemiBold, fontSize = 12.5.sp,
                textAlign = androidx.compose.ui.text.style.TextAlign.Center,
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(2.dp))
                    .clickable { viewModel.clearDeleteAccountError(); confirmDelete = true }
                    .padding(vertical = 10.dp)
            )
            if (confirmDelete || state.deletingAccount) {
                androidx.compose.material3.AlertDialog(
                    onDismissRequest = { if (!state.deletingAccount) { confirmDelete = false; viewModel.clearDeleteAccountError() } },
                    title = { Text(Loc.f.deleteAccountTitle, fontWeight = FontWeight.Bold) },
                    text = {
                        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text(Loc.f.deleteAccountText, fontSize = 13.5.sp, lineHeight = 19.sp)
                            state.deleteAccountError?.let { Text(it, color = GodjiColors.Danger, fontSize = 12.5.sp) }
                        }
                    },
                    confirmButton = {
                        androidx.compose.material3.TextButton(
                            enabled = !state.deletingAccount,
                            onClick = { viewModel.requestAccountDeletion() }
                        ) {
                            if (state.deletingAccount) {
                                androidx.compose.material3.CircularProgressIndicator(Modifier.size(16.dp), strokeWidth = 2.dp, color = GodjiColors.Danger)
                            } else {
                                Text(Loc.f.deleteAccountConfirm, color = GodjiColors.Danger, fontWeight = FontWeight.Bold)
                            }
                        }
                    },
                    dismissButton = {
                        androidx.compose.material3.TextButton(
                            enabled = !state.deletingAccount,
                            onClick = { confirmDelete = false; viewModel.clearDeleteAccountError() }
                        ) { Text(Loc.f.deleteAccountCancel) }
                    }
                )
            }
            return@Column
        }

        Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(bottom = 6.dp)) {
            BackButton(onClick = { page = SettingsPage.MAIN })
            Text(
                when (page) {
                    SettingsPage.APPEARANCE -> Loc.s.settingsAppearance
                    SettingsPage.CONNECTION -> Loc.s.settingsConnection
                    SettingsPage.SECURITY -> Loc.f.securitySection
                    SettingsPage.NOTIFICATIONS -> Loc.s.settingsNotifications
                    SettingsPage.ABOUT -> Loc.s.settingsAbout
                    SettingsPage.MAIN -> ""
                },
                color = GodjiColors.TextPrimary, fontFamily = SpaceGroteskFamily, fontWeight = FontWeight.SemiBold, fontSize = 30.sp
            )
        }

        when (page) {
            SettingsPage.APPEARANCE -> RefCard {
                // Тема — три состояния (Светлая/Тёмная/Системная), тем же сегмент-контролом, что и язык.
                SegmentedRow(
                    title = Loc.s.settingsDarkTheme, subtitle = null,
                    options = ThemeMode.entries.map {
                        when (it) {
                            ThemeMode.LIGHT -> Loc.s.themeModeLight
                            ThemeMode.DARK -> Loc.s.themeModeDark
                            ThemeMode.SYSTEM -> Loc.s.themeModeSystem
                        }
                    },
                    selectedIndex = ThemeMode.entries.indexOf(state.themeMode),
                    onSelect = { viewModel.setThemeMode(ThemeMode.entries[it]) }
                )
                Hair()
                PaletteGrid(state.paletteId, viewModel::setPalette)
                Hair()
                SegmentedRow(
                    title = Loc.s.settingsLanguage, subtitle = Loc.s.settingsLanguageDesc,
                    options = AppLanguage.entries.map { it.displayName },
                    selectedIndex = AppLanguage.entries.indexOf(state.language),
                    onSelect = { viewModel.setLanguage(AppLanguage.entries[it]) }
                )
                Hair()
                SegmentedRow(
                    title = Loc.s.settingsFontSize, subtitle = null,
                    options = FontSizePreset.entries.map {
                        when (it) {
                            FontSizePreset.SMALL -> Loc.s.fontSizeSmall
                            FontSizePreset.NORMAL -> Loc.s.fontSizeNormal
                            FontSizePreset.LARGE -> Loc.s.fontSizeLarge
                        }
                    },
                    selectedIndex = FontSizePreset.entries.indexOf(state.fontSize),
                    onSelect = { viewModel.setFontSize(FontSizePreset.entries[it]) }
                )
                if (isWinterSeason()) {
                    Hair()
                    ToggleRow(Loc.f.winterDecorTitle, Loc.f.winterDecorDesc, state.winterDecor, viewModel::setWinterDecor)
                }
            }

            SettingsPage.CONNECTION -> {
                RefCard {
                    LinkRow(Loc.f.journalTitle, Loc.f.journalLinkDesc, onOpenJournal)
                    Hair()
                    LinkRow(Loc.f.netRulesTitle, Loc.f.netRulesLinkDesc, onOpenNetworkRules)
                    Hair()
                    LinkRow(Loc.s.settingsPingLink, Loc.s.settingsPingLinkDesc, onOpenPingSettings)
                    Hair()
                    LinkRow(Loc.s.settingsAppTunneling, Loc.s.settingsAppTunnelingDesc, onOpenAppTunneling)
                    Hair()
                    LinkRow(Loc.f.bypassTitle, Loc.f.bypassLinkDesc, onOpenBypassDomains)
                }
                Spacer(Modifier.height(4.dp))
                RefCard {
                    ToggleRow(Loc.s.settingsKillSwitch, Loc.s.settingsKillSwitchDesc, state.killSwitch, viewModel::setKillSwitch)
                    Hair()
                    // Программно включить Always-on VPN нельзя — ограничение Android; открываем
                    // системный экран, где пользователь включает это сам.
                    LinkRow(Loc.s.settingsAlwaysOnTitle, Loc.s.settingsAlwaysOnDesc) {
                        runCatching { context.startActivity(Intent(Settings.ACTION_VPN_SETTINGS)) }
                    }
                }
            }

            SettingsPage.SECURITY -> LeakCheckCard(state, onCheck = viewModel::checkLeak)

            SettingsPage.NOTIFICATIONS -> RefCard {
                ToggleRow(Loc.s.settingsPinNotif, Loc.s.settingsPinNotifDesc, state.pinNotification, viewModel::setPinNotification)
                Hair()
                ToggleRow(Loc.f.hapticsTitle, Loc.f.hapticsDesc, state.haptics, viewModel::setHaptics)
            }

            SettingsPage.ABOUT -> {
                LaunchedEffect(Unit) { viewModel.refreshSubscriptionId() }
                SectionLabel(Loc.s.settingsAboutCopyHint)
                RefCard {
                    AboutRow(Loc.s.settingsAppVersion, state.appVersion)
                    Hair()
                    AboutRow(Loc.s.settingsXrayVersion, state.xrayVersion)
                    Hair()
                    state.subscriptionId?.let {
                        AboutRow(Loc.f.aboutSubscriptionId, it)
                        Hair()
                    }
                    AboutRow(Loc.s.settingsHwid, state.hwid)
                    Hair()
                    AboutRow(Loc.s.settingsDeviceInfo, state.deviceInfo)
                }
                // Обновления — здесь же, рядом с версией (отдельный экран был из одной строки).
                // Самообновление есть только в сборке site (см. app/build.gradle.kts).
                if (BuildConfig.ENABLE_SELF_UPDATE) {
                    SectionLabel(Loc.s.settingsUpdatesTitle)
                    Column(
                        Modifier
                            .fillMaxWidth()
                            .godjiCard(RoundedCornerShape(2.dp))
                            .padding(horizontal = 14.dp, vertical = 13.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        UpdateSectionContent(state, viewModel)
                    }
                }
                SectionLabel(Loc.s.settingsLogs)
                RefCard {
                    LinkRow(Loc.s.settingsLogLevel, Loc.s.settingsLogLevelDesc, onOpenLogLevel)
                    listOf(
                        LogCategory.MAIN to Loc.s.logMain,
                        LogCategory.CORE to Loc.s.logCore,
                        LogCategory.SUBSCRIPTION to Loc.s.logSubscription,
                        LogCategory.SERVICE to Loc.s.logService,
                        LogCategory.PUSH to Loc.s.logPush,
                    ).forEach { (category, title) ->
                        Hair()
                        LogRow(title) { logDialog = category to title }
                    }
                }
            }

            SettingsPage.MAIN -> {}
        }
    }
}

/** Окна настроек (как разделы Windows-клиента). */
private enum class SettingsPage { MAIN, APPEARANCE, CONNECTION, SECURITY, NOTIFICATIONS, ABOUT }

/** Проверка утечек: заголовок + кнопка, после проверки — вердикт (круг 28dp с иконкой, как у
 *  баннеров Главной) и строки деталей в стиле "О программе". */
@Composable
private fun LeakCheckCard(state: SettingsUiState, onCheck: () -> Unit) {
    val report = state.leakReport
    Column(
        Modifier
            .fillMaxWidth()
            .godjiCard(RoundedCornerShape(2.dp))
            .padding(horizontal = 14.dp, vertical = 13.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            RowTexts(Loc.f.leakTitle, Loc.f.leakDesc, Modifier.weight(1f))
            val interaction = remember { MutableInteractionSource() }
            val pressed by interaction.collectIsPressedAsState()
            Box(
                Modifier
                    .height(32.dp)
                    .scale(if (pressed) 0.95f else 1f)
                    .clip(RoundedCornerShape(2.dp))
                    .background(GodjiColors.Chip)
                    .border(1.dp, GodjiColors.CardBorder, RoundedCornerShape(2.dp))
                    .clickable(interactionSource = interaction, indication = null, enabled = !state.leakChecking, onClick = onCheck)
                    .padding(horizontal = 14.dp),
                contentAlignment = Alignment.Center
            ) {
                if (state.leakChecking) {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(7.dp)) {
                        CircularProgressIndicator(Modifier.size(13.dp), strokeWidth = 2.dp, color = GodjiColors.Teal, trackColor = GodjiColors.TrackBg)
                        Text(Loc.f.leakChecking, color = GodjiColors.TealDeep, fontWeight = FontWeight.Bold, fontSize = 12.sp)
                    }
                } else {
                    Text(Loc.f.leakButton, color = GodjiColors.TealDeep, fontWeight = FontWeight.Bold, fontSize = 12.sp)
                }
            }
        }
        if (report == null && !state.leakChecking) {
            Text(
                when (Loc.lang) {
                    xyz.gojihub.vpn.i18n.AppLanguage.RU -> "Покажем, какой IP видят сайты и чей DNS отвечает. Если они совпадают с данными вашего провайдера — защита протекает."
                    xyz.gojihub.vpn.i18n.AppLanguage.EN -> "We show which IP sites see and whose DNS answers. If they match your provider, protection is leaking."
                    xyz.gojihub.vpn.i18n.AppLanguage.ZH -> "我们会显示网站看到的 IP 和响应的 DNS。若与运营商一致，说明保护存在泄漏。"
                },
                color = GodjiColors.TextSecondary, fontSize = 12.sp, lineHeight = 17.sp
            )
        }
        if (report != null && !state.leakChecking) {
            val ok = report.verdict == NetworkDiagnostics.Verdict.SAFE
            val (title, desc) = when (report.verdict) {
                NetworkDiagnostics.Verdict.SAFE -> Loc.f.leakSafe to Loc.f.leakSafeDesc
                NetworkDiagnostics.Verdict.LEAK -> Loc.f.leakFound to listOfNotNull(
                    Loc.f.leakIpProblem.takeIf { report.ipLeak },
                    (if (report.dnsViaIsp) Loc.f.leakDnsProblem(report.dns?.isp) else Loc.f.leakDnsLocalProblem(report.dns?.isp))
                        .takeIf { report.dnsLeak }
                ).joinToString(" ")
                NetworkDiagnostics.Verdict.VPN_OFF -> Loc.f.leakVpnOff to Loc.f.leakVpnOffDesc
                NetworkDiagnostics.Verdict.ERROR -> Loc.f.leakError to Loc.f.leakErrorDesc
            }
            Row(
                Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(2.dp))
                    .background(if (ok) GodjiColors.TealTint else GodjiColors.TerracottaTint)
                    .padding(horizontal = 12.dp, vertical = 9.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(11.dp)
            ) {
                Box(
                    Modifier.size(28.dp).clip(androidx.compose.foundation.shape.RoundedCornerShape(2.dp)).background(if (ok) GodjiColors.Teal else GodjiColors.Terracotta),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(if (ok) Icons.Filled.Check else Icons.Filled.PriorityHigh, contentDescription = null, tint = GodjiColors.Surface, modifier = Modifier.size(15.dp))
                }
                Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    Text(title, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    Text(desc, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 11.5.sp, lineHeight = 16.sp)
                }
            }
            report.vpnIp?.let { LeakRow(Loc.f.leakRowSiteIp, listOfNotNull(it.ip, it.country).joinToString(" · ")) }
            report.realIp?.let { LeakRow(Loc.f.leakRowRealIp, listOfNotNull(it.ip, it.country).joinToString(" · ")) }
            report.dns?.let { LeakRow(Loc.f.leakRowDns, listOfNotNull(it.isp, it.country).joinToString(" · ").ifBlank { it.ip }) }
        }
    }
}

@Composable
private fun LeakRow(label: String, value: String) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(label, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 12.5.sp)
        Text(
            value, color = GodjiColors.TextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 12.5.sp,
            modifier = Modifier.weight(1f), textAlign = TextAlign.End, maxLines = 1, overflow = TextOverflow.Ellipsis
        )
    }
}

/** Подпись раздела эталона: 11px/600, трекинг ~1sp, padding 8 8 0 (плюс общий gap 8 колонки). */
@Composable
internal fun SectionLabel(title: String) {
    Text(
        title.uppercase(), color = GodjiColors.TextMuted, fontFamily = MonoFamily,
        fontSize = 11.sp,
        modifier = Modifier.padding(start = 8.dp, top = 8.dp, end = 8.dp)
    )
}

/** Стеклянная карточка раздела: радиус 24, без внутренних отступов, overflow hidden. */
@Composable
internal fun RefCard(content: @Composable ColumnScope.() -> Unit) {
    val lineColor = GodjiColors.OutlineVariant
    Column(
        Modifier
            .fillMaxWidth()
            .drawBehind {
                drawLine(lineColor, Offset.Zero, Offset(size.width, 0f), 1.dp.toPx())
                drawLine(lineColor, Offset(0f, size.height), Offset(size.width, size.height), 1.dp.toPx())
            },
        content = content
    )
}

@Composable
// M3 Expressive (настройки Android 16): строки группы разделены зазором цвета фона — карточка
// читается как стопка отдельных плашек.
internal fun Hair() = HorizontalDivider(thickness = 1.dp, color = GodjiColors.OutlineVariant)

/** Подпись секции + карточка с внутренним padding 16 — для подэкранов (пинг, логирование). */
@Composable
fun SettingsSection(title: String, content: @Composable ColumnScope.() -> Unit) {
    Column {
        Text(
            title.uppercase(), color = GodjiColors.TextMuted, fontFamily = MonoFamily,
            fontSize = 11.sp,
            modifier = Modifier.padding(start = 8.dp, bottom = 8.dp)
        )
        Column(
            Modifier
                .fillMaxWidth()
                .padding(vertical = 8.dp)
        ) {
            content()
        }
    }
}

@Composable
internal fun RowTexts(title: String, subtitle: String?, modifier: Modifier = Modifier) {
    Column(modifier, verticalArrangement = Arrangement.spacedBy(2.dp)) {
        Text(title, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Medium, fontSize = 15.sp)
        subtitle?.let { Text(it, color = GodjiColors.TextSecondary, fontSize = 12.sp, lineHeight = 16.sp) }
    }
}

/** Кликабельная строка с подсветкой при нажатии, как кнопки-строки эталона. */
@Composable
internal fun pressableRow(onClick: () -> Unit): Modifier {
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    return Modifier
        .fillMaxWidth()
        .background(if (pressed) PressedRowBg else Color.Transparent)
        .clickable(interactionSource = interaction, indication = null, onClick = onClick)
}

@Composable
internal fun ToggleRow(title: String, subtitle: String?, checked: Boolean, onCheckedChange: (Boolean) -> Unit) {
    Row(
        Modifier
            .fillMaxWidth()
            .clickable(interactionSource = remember { MutableInteractionSource() }, indication = null) { onCheckedChange(!checked) }
            .padding(vertical = 13.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        RowTexts(title, subtitle, Modifier.weight(1f))
        GlassSwitch(checked) { onCheckedChange(!checked) }
    }
}

/** Переключатель эталона: трек 54×32 (accent / track), бегунок 36×26, left 3 → 15,
 *  пружина cubic-bezier(.3,1.5,.5,1). */
@Composable
internal fun GlassSwitch(checked: Boolean, onToggle: () -> Unit) {
    val knobX by animateDpAsState(if (checked) 23.dp else 3.dp, tween(150), label = "swKnob")
    val shape = RoundedCornerShape(2.dp)
    Box(
        Modifier
            .size(width = 44.dp, height = 24.dp)
            .background(if (checked) GodjiColors.InkBtn else Color.Transparent, shape)
            .border(1.dp, if (checked) GodjiColors.InkBtn else GodjiColors.OutlineVariant, shape)
            .clickable(interactionSource = remember { MutableInteractionSource() }, indication = null, role = Role.Switch, onClick = onToggle)
    ) {
        Box(
            Modifier
                .offset(x = knobX, y = 3.dp)
                .size(16.dp)
                .background(if (checked) GodjiColors.Surface else GodjiColors.TextMuted)
        )
    }
}

@Composable
internal fun LinkRow(title: String, subtitle: String?, onClick: () -> Unit) {
    Row(
        pressableRow(onClick).padding(vertical = 13.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        RowTexts(title, subtitle, Modifier.weight(1f))
        Text("›", color = GodjiColors.TextMuted, fontSize = 18.sp)
    }
}

/** Заголовок (+ подпись) и сегмент-контрол под ним — блок "Язык приложения" эталона:
 *  gap 9, дорожка padding 3 на chip + stroke, бегунок thumb + stroke, кнопки 34px, 12.5px/700. */
@Composable
private fun SegmentedRow(title: String, subtitle: String?, options: List<String>, selectedIndex: Int, onSelect: (Int) -> Unit) {
    Column(
        Modifier.fillMaxWidth().padding(vertical = 13.dp),
        verticalArrangement = Arrangement.spacedBy(9.dp)
    ) {
        RowTexts(title, subtitle)
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            options.forEachIndexed { i, label ->
                val on = i == selectedIndex
                Text(
                    label.uppercase(),
                    color = if (on) GodjiColors.SurfaceBase else GodjiColors.TextSecondary,
                    fontFamily = MonoFamily, fontSize = 11.sp, maxLines = 1, overflow = TextOverflow.Ellipsis,
                    modifier = Modifier
                        .heightIn(min = 36.dp)
                        .then(if (on) Modifier.background(GodjiColors.Ink) else Modifier.border(1.dp, GodjiColors.OutlineVariant))
                        .clickable(role = Role.RadioButton) { onSelect(i) }
                        .padding(horizontal = 12.dp, vertical = 10.dp)
                        .wrapContentHeight(Alignment.CenterVertically)
                )
            }
        }
    }
}

/** "Проверить обновления" — капсула 34 на chip + stroke, текст accentInk 12/700, нажатие scale .95. */
@Composable
private fun CheckUpdatesPill(onClick: () -> Unit) {
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    Box(
        Modifier
            .height(32.dp)
            .scale(if (pressed) 0.95f else 1f)
            .clip(RoundedCornerShape(2.dp))
            .background(GodjiColors.Chip)
            .border(1.dp, GodjiColors.CardBorder, RoundedCornerShape(2.dp))
            .clickable(interactionSource = interaction, indication = null, onClick = onClick)
            .padding(horizontal = 14.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(Loc.s.settingsCheckUpdates, color = GodjiColors.TealDeep, fontWeight = FontWeight.Bold, fontSize = 12.sp)
    }
}

@Composable
private fun AboutRow(label: String, value: String) {
    val clipboard = LocalClipboardManager.current
    Row(
        pressableRow { clipboard.setText(AnnotatedString(value)) }.padding(horizontal = 14.dp, vertical = 9.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Text(label, color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 13.sp)
        Text(
            value,
            color = GodjiColors.TextPrimary,
            fontWeight = FontWeight.SemiBold,
            fontSize = 13.sp,
            modifier = Modifier.weight(1f),
            textAlign = TextAlign.End,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
    }
}

@Composable
private fun LogRow(title: String, onClick: () -> Unit) {
    Row(
        pressableRow(onClick).padding(horizontal = 14.dp, vertical = 9.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Text(title, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Medium, fontSize = 13.5.sp, modifier = Modifier.weight(1f))
        Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, contentDescription = null, tint = GodjiColors.TextSecondary, modifier = Modifier.size(15.dp))
    }
}

@Composable
fun PingMethodRow(label: String, method: PingMethod, selected: PingMethod, onSelect: (PingMethod) -> Unit) {
    Row(
        Modifier
            .fillMaxWidth()
            .clickable { onSelect(method) }
            .padding(vertical = 7.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        RadioButton(
            selected = selected == method,
            onClick = { onSelect(method) },
            colors = RadioButtonDefaults.colors(selectedColor = GodjiColors.TealDeep, unselectedColor = GodjiColors.ButtonBorder)
        )
        Text(label, color = GodjiColors.TextPrimary, fontWeight = FontWeight.Medium, fontSize = 13.sp)
    }
}

/** Блок обновлений по эталону: idle (текст + "Проверить обновления"), проверка (спиннер),
 *  найдено (заголовок + НОВАЯ, changelog на chip, кнопка-градиент 44), загрузка (текст/% + полоса 8). */
@Composable
private fun UpdateSectionContent(state: SettingsUiState, viewModel: SettingsViewModel) {
    when {
        state.updateDownloading -> {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                Text(Loc.s.updateDownloading, color = GodjiColors.TextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 12.5.sp)
                Text("${state.updateDownloadProgress}%", color = GodjiColors.TextSecondary, fontWeight = FontWeight.SemiBold, fontSize = 12.5.sp)
            }
            Box(Modifier.fillMaxWidth().height(8.dp).clip(RoundedCornerShape(2.dp)).background(GodjiColors.TrackBg)) {
                Box(
                    Modifier
                        .fillMaxHeight()
                        .fillMaxWidth((state.updateDownloadProgress / 100f).coerceIn(0f, 1f))
                        .clip(RoundedCornerShape(2.dp))
                        .background(Brush.verticalGradient(0f to GodjiColors.AccentGradTop, 0.55f to GodjiColors.AccentGradMid, 1f to GodjiColors.AccentGradBottom))
                )
            }
        }
        state.updateAvailable != null -> {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(Loc.s.updateAvailableText(state.updateAvailable.version), color = GodjiColors.TextPrimary, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                NewBadge()
            }
            if (state.updateAvailable.changelog.isNotBlank()) {
                Box(
                    Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(2.dp))
                        .background(GodjiColors.Chip)
                        .padding(horizontal = 12.dp, vertical = 10.dp)
                        .wrapContentHeight(Alignment.CenterVertically)
                ) {
                    RichContent(raw = state.updateAvailable.changelog, collapsedBlocks = 5, readMoreLabel = Loc.s.plansNewsReadMore)
                }
            }
            val (interaction, scale) = rememberPressScale()
            Box(
                Modifier
                    .fillMaxWidth()
                    .height(44.dp)
                    .scale(scale.value)
                    .shadow(10.dp, RoundedCornerShape(2.dp), ambientColor = GodjiColors.AccentGlow, spotColor = GodjiColors.AccentGlow)
                    .clip(RoundedCornerShape(2.dp))
                    .background(Brush.verticalGradient(0f to GodjiColors.AccentGradTop, 0.55f to GodjiColors.AccentGradMid, 1f to GodjiColors.AccentGradBottom))
                    .clickable(interactionSource = interaction, indication = null, onClick = viewModel::downloadUpdate),
                contentAlignment = Alignment.Center
            ) {
                Text(Loc.s.updateDownloadInstall, color = GodjiColors.Surface, fontWeight = FontWeight.Bold, fontSize = 13.5.sp)
            }
        }
        state.updateChecking -> {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                CircularProgressIndicator(Modifier.size(16.dp), strokeWidth = 2.dp, color = GodjiColors.Teal, trackColor = GodjiColors.TrackBg)
                Text(Loc.s.updateChecking, color = GodjiColors.TextSecondary, fontWeight = FontWeight.SemiBold, fontSize = 12.5.sp)
            }
        }
        else -> {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                Text(
                    if (state.updateChecked) Loc.s.updateUpToDate else Loc.s.settingsVersionShort(state.appVersion),
                    color = GodjiColors.TextSecondary, fontWeight = FontWeight.Medium, fontSize = 12.sp,
                    modifier = Modifier.weight(1f)
                )
                CheckUpdatesPill(onClick = viewModel::checkForUpdate)
            }
        }
    }
}


/** Сетка палитр: образец (2dp, как весь «Прибор») + подпись; выбранная — рамка 2dp. */
@OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
@Composable
private fun PaletteGrid(selectedId: String, onSelect: (String) -> Unit) {
    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Text(
            when (Loc.lang) { AppLanguage.RU -> "Палитра"; AppLanguage.EN -> "Palette"; AppLanguage.ZH -> "配色" },
            color = GodjiColors.TextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 15.sp
        )
        FlowRow(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Palettes.all.forEach { p ->
                val tones = if (GodjiColors.isDark) p.dark() else p.light()
                val selected = p.id == selectedId
                Column(
                    Modifier
                        .width(72.dp)
                        .clickable(role = androidx.compose.ui.semantics.Role.RadioButton) { onSelect(p.id) }
                        .semantics { this.selected = selected },
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Row(
                        Modifier.size(width = 72.dp, height = 44.dp)
                            .border(if (selected) 2.dp else 1.dp, if (selected) GodjiColors.TextPrimary else GodjiColors.Hair, RoundedCornerShape(2.dp))
                            .padding(3.dp)
                    ) {
                        Box(Modifier.weight(1f).fillMaxHeight().background(tones.surface))
                        Box(Modifier.weight(1f).fillMaxHeight().background(tones.primary))
                        Box(Modifier.weight(1f).fillMaxHeight().background(tones.tertiary))
                    }
                    Text(p.localName(), color = if (selected) GodjiColors.TextPrimary else GodjiColors.TextMuted,
                        fontSize = 11.sp, maxLines = 1, modifier = Modifier.padding(top = 4.dp))
                }
            }
        }
    }
}
