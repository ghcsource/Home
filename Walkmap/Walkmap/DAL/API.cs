using System;
using System.Collections.Generic;
using System.Linq;
using Walkmap.DAL.Model;
using System.Data.Entity;
using System.Globalization;
using Walkmap.Models;

namespace Walkmap.DAL
{
    public class API
    {
        public static Device GetDevice(string deviceUniqueId)
        {
            using (var context = new DBModel())
            {
                Device device = context.Set<Device>().Where(i => i.DeviceUniqueId == deviceUniqueId).Include(i => i.User).FirstOrDefault();
                return device;
            }
        }

        public static long? UserExist(string userId)
        {
            using (var context = new DBModel())
            {
                User user = context.Set<User>().Where(i => i.UserName == userId).FirstOrDefault();
                return user == null ? null : new long?(user.ID);
            }
        }

        public static bool DeviceNameExist(string deviceUniqueId, long userId, string deviceName)
        {
            using (var context = new DBModel())
            {
                int count = context.Set<Device>().Where(i => i.DeviceUniqueId != deviceUniqueId && i.UserID == userId && i.DeviceName == deviceName).Count();
                if (count > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public static void SetDeviceOwner(string deviceUniqueId, long ownerPrimaryId, string deviceName)
        {
            using (var context = new DBModel())
            {
                Device device = context.Set<Device>().Where(i => i.DeviceUniqueId == deviceUniqueId).FirstOrDefault();
                if (device == null)
                {
                    device = context.Set<Device>().Create();
                    device.DeviceUniqueId = deviceUniqueId;
                    context.Set<Device>().Add(device);
                }
                device.UserID = ownerPrimaryId;
                device.DeviceName = deviceName;

                context.SaveChanges();
            }
        }

        public static void UnbindDevice(string deviceUniqueId)
        {
            using (var context = new DBModel())
            {
                Device device = context.Set<Device>().Where(i => i.DeviceUniqueId == deviceUniqueId).FirstOrDefault();
                if (device != null)
                {
                    device.UserID = null;
                    device.DeviceName = null;
                    context.SaveChanges();
                }
            }
        }

        public static void SendPosition(string deviceUniqueId, List<Trail> list)
        {
            using (var context = new DBModel())
            {
                Device device = context.Set<Device>().Where(i => i.DeviceUniqueId == deviceUniqueId).FirstOrDefault();
                if (device == null)
                {
                    return;
                }

                string dateTime = TimeSyncer.GetTime().AddHours(8).ToString(CultureInfo.CreateSpecificCulture("zh-CN"));
                foreach (var item in list)
                {
                    item.DeviceId = device.ID;
                    item.CreateTime = dateTime;
                    context.Set<Trail>().Add(item);
                }

                context.SaveChanges();
            }
        }
    }
}