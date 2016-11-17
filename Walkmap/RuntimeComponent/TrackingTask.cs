using Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.System.Profile;
using Windows.UI.Notifications;

namespace RuntimeComponent
{
    public sealed class TrackingTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();

            try
            {
                Geolocator geolocator = new Geolocator();
                Geoposition position = await geolocator.GetGeopositionAsync();

                //////////////////////////////////////////////////////////////////////////
#if DEBUG
                XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText01);
                XmlNodeList toastTextElements = toastXml.GetElementsByTagName("text");
                toastTextElements[0].AppendChild(toastXml.CreateTextNode(position.Coordinate.Point.Position.Latitude + "|" + position.Coordinate.Point.Position.Longitude));
                ToastNotification toastNotification = new ToastNotification(toastXml);
                ToastNotificationManager.CreateToastNotifier().Show(toastNotification);
#endif
                //////////////////////////////////////////////////////////////////////////

                ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
                LimitedQueue<GPSInfo> queue;
                if (settings.Values.ContainsKey("Queue") == true)
                {
                    string queueString = (string)settings.Values["Queue"];
                    queue = SerializerUtility.Deserialize<LimitedQueue<GPSInfo>>(queueString);
                }
                else
                {
                    queue = new LimitedQueue<GPSInfo>(20);
                }

                queue.Push(new GPSInfo() { Latitude = position.Coordinate.Point.Position.Latitude, Longitude = position.Coordinate.Point.Position.Longitude, PositionSource = position.Coordinate.PositionSource.ToString() });

                try
                {
                    string deviceUniqueId = UniqueIdUtility.GetUniqueId();

                    GPSInfo[] gpsInfoList = queue.Retrive();
                    string gpsInfoListJson = SerializerUtility.Serialize<GPSInfo[]>(gpsInfoList);

                    KeyValuePair<string, string>[] postData = new KeyValuePair<string, string>[2];
                    postData[0] = new KeyValuePair<string, string>("deviceUniqueId", deviceUniqueId);
                    postData[1] = new KeyValuePair<string, string>("gpsInfoList", gpsInfoListJson);

                    await HttpUtility.GetHttpResponse(ConfigUtility.GetValue("SendPositionAPI"), postData);

                    queue.Clear();
                }
                finally
                {
                    string queueString = SerializerUtility.Serialize<LimitedQueue<GPSInfo>>(queue);
                    settings.Values["Queue"] = queueString;
                }
            }
            catch(Exception ex)
            {

            }

            deferral.Complete();
        }
    }
}
