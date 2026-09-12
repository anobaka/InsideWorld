using Bakabase.Abstractions.Components.Localization;
using Bakabase.Modules.ThirdParty.Abstractions.Http.Cookie;

namespace Bakabase.Service.Components;

/// <summary>
/// The all-in-one's five strings, from its own resources.
/// </summary>
public class BakabaseCookieCaptureLocalizer(IBakabaseLocalizer localizer) : ICookieCaptureLocalizer
{
    public string LoginTo(string platformName) => localizer["CookieCapture_LoginTo", platformName];
    public string Confirm => localizer["CookieCapture_Confirm"];
    public string Cancel => localizer["CookieCapture_Cancel"];
    public string WaitingForLogin => localizer["CookieCapture_WaitingForLogin"];
    public string ExtractingCookies => localizer["CookieCapture_ExtractingCookies"];
    public string Cancelled => localizer["CookieCapture_Cancelled"];
}
