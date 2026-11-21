package org.errin.scanner.ui.settings

import android.widget.Toast
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Cloud
import androidx.compose.material.icons.filled.Computer
import androidx.compose.material.icons.filled.Save
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.launch
import org.errin.scanner.data.CloudDataStore
import org.errin.scanner.ui.theme.ErrinCard
import org.errin.scanner.ui.theme.ErrinDarkBg
import org.errin.scanner.ui.theme.ErrinGreen
import org.errin.scanner.ui.theme.ErrinRed
import org.errin.scanner.ui.theme.ErrinSurface
import org.errin.scanner.ui.theme.ErrinSurfaceVariant
import org.errin.scanner.ui.theme.ErrinTextMuted
import org.errin.scanner.ui.theme.ErrinTextPrimary
import org.errin.scanner.ui.theme.ErrinTextSecondary
import org.errin.scanner.ui.theme.ErrinYellow

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    onNavigateBack: () -> Unit,
    modifier: Modifier = Modifier
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val store = remember { CloudDataStore(context) }

    var accountEmail by remember { mutableStateOf(store.googleAccount) }
    var githubRepo by remember { mutableStateOf(store.githubRepo) }
    var githubPat by remember { mutableStateOf(store.githubToken) }
    var webAppUrl by remember { mutableStateOf(store.webAppUrl) }
    var localServerUrl by remember { mutableStateOf(store.localServerUrl) }
    var isStandalone by remember { mutableStateOf(store.isStandaloneMode) }
    var soundEnabled by remember { mutableStateOf(store.soundEnabled) }
    var vibrateEnabled by remember { mutableStateOf(store.vibrateEnabled) }

    var isTesting by remember { mutableStateOf(false) }
    var testResult by remember { mutableStateOf<String?>(null) }
    var testSuccess by remember { mutableStateOf(false) }

    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = ErrinDarkBg,
        topBar = {
            TopAppBar(
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ErrinSurface),
                title = { Text("Tetapan & Sambungan Awan", color = ErrinTextPrimary, fontSize = 17.sp, fontWeight = FontWeight.Bold) },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ErrinYellow)
                    }
                }
            )
        }
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            // Mode Selector
            Card(
                colors = CardDefaults.cardColors(containerColor = ErrinSurface),
                shape = RoundedCornerShape(12.dp)
            ) {
                Column(modifier = Modifier.padding(14.dp)) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text("Mod Awan Kendiri (Standalone)", color = ErrinTextPrimary, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                            Text("Imbas & simpan terus ke Google Drive & GitHub tanpa perlu PC desktop.", color = ErrinTextMuted, fontSize = 12.sp)
                        }
                        Switch(
                            checked = isStandalone,
                            onCheckedChange = {
                                isStandalone = it
                                store.isStandaloneMode = it
                            },
                            colors = SwitchDefaults.colors(checkedThumbColor = ErrinYellow, checkedTrackColor = ErrinCard)
                        )
                    }
                }
            }

            // Cloud Sync Credentials
            Card(
                colors = CardDefaults.cardColors(containerColor = ErrinSurface),
                shape = RoundedCornerShape(12.dp)
            ) {
                Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.Cloud, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(20.dp))
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Konfigurasi Google Drive & GitHub", color = ErrinYellow, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    }

                    OutlinedTextField(
                        value = accountEmail,
                        onValueChange = { accountEmail = it },
                        label = { Text("Emel Akaun Google Sekolah", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )

                    OutlinedTextField(
                        value = githubRepo,
                        onValueChange = { githubRepo = it },
                        label = { Text("Repositori GitHub Katalog", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )

                    OutlinedTextField(
                        value = githubPat,
                        onValueChange = { githubPat = it },
                        label = { Text("Token Akses Peribadi GitHub (PAT)", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )

                    OutlinedTextField(
                        value = webAppUrl,
                        onValueChange = { webAppUrl = it },
                        label = { Text("URL Web App Google Apps Script", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Button(
                            onClick = {
                                store.googleAccount = accountEmail.trim()
                                store.githubRepo = githubRepo.trim()
                                store.githubToken = githubPat.trim()
                                store.webAppUrl = webAppUrl.trim()
                                Toast.makeText(context, "Tetapan disimpan!", Toast.LENGTH_SHORT).show()
                            },
                            modifier = Modifier.weight(1f),
                            colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow),
                            shape = RoundedCornerShape(8.dp)
                        ) {
                            Icon(Icons.Default.Save, contentDescription = null, tint = ErrinDarkBg, modifier = Modifier.size(16.dp))
                            Spacer(modifier = Modifier.width(6.dp))
                            Text("Simpan", color = ErrinDarkBg, fontWeight = FontWeight.Bold)
                        }

                        Button(
                            onClick = {
                                scope.launch {
                                    isTesting = true
                                    testResult = null
                                    store.googleAccount = accountEmail.trim()
                                    store.githubRepo = githubRepo.trim()
                                    store.githubToken = githubPat.trim()
                                    store.webAppUrl = webAppUrl.trim()

                                    val res = store.syncAll()
                                    isTesting = false
                                    testSuccess = res.isSuccess
                                    testResult = if (res.isSuccess) {
                                        "✅ Sambungan Berjaya!\n• Katalog: ${store.cachedItems.size} buah buku\n• Murid: ${store.cachedPatrons.size} orang"
                                    } else {
                                        "❌ Ralat Sambungan: ${res.exceptionOrNull()?.message}"
                                    }
                                }
                            },
                            modifier = Modifier.weight(1f),
                            enabled = !isTesting,
                            colors = ButtonDefaults.buttonColors(containerColor = ErrinSurfaceVariant),
                            shape = RoundedCornerShape(8.dp)
                        ) {
                            if (isTesting) {
                                CircularProgressIndicator(modifier = Modifier.size(16.dp), color = ErrinYellow, strokeWidth = 2.dp)
                            } else {
                                Icon(Icons.Default.Sync, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(16.dp))
                                Spacer(modifier = Modifier.width(6.dp))
                                Text("Uji Sambungan", color = ErrinTextPrimary, fontSize = 12.sp)
                            }
                        }
                    }

                    if (testResult != null) {
                        Text(
                            text = testResult ?: "",
                            color = if (testSuccess) ErrinGreen else ErrinRed,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Medium
                        )
                    }
                }
            }

            // Local PC Server Fallback
            if (!isStandalone) {
                Card(
                    colors = CardDefaults.cardColors(containerColor = ErrinSurface),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Default.Computer, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(20.dp))
                            Spacer(modifier = Modifier.width(8.dp))
                            Text("Sambungan Komputer Desktop ErrinILS", color = ErrinYellow, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                        }
                        OutlinedTextField(
                            value = localServerUrl,
                            onValueChange = {
                                localServerUrl = it
                                store.localServerUrl = it
                            },
                            label = { Text("URL Komputer (cth. http://192.168.1.50:8080)", fontSize = 12.sp) },
                            singleLine = true,
                            colors = outlinedColors(),
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                }
            }

            // Feedback Toggles
            Card(
                colors = CardDefaults.cardColors(containerColor = ErrinSurface),
                shape = RoundedCornerShape(12.dp)
            ) {
                Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Maklum Balas Imbasan", color = ErrinYellow, fontWeight = FontWeight.Bold, fontSize = 14.sp)

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text("Bunyi Beep Semasa Imbas", color = ErrinTextPrimary, fontSize = 13.sp)
                        Switch(
                            checked = soundEnabled,
                            onCheckedChange = {
                                soundEnabled = it
                                store.soundEnabled = it
                            },
                            colors = SwitchDefaults.colors(checkedThumbColor = ErrinYellow, checkedTrackColor = ErrinCard)
                        )
                    }

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text("Getaran (Haptic Feedback)", color = ErrinTextPrimary, fontSize = 13.sp)
                        Switch(
                            checked = vibrateEnabled,
                            onCheckedChange = {
                                vibrateEnabled = it
                                store.vibrateEnabled = it
                            },
                            colors = SwitchDefaults.colors(checkedThumbColor = ErrinYellow, checkedTrackColor = ErrinCard)
                        )
                    }
                }
            }

            // About / Info
            Card(
                colors = CardDefaults.cardColors(containerColor = ErrinSurfaceVariant),
                shape = RoundedCornerShape(12.dp)
            ) {
                Column(modifier = Modifier.padding(14.dp)) {
                    Text("ErrinILS Scanner v1.0", color = ErrinTextPrimary, fontWeight = FontWeight.Bold, fontSize = 13.sp)
                    Text("Sistem Pengurusan Perpustakaan Sekolah Bersepadu", color = ErrinTextSecondary, fontSize = 11.sp)
                    Text("SK Taman Ria • Pustaka Terbuka Union", color = ErrinTextMuted, fontSize = 11.sp)
                }
            }
        }
    }
}

@Composable
private fun outlinedColors() = OutlinedTextFieldDefaults.colors(
    focusedBorderColor = ErrinYellow,
    unfocusedBorderColor = ErrinSurfaceVariant,
    focusedTextColor = ErrinTextPrimary,
    unfocusedTextColor = ErrinTextPrimary,
    focusedLabelColor = ErrinYellow,
    unfocusedLabelColor = ErrinTextMuted
)
