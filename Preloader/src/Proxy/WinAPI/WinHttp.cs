using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AsyncLoggers.Proxy.WinAPI;

internal static class WinHttp
{
    private const uint WINHTTP_FLAG_SECURE = 0x00800000U;

    [DllImport("winhttp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr WinHttpOpen(
        string pwszUserAgent,
        uint dwAccessType,
        IntPtr pwszProxyName,
        IntPtr pwszProxyBypass,
        uint dwFlags);

    [DllImport("winhttp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr WinHttpConnect(
        IntPtr hSession,
        string pswzServerName,
        ushort nServerPort,
        uint dwReserved);

    [DllImport("winhttp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr WinHttpOpenRequest(
        IntPtr hConnect,
        string pwszVerb,
        string pwszObjectName,
        string pwszVersion,
        string pwszReferrer,
        IntPtr ppwszAcceptTypes,
        uint dwFlags);

    [DllImport("winhttp.dll", SetLastError = true)]
    private static extern bool WinHttpSendRequest(
        IntPtr hRequest,
        string pwszHeaders,
        uint dwHeadersLength,
        IntPtr lpOptional,
        uint dwOptionalLength,
        uint dwTotalLength,
        IntPtr dwContext);

    [DllImport("winhttp.dll", SetLastError = true)]
    private static extern bool WinHttpReceiveResponse(
        IntPtr hRequest,
        IntPtr lpReserved);

    [DllImport("winhttp.dll", SetLastError = true)]
    private static extern bool WinHttpReadData(
        IntPtr hRequest,
        byte[] lpBuffer,
        uint dwNumberOfBytesToRead,
        out uint lpdwNumberOfBytesRead);

    [DllImport("winhttp.dll", SetLastError = true)]
    private static extern bool WinHttpCloseHandle(IntPtr hInternet);

    private static void DownloadToStream(string url, Stream destination)
    {
        var uri = new Uri(url);

        IntPtr hSession = IntPtr.Zero, hConnect = IntPtr.Zero, hRequest = IntPtr.Zero;
        try
        {
            hSession = WinHttpOpen("NativeWinHttp/1.0", 1, IntPtr.Zero, IntPtr.Zero, 0);
            if (hSession == IntPtr.Zero)
                throw new Exception("WinHttpOpen failed");

            hConnect = WinHttpConnect(hSession, uri.Host, (ushort)uri.Port, 0);
            if (hConnect == IntPtr.Zero)
                throw new Exception("WinHttpConnect failed");

            var flags = uri.Scheme == "https" ? WINHTTP_FLAG_SECURE : 0U;
            hRequest = WinHttpOpenRequest(hConnect, "GET", uri.PathAndQuery, null, null, IntPtr.Zero, flags);
            if (hRequest == IntPtr.Zero)
                throw new Exception("WinHttpOpenRequest failed");

            if (!WinHttpSendRequest(hRequest, null, 0, IntPtr.Zero, 0, 0, IntPtr.Zero))
                throw new Exception("WinHttpSendRequest failed");

            if (!WinHttpReceiveResponse(hRequest, IntPtr.Zero))
                throw new Exception("WinHttpReceiveResponse failed");

            byte[] buffer = new byte[16 * 1024];
            while (true)
            {
                if (!WinHttpReadData(hRequest, buffer, (uint)buffer.Length, out uint bytesRead))
                    throw new Exception("WinHttpReadData failed");

                if (bytesRead == 0) break;

                destination.Write(buffer, 0, (int)bytesRead);
            }
        }
        finally
        {
            if (hRequest != IntPtr.Zero) WinHttpCloseHandle(hRequest);
            if (hConnect != IntPtr.Zero) WinHttpCloseHandle(hConnect);
            if (hSession != IntPtr.Zero) WinHttpCloseHandle(hSession);
        }
    }

    internal static void DownloadFile(string url, string filePath)
    {
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            DownloadToStream(url, fs);
        }
    }

    internal static string DownloadAsText(string url)
    {
        using (var ms = new MemoryStream())
        {
            DownloadToStream(url, ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}