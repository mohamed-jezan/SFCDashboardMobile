using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using SFCDashboardMobile.Data;

namespace SFCDashboardMobile.Middleware
{
    public class UserRegistrationMiddleware
    {
        private readonly RequestDelegate _next;

        public UserRegistrationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Add your user registration logic here.  
            await _next(context);
        }
    }
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UserRegistration(this IApplicationBuilder app)
        {
            return app.UseMiddleware<UserRegistrationMiddleware>();
        }
    }
}