package xyz.gojihub.vpn.ui.theme

import android.provider.Settings
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.scale
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.graphics.drawscope.withTransform
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalWindowInfo
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.delay
import java.time.LocalDate
import kotlin.math.PI
import kotlin.math.cos
import kotlin.math.sin

private val BULBS = listOf(
    Color(0xFFFF5A4E), Color(0xFFFFD257), Color(0xFF63D68C), Color(0xFF6AA9FF), Color(0xFFFF9D5C)
)
private const val FRAME_MS = 80L // ~12 к/с: плавно для снега и не держит UI-поток
private const val FLAKES_BACK = 60
private const val FLAKES_FRONT = 14
private const val TAU = (2.0 * PI).toFloat()
private const val WIRE = 0xFF2C3634.toInt()
private const val SOCKET = 0xFF3A4542.toInt()
private const val SOCKET_LINE = 0xFF202927.toInt()
private const val CORE = 0x4DFFF8D9.toInt()
private const val FILAMENT = 0xD9FFF8D9.toInt()

/** Зимний сезон: 1 ноября — 31 января. */
fun isWinterSeason(): Boolean {
    val m = LocalDate.now().monthValue
    return m == 11 || m == 12 || m == 1
}

/** Шапка Деда Мороза (дизайн 120×104, масштабируется под ширину элемента). */
@Composable
fun SantaHat(modifier: Modifier) {
    Canvas(modifier) {
        val s = size.width / 120f
        withTransform({ scale(s, s, pivot = Offset.Zero) }) {
            val red = Brush.linearGradient(
                listOf(Color(0xFFE8505B), Color(0xFFC9303C), Color(0xFF9E1F2E)),
                start = Offset.Zero, end = Offset(120f, 104f)
            )
            val fur = Brush.verticalGradient(
                listOf(Color(0xFFFBFDFF), Color(0xFFDCE3EA)), startY = 54f, endY = 92f
            )
            drawPath(HAT_CONE, red)
            drawPath(HAT_SHADE, Color(0x808F1826))
            drawPath(HAT_HIGHLIGHT, Color(0x61F2717A))
            drawCircle(Color(0xFFFBFDFF), 10.5f, Offset(96f, 19f))

            drawPath(HAT_FUR, fur)
            drawPath(HAT_FUR_LINE, Color(0x9EC7D0DA), style = Stroke(width = 1.4f))
        }
    }
}

private val HAT_CONE = Path().apply {
    moveTo(14f, 68f)
    cubicTo(12f, 44f, 26f, 16f, 58f, 8f)
    cubicTo(76f, 4f, 90f, 10f, 96f, 20f)
    cubicTo(99f, 26f, 97f, 32f, 92f, 32f)
    cubicTo(90f, 44f, 88f, 56f, 88f, 68f)
    close()
}
private val HAT_SHADE = Path().apply {
    moveTo(92f, 32f)
    cubicTo(90f, 44f, 88f, 56f, 88f, 68f)
    lineTo(78f, 68f)
    cubicTo(80f, 54f, 84f, 42f, 87f, 33f)
    close()
}
private val HAT_HIGHLIGHT = Path().apply {
    moveTo(18f, 62f)
    cubicTo(17f, 42f, 28f, 20f, 52f, 10f)
    cubicTo(36f, 22f, 25f, 42f, 25f, 62f)
    close()
}
private val HAT_FUR = Path().apply {
    moveTo(8f, 62f)
    cubicTo(38f, 74f, 80f, 70f, 106f, 56f)
    lineTo(110f, 71f)
    cubicTo(82f, 86f, 36f, 90f, 12f, 77f)
    cubicTo(4f, 74f, 3f, 67f, 8f, 62f)
    close()
}
private val HAT_FUR_LINE = Path().apply {
    moveTo(10f, 64f)
    cubicTo(40f, 75f, 80f, 71f, 104f, 58f)
}

/** Снежинка: физика как в превью — свои скорость, покачивание, турбулентность и порывы ветра. */
private class Flake(isFront: Boolean, w: Float, h: Float, private val rnd: java.util.Random) {
    val front = isFront
    private fun rr(a: Float, b: Float) = a + rnd.nextFloat() * (b - a)

