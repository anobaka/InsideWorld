using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Infrastructures.Components.Gui;

namespace Bakabase.Modules.ThirdParty.Abstractions.Http.Cookie;

/// <summary>
/// Drives a cookie-capture flow against an <see cref="IWebViewSession"/>:
///
/// <list type="number">
///     <item>Pre-init: wipe stale cookies (e.g. exhentai's `yay=louder` Sad Panda flag).</item>
///     <item>Subscribe to Navigated: walk the chain rules in <paramref name="flow"/>, deduping
///           so each unique target is hit at most once. Before each chain hop, re-clear stale
///           cookies and run cross-domain cookie mirrors.</item>
///     <item>Open <see cref="ICookieCaptureFlow.StartUrl"/>.</item>
///     <item>Wait for the user to click Confirm. Cancel/window-close throws
///           <see cref="OperationCanceledException"/>, which surfaces as a null result.</item>
///     <item>Extract cookies from <see cref="ICookieCaptureFlow.CookieUrls"/>.</item>
/// </list>
///
/// All chain / mirror / stale policy lives here — the GUI adapter is policy-free, the
/// flow is a pure declarative spec.
/// </summary>
public class CookieCaptureOrchestrator(IGuiAdapter guiAdapter, ICookieCaptureLocalizer localizer)
{
    public async Task<string?> CaptureAsync(ICookieCaptureFlow flow, CancellationToken cancellationToken = default)
    {
        var options = new WebViewSessionOptions
        {
            Title = localizer.LoginTo(flow.PlatformName),
            ConfirmButtonText = localizer.Confirm,
            CancelButtonText = localizer.Cancel,
            InitialStatusText = localizer.WaitingForLogin,
            InitialUrl = flow.StartUrl,
        };

        await using var session = guiAdapter.CreateWebViewSession(options);

        // Pre-init: wipe stale markers before any navigation runs. Idempotent if the
        // cookie isn't there yet.
        foreach (var (url, name) in flow.StaleCookiesToClear)
        {
            await session.DeleteCookieAsync(url, name);
        }

        // Chain navigation: each unique target gets navigated to at most once.
        var chainTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        session.OnNavigated(url => HandleNavigatedAsync(session, flow, url, chainTargets));

        await session.NavigateAsync(flow.StartUrl);

        try
        {
            await session.WaitForUserConfirmAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        session.SetStatusText(localizer.ExtractingCookies);
        return await session.GetCookiesAsync(flow.CookieUrls);
    }

    private static async Task HandleNavigatedAsync(
        IWebViewSession session,
        ICookieCaptureFlow flow,
        string url,
        HashSet<string> chainTargets)
    {
        var nextUrl = flow.GetNextUrl(url);
        if (string.IsNullOrEmpty(nextUrl)) return;
        if (string.Equals(nextUrl, url, StringComparison.OrdinalIgnoreCase)) return;
        if (!chainTargets.Add(nextUrl)) return;

        // Defense in depth: re-clear stale markers right before each hop. Some servers
        // re-issue blocking markers on every visit (exhentai's yay=louder is the canonical
        // case), so a one-shot pre-init clear isn't enough.
        foreach (var (u, n) in flow.StaleCookiesToClear)
        {
            await session.DeleteCookieAsync(u, n);
        }

        // Mirror cross-domain auth cookies (e.g. .e-hentai.org's ipb_*/igneous onto
        // .exhentai.org so exhentai's auth check finds them on its own domain).
        foreach (var mirror in flow.CookieMirrors)
        {
            await session.MirrorCookiesAsync(mirror.SourceUrl, mirror.TargetDomain, mirror.CookieNames);
        }

        await session.NavigateAsync(nextUrl);
    }
}

/// <summary>
/// The five strings the login window shows.
/// </summary>
/// <remarks>
/// A port rather than the application's own localizer, so the orchestration can run in
/// any process that can put a window on the user's screen — which, once the client is a
/// separate program, is not only the one holding the database.
/// </remarks>
public interface ICookieCaptureLocalizer
{
    string LoginTo(string platformName);
    string Confirm { get; }
    string Cancel { get; }
    string WaitingForLogin { get; }
    string ExtractingCookies { get; }

    /// <summary>Shown when the user closes the window instead of signing in.</summary>
    string Cancelled { get; }
}
