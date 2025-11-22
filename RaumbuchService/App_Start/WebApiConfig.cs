using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace RaumbuchService
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Force JSON responses (remove XML formatter)
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            
            // Make JSON output readable in browser
            config.Formatters.JsonFormatter.SerializerSettings.Formatting = 
                Newtonsoft.Json.Formatting.Indented;

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