    var x = rr(-40f, w + 40f)
    var y = rr(-30f, h)
    var vy: Float
    var size: Float
    var alpha: Float
    var depth: Float
    var rot = rr(0f, 360f)
    var rs: Float
    var ph = rr(0f, TAU)
    var ph2 = rr(0f, TAU)
    var ph3 = rr(0f, TAU)
    var swayA = rr(8f, 30f)
    var swayF = rr(0.2f, 0.55f)
    var drift = rr(-7f, 7f)
    var a2 = rr(4f, 14f)
    var f2 = rr(0.6f, 1.8f)
    var vf = rr(0.25f, 0.8f)
    var gust = rr(0.5f, 1.6f)
    var spr = rnd.nextInt(3)

    init {
        if (front) {
            vy = rr(48f, 88f); size = rr(9f, 16f); alpha = rr(0.35f, 0.6f); depth = 1.25f; rs = rr(-0.5f, 0.5f)
        } else {
            vy = rr(14f, 38f); size = rr(2f, 5.5f); alpha = rr(0.3f, 0.72f); depth = 0.7f; rs = rr(-0.9f, 0.9f)
        }
    }

    fun respawn(w: Float) {
        y = -26f
        x = rr(-30f, w + 30f)
    }
}

private class Bulb(val t: Float, rnd: java.util.Random) {
    var tilt = 0f
    var px = 0f
    var init = false
    var resp = 0.7f + rnd.nextFloat() * 0.8f
    var wf = 0.4f + rnd.nextFloat() * 0.7f
    var pp = rnd.nextFloat() * TAU
}

private class Strand(
    val x0: Float, val cx: Float, val x2: Float,
    val y0: Float, val cy: Float, val y2: Float,
    val n: Int, val sc: Float, amplitude: Float, private val rnd: java.util.Random
) {
    private fun rr(a: Float, b: Float) = a + rnd.nextFloat() * (b - a)

    val bulbs = Array(n) { Bulb(0.06f + 0.88f * it / (n - 1), rnd) }
    val a = amplitude * rr(0.8f, 1.15f)
    val p1 = rr(0f, TAU)
    val p2 = rr(0f, TAU)
    val p3 = rr(0f, TAU)
}

/** Сцена (снежинки + гирлянды), пересобирается под фактический размер экрана:
 *  плотность снега — от площади, геометрия и размер ламп — от ширины. */
private class WinterScene {
    var builtW = 0f
    var builtH = 0f
    var builtDp = 0f
    var flakes = arrayOf<Flake>()
    var strands = listOf<Strand>()
    var backPaths = listOf<Path>()
    var frontPaths = listOf<Path>()
    private val rnd = java.util.Random()

    fun ensure(w: Float, h: Float, density: Float) {
        if (builtW > 0 && kotlin.math.abs(w - builtW) < w * 0.2f && kotlin.math.abs(h - builtH) < h * 0.2f && builtDp == density) return
        builtW = w; builtH = h; builtDp = density
        val screens = (w / (density * 360f)) * (h / (density * 760f)) // площадь в «эталонных» экранах
        flakes = Array(
            (FLAKES_BACK * screens).toInt().coerceIn(40, 160) + (FLAKES_FRONT * screens).toInt().coerceIn(10, 32)
        ) { i -> Flake(i < FLAKES_BACK, w, h, rnd) }
        val ratio = (w / (density * 372f)).coerceIn(1f, 1.8f) // >1 на широких экранах
        val n1 = (8 * ratio).toInt().coerceIn(8, 16)
        strands = listOf(
            Strand(-density * 4, w / 2f, w + density * 4,
                w * 0.022f, w * 0.022f + w * 0.235f, w * 0.042f,
                n1, density * ratio, w * 0.032f, rnd),
            Strand(-density * 4, w / 2f, w + density * 4,
                w * 0.10f, w * 0.10f + w * 0.085f, w * 0.16f,
                (n1 - 2).coerceAtLeast(6), density * ratio * 0.85f, w * 0.032f, rnd)
        )
        backPaths = List(3) { flakePath(BACK_BASE[it] * density, it) }
        frontPaths = List(3) { flakePath(FRONT_BASE[it] * density, it) }
    }
}

