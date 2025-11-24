using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace RaumbuchService
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        // Enable session state for Web API
        protected void Application_PostAuthorizeRequest()
        {
            if (HttpContext.Current != null)
            {
                HttpContext.Current.SetSessionStateBehavior(System.Web.SessionState.SessionStateBehavior.Required);
            }
        }

        // Global exception handler to catch unhandled exceptions
        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            if (ex != null)
            {
                System.Diagnostics.Trace.WriteLine("[GLOBAL ERROR] Unhandled exception caught");
                System.Diagnostics.Trace.WriteLine($"[GLOBAL ERROR] Exception Type: {ex.GetType().Name}");
                System.Diagnostics.Trace.WriteLine($"[GLOBAL ERROR] Message: {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"[GLOBAL ERROR] StackTrace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Trace.WriteLine($"[GLOBAL ERROR] Inner Exception: {ex.InnerException.GetType().Name}");
                    System.Diagnostics.Trace.WriteLine($"[GLOBAL ERROR] Inner Message: {ex.InnerException.Message}");
                }

                // Try to write to event log as well
                try
                {
                    System.Diagnostics.EventLog.WriteEntry("Application", 
                        $"[GLOBAL ERROR] {ex.GetType().Name}: {ex.Message}\n\nStack: {ex.StackTrace}",
                        System.Diagnostics.EventLogEntryType.Error);
                }
                catch { /* Ignore if event log write fails */ }
            }
        }
    }
}
