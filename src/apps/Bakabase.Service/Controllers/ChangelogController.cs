using System.Threading.Tasks;
using Bakabase.Service.Components.Changelog;
using Bakabase.Service.Components.RemoteAccess;
using Bakabase.Service.Models.View;
using Bootstrap.Models.ResponseModels;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Bakabase.Service.Controllers
{
    /// <summary>
    /// Release notes, mirrored from GitHub Releases onto the update CDN by CI.
    /// </summary>
    [Route("~/changelog")]
    public class ChangelogController(ChangelogService changelogService) : Controller
    {
        /// <summary>
        /// Every published release, newest first. Null data when the archive is
        /// unreachable and nothing is cached (offline host, or a blocked CDN).
        /// </summary>
        [HttpGet("releases")]
        [SwaggerOperation(OperationId = "GetChangelogReleases")]
        [RemoteAccessible]
        public async Task<SingletonResponse<ChangelogIndexViewModel>> GetReleases()
        {
            return new SingletonResponse<ChangelogIndexViewModel>(
                await changelogService.GetIndexAsync(HttpContext.RequestAborted));
        }

        /// <summary>
        /// Every release crossed by one update — from the version the caller has
        /// (exclusive) to the version it is about to install (inclusive), newest first.
        /// <para>
        /// Null data when the span cannot be resolved: an unreadable bound, an unreachable
        /// archive, or a lower bound that is not lower. Callers fall back to the target
        /// version's notes alone — a failure here never widens into the whole history.
        /// </para>
        /// </summary>
        /// <param name="from">Bare version the caller currently has, exclusive.</param>
        /// <param name="to">Bare version being installed, inclusive.</param>
        [HttpGet("range")]
        [SwaggerOperation(OperationId = "GetChangelogRange")]
        [RemoteAccessible]
        public async Task<SingletonResponse<ChangelogRangeViewModel>> GetRange([FromQuery] string from,
            [FromQuery] string to)
        {
            return new SingletonResponse<ChangelogRangeViewModel>(
                await changelogService.GetRangeAsync(from, to, HttpContext.RequestAborted));
        }

        /// <summary>
        /// One release's notes. Null data when that version has no published notes —
        /// a local build, or a release older than the archive.
        /// </summary>
        /// <param name="version">Bare version, no <c>v</c> prefix.</param>
        [HttpGet("content")]
        [SwaggerOperation(OperationId = "GetChangelog")]
        [RemoteAccessible]
        public async Task<SingletonResponse<ChangelogViewModel>> Get([FromQuery] string version)
        {
            return new SingletonResponse<ChangelogViewModel>(
                await changelogService.GetAsync(version, HttpContext.RequestAborted));
        }
    }
}