/** Зимний декор: с 1 ноября по 31 января снег и гирлянда поверх любой палитры. */
@Composable
fun WinterOverlay() {
    val season = remember { isWinterSeason() }
    if (!season) return
    val context = LocalContext.current
    val density = LocalDensity.current
    // Анимации выключены в системе — рисуем один неподвижный кадр.
    val still = remember { Settings.Global.getFloat(context.contentResolver, Settings.Global.ANIMATOR_DURATION_SCALE, 1f) == 0f }
    val focused = LocalWindowInfo.current.isWindowFocused
    var t by remember { mutableFloatStateOf(2.1f) }
    if (!still && focused) LaunchedEffect(Unit) {
        val t0 = System.nanoTime() - (t * 1e9).toLong()
        while (true) { delay(FRAME_MS); t = (System.nanoTime() - t0) / 1e9f }
    }

    // Белый снег невидим на светлых палитрах — там берём серо-голубой.
    val snow = if (GodjiColors.SurfaceBase.luminance() > 0.5f) Color(0xFF8FA8C4) else Color.White
    val wirePath = remember { Path() }
    val glassPath = remember { glassPath() }
    val scene = remember { WinterScene() }

    Canvas(Modifier.fillMaxSize().statusBarsPadding()) {
        val w = size.width
        val h = size.height
        val dt = FRAME_MS / 1000f
        scene.ensure(w, h, density.density)

        // Гирлянды: провод гнётся как верёвка (контрольная точка плывёт по трём гармоникам),
        // лампочки едут вдоль него и отстают, как маятники.
        for (d in scene.strands) {
            val dx = strandSway(d, t) * 2.2f
            val dy = 3f * sin(TAU / 8.3f * t + d.p2)
            wirePath.rewind()
            wirePath.moveTo(d.x0, d.y0)
            wirePath.quadraticBezierTo(d.cx + dx, d.cy + dy, d.x2, d.y2)
            drawPath(wirePath, Color(WIRE), style = Stroke(width = 1.6.dp.toPx()))
            for ((k, bl) in d.bulbs.withIndex()) {
                val u = 1f - bl.t
                val x = u * u * d.x0 + 2f * u * bl.t * (d.cx + dx) + bl.t * bl.t * d.x2
                val y = u * u * d.y0 + 2f * u * bl.t * (d.cy + dy) + bl.t * bl.t * d.y2
                val vel = if (bl.init && dt > 0) (x - bl.px) / dt else 0f
                bl.px = x; bl.init = true
                val target = (-vel * 0.38f).coerceIn(-11f, 11f) + 1.1f * sin(t * bl.wf * TAU + bl.pp)
                bl.tilt += (target - bl.tilt) * (dt * bl.resp * 3).coerceAtMost(1f)
                val col = BULBS[k % BULBS.size]
                translate(x, y) {
                    rotate(bl.tilt, pivot = Offset.Zero) {
                        scale(d.sc, d.sc, pivot = Offset.Zero) {
                            // ВАЖНО: у drawCircle без явного центра центр = центр DrawScope,
                            // а не начало координат — лампы уезжают в середину экрана.
                            drawCircle(col.copy(alpha = 0.20f), 16f, Offset(0f, 17f))
                            drawCircle(col.copy(alpha = 0.50f), 9f, Offset(0f, 17f))
                            drawRoundRect(Color(SOCKET), topLeft = Offset(-3.6f, 3f), size = Size(7.2f, 7.4f), cornerRadius = CornerRadius(2f, 2f))
                            drawLine(Color(SOCKET_LINE), Offset(-3.6f, 4.8f), Offset(3.6f, 4.8f), strokeWidth = 0.8f)
                            drawLine(Color(SOCKET_LINE), Offset(-3.6f, 6.6f), Offset(3.6f, 6.6f), strokeWidth = 0.8f)
                            drawLine(Color(WIRE), Offset(0f, 0f), Offset(0f, 3.4f), strokeWidth = 1.8f)
                            drawPath(glassPath, col)
                            drawCircle(Color(CORE), 2.7f, Offset(0f, 17.4f))
                            drawLine(Color(FILAMENT), Offset(-1.8f, 14.5f), Offset(0f, 19f), strokeWidth = 0.9f)
                            drawLine(Color(FILAMENT), Offset(0f, 19f), Offset(1.8f, 14.5f), strokeWidth = 0.9f)
                            drawOval(Color(0x80FFFFFF), topLeft = Offset(-4.7f, 11f), size = Size(3.4f, 5.2f))
                        }
                    }
                }
            }
        }

        // Снег: сзади мелкий и блеклый, спереди крупный и мягкий.
        for (f in scene.flakes) {
            f.ph += dt; f.ph2 += dt * f.f2 * TAU; f.ph3 += dt * f.vf * TAU
            f.x += (wind(t) * f.depth * f.gust * 0.5f + f.drift + sin(f.ph2) * f.a2 * 0.55f) * dt
            f.y += f.vy * (1f + 0.3f * sin(f.ph3)) * dt
            f.rot += f.rs * dt
            if (f.y > h + 26f) f.respawn(w)
            if (f.x > w + 45f) f.x -= w + 90f
            if (f.x < -45f) f.x += w + 90f
            val rx = f.x + sin(f.ph * f.swayF * TAU) * f.swayA + sin(f.ph2 * 0.6f + 1.3f) * f.a2
            val base = if (f.front) FRONT_BASE[f.spr] else BACK_BASE[f.spr]
            val scaleS = (f.size + 16f) / base
            val path = if (f.front) scene.frontPaths[f.spr] else scene.backPaths[f.spr]
            val strokeW = (base * 0.05f).coerceAtLeast(0.7f)
            translate(rx, f.y) {
                rotate(f.rot, pivot = Offset.Zero) {
                    scale(scaleS, scaleS, pivot = Offset.Zero) {
                        if (f.front) drawPath(path, snow.copy(alpha = f.alpha * 0.35f), style = Stroke(width = strokeW * 2.6f))
                        drawPath(path, snow.copy(alpha = f.alpha), style = Stroke(width = strokeW))
                    }
                }
            }
        }
    }
}

