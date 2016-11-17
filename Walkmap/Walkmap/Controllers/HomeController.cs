using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Data.SQLite;
using System.Data.SqlServerCe;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Walkmap.BLL;
using Walkmap.DAL.Model;

namespace Walkmap.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            Device[] devices = Show.GetUserDevices(this.HttpContext.User.Identity.Name);

            return View(devices);
        }

        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string userName, string password)
        {
            userName = userName.Trim();
            bool success = Show.CheckUserNameAndPassword(userName, password);
            if (success == true)
            {
                FormsAuthentication.SetAuthCookie(userName, false);
                return Redirect("~/Home/Index");
            }
            else
            {
                return View();
            }
        }

        [HttpGet]
        public ActionResult Regist()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Regist(string userName, string password)
        {
            userName = userName.Trim();

            EmailAddressAttribute validator = new EmailAddressAttribute();
            bool isValid = validator.IsValid(userName);
            if (isValid == false)
            {
                ViewBag.ErrorMessage = "Email Address Invalid!";
                return View();
            }

            bool success = Show.RegistUser(userName, password);
            if (success == false)
            {
                ViewBag.ErrorMessage = "Email Address Existed!";
                return View();
            }

            return Redirect("~/Home/Login");
        }

        [HttpGet]
        public ActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ResetPassword(string userName)
        {
            userName = userName.Trim();

            EmailAddressAttribute validator = new EmailAddressAttribute();
            bool isValid = validator.IsValid(userName);
            if (isValid == false)
            {
                ViewBag.ErrorMessage = "Email Address Invalid!";
                return View();
            }

            bool userExist = Show.UserExist(userName);
            if (userExist == false)
            {
                ViewBag.ErrorMessage = "Email Address Not Exist!";
                return View();
            }

            Show.SendRestPasswordEmail(userName);

            ViewBag.Email = userName;
            return View("ResetPasswordNotify");
        }

        public ActionResult Admin()
        {
            Device[] devices = Show.GetAllDevices();
            return View(devices);
        }

        [HttpGet]
        public ActionResult Help()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return Redirect("~/Home/Login");
        }

        public ActionResult ShowTrail(long id)
        {
            Trail[] trails = Show.GetDeviceTrail(id);

            trails = trails.Select(i => new Trail() {CreateTime = i.CreateTime, Latitude = i.Latitude, Longitude = i.Longitude, LatitudeForMap = i.LatitudeForMap, LongitudeForMap = i.LongitudeForMap, PositionSource = i.PositionSource }).ToArray();
            return View(trails);
        }

        [HttpGet]
        public ActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ChangePassword(string password)
        {
            string userName = this.User.Identity.Name;
            Show.ChangePassword(userName, password);

            FormsAuthentication.SignOut();
            return Redirect("~/Home/Login");
        }

        public ActionResult DownloadClient(Client client)
        {
            switch (client)
            {
                case Client.Win:
                    return File(Server.MapPath("~/DownloadFile/Walkmap.appx"), "application/x-silverlight-app", "Walkmap.appx");
                case Client.Android:
                    return File(Server.MapPath("~/DownloadFile/Walkmap.apk"), "application/vnd.android.package-archive", "Walkmap.apk");
            }
            return null;
        }

        public enum Client
        {
            Win,
            Android
        }
    }
}