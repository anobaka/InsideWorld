using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Modules.RemoteAccess.Abstractions.Models;
using Bakabase.Modules.RemoteAccess.Abstractions.Services;
using Bakabase.Modules.RemoteAccess.Components.Pairing;
using Bakabase.Service.Components.RemoteAccess;
using Bakabase.Service.Models.Input;
using Bakabase.Service.Models.View;
using Bootstrap.Components.Miscellaneous.ResponseBuilders;
using Bootstrap.Models.Constants;
using Bootstrap.Models.ResponseModels;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Bakabase.Service.Controllers
{
    /// <summary>
    /// Status and settings for using Bakabase from a device other than the host.
    /// </summary>
    /// <remarks>
    /// The pairing endpoints split into two groups, and the split is what keeps them
    /// safe. Everything under <c>pair/</c> is reachable without credentials — a device
    /// with no key has to be able to get one — and each of those carries its secret in
    /// the body rather than the URL. Everything under <c>pairing/</c> and
    /// <c>devices/</c> is management, so it is left unmarked and only the host or an
    /// already-paired device reaches it.
    /// </remarks>
    [Route("~/remote-access")]
    public class RemoteAccessController(
        IRemoteAccessService remoteAccessService,
        IRemoteDeviceService deviceService,
        RemoteConnectionRegistry connections) : Controller
    {
        /// <summary>
        /// Recorded as the approver when the approval came from the host itself rather
        /// than from another device. Distinct from null, which means "paired with a
        /// code, nobody approved it".
        /// </summary>
        private const string HostApproverId = "host";

        /// <summary>
        /// Tells a client which side of the gate it is on. Called by the SPA at
        /// startup so it knows whether to offer host-only actions (launching a player,
        /// opening a folder) or route playback into the browser instead.
        /// </summary>
        [HttpGet("context")]
        [SwaggerOperation(OperationId = "GetRemoteAccessContext")]
        [RemoteAccessible]
        public SingletonResponse<RemoteAccessClientContextViewModel> GetContext()
        {
            var context = HttpContext.GetRemoteAccessContext();

            return new SingletonResponse<RemoteAccessClientContextViewModel>(
                new RemoteAccessClientContextViewModel
                {
                    IsLocal = context?.IsLoopback ?? true,
                    Mode = remoteAccessService.GetEffectiveMode(),
                    Paired = context?.IsPaired ?? false,
                    DeviceId = context?.Device?.Id,
                    DeviceName = context?.Device?.Name
                });
        }

        /// <summary>
        /// Who this server is — same facts as the discovery beacon broadcasts, so a
        /// client that typed an address by hand still learns the install's identity,
        /// name and protocol version before talking further.
        /// </summary>
        [HttpGet("server-info")]
        [SwaggerOperation(OperationId = "GetRemoteAccessServerInfo")]
        [RemoteAccessible]
        public async Task<SingletonResponse<RemoteAccessServerInfoViewModel>> GetServerInfo()
        {
            var descriptor = await remoteAccessService.GetServerDescriptorAsync();

            return new SingletonResponse<RemoteAccessServerInfoViewModel>(new RemoteAccessServerInfoViewModel
            {
                Id = descriptor.Id,
                Name = descriptor.Name,
                AppVersion = descriptor.AppVersion,
                ProtocolVersion = descriptor.ProtocolVersion,
                Mode = remoteAccessService.GetEffectiveMode(),
                PairingSupported = true,
                ServerTime = DateTime.UtcNow
            });
        }

        /// <summary>
        /// The current mode plus the addresses another device can open. Host-only:
        /// this is the page remote access is configured from.
        /// </summary>
        [HttpGet("settings")]
        [SwaggerOperation(OperationId = "GetRemoteAccessSettings")]
        public SingletonResponse<RemoteAccessSettingsViewModel> GetSettings()
        {
            var code = deviceService.GetPairingCodeStatus();

            return new SingletonResponse<RemoteAccessSettingsViewModel>(new RemoteAccessSettingsViewModel
            {
                Mode = remoteAccessService.GetEffectiveMode(),
                Addresses = remoteAccessService.GetReachableAddresses()
                    .Select(a => new RemoteAccessAddressViewModel {Url = a.Url, InterfaceName = a.InterfaceName})
                    .ToList(),
                AllowLiveTranscode = remoteAccessService.GetAllowLiveTranscode(),
                RequirePairing = remoteAccessService.GetRequirePairing(),
                Devices = deviceService.GetDevices().Select(ToViewModel).ToList(),
                PendingRequests = deviceService.GetPendingRequests().Select(ToViewModel).ToList(),
                PairingCode = code == null
                    ? null
                    : new RemoteAccessPairingCodeViewModel
                    {
                        ExpiresAt = code.ExpiresAt,
                        RemainingAttempts = code.RemainingAttempts
                    }
            });
        }

        [HttpPut("mode")]
        [SwaggerOperation(OperationId = "SetRemoteAccessMode")]
        public async Task<BaseResponse> SetMode([FromBody] RemoteAccessModeInputModel model)
        {
            await remoteAccessService.SetModeAsync(model.Mode);

            // Every other check is per-request and takes effect on the next call. A hub
            // connection is authorized once at its handshake, so switching remote access
            // off has to reach the ones already open or they keep receiving pushes.
            if (remoteAccessService.GetEffectiveMode() == RemoteAccessMode.Disabled)
            {
                connections.AbortAll();
            }

            return BaseResponseBuilder.Ok;
        }

        [HttpPut("live-transcode")]
        [SwaggerOperation(OperationId = "SetRemoteAccessLiveTranscode")]
        public async Task<BaseResponse> SetLiveTranscode([FromBody] RemoteAccessLiveTranscodeInputModel model)
        {
            await remoteAccessService.SetAllowLiveTranscodeAsync(model.Allow);
            return BaseResponseBuilder.Ok;
        }

        [HttpPut("require-pairing")]
        [SwaggerOperation(OperationId = "SetRemoteAccessRequirePairing")]
        public async Task<BaseResponse> SetRequirePairing([FromBody] RemoteAccessRequirePairingInputModel model)
        {
            await remoteAccessService.SetRequirePairingAsync(model.Require);

            // Leaving unpaired hub connections open would keep serving exactly the
            // callers this switch was flipped to shut out.
            if (model.Require)
            {
                connections.AbortUnpaired();
            }

            return BaseResponseBuilder.Ok;
        }

        #region Pairing: what a device with no credentials may call

        /// <summary>
        /// Exchanges a pairing code for credentials. The code is spent on success and
        /// counted against on failure, so guessing runs out.
        /// </summary>
        [HttpPost("pair/code")]
        [SwaggerOperation(OperationId = "PairRemoteDeviceWithCode")]
        [RemoteAccessible]
        public async Task<SingletonResponse<RemoteAccessPairingResultViewModel>> PairWithCode(
            [FromBody] RemoteAccessPairWithCodeInputModel model)
        {
            var result = await deviceService.PairWithCodeAsync(model.Code, model.DeviceName ?? string.Empty,
                model.Platform, HttpContext.RequestAborted);

            return new SingletonResponse<RemoteAccessPairingResultViewModel>(await ToViewModelAsync(result));
        }

        /// <summary>
        /// Asks an already-paired device to let this one in, for when nobody can read
        /// the host's screen or logs to fetch a code.
        /// </summary>
        [HttpPost("pair/request")]
        [SwaggerOperation(OperationId = "RequestRemoteDevicePairing")]
        [RemoteAccessible]
        public async Task<SingletonResponse<RemoteAccessPairingRequestAcceptedViewModel>> RequestPairing(
            [FromBody] RemoteAccessPairRequestInputModel model)
        {
            var request = await deviceService.RequestPairingAsync(model.DeviceName ?? string.Empty, model.Platform,
                HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.RequestAborted);

            return new SingletonResponse<RemoteAccessPairingRequestAcceptedViewModel>(
                new RemoteAccessPairingRequestAcceptedViewModel
                {
                    RequestId = request.Id,
                    ExpiresAt = request.ExpiresAt
                });
        }

        /// <summary>
        /// Collects the credentials an approval produced. The waiting device polls this;
        /// until somebody approves it answers <see cref="PairingFailure.NotYetApproved"/>
        /// rather than an error, because waiting is the normal state here.
        /// </summary>
        [HttpPost("pair/claim")]
        [SwaggerOperation(OperationId = "ClaimRemoteDevicePairing")]
        [RemoteAccessible]
        public async Task<SingletonResponse<RemoteAccessPairingResultViewModel>> ClaimPairing(
            [FromBody] RemoteAccessPairClaimInputModel model)
        {
            var result = await deviceService.ClaimApprovedAsync(model.RequestId ?? string.Empty,
                HttpContext.RequestAborted);

            return new SingletonResponse<RemoteAccessPairingResultViewModel>(await ToViewModelAsync(result));
        }

        #endregion

        #region Pairing: management, for the host and already-paired devices

        /// <summary>
        /// Issues a fresh code and returns it in plain text. This is the only response
        /// that ever carries one; the settings page can afterwards see that a code
        /// exists and when it lapses, but not what it is.
        /// </summary>
        [HttpPost("pairing/code")]
        [SwaggerOperation(OperationId = "IssueRemoteAccessPairingCode")]
        public async Task<SingletonResponse<RemoteAccessIssuedPairingCodeViewModel>> IssuePairingCode()
        {
            var issue = await deviceService.IssuePairingCodeAsync(ct: HttpContext.RequestAborted);

            return new SingletonResponse<RemoteAccessIssuedPairingCodeViewModel>(
                new RemoteAccessIssuedPairingCodeViewModel {Code = issue.Code, ExpiresAt = issue.ExpiresAt});
        }

        [HttpPost("pairing/requests/{id}/approve")]
        [SwaggerOperation(OperationId = "ApproveRemoteDevicePairingRequest")]
        public async Task<BaseResponse> ApprovePairingRequest(string id)
        {
            var approver = HttpContext.GetRemoteAccessContext()?.Device?.Id ?? HostApproverId;
            var approved = await deviceService.ApproveRequestAsync(id, approver, HttpContext.RequestAborted);

            return approved
                ? BaseResponseBuilder.Ok
                : BaseResponseBuilder.Build(ResponseCode.NotFound,
                    "This pairing request has expired or was already handled.");
        }

        [HttpPost("pairing/requests/{id}/reject")]
        [SwaggerOperation(OperationId = "RejectRemoteDevicePairingRequest")]
        public async Task<BaseResponse> RejectPairingRequest(string id)
        {
            await deviceService.RejectRequestAsync(id, HttpContext.RequestAborted);

            // Rejecting something that is already gone is the outcome the caller wanted.
            return BaseResponseBuilder.Ok;
        }

        [HttpGet("devices")]
        [SwaggerOperation(OperationId = "GetRemoteAccessDevices")]
        public ListResponse<RemoteAccessDeviceViewModel> GetDevices()
        {
            return new ListResponse<RemoteAccessDeviceViewModel>(
                deviceService.GetDevices().Select(ToViewModel).ToList());
        }

        [HttpDelete("devices/{id}")]
        [SwaggerOperation(OperationId = "RevokeRemoteAccessDevice")]
        public async Task<BaseResponse> RevokeDevice(string id)
        {
            await deviceService.RevokeAsync(id, HttpContext.RequestAborted);

            // The device's next HTTP call fails on its own — the authenticator looks it
            // up every time — but a hub connection it already holds would outlive the
            // revocation.
            connections.AbortDevice(id);

            return BaseResponseBuilder.Ok;
        }

        [HttpPut("devices/{id}/name")]
        [SwaggerOperation(OperationId = "RenameRemoteAccessDevice")]
        public async Task<BaseResponse> RenameDevice(string id, [FromBody] RemoteAccessDeviceNameInputModel model)
        {
            var renamed = await deviceService.RenameAsync(id, model.Name ?? string.Empty, HttpContext.RequestAborted);

            return renamed
                ? BaseResponseBuilder.Ok
                : BaseResponseBuilder.Build(ResponseCode.NotFound, "This device is no longer paired.");
        }

        #endregion

        private async Task<RemoteAccessPairingResultViewModel> ToViewModelAsync(PairingResult result)
        {
            if (result.Credentials == null)
            {
                return new RemoteAccessPairingResultViewModel {Failure = result.Failure};
            }

            return new RemoteAccessPairingResultViewModel
            {
                Credentials = new RemoteAccessPairingCredentialsViewModel
                {
                    DeviceId = result.Credentials.DeviceId,
                    Key = result.Credentials.Key,
                    // Resolved here rather than in the service so the device is told the
                    // same identity discovery and server-info report, and can refuse to
                    // reuse credentials against a different install.
                    ServerId = await remoteAccessService.GetOrCreateServerIdAsync()
                }
            };
        }

        private static RemoteAccessDeviceViewModel ToViewModel(RemoteDevice device) =>
            new()
            {
                Id = device.Id,
                Name = device.Name,
                Platform = device.Platform,
                CreatedAt = device.CreatedAt,
                LastSeenAt = device.LastSeenAt,
                ApprovedByDeviceId = device.ApprovedByDeviceId
            };

        private static RemoteAccessPendingRequestViewModel ToViewModel(PendingPairingRequest request) =>
            new()
            {
                Id = request.Id,
                DeviceName = request.DeviceName,
                Platform = request.Platform,
                RemoteAddress = request.RemoteAddress,
                RequestedAt = request.RequestedAt,
                ExpiresAt = request.ExpiresAt
            };
    }
}
