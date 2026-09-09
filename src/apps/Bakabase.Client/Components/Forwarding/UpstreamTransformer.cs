using Bakabase.Client.Components.Connection;
using Bakabase.Modules.RemoteAccess.Components.Pairing;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace Bakabase.Client.Components.Forwarding;

/// <summary>
/// Turns a request the local browser made into one the server will accept.
/// </summary>
/// <remarks>
/// <para>
/// Signing happens here rather than in a message handler because the signature covers
/// the path and query, and YARP only settles those while building the outgoing request.
/// Signing earlier would sign a URL that no longer matches what goes on the wire.
/// </para>
/// <para>
/// Anything the caller sent as <c>Authorization</c> is dropped first. A page in the
/// user's browser can put whatever it likes in that header, and forwarding it would let
/// it choose which device the server thinks is calling.
/// </para>
/// </remarks>
public sealed class UpstreamTransformer(IClientCredentialProvider credentials, ServerClock clock) : HttpTransformer
{
    public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest,
        string destinationPrefix, CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

        // The upstream decides who we are from the signature alone; a Host of
        // 127.0.0.1 would just be a lie it has no use for.
        proxyRequest.Headers.Host = null;
        proxyRequest.Headers.Authorization = null;

        var current = credentials.Current;
        if (current == null)
        {
            // Not paired. The request still goes: a server that does not require
            // pairing serves anonymous callers, and the pairing handshake itself has
            // nothing to sign with yet.
            return;
        }

        byte[] key;
        try
        {
            key = RemoteRequestSignature.FromBase64Url(current.Key);
        }
        catch (FormatException)
        {
            // A stored key that will not decode cannot sign. Going out anonymous
            // produces a refusal the user can act on rather than an unexplained crash
            // inside the proxy pipeline.
            return;
        }

        await UpstreamRequestSigner.SignAsync(proxyRequest, current.DeviceId, key, clock.NowUnixSeconds,
            ct: cancellationToken);
    }
}
