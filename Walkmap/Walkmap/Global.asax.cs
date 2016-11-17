using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using Walkmap.BLL;
using Walkmap.Models;

namespace Walkmap
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            TimeSyncer.Start();
        }

        protected void Application_AuthenticateRequest(Object sender, EventArgs e)
        {
            if (this.Context.User != null)
            {
                if (this.Context.User.Identity.IsAuthenticated)
                {
                    if (this.Context.User.Identity is FormsIdentity)
                    {
                        string[] roles = Show.GetRoles(this.Context.User.Identity.Name);

                        this.Context.User = new GenericPrincipal(this.Context.User.Identity, roles);
                    }
                }
            }
        }

        protected void Application_AuthorizeRequest(object sender, EventArgs e)
        {
            string url = this.Request.AppRelativeCurrentExecutionFilePath;
            if (url.StartsWith("~/Home/Trail", StringComparison.OrdinalIgnoreCase) == true)
            {
                if(this.Context.User.IsInRole("Admin") == false)
                {
                    string deviceOnwer = Show.GetDeviceOwner(url.Split('/').Last());
                    if(deviceOnwer != this.Context.User.Identity.Name)
                    {
                        this.Response.End();
                    }
                }
            }
        }
    }
}
