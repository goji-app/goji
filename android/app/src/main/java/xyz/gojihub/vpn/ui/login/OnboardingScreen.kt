package xyz.gojihub.vpn.ui.login

import xyz.gojihub.vpn.ui.theme.MonoFamily
import androidx.compose.ui.unit.em
import androidx.compose.foundation.clickable
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.ui.semantics.Role
import android.content.Context
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.layout.layout
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import xyz.gojihub.vpn.R
import xyz.gojihub.vpn.i18n.Loc
import xyz.gojihub.vpn.ui.globe.GojiGlobe
import xyz.gojihub.vpn.ui.theme.GlassBackdrop
import xyz.gojihub.vpn.ui.theme.GodjiColors
import xyz.gojihub.vpn.ui.theme.SpaceGroteskFamily
import xyz.gojihub.vpn.ui.theme.godjiGlassPill
import xyz.gojihub.vpn.ui.theme.godjiGlassStrong
import xyz.gojihub.vpn.ui.util.rememberPressScale

/** Первый запуск: три коротких экрана до входа — что такое Goji, как начать, зачем системе
 *  разрешение на VPN (одинаково на всех платформах, 2026-10-28). Вид — как у экрана входа. */
object Onboarding {
    private const val PREFS = "onboarding"
    private const val KEY_SEEN = "seen"

    fun isSeen(context: Context) = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getBoolean(KEY_SEEN, false)

    fun markSeen(context: Context) =
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putBoolean(KEY_SEEN, true).apply()
}

@Composable
fun OnboardingScreen(onDone: () -> Unit) {
    val pages = listOf(
        Loc.f.onboardingTitle1 to Loc.f.onboardingBody1,
        Loc.f.onboardingTitle2 to Loc.f.onboardingBody2,
        Loc.f.onboardingTitle3 to Loc.f.onboardingBody3
    )
    var page by rememberSaveable { mutableIntStateOf(0) }
    val last = page == pages.lastIndex

    // Макет 9a «Знакомство»: «Пропустить», панель с огромным кодом, заголовок, текст, три сегмента и кнопка.
    val codes = listOf("DE", "LTE", "0")
    val captions = listOf("СТРАНА, ИЗ КОТОРОЙ ТЫ ВЫХОДИШЬ В СЕТЬ", "РОССИЙСКИЕ УЗЛЫ, КОГДА МОБИЛЬНУЮ СВЯЗЬ ГЛУШАТ", "БЕЗ ПАРОЛЯ И АВТОСПИСАНИЙ")
    Column(Modifier.fillMaxSize().background(GodjiColors.SurfaceBase).statusBarsPadding()) {
        Row(Modifier.fillMaxWidth().padding(horizontal = 20.dp, vertical = 8.dp), horizontalArrangement = Arrangement.End) {
            Text(
                if (last) "" else Loc.f.onboardingSkip, color = GodjiColors.TextSecondary, fontSize = 14.sp,
                modifier = Modifier.heightIn(min = 48.dp).clickable(enabled = !last, role = Role.Button, onClick = onDone).padding(vertical = 14.dp)
            )
        }
        Box(Modifier.fillMaxWidth().height(230.dp).background(GodjiColors.SecondaryContainer), contentAlignment = Alignment.Center) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Text(
                    codes[page], color = GodjiColors.OnSecondaryContainer, fontFamily = SpaceGroteskFamily, fontWeight = FontWeight.Bold,
                    fontSize = 120.sp, lineHeight = 120.sp, letterSpacing = (-0.06).em, maxLines = 1, softWrap = false
                )
                Text(
                    captions[page], color = GodjiColors.OnSecondaryContainer, fontFamily = MonoFamily, fontSize = 11.sp,
                    textAlign = androidx.compose.ui.text.style.TextAlign.Center, modifier = Modifier.padding(start = 24.dp, end = 24.dp, top = 10.dp)
                )
            }
        }
        val (title, body) = pages[page]
        Column(Modifier.weight(1f).verticalScroll(rememberScrollState()).padding(horizontal = 20.dp)) {
            Text(
                title, color = GodjiColors.Ink, fontFamily = SpaceGroteskFamily, fontWeight = FontWeight.SemiBold,
                fontSize = 30.sp, lineHeight = 32.sp, letterSpacing = (-0.02).em, modifier = Modifier.padding(top = 26.dp)
            )
            body.lines().filter { it.isNotBlank() }.forEach { line ->
                Text(line, color = GodjiColors.TextSecondary, fontSize = 15.sp, lineHeight = 22.sp, modifier = Modifier.padding(top = 10.dp))
            }
        }
        // «1 из 3» — для экранного диктора.
        Row(
            Modifier.fillMaxWidth().padding(horizontal = 20.dp).padding(bottom = 16.dp).semantics { contentDescription = "${page + 1} / ${pages.size}" },
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            pages.indices.forEach { i ->
                Box(Modifier.weight(1f).height(2.dp).background(if (i == page) GodjiColors.Ink else GodjiColors.OutlineVariant))
            }
        }
        Box(
            Modifier
                .padding(start = 20.dp, end = 20.dp, bottom = 24.dp)
                .navigationBarsPadding()
                .fillMaxWidth().heightIn(min = 56.dp)
                .clip(RoundedCornerShape(2.dp))
                .background(GodjiColors.InkBtn)
                .clickable(role = Role.Button) { if (last) onDone() else page++ },
            contentAlignment = Alignment.Center
        ) {
            Text(
                (if (last) Loc.f.onboardingStart else Loc.f.onboardingNext).uppercase(),
                color = GodjiColors.Surface, fontFamily = MonoFamily, fontWeight = FontWeight.Bold, fontSize = 14.sp, letterSpacing = 0.06.em
            )
        }
    }
}
