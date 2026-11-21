package org.errin.scanner

import androidx.compose.runtime.Composable
import androidx.navigation3.runtime.entryProvider
import androidx.navigation3.runtime.rememberNavBackStack
import androidx.navigation3.ui.NavDisplay
import org.errin.scanner.ui.scanner.ScannerScreen
import org.errin.scanner.ui.settings.SettingsScreen

@Composable
fun MainNavigation() {
    val backStack = rememberNavBackStack(ScannerNav)

    NavDisplay(
        backStack = backStack,
        onBack = { backStack.removeLastOrNull() },
        entryProvider = entryProvider {
            entry<ScannerNav> {
                ScannerScreen(onNavigateToSettings = { backStack.add(SettingsNav) })
            }
            entry<SettingsNav> {
                SettingsScreen(onNavigateBack = { backStack.removeLastOrNull() })
            }
        }
    )
}
