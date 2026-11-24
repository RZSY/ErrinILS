package org.errin.scanner

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import org.errin.scanner.server.ScannerHttpServer
import org.errin.scanner.ui.theme.ErrinILSScannerTheme

class MainActivity : ComponentActivity() {
  override fun onCreate(savedInstanceState: Bundle?) {
    super.onCreate(savedInstanceState)

    ScannerHttpServer.start(applicationContext)

    enableEdgeToEdge()
    setContent {
      ErrinILSScannerTheme { Surface(modifier = Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) { MainNavigation() } }
    }
  }

  override fun onDestroy() {
    super.onDestroy()
    ScannerHttpServer.stop()
  }
}
