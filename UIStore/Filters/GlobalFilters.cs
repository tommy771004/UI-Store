using System; using System.Linq; using System.Net; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Filters; using Microsoft.Extensions.Logging; using UIStore.Services;
namespace UIStore.Filters {
    public class ValidateIPWhitelistAttribute : ActionFilterAttribute {
        public override void OnActionExecuting(ActionExecutingContext c) {
            var ip = c.HttpContext.Connection.RemoteIpAddress?.ToString(); var s = c.HttpContext.RequestServices.GetService(typeof(SystemSettingService)) as SystemSettingService;
            var w = s.GetSettingAsync("Security_WebhookAllowedIPs", "127.0.0.1,::1").GetAwaiter().GetResult().Split(',');
            if (string.IsNullOrEmpty(ip) || !w.Any(x => ip.StartsWith(x.Split('/')[0]))) { c.Result = new StatusCodeResult(403); return; } base.OnActionExecuting(c);
        }
    }
    public class GlobalSecurityFilter : IActionFilter {
        public void OnActionExecuting(ActionExecutingContext c) {
            if (c.HttpContext.Request.Query.Any(q => q.Value.ToString().Length > 200 || q.Value.ToString().Contains('\r') || q.Value.ToString().Contains('\n'))) { c.Result = new BadRequestObjectResult("Invalid param"); return; }
        } public void OnActionExecuted(ActionExecutedContext c) { }
    }
}
