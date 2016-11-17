using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Script.Serialization;
using Walkmap.DAL.Model;
using Walkmap.Models;

namespace Walkmap.BLL
{
    public class API
    {
        public static string[] GetDeviceOwner(string deviceUniqueId)
        {
            Device device = DAL.API.GetDevice(deviceUniqueId);

            string[] owner = null;
            if (device != null && device.User != null)
            {
                owner = new string[2] { device.User.UserName, device.DeviceName };
            }

            return owner;
        }

        public static string BindDevice(string deviceUniqueId, string userId, string deviceName)
        {
            long? ownerPrimaryId = DAL.API.UserExist(userId);
            if (ownerPrimaryId == null)
            {
                return "1";
            }

            bool deviceNameExist = DAL.API.DeviceNameExist(deviceUniqueId, ownerPrimaryId.Value, deviceName);
            if (deviceNameExist == true)
            {
                return "2";
            }

            DAL.API.SetDeviceOwner(deviceUniqueId, ownerPrimaryId.Value, deviceName);
            return "";
        }

        public static void UnbindDevice(string deviceUniqueId)
        {
            DAL.API.UnbindDevice(deviceUniqueId);
        }

        public static void SendPosition(string deviceUniqueId, GPSInfo[] list, bool isRealCoord)
        {
            if (list.Count() == 0)
            {
                return;
            }

            List<Trail> gpsList = new List<Trail>();
            for (int i = 0; i < list.Length; i++)
            {
                Trail trail = new Trail();
                trail.PositionSource = list[i].PositionSource;
                if (isRealCoord == true)
                {
                    trail.Latitude = list[i].Latitude;
                    trail.Longitude = list[i].Longitude;
                }
                else
                {
                    trail.LatitudeForMap = list[i].Latitude;
                    trail.LongitudeForMap = list[i].Longitude;
                }
                gpsList.Add(trail);
            }

            DAL.API.SendPosition(deviceUniqueId, gpsList);
        }
    }
}