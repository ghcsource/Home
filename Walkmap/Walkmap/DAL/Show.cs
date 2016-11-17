using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Walkmap.DAL.Model;
using System.Data.Entity;

namespace Walkmap.DAL
{
    public class Show
    {
        public static string[] GetRoles(string userId)
        {
            using (var context = new DBModel())
            {
                return context.Set<UserRole>().Join(context.Set<User>().Where(user => user.UserName == userId), o => o.UserId, i => i.ID, (o, i) => o.RoleName).ToArray();
            }
        }

        public static Device[] GetUserDevices(string userId)
        {
            using (var context = new DBModel())
            {
                return context.Set<Device>().Join(context.Set<User>().Where(user => user.UserName == userId), o => o.UserID, i => i.ID, (o, i) => o).ToArray();
            }
        }

        public static Device[] GetAllDevices()
        {
            using (var context = new DBModel())
            {
                return context.Set<Device>().Where(i => i.UserID != null).OrderBy(i => i.UserID).Include(i => i.User).ToArray();
            }
        }

        public static string GetDeviceOwner(string deviceId)
        {
            using (var context = new DBModel())
            {
                Device device = context.Set<Device>().Where(i => i.ID == long.Parse(deviceId)).Include(i => i.User).First();
                return device.User.UserName;
            }
        }

        public static Trail[] GetDeviceTrail(long deviceId)
        {
            using (var context = new DBModel())
            {
                return context.Set<Trail>().Where(i => i.DeviceId == deviceId).Where(i => (i.Latitude.Value > -90 && i.Latitude.Value < 90) && (i.Longitude.Value > -180 && i.Longitude.Value < 180)).OrderByDescending(i => i.ID).Take(20).ToArray();
            }
        }

        public static User GetUser(string userId)
        {
            using (var context = new DBModel())
            {
                return context.Set<User>().Where(i => i.UserName == userId).FirstOrDefault();
            }
        }

        public static void RegistUser(string userId, string password, string role)
        {
            using (var context = new DBModel())
            {
                User user = context.Set<User>().Create();
                user.UserName = userId;
                user.Password = password;
                context.Set<User>().Add(user);

                UserRole userRole = context.Set<UserRole>().Create();
                userRole.UserId = user.ID;
                userRole.RoleName = role;

                user.UserRole.Add(userRole);

                context.SaveChanges();
            }
        }

        public static void ChangePassword(string userName, string password)
        {
            using (var context = new DBModel())
            {
                User user = context.Set<User>().Where(i => i.UserName == userName).Single();
                user.Password = password;

                context.SaveChanges();
            }
        }
    }
}