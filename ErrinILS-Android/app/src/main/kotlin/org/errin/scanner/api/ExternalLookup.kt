package org.errin.scanner.api

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.util.concurrent.TimeUnit

object ExternalLookup {

    private val client = OkHttpClient.Builder()
        .connectTimeout(6, TimeUnit.SECONDS)
        .readTimeout(8, TimeUnit.SECONDS)
        .build()

    suspend fun lookupIsbnOnline(isbnRaw: String): IsbnLookupResult = withContext(Dispatchers.IO) {
        val clean = isbnRaw.replace(Regex("[^0-9X]"), "")
        if (clean.length < 10) {
            return@withContext IsbnLookupResult(ok = false, error = "ISBN tidak sah: $isbnRaw")
        }

        // 1. Try Google Books API
        try {
            val gUrl = "https://www.googleapis.com/books/v1/volumes?q=isbn:$clean"
            val req = Request.Builder().url(gUrl).header("User-Agent", "ErrinILS-Scanner").build()
            client.newCall(req).execute().use { response ->
                if (response.isSuccessful) {
                    val body = response.body?.string().orEmpty()
                    val json = JSONObject(body)
                    val total = json.optInt("totalItems", 0)
                    if (total > 0) {
                        val item = json.getJSONArray("items").getJSONObject(0)
                        val vi = item.getJSONObject("volumeInfo")
                        val title = vi.optString("title", "")
                        val authorsArr = vi.optJSONArray("authors")
                        val author = if (authorsArr != null && authorsArr.length() > 0) authorsArr.getString(0) else ""
                        val publisher = vi.optString("publisher", "")
                        val publishedDate = vi.optString("publishedDate", "")
                        val year = if (publishedDate.length >= 4) publishedDate.substring(0, 4) else publishedDate
                        val cats = vi.optJSONArray("categories")
                        val subject = if (cats != null && cats.length() > 0) cats.getString(0) else ""

                        if (title.isNotEmpty()) {
                            return@withContext IsbnLookupResult(
                                ok = true,
                                found = true,
                                title = title,
                                author = author,
                                publisher = publisher,
                                year = year,
                                isbn = clean,
                                subject = subject,
                                source = "Google Books"
                            )
                        }
                    }
                }
            }
        } catch (_: Exception) {}

        // 2. Try OpenLibrary API
        try {
            val olUrl = "https://openlibrary.org/api/books?bibkeys=ISBN:$clean&format=json&jscmd=data"
            val req = Request.Builder().url(olUrl).header("User-Agent", "ErrinILS-Scanner").build()
            client.newCall(req).execute().use { response ->
                if (response.isSuccessful) {
                    val body = response.body?.string().orEmpty()
                    val json = JSONObject(body)
                    val key = "ISBN:$clean"
                    if (json.has(key)) {
                        val book = json.getJSONObject(key)
                        val title = book.optString("title", "")
                        val authArr = book.optJSONArray("authors")
                        val author = if (authArr != null && authArr.length() > 0) authArr.getJSONObject(0).optString("name", "") else ""
                        val pubArr = book.optJSONArray("publishers")
                        val publisher = if (pubArr != null && pubArr.length() > 0) pubArr.getJSONObject(0).optString("name", "") else ""
                        val year = book.optString("publish_date", "")

                        return@withContext IsbnLookupResult(
                            ok = true,
                            found = true,
                            title = title,
                            author = author,
                            publisher = publisher,
                            year = year,
                            isbn = clean,
                            source = "OpenLibrary"
                        )
                    }
                }
            }
        } catch (_: Exception) {}

        IsbnLookupResult(ok = true, found = false, error = "Tiada rekod bibliografi dijumpai untuk ISBN: $clean")
    }
}
