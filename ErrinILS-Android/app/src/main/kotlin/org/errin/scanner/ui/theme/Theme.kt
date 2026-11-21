package org.errin.scanner.ui.theme

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat

private val ErrinDarkColorScheme = darkColorScheme(
    primary = ErrinYellow,
    onPrimary = ErrinDarkBg,
    primaryContainer = ErrinSurfaceVariant,
    onPrimaryContainer = ErrinYellow,
    secondary = ErrinBlueLight,
    onSecondary = ErrinDarkBg,
    secondaryContainer = ErrinCard,
    onSecondaryContainer = ErrinTextPrimary,
    background = ErrinDarkBg,
    onBackground = ErrinTextPrimary,
    surface = ErrinSurface,
    onSurface = ErrinTextPrimary,
    surfaceVariant = ErrinSurfaceVariant,
    onSurfaceVariant = ErrinTextSecondary,
    outline = ErrinBorder,
    error = ErrinRed,
    onError = ErrinTextPrimary
)

@Composable
fun ErrinILSScannerTheme(
    content: @Composable () -> Unit
) {
    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as Activity).window
            window.statusBarColor = ErrinDarkBg.toArgb()
            window.navigationBarColor = ErrinDarkBg.toArgb()
            val controller = WindowCompat.getInsetsController(window, view)
            controller.isAppearanceLightStatusBars = false
            controller.isAppearanceLightNavigationBars = false
        }
    }

    MaterialTheme(
        colorScheme = ErrinDarkColorScheme,
        typography = Typography,
        content = content
    )
}
