using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Security;
using Walkmap.DAL.Model;

namespace Walkmap.BLL
{
    public class Show
    {
        public static string[] GetRoles(string userId)
        {
            return DAL.Show.GetRoles(userId);
        }

        public static Device[] GetUserDevices(string userId)
        {
            return DAL.Show.GetUserDevices(userId);
        }

        public static Device[] GetAllDevices()
        {
            return DAL.Show.GetAllDevices();
        }

        public static Trail[] GetDeviceTrail(long deviceId)
        {
            return DAL.Show.GetDeviceTrail(deviceId);
        }

        public static string GetDeviceOwner(string deviceId)
        {
            return DAL.Show.GetDeviceOwner(deviceId);
        }

        public static bool CheckUserNameAndPassword(string userName, string password)
        {
            bool result = false;

            User user = DAL.Show.GetUser(userName);
            if (user != null)
            {
                string md5Password = CalculateMD5Hash(password);
                if (md5Password == user.Password)
                {
                    result = true;
                }
            }
            return result;
        }

        public static bool RegistUser(string userName, string password)
        {
            bool result = false;

            User user = DAL.Show.GetUser(userName);
            if (user == null)
            {
                string md5Password = CalculateMD5Hash(password);
                DAL.Show.RegistUser(userName, md5Password, "User");
                result = true;
            }
            return result;
        }

        public static bool UserExist(string userName)
        {
            User user = DAL.Show.GetUser(userName);

            return user == null ? false : true;
        }

        public static void SendRestPasswordEmail(string userName)
        {
            string password = Guid.NewGuid().ToString().Substring(0, 8);

            MailMessage message = new MailMessage();
            message.To.Add(new MailAddress(userName));
            message.Subject = "Password Reset Notification";
            message.Body = "Your new password is : " + password;

            SmtpClient client = new SmtpClient();
            client.EnableSsl = true;
            client.Send(message);

            ChangePassword(userName, password);
        }

        public static string CalculateMD5Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public static void ChangePassword(string userName, string password)
        {
            string md5Password = CalculateMD5Hash(password);
            DAL.Show.ChangePassword(userName, md5Password);
        }
    }
}