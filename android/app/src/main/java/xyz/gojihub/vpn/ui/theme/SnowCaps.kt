package xyz.gojihub.vpn.ui.theme

import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.BlendMode
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Paint
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Fill
import androidx.compose.ui.graphics.drawscope.clipRect
import androidx.compose.ui.graphics.drawscope.drawIntoCanvas
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.drawText
import kotlin.math.sin

/** Высота заглавной и строчной буквы у Space Grotesk в долях кегля. */
const val CAP_HEIGHT = 0.70f
private const val X_HEIGHT = 0.49f
private const val ASCENDERS = "бфЙ"

private class CapSpan(val left: Float, val right: Float, val level: Float)

/**
 * Сугробы на буквах: те же буквы тем же шрифтом, но снежным цветом, чуть приподнятые и обрезанные
 * волнистой кромкой сверху — получается шапка снега, лежащая на кромке каждой буквы и выступающая над ней.
 * Ничего не растеризуется заранее: слой рисуется один раз на изменение текста/цвета.
 * [perGlyph] — отдельный уровень для каждой буквы (заглавные и строчные); иначе одна линия по заглавным.
 */
fun Modifier.snowCaps(
    layout: TextLayoutResult?, text: String, snow: Color, perGlyph: Boolean,
    depthEm: Float = 0.04f, liftEm: Float = 0.012f
): Modifier {
    if (layout == null || !winterDecorOn()) return this
    return drawWithContent {
        drawContent()
        val em = layout.layoutInput.style.fontSize.toPx()
        val base = layout.getLineBaseline(0)
        val lift = em * liftEm
        val depth = em * depthEm
        // Текст берём из самой раскладки: она может отстать от нового заголовка на один кадр.
        val laid = layout.layoutInput.text.text
        val spans = if (perGlyph) {
            laid.indices.filter { !laid[it].isWhitespace() }.map {
                val c = laid[it]
                val box = layout.getBoundingBox(it)
                val cap = c.isUpperCase() || c.isDigit() || c in ASCENDERS
                CapSpan(box.left, box.right, base - em * if (cap) CAP_HEIGHT else X_HEIGHT)
            }
        } else listOf(CapSpan(-em, size.width + em, base - em * CAP_HEIGHT))
        val bottom = size.height + em
        drawIntoCanvas { canvas ->
            canvas.saveLayer(Rect(-em, -em, size.width + em, bottom), Paint())
            for (s in spans) clipRect(s.left, -em, s.right, bottom) {
                drawText(layout, color = snow, topLeft = Offset(0f, -lift), drawStyle = Fill)
                drawPath(edgeMask(s, depth, em, bottom), Color.Black, blendMode = BlendMode.DstOut)
            }
            canvas.restore()
        }
    }
}

/** Всё, что ниже волнистой нижней кромки сугроба, — стирается. */
private fun edgeMask(s: CapSpan, depth: Float, em: Float, bottom: Float): Path {
    fun edge(x: Float) = s.level + depth * (0.62f + 0.38f * sin(x / em * 11f + 0.7f * sin(x / em * 4f)))
    val step = (em * 0.04f).coerceAtLeast(2f)
    return Path().apply {
        moveTo(s.left, bottom)
        var x = s.left
        lineTo(x, edge(x))
        while (x < s.right) { x = minOf(x + step, s.right); lineTo(x, edge(x)) }
        lineTo(s.right, bottom)
        close()
    }
}
