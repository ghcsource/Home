using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using Walkmap.BLL;
using Walkmap.Models;

namespace Walkmap.Controllers
{
    public class PhoneController : Controller
    {
        public ActionResult GetDeviceOwner(string deviceUniqueId)
        {
            string[] owner = API.GetDeviceOwner(deviceUniqueId);
            string ownerAndDeviceName = "";
            if (owner != null)
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                ownerAndDeviceName = serializer.Serialize(owner);
            }
            return Content(ownerAndDeviceName, "text/plain");
        }

        public ActionResult BindDevice(string deviceUniqueId, string userId, string deviceName)
        {
            string status = API.BindDevice(deviceUniqueId, userId, deviceName);
            return Content(status, "text/plain");
        }

        public ActionResult UnbindDevice(string deviceUniqueId)
        {
            API.UnbindDevice(deviceUniqueId);
            return Content("", "text/plain");
        }

        public ActionResult SendPosition(string deviceUniqueId, string gpsInfoList, bool isRealCoord = true)
        {
            Task.Run(() =>
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                GPSInfo[] list = serializer.Deserialize<GPSInfo[]>(gpsInfoList);

                API.SendPosition(deviceUniqueId, list, isRealCoord);
            });
            return Content("", "text/plain");
        }
    }
}