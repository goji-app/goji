package xyz.gojihub.vpn.ui.login

import xyz.gojihub.vpn.ui.theme.MonoFamily
import xyz.gojihub.vpn.i18n.AppLanguage
import androidx.compose.ui.unit.em
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import android.content.Intent
import android.net.Uri
import xyz.gojihub.vpn.BuildConfig
import androidx.compose.foundation.Image
import androidx.compose.foundation.LocalIndication
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.ui.layout.layout
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.Constraints
import xyz.gojihub.vpn.R
import xyz.gojihub.vpn.ui.theme.godjiGlassPill
import androidx.compose.foundation.verticalScroll
import androidx.compose.ui.draw.scale
import xyz.gojihub.vpn.ui.util.rememberPressScale
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.activity.compose.rememberLauncherForActivityResult
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.LinkAnnotation
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.withLink
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import xyz.gojihub.vpn.i18n.Loc
import xyz.gojihub.vpn.ui.globe.GojiGlobe
import xyz.gojihub.vpn.ui.theme.GlassBackdrop
import xyz.gojihub.vpn.ui.theme.GodjiColors
import xyz.gojihub.vpn.ui.theme.SpaceGroteskFamily
import xyz.gojihub.vpn.ui.theme.godjiGlassFlat
import xyz.gojihub.vpn.ui.theme.godjiGlassStrong

