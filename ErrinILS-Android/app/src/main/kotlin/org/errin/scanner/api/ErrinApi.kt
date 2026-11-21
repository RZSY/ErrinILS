package org.errin.scanner.api

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.net.URLEncoder
import java.util.concurrent.TimeUnit

class ErrinApi(
    private var baseUrl: String = "",
    private var token: String = ""
) {
    private val jsonMedia = "application/json; charset=utf-8".toMediaType()

    private val client = OkHttpClient.Builder()
        .connectTimeout(8, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .writeTimeout(15, TimeUnit.SECONDS)
        .build()

    fun updateCredentials(url: String, newToken: String) {
        baseUrl = cleanUrl(url)
        token = newToken
    }

    fun getBaseUrl(): String = baseUrl
    fun getToken(): String = token

    private fun cleanUrl(raw: String): String {
        var u = raw.trim()
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
                    val err = resObj.optString("error", "Pairing failed (HTTP ${response.code})")
                    Result.success(PairResult(ok = false, error = err))
                }
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun verifyConnection(): Result<MeResult> = withContext(Dispatchers.IO) {
        try {
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
                    val err = json.optString("error", "Unauthorized or server error (${response.code})")
                    Result.success(MeResult(ok = false, error = err))
                }
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun returnBook(barcode: String): Result<CirculationResult> = withContext(Dispatchers.IO) {
        try {
            val json = JSONObject().apply { put("barcode", barcode.trim()) }
            val req = buildRequest("/api/return", method = "POST", jsonBody = json)
            client.newCall(req).execute().use { response ->
                val bodyStr = response.body?.string().orEmpty()
                val resObj = try { JSONObject(bodyStr) } catch (_: Exception) { JSONObject() }
                val ok = response.isSuccessful && resObj.optBoolean("ok", false)
                val msg = resObj.optString("message", "")
                val err = if (resObj.has("error")) resObj.optString("error") else null
                val friendly = if (resObj.has("friendly")) resObj.optString("friendly") else null

                // Check for overdue fines mentioned in message
                var isOverdue = false
                var fine = 0.0
                if (msg.contains("overdue", ignoreCase = true) || msg.contains("denda", ignoreCase = true)) {
                    isOverdue = true
                }

                Result.success(
                    CirculationResult(
                        ok = ok,
                        message = msg,
                        error = err,
                        friendly = friendly,
                        isOverdue = isOverdue,
                        fineAmount = fine
                    )
                )
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun checkoutBook(patronId: String, barcode: String): Result<CirculationResult> = withContext(Dispatchers.IO) {
        try {
            val json = JSONObject().apply {
                put("patron", patronId.trim())
                put("barcode", barcode.trim())
            }
            val req = buildRequest("/api/checkout", method = "POST", jsonBody = json)
            client.newCall(req).execute().use { response ->
                val bodyStr = response.body?.string().orEmpty()
                val resObj = try { JSONObject(bodyStr) } catch (_: Exception) { JSONObject() }
                val ok = response.isSuccessful && resObj.optBoolean("ok", false)
                val msg = resObj.optString("message", "")
                val err = if (resObj.has("error")) resObj.optString("error") else null
                val friendly = if (resObj.has("friendly")) resObj.optString("friendly") else null

                Result.success(
                    CirculationResult(
                        ok = ok,
                        message = msg,
                        error = err,
                        friendly = friendly
                    )
                )
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun renewBook(barcode: String): Result<CirculationResult> = withContext(Dispatchers.IO) {
        try {
            val json = JSONObject().apply { put("barcode", barcode.trim()) }
            val req = buildRequest("/api/renew", method = "POST", jsonBody = json)
            client.newCall(req).execute().use { response ->
                val bodyStr = response.body?.string().orEmpty()
                val resObj = try { JSONObject(bodyStr) } catch (_: Exception) { JSONObject() }
                val ok = response.isSuccessful && resObj.optBoolean("ok", false)
                val msg = resObj.optString("message", "")
                val err = if (resObj.has("error")) resObj.optString("error") else null
                val friendly = if (resObj.has("friendly")) resObj.optString("friendly") else null

                Result.success(
                    CirculationResult(
                        ok = ok,
                        message = msg,
                        error = err,
                        friendly = friendly
                    )
                )
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun getItem(barcodeOrId: String): Result<BookItem?> = withContext(Dispatchers.IO) {
        try {
            val enc = URLEncoder.encode(barcodeOrId.trim(), "UTF-8")
            val req = buildRequest("/api/item?barcode=$enc")
            client.newCall(req).execute().use { response ->
                if (!response.isSuccessful) {
                    return@withContext Result.success(null)
                }
                val bodyStr = response.body?.string().orEmpty()
                val resObj = JSONObject(bodyStr)
                if (resObj.optBoolean("ok", false) && resObj.has("item")) {
                    val itemObj = resObj.getJSONObject("item")
                    Result.success(BookItem.fromJson(itemObj))
                } else {
                    Result.success(null)
                }
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun getPatron(patronId: String): Result<PatronSummary?> = withContext(Dispatchers.IO) {
        try {
            val enc = URLEncoder.encode(patronId.trim(), "UTF-8")
            val req = buildRequest("/api/patron?id=$enc")
            client.newCall(req).execute().use { response ->
                if (!response.isSuccessful) {
                    return@withContext Result.success(null)
                }
                val bodyStr = response.body?.string().orEmpty()
                val resObj = JSONObject(bodyStr)
                if (resObj.optBoolean("ok", false) && resObj.has("patron")) {
                    val patronObj = resObj.getJSONObject("patron")
                    Result.success(PatronSummary.fromJson(patronObj))
                } else {
                    Result.success(null)
                }
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun lookupIsbn(isbn: String): Result<IsbnLookupResult> = withContext(Dispatchers.IO) {
        try {
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

    suspend fun saveBook(book: BookItem): Result<Boolean> = withContext(Dispatchers.IO) {
        try {
            val json = JSONObject().apply {
                put("barcode", book.barcode)
                put("title", book.title)
                put("author", book.author)
                put("isbn", book.isbn)
                put("pub", book.publisher)
                put("year", book.year)
                put("call", book.call)
                put("subject", book.subject)
            }
            val req = buildRequest("/api/item", method = "POST", jsonBody = json)
            client.newCall(req).execute().use { response ->
                val bodyStr = response.body?.string().orEmpty()
                val resObj = try { JSONObject(bodyStr) } catch (_: Exception) { JSONObject() }
                Result.success(response.isSuccessful && resObj.optBoolean("ok", false))
            }
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun sendWedge(barcode: String, enter: Boolean = true): Result<Boolean> = withContext(Dispatchers.IO) {
        try {
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
}
