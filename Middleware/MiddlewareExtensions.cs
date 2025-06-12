namespace SFCDashboardMobile.Middleware
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseUserRegistration(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserRegistrationMiddleware>();
        }
    }
}
