using Bakabase.Modules.ThirdParty.Helpers;

namespace Bakabase.Modules.ThirdParty.Abstractions.Http.Cookie;

/// <summary>
/// What a capture produced: the cookie, plus how the window that got it identified itself.
/// </summary>
/// <remarks>
/// The user agent and TLS preset travel with the cookie because a site that handed it out
/// to one browser will often refuse a request that presents it as another. They describe
/// the machine the window opened on, which is why this is built where the capture ran
/// rather than where the cookie is stored.
/// </remarks>
public class CookieCaptureResult
{
    public string Cookie { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? TlsPreset { get; set; }

    /// <summary>
    /// What the embedded browser presents itself as. It is the host OS's Chrome string,
    /// not the running browser's — the WebView is Chromium everywhere, and the platform is
    /// the part sites key off.
    /// </summary>
    public static string WebViewUserAgent =>
        OperatingSystem.IsMacOS()
            ? "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36"
            : OperatingSystem.IsLinux()
                ? "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36"
                : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

    public static CookieCaptureResult For(string cookie) => new()
    {
        Cookie = cookie,
        UserAgent = WebViewUserAgent,
        TlsPreset = TlsPresetHelper.InferPresetFromUserAgent(WebViewUserAgent)
    };
}
