using Bakabase.Infrastructures.Components.Gui;
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
    /// Builds a result for a cookie a window just produced.
    /// </summary>
    /// <param name="userAgent">
    /// What that window presented itself as — <see cref="IWebViewSession.UserAgent"/>.
    /// Falls back to the platform default for a caller with no session to ask, which is
    /// the same string every platform host sets.
    /// </param>
    public static CookieCaptureResult For(string cookie, string? userAgent = null)
    {
        var agent = string.IsNullOrWhiteSpace(userAgent) ? WebViewUserAgent.ForThisPlatform : userAgent;

        return new CookieCaptureResult
        {
            Cookie = cookie,
            UserAgent = agent,
            TlsPreset = TlsPresetHelper.InferPresetFromUserAgent(agent)
        };
    }
}
