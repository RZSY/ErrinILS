package org.errin.scanner.ui.components

import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.RoundRect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.ClipOp
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.clipPath
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import org.errin.scanner.ui.theme.ErrinYellow

@Composable
fun ScannerOverlay(
    modifier: Modifier = Modifier,
    hintText: String = "Point camera at barcode / ISBN"
) {
    val infiniteTransition = rememberInfiniteTransition(label = "laser")
    val laserProgress by infiniteTransition.animateFloat(
        initialValue = 0f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(1800, easing = FastOutSlowInEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "laser_y"
    )

    Box(modifier = modifier.fillMaxSize()) {
        Canvas(modifier = Modifier.fillMaxSize()) {
            val canvasW = size.width
            val canvasH = size.height

            // Calculate bounding box for reticle (square or slightly wide rectangle)
            val boxW = canvasW * 0.78f
            val boxH = boxW * 0.65f
            val boxLeft = (canvasW - boxW) / 2f
            val boxTop = (canvasH - boxH) / 2f - 40f

            val reticleRect = Rect(boxLeft, boxTop, boxLeft + boxW, boxTop + boxH)
            val roundRect = RoundRect(reticleRect, CornerRadius(16.dp.toPx(), 16.dp.toPx()))

            // Draw dark scrim with transparent reticle cutout
            val clipPath = Path().apply { addRoundRect(roundRect) }
            clipPath(clipPath, clipOp = ClipOp.Difference) {
                drawRect(Color(0x99000000))
            }

            // Draw corner brackets
            val cornerLen = 28.dp.toPx()
            val strokeW = 4.dp.toPx()
            val cornerColor = ErrinYellow

            // Top-Left
            drawLine(cornerColor, Offset(boxLeft, boxTop + cornerLen), Offset(boxLeft, boxTop), strokeW, StrokeCap.Round)
            drawLine(cornerColor, Offset(boxLeft, boxTop), Offset(boxLeft + cornerLen, boxTop), strokeW, StrokeCap.Round)

            // Top-Right
            drawLine(cornerColor, Offset(boxLeft + boxW - cornerLen, boxTop), Offset(boxLeft + boxW, boxTop), strokeW, StrokeCap.Round)
            drawLine(cornerColor, Offset(boxLeft + boxW, boxTop), Offset(boxLeft + boxW, boxTop + cornerLen), strokeW, StrokeCap.Round)

            // Bottom-Left
            drawLine(cornerColor, Offset(boxLeft, boxTop + boxH - cornerLen), Offset(boxLeft, boxTop + boxH), strokeW, StrokeCap.Round)
            drawLine(cornerColor, Offset(boxLeft, boxTop + boxH), Offset(boxLeft + cornerLen, boxTop + boxH), strokeW, StrokeCap.Round)

            // Bottom-Right
            drawLine(cornerColor, Offset(boxLeft + boxW - cornerLen, boxTop + boxH), Offset(boxLeft + boxW, boxTop + boxH), strokeW, StrokeCap.Round)
            drawLine(cornerColor, Offset(boxLeft + boxW, boxTop + boxH), Offset(boxLeft + boxW, boxTop + boxH - cornerLen), strokeW, StrokeCap.Round)

            // Animated Laser Line
            val laserY = boxTop + (boxH * laserProgress)
            drawLine(
                color = ErrinYellow.copy(alpha = 0.85f),
                start = Offset(boxLeft + 8.dp.toPx(), laserY),
                end = Offset(boxLeft + boxW - 8.dp.toPx(), laserY),
                strokeWidth = 2.5.dp.toPx(),
                cap = StrokeCap.Round
            )
        }

        Text(
            text = hintText,
            color = Color.White.copy(alpha = 0.9f),
            fontSize = 13.sp,
            fontWeight = FontWeight.Medium,
            modifier = Modifier
                .align(Alignment.Center)
                .padding(top = 160.dp)
        )
    }
}
