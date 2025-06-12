using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SFCDashboardMobile.Data;

namespace SFCDashboardMobile.Controllers
{
    public class ApiAdminControllerBase : Controller
    {
        private readonly ApplicationDbContext _context;

        public ApiAdminControllerBase(ApplicationDbContext context)
        {
            _context = context;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var isAdmin = await HttpContext.HasAdminPermissionAsync(_context);

            if (!isAdmin)
            {
                context.Result = new ForbidResult();
                return;
            }

            await base.OnActionExecutionAsync(context, next);
        }
    }
}