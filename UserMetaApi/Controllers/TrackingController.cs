using Microsoft.AspNetCore.Mvc;
using UserMetaApi.Interfaces;

namespace UserMetaApi.Controllers
{
    [Route("api/track")]
    [ApiController]
    public class TrackingController : ControllerBase
    {
        private readonly IUserMetaService _service;

        public TrackingController(IUserMetaService service)
        {
            _service = service;
        }

        [HttpGet("{token}")]
        public async Task<IActionResult> Track(Guid token)
        {
            if (token == Guid.Empty)
                return BadRequest("Invalid token");

            await _service.CaptureAsync(token, HttpContext);

            return NotFound();
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok("test");
        }
    }
}
