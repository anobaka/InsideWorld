using System.Threading;
using System.Threading.Tasks;
using Bakabase.Service.Components.Downloads;
using Bakabase.Service.Components.RemoteAccess;
using Bakabase.Service.Models.View;
using Bootstrap.Models.ResponseModels;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Bakabase.Service.Controllers
{
    /// <summary>
    /// How to reach this library from a device that is not this one.
    /// </summary>
    [Route("~/other-devices")]
    public class OtherDevicesController(AppDownloadManifestService manifests) : Controller
    {
        private const string MobileManifestUrl =
            "https://cdn-public.anobaka.com/app/bakabase-mobile/manifest.json";

        private const string ClientManifestUrl =
            "https://cdn-public.anobaka.com/app/bakabase-client/manifest.json";

        /// <summary>
        /// The latest published packages for phones and for a second computer.
        /// </summary>
        /// <remarks>
        /// Either half is null when its manifest is unreachable and nothing is cached.
        /// The two are fetched together but cached separately, so one product being
        /// unpublished never hides the other.
        /// </remarks>
        [HttpGet("downloads")]
        [SwaggerOperation(OperationId = "GetOtherDeviceDownloads")]
        [RemoteAccessible]
        public async Task<SingletonResponse<OtherDeviceDownloadsViewModel>> GetDownloads()
        {
            var ct = HttpContext.RequestAborted;

            var mobile = manifests.GetAsync<MobileAppDownloadsViewModel>(MobileManifestUrl, ct);
            var client = manifests.GetAsync<ClientAppDownloadsViewModel>(ClientManifestUrl, ct);

            await Task.WhenAll(mobile, client);

            return new SingletonResponse<OtherDeviceDownloadsViewModel>(
                new OtherDeviceDownloadsViewModel
                {
                    Mobile = await mobile,
                    DesktopClient = await client
                });
        }
    }
}
