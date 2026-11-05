// Zero-dependency TLS/SSL X.509 certificate generator and loader for secure remote access.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Errin
{
    public static class CertHelper
    {
        const uint PROV_RSA_FULL = 1;
        const uint CRYPT_NEWKEYSET = 0x08;
        const uint CRYPT_DELETEKEYSET = 0x10;
        const uint AT_SIGNATURE = 2;
        const uint CRYPT_EXPORTABLE = 1;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool CryptAcquireContext(out IntPtr hProv, string pszContainer, string pszProvider, uint dwProvType, uint dwFlags);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool CryptGenKey(IntPtr hProv, uint Algid, uint dwFlags, out IntPtr phKey);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool CryptDestroyKey(IntPtr hKey);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool CryptReleaseContext(IntPtr hProv, uint dwFlags);

        [DllImport("crypt32.dll", SetLastError = true)]
        static extern IntPtr CertCreateSelfSignCertificate(
            IntPtr hCryptProvOrNDOption,
            [In] ref CRYPTOAPI_BLOB pSubjectIssuerBlob,
            uint dwFlags,
            IntPtr pKeyProvInfo,
            IntPtr pSignatureAlgorithm,
            IntPtr pStartTime,
            IntPtr pEndTime,
            IntPtr pExtensions);

        [StructLayout(LayoutKind.Sequential)]
        struct CRYPTOAPI_BLOB
        {
            public uint cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true)]
        static extern bool CertStrToName(
            uint dwCertEncodingType,
            string pszX500,
            uint dwStrType,
            IntPtr pvReserved,
            [In, Out] byte[] pbEncoded,
            ref uint pcbEncoded,
            IntPtr pszError);

        /// <summary>Generates a self-signed RSA 2048-bit certificate using standard Windows CryptoAPI.</summary>
        public static X509Certificate2 GenerateSelfSigned(string subject, int years, string password)
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                throw new PlatformNotSupportedException("Automatic self-signed cert generation is supported on Windows. On Linux/macOS, provide a .pfx file.");

            string container = "ErrinILS_" + Guid.NewGuid().ToString("N");
            IntPtr hProv = IntPtr.Zero;
            IntPtr hKey = IntPtr.Zero;
            try
            {
                if (!CryptAcquireContext(out hProv, container, null, PROV_RSA_FULL, CRYPT_NEWKEYSET))
                    throw new Exception("CryptAcquireContext failed: " + Marshal.GetLastWin32Error());

                uint keyFlags = (2048 << 16) | CRYPT_EXPORTABLE;
                if (!CryptGenKey(hProv, AT_SIGNATURE, keyFlags, out hKey))
                    throw new Exception("CryptGenKey failed: " + Marshal.GetLastWin32Error());

                uint cbEncoded = 0;
                CertStrToName(0x00010001, subject, 3, IntPtr.Zero, null, ref cbEncoded, IntPtr.Zero);
                byte[] encoded = new byte[cbEncoded];
                CertStrToName(0x00010001, subject, 3, IntPtr.Zero, encoded, ref cbEncoded, IntPtr.Zero);

                GCHandle handle = GCHandle.Alloc(encoded, GCHandleType.Pinned);
                try
                {
                    var blob = new CRYPTOAPI_BLOB();
                    blob.cbData = cbEncoded;
                    blob.pbData = handle.AddrOfPinnedObject();

                    IntPtr certContext = CertCreateSelfSignCertificate(hProv, ref blob, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    if (certContext == IntPtr.Zero)
                        throw new Exception("CertCreateSelfSignCertificate failed: " + Marshal.GetLastWin32Error());

                    var cert = new X509Certificate2(certContext);
                    byte[] pfx = cert.Export(X509ContentType.Pfx, password ?? "errin");
                    return new X509Certificate2(pfx, password ?? "errin", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                }
                finally
                {
                    handle.Free();
                }
            }
            finally
            {
                if (hKey != IntPtr.Zero) CryptDestroyKey(hKey);
                if (hProv != IntPtr.Zero) CryptReleaseContext(hProv, 0);
                IntPtr hClean;
                CryptAcquireContext(out hClean, container, null, PROV_RSA_FULL, CRYPT_DELETEKEYSET);
            }
        }

        /// <summary>Loads or generates a certificate for remote access.</summary>
        public static X509Certificate2 GetOrCreateCertificate(string customPath, string customPass, string appDataDir)
        {
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            {
                return new X509Certificate2(customPath, customPass ?? "", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            }

            string autoPfx = Path.Combine(appDataDir, "errin-ssl.pfx");
            if (File.Exists(autoPfx))
            {
                try
                {
                    return new X509Certificate2(autoPfx, "errin", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                }
                catch { }
            }

            // Generate a fresh 10-year self-signed certificate
            var cert = GenerateSelfSigned("CN=ErrinILS", 10, "errin");
            try
            {
                Directory.CreateDirectory(appDataDir);
                byte[] pfxBytes = cert.Export(X509ContentType.Pfx, "errin");
                File.WriteAllBytes(autoPfx, pfxBytes);
            }
            catch { }
            return cert;
        }

        /// <summary>Exports public certificate (.cer) so client devices can trust it.</summary>
        public static void ExportCer(X509Certificate2 cert, string outPath)
        {
            byte[] cer = cert.Export(X509ContentType.Cert);
            File.WriteAllBytes(outPath, cer);
        }

        public static string GetSummary(X509Certificate2 cert)
        {
            if (cert == null) return "No certificate";
            return string.Format("{0} \u00B7 Expires {1} \u00B7 Thumbprint {2}...",
                cert.Subject,
                cert.NotAfter.ToString("yyyy-MM-dd"),
                cert.Thumbprint != null && cert.Thumbprint.Length > 8 ? cert.Thumbprint.Substring(0, 8) : cert.Thumbprint);
        }
    }
}
