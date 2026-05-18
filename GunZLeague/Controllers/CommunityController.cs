using Microsoft.AspNetCore.Mvc;
using GunZLeague.Services;

namespace GunZLeague.Controllers
{
    public class CommunityController : Controller
    {
        private readonly TwitchStreamStatusService _twitchStreamStatusService;

        public CommunityController(TwitchStreamStatusService twitchStreamStatusService)
        {
            _twitchStreamStatusService = twitchStreamStatusService;
        }

        [HttpGet]
        public async Task<IActionResult> Streams(CancellationToken cancellationToken)
        {
            var streams = await _twitchStreamStatusService.GetStreamsAsync(cancellationToken);
            return Json(streams);
        }
    }
}