@Composable
fun LoginScreen(
    onCodeSent: (String) -> Unit,
    viewModel: LoginViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsState()
    val context = LocalContext.current

    val qrScanner = rememberLauncherForActivityResult(ScanContract()) { result ->
        val text = result.contents ?: return@rememberLauncherForActivityResult
        viewModel.loginWithTransfer(text, onCodeSent)
    }

    // Макет 9a «Вход»: сразу поле почты (без выбора способа), код придёт в письме.
    LaunchedEffect(Unit) { if (!state.emailMode) viewModel.toggleEmailMode(true) }
    val lang = Loc.lang
    fun t(ru: String, en: String, zh: String) = when (lang) { AppLanguage.RU -> ru; AppLanguage.EN -> en; AppLanguage.ZH -> zh }
    Column(Modifier.fillMaxSize().background(GodjiColors.SurfaceBase).statusBarsPadding().imePadding()) {
        Column(Modifier.weight(1f).verticalScroll(rememberScrollState()).padding(horizontal = 20.dp)) {
            // Язык приложения выбирается до входа: RU · EN · 中文 (макет 9a).
            Row(Modifier.padding(top = 16.dp), horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                AppLanguage.entries.forEach { l ->
                    Text(
                        l.displayName.uppercase(), fontFamily = MonoFamily, fontSize = 12.sp,
                        fontWeight = if (l == lang) FontWeight.Bold else FontWeight.Normal,
                        color = if (l == lang) GodjiColors.Ink else GodjiColors.TextSecondary,
                        modifier = Modifier.clickable { viewModel.setLanguage(l) }.padding(vertical = 8.dp)
                    )
                }
            }
            Text(
                t("Войти по почте", "Sign in by email", "邮箱登录"), color = GodjiColors.Ink, fontFamily = SpaceGroteskFamily,
                fontWeight = FontWeight.SemiBold, fontSize = 30.sp, lineHeight = 32.sp, letterSpacing = (-0.02).em,
                modifier = Modifier.padding(top = 24.dp)
            )
            Text(
                t("Пришлём код из шести цифр. Пароль не нужен. Первые 3 дня бесплатно.",
                    "We'll send a six-digit code. No password. First 3 days are free.",
                    "我们会发送六位验证码，无需密码。前 3 天免费。"),
                color = GodjiColors.TextSecondary, fontSize = 15.sp, lineHeight = 22.sp, modifier = Modifier.padding(top = 10.dp)
            )
            Text(t("ПОЧТА", "EMAIL", "邮箱"), color = GodjiColors.TextSecondary, fontFamily = MonoFamily, fontSize = 11.sp, modifier = Modifier.padding(top = 34.dp))
            androidx.compose.foundation.text.BasicTextField(
                value = state.email,
                onValueChange = viewModel::onEmailChange,
                singleLine = true,
                textStyle = androidx.compose.ui.text.TextStyle(color = GodjiColors.Ink, fontSize = 18.sp, fontFamily = SpaceGroteskFamily),
                cursorBrush = androidx.compose.ui.graphics.SolidColor(GodjiColors.Ink),
                keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(keyboardType = androidx.compose.ui.text.input.KeyboardType.Email),
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp)
                    .semantics { contentDescription = Loc.s.loginEmailLabel }
                    .drawBehind {
                        drawLine(GodjiColors.Ink, androidx.compose.ui.geometry.Offset(0f, size.height), androidx.compose.ui.geometry.Offset(size.width, size.height), 1.dp.toPx())
                    }
                    .padding(bottom = 12.dp)
            )
            state.error?.let {
                Text(it, color = GodjiColors.Danger, fontSize = 12.sp, modifier = Modifier.fillMaxWidth().padding(top = 10.dp))
            }
            // Согласие с документами: те же два документа, что и на сайте кабинета.
            val consentText = remember(Loc.lang) {
                buildAnnotatedString {
                    append(Loc.s.loginConsentPrefix)
                    withLink(LinkAnnotation.Url("https://telegra.ph/Polzovatelskoe-soglashenie-Goji-VPN-09-20")) {
                        withStyle(SpanStyle(color = GodjiColors.Ink, fontWeight = FontWeight.SemiBold)) { append(Loc.s.loginConsentTerms) }
                    }
                    append(Loc.s.loginConsentAnd)
                    withLink(LinkAnnotation.Url("https://telegra.ph/Politika-konfidencialnosti-Goji-VPN-09-20")) {
                        withStyle(SpanStyle(color = GodjiColors.Ink, fontWeight = FontWeight.SemiBold)) { append(Loc.s.loginConsentPrivacy) }
                    }
                    append(Loc.s.loginConsentSuffix)
                }
            }
            Text(Loc.s.loginNoAccount, color = GodjiColors.TextSecondary, fontSize = 12.sp, modifier = Modifier.padding(top = 24.dp))
            Text(consentText, color = GodjiColors.TextSecondary, fontSize = 12.sp, lineHeight = 17.sp, modifier = Modifier.padding(top = 6.dp))
        }
        Box(
            Modifier
                .padding(horizontal = 20.dp)
                .fillMaxWidth().heightIn(min = 56.dp)
                .clip(RoundedCornerShape(2.dp))
                .background(GodjiColors.InkBtn.copy(alpha = if (state.loading) 0.5f else 1f))
                .clickable(enabled = !state.loading, role = Role.Button) { viewModel.sendOtp(onSent = onCodeSent) },
            contentAlignment = Alignment.Center
        ) {
            Text(
                (if (state.loading) Loc.s.loginSendingOtp else Loc.s.loginGetCode).uppercase(),
                color = GodjiColors.Surface, fontFamily = MonoFamily, fontWeight = FontWeight.Bold, fontSize = 14.sp, letterSpacing = 0.06.em
            )
        }
        Text(
            t("ВОЙТИ ПО QR С ДРУГОГО УСТРОЙСТВА", "SIGN IN WITH QR FROM ANOTHER DEVICE", "用其他设备扫码登录"),
            color = GodjiColors.TextSecondary, fontFamily = MonoFamily, fontSize = 12.sp, textAlign = TextAlign.Center,
            modifier = Modifier
                .fillMaxWidth()
                .clickable(enabled = !state.loading, role = Role.Button) {
                    qrScanner.launch(
                        ScanOptions()
                            .setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                            .setPrompt(Loc.f.loginQrPrompt)
                            .setBeepEnabled(false)
                            .setOrientationLocked(true)
                    )
                }
                .padding(vertical = 18.dp)
                .navigationBarsPadding()
        )
    }
}