private val BACK_BASE = floatArrayOf(7f, 6f, 5f)
private val FRONT_BASE = floatArrayOf(30f, 26f, 22f)

private fun Float.dpToPx(density: androidx.compose.ui.unit.Density): Float = with(density) { this@dpToPx.dp.toPx() }

private fun wind(t: Float): Float =
    16f * sin(t * 0.11f + 1.3f) + 9f * sin(t * 0.31f + 4f) + 22f * sin(t * 0.043f) * sin(t * 0.097f)

private fun strandSway(d: Strand, t: Float): Float =
    d.a * (0.62f * sin(TAU / 6.4f * t + d.p1) + 0.26f * sin(TAU / 2.7f * t + d.p2) + 0.12f * sin(TAU / 11.5f * t + d.p3))

private fun glassPath(): Path = Path().apply {
    moveTo(0f, 10f)
    cubicTo(4.8f, 10f, 7.8f, 13f, 7.8f, 17f)
    cubicTo(7.8f, 22.2f, 4.4f, 25.2f, 0f, 25.2f)
    cubicTo(-4.4f, 25.2f, -7.8f, 22.2f, -7.8f, 17f)
    cubicTo(-7.8f, 13f, -4.8f, 10f, 0f, 10f)
    close()
}

/** Дендрит (0), гексагональная пластина (1), игольчатая звезда (2) — 6-лучевой контур. */
private fun flakePath(px: Float, variant: Int): Path {
    val r = px / 2f
    val path = Path()
    for (k in 0 until 6) {
        val a = k * PI / 3.0
        val ca = cos(a).toFloat(); val sa = sin(a).toFloat()
        fun line(x1: Float, y1: Float, x2: Float, y2: Float) {
            path.moveTo(x1 * ca - y1 * sa, x1 * sa + y1 * ca)
            path.lineTo(x2 * ca - y2 * sa, x2 * sa + y2 * ca)
        }
        when (variant) {
            0 -> {
                line(0f, 0f, 0f, -r)
                val brs = arrayOf(0.42f to 0.42f, 0.6f to 0.3f, 0.78f to 0.18f)
                for ((p, blf) in brs) {
                    val by = -r * p; val bl = r * blf
                    line(0f, by, bl * 0.5f, by - bl * 0.87f)
                    line(0f, by, -bl * 0.5f, by - bl * 0.87f)
                }
                line(0f, -r * 0.9f, r * 0.12f, -r * 0.78f)
                line(0f, -r * 0.9f, -r * 0.12f, -r * 0.78f)
            }
            1 -> {
                for (rf in listOf(1f, 0.62f, 0.3f)) {
                    for (i in 0 until 6) {
                        val a1 = i * PI / 3.0; val a2 = (i + 1) * PI / 3.0
                        path.moveTo(cos(a1).toFloat() * r * rf, sin(a1).toFloat() * r * rf)
                        path.lineTo(cos(a2).toFloat() * r * rf, sin(a2).toFloat() * r * rf)
                    }
                }
                line(0f, 0f, 0f, -r)
            }
            else -> {
                line(0f, 0f, 0f, -r)
                for (p in listOf(0.5f, 0.72f)) {
                    val y = -r * p; val l = r * 0.34f * (p / 0.5f) * 0.9f
                    line(0f, y, l, y - l * 0.6f)
                    line(0f, y, -l, y - l * 0.6f)
                }
            }
        }
    }
    return path
}
