using Bakabase.Infrastructures.Components.Gui;
using Bakabase.Modules.ThirdParty.Abstractions.Http.Cookie;
using Bakabase.Modules.ThirdParty.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.CookieCapture;

/// <summary>
/// The string the sign-in window presents itself as.
/// </summary>
/// <remarks>
/// It is written in one place and read in two: the platform host sets it on the browser
/// control, and every request made with the resulting cookie sends it again. A site that
/// gave a session to one browser will refuse a request that presents it as another, so a
/// mismatch shows up as a sign-in that works and then quietly stops working.
/// </remarks>
[TestClass]
public class WebViewUserAgentTests
{
    [TestMethod]
    public void Every_platform_string_is_a_browser_a_site_will_recognise()
    {
        foreach (var agent in new[] {WebViewUserAgent.Windows, WebViewUserAgent.MacOS, WebViewUserAgent.Linux})
        {
            StringAssert.StartsWith(agent, "Mozilla/5.0");
            StringAssert.Contains(agent, "Chrome/");

            // Which matters twice: the TLS fingerprint a later request uses is inferred
            // from this string, and an unrecognised one falls back to a preset that does
            // not match the browser the site saw.
            Assert.AreNotEqual(TlsPresetHelper.DefaultPreset, TlsPresetHelper.InferPresetFromUserAgent(agent),
                "the preset should be inferred from the agent, not fallen back to");
        }
    }

    [TestMethod]
    public void The_platform_answer_is_one_of_the_three()
    {
        CollectionAssert.Contains(new[] {WebViewUserAgent.Windows, WebViewUserAgent.MacOS, WebViewUserAgent.Linux},
            WebViewUserAgent.ForThisPlatform);
    }

    [TestMethod]
    public void A_capture_keeps_the_agent_it_was_given()
    {
        // The window may be on a different machine from the one storing the cookie — that
        // is the whole point of the client running this flow — so the agent travels with
        // the cookie rather than being inferred where it lands.
        var captured = CookieCaptureResult.For("a=1", WebViewUserAgent.MacOS);

        Assert.AreEqual(WebViewUserAgent.MacOS, captured.UserAgent);
        Assert.AreEqual(TlsPresetHelper.InferPresetFromUserAgent(WebViewUserAgent.MacOS), captured.TlsPreset);
    }

    [TestMethod]
    public void A_capture_with_nothing_to_go_on_falls_back_to_this_platform()
    {
        foreach (var missing in new string?[] {null, "", "   "})
        {
            Assert.AreEqual(WebViewUserAgent.ForThisPlatform, CookieCaptureResult.For("a=1", missing).UserAgent);
        }
    }
}
