package org.errin.scanner.api

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.net.URLEncoder
import java.util.concurrent.TimeUnit

class ErrinApi(
    private var baseUrl: String = "",
    private var token: String = ""
) {
    private val jsonMedia = "application/json; charset=utf-8".toMediaType()

    private val client = OkHttpClient.Builder()
        .connectTimeout(7, TimeUnit.SECONDS)
        .readTimeout(12, TimeUnit.SECONDS)
        .writeTimeout(12, TimeUnit.SECONDS)
        .build()

    fun updateCredentials(url: String, newToken: String) {
        baseUrl = cleanUrl(url)
        token = newToken
    }

    fun getBaseUrl(): String = baseUrl
    fun getToken(): String = token

    private fun cleanUrl(raw: String): String {
        var u = raw.trim()
        if (u.isEmpty()) return ""
        if (!u.startsWith("http://") && !u.startsWith("https://")) {
            u = "http://$u"
        }
        return u.trimEnd('/')
    }

    private fun buildRequest(path: String, method: String = "GET", jsonBody: JSONObject? = null): Request {
        val fullUrl = "$baseUrl$path"
        val builder = Request.Builder()
            .url(fullUrl)
            .addHeader("Accept", "application/json")

        if (token.isNotEmpty()) {
            builder.addHeader("X-Errin-Token", token)
        }

        if (method == "POST") {
            val body = (jsonBody?.toString() ?: "{}").toRequestBody(jsonMedia)
            builder.post(body)
        } else {
            builder.get()
        }

        return builder.build()
    }

    suspend fun pair(serverUrl: String, code: String, deviceName: String): Result<PairResult> = withContext(Dispatchers.IO) {
        try {
            val cleanBase = cleanUrl(serverUrl)
            val json = JSONObject().apply {
                put("code", code.replace(Regex("[\\s-]"), ""))
                put("name", deviceName)
            }
            val req = Request.Builder()
                .url("$cleanBase/api/pair")
                .addHeader("Accept", "application/json")
                .post(json.toString().toRequestBody(jsonMedia))
                .build()

            client.newCall(req).execute().use { response ->
                val bodyStr = response.body?.string().orEmpty()
                val resObj = try { JSONObject(bodyStr) } catch (_: Exception) { JSONObject() }
                if (response.isSuccessful && resObj.optBoolean("ok", false)) {
                    val tok = resObj.optString("token", "")
                    val name = resObj.optString("name", deviceName)
                    baseUrl = cleanBase
                    token = tok
                    Result.success(PairResult(ok = true, token = tok, name = name))
                } else {
                    val err = resObj.optString("error", "Padanan gagal (HTTP ${response.code})")
                    Result.success(PairResult(ok = false, error = err))
                }
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun verifyConnection(): Result<MeResult> = withContext(Dispatchers.IO) {
        try {
            if (baseUrl.isEmpty()) {
                return@withContext Result.success(MeResult(ok = false, error = "URL pelayan belum ditetapkan"))
            }
            val req = buildRequest("/api/me")
            client.newCall(req).execute().use { response ->
                val bodyStr = response.body?.string().orEmpty()
                val json = try { JSONObject(bodyStr) } catch (_: Exception) { JSONObject() }
                if (response.isSuccessful && json.optBoolean("ok", false)) {
                    Result.success(
                        MeResult(
                            ok = true,
                            name = json.optString("name", "ErrinILS Scanner"),
                            paired = json.optString("paired", "")
                        )
                    )
                } else {
                    val err = json.optString("error", "Sambungan ditolak atau pelayan tidak sah (${response.code})")
                    Result.success(MeResult(ok = false, error = err))
                }
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun sendWedge(barcode: String, enter: Boolean = true): Result<Boolean> = withContext(Dispatchers.IO) {
        try {
            if (baseUrl.isEmpty()) {
                return@withContext Result.failure(IllegalStateException("Tiada sambungan ke PC"))
            }
            val json = JSONObject().apply {
                put("barcode", barcode.trim())
                put("enter", enter)
            }
            val req = buildRequest("/api/wedge", method = "POST", jsonBody = json)
            client.newCall(req).execute().use { response ->
                val bodyStr = response.body?.string().orEmpty()
                val resObj = try { JSONObject(bodyStr) } catch (_: Exception) { JSONObject() }
                Result.success(response.isSuccessful && resObj.optBoolean("ok", false))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun lookupIsbn(isbn: String): Result<IsbnLookupResult> = withContext(Dispatchers.IO) {
        try {
            if (baseUrl.isEmpty()) {
                return@withContext Result.failure(IllegalStateException("Tiada sambungan ke PC"))
            }
            val enc = URLEncoder.encode(isbn.trim(), "UTF-8")
            val req = buildRequest("/api/lookup/isbn?isbn=$enc")
            client.newCall(req).execute().use { response ->
                val bodyStr = response.body?.string().orEmpty()
                val resObj = try { JSONObject(bodyStr) } catch (_: Exception) { JSONObject() }
                Result.success(IsbnLookupResult.fromJson(resObj))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun saveItem(book: BookItem): Result<Boolean> = withContext(Dispatchers.IO) {
        try {
            if (baseUrl.isEmpty()) {
                return@withContext Result.failure(IllegalStateException("Tiada sambungan ke PC"))
            }
            val json = book.toJson()
            val req = buildRequest("/api/item", method = "POST", jsonBody = json)
            client.newCall(req).execute().use { response ->
                val bodyStr = response.body?.string().orEmpty()
                val resObj = try { JSONObject(bodyStr) } catch (_: Exception) { JSONObject() }
                if (response.isSuccessful && resObj.optBoolean("ok", false)) {
                    Result.success(true)
                } else {
                    val err = resObj.optString("error", "Gagal menyimpan item ke katalog (HTTP ${response.code})")
                    Result.failure(Exception(err))
                }
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun saveItemToGitHub(repo: String, pat: String, book: BookItem): Result<Boolean> = withContext(Dispatchers.IO) {
        try {
            if (repo.isBlank() || pat.isBlank()) {
                return@withContext Result.failure(IllegalStateException("Tetapan GitHub tidak lengkap"))
            }

            val cleanRepo = repo.trim().removePrefix("https://github.com/").trim('/')
            val getUrl = "https://api.github.com/repos/$cleanRepo/contents/catalog.json"

            // 1. Fetch current catalog.json to get sha and existing list
            val getReq = Request.Builder()
                .url(getUrl)
                .header("Authorization", "Bearer $pat")
                .header("Accept", "application/vnd.github.v3+json")
                .header("User-Agent", "ErrinILS-Scanner")
                .build()

            var sha: String? = null
            val existingCatalog = JSONArray()

            client.newCall(getReq).execute().use { response ->
                if (response.isSuccessful) {
                    val bodyStr = response.body?.string().orEmpty()
                    val fileObj = JSONObject(bodyStr)
                    sha = fileObj.optString("sha", null)
                    val encodedContent = fileObj.optString("content", "").replace("\n", "")
                    if (encodedContent.isNotEmpty()) {
                        val decodedBytes = android.util.Base64.decode(encodedContent, android.util.Base64.DEFAULT)
                        val catalogArray = JSONArray(String(decodedBytes, Charsets.UTF_8))
                        for (i in 0 until catalogArray.length()) {
                            existingCatalog.put(catalogArray.getJSONObject(i))
                        }
                    }
                }
            }

            // 2. Append new book
            existingCatalog.put(book.toJson())

            // 3. Put updated catalog.json
            val newJsonBytes = existingCatalog.toString(2).toByteArray(Charsets.UTF_8)
            val base64Content = android.util.Base64.encodeToString(newJsonBytes, android.util.Base64.NO_WRAP)

            val putPayload = JSONObject().apply {
                put("message", "feat(catalog): add '${book.title}' (${book.barcode}) via Android Scanner")
                put("content", base64Content)
                if (sha != null) put("sha", sha)
            }

            val putReq = Request.Builder()
                .url(getUrl)
                .header("Authorization", "Bearer $pat")
                .header("Accept", "application/vnd.github.v3+json")
                .header("User-Agent", "ErrinILS-Scanner")
                .put(putPayload.toString().toRequestBody(jsonMedia))
                .build()

            client.newCall(putReq).execute().use { response ->
                if (response.isSuccessful) {
                    Result.success(true)
                } else {
                    val body = response.body?.string().orEmpty()
                    Result.failure(Exception("GitHub API Error (${response.code}): $body"))
                }
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }
}
