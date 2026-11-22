package org.errin.scanner.core

import android.content.Context
import android.content.SharedPreferences

class AppPreferences(context: Context) {

    private val prefs: SharedPreferences = context.getSharedPreferences("errin_scanner_prefs", Context.MODE_PRIVATE)

    var serverUrl: String
        get() = prefs.getString("server_url", "") ?: ""
        set(value) = prefs.edit().putString("server_url", value.trim()).apply()

    var serverToken: String
        get() = prefs.getString("server_token", "") ?: ""
        set(value) = prefs.edit().putString("server_token", value.trim()).apply()

    var deviceName: String
        get() = prefs.getString("device_name", "ErrinILS Scanner") ?: "ErrinILS Scanner"
        set(value) = prefs.edit().putString("device_name", value.trim()).apply()

    var soundEnabled: Boolean
        get() = prefs.getBoolean("sound_enabled", true)
        set(value) = prefs.edit().putBoolean("sound_enabled", value).apply()

    var vibrateEnabled: Boolean
        get() = prefs.getBoolean("vibrate_enabled", true)
        set(value) = prefs.edit().putBoolean("vibrate_enabled", value).apply()

    var autoEnter: Boolean
        get() = prefs.getBoolean("auto_enter", true)
        set(value) = prefs.edit().putBoolean("auto_enter", value).apply()

    var githubRepo: String
        get() = prefs.getString("github_repo", "") ?: ""
        set(value) = prefs.edit().putString("github_repo", value.trim()).apply()

    var githubToken: String
        get() = prefs.getString("github_token", "") ?: ""
        set(value) = prefs.edit().putString("github_token", value.trim()).apply()

    val isConnectedToPc: Boolean
        get() = serverUrl.isNotBlank() && serverToken.isNotBlank()

    fun clearConnection() {
        prefs.edit().remove("server_url").remove("server_token").apply()
    }
}
