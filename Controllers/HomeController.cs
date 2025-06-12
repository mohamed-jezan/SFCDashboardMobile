using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SFCDashboardMobile.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HomeController : ControllerBase
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [HttpGet("Index")]
        public IActionResult Index()
        {
            return Ok(new { Message = "Welcome to the API Home Index" });
        }

        [HttpGet("Privacy")]
        public IActionResult Privacy()
        {
            return Ok(new { Message = "Privacy information from API" });
        }

        [HttpGet("Error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            return Problem(detail: $"Error occurred. RequestId: {requestId}", statusCode: 500);
        }

        [HttpGet("Unauthorized")]
        public IActionResult UnauthorizedAccess()
        {
            var email = User.Identity?.Name ?? string.Empty;
            var serviceId = ExtractServiceId(email);
            return Unauthorized(new { Message = "You are not authorized.", ServiceId = serviceId });
        }

        private static string ExtractServiceId(string email)
        {
            if (string.IsNullOrEmpty(email))
                return string.Empty;

            return email[..Math.Min(email.Length, 6)];
        }
    }
}
