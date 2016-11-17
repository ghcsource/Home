using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Profile;

namespace Common
{
    [DataContract]
    public class LimitedQueue<T> : IEnumerable<T>
    {
        public LimitedQueue(int len)
        {
            if (len < 2)
            {
                throw new Exception();
            }
            length = len;
            container = new T[len];
            index = -1;
        }

        [DataMember]
        private readonly int length;

        [DataMember]
        private T[] container;

        [DataMember]
        private int index;

        public void Push(T obj)
        {
            if (index == length - 1)
            {
                T[] temp = new T[length];
                Array.Copy(container, 1, temp, 0, length - 1);
                container = temp;
                index--;
            }

            index++;
            container[index] = obj;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i <= index; i++)
            {
                yield return container[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public T[] Retrive()
        {
            T[] result = this.ToArray();
            return result;
        }

        public void Clear()
        {
            index = -1;
            container = new T[length];
        }
    }

    public class GPSInfo
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string PositionSource { get; set; }
    }

    public static class HttpUtility
    {
        public static async Task<string> GetHttpResponse(string url, KeyValuePair<string, string>[] dataSet)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpContent content = new FormUrlEncodedContent(dataSet);
                HttpResponseMessage response = await client.PostAsync(url, content);
                if(response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception(response.StatusCode.ToString());
                }
                string result = await response.Content.ReadAsStringAsync();
                return result;
            }
        }
    }

    public static class UniqueIdUtility
    {
        public static string GetUniqueId()
        {
            string deviceUniqueId;
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

            if (settings.Values.ContainsKey("DeviceUniqueId") == true)
            {
                deviceUniqueId = (string)settings.Values["DeviceUniqueId"];
            }
            else
            {
                HardwareToken token = HardwareIdentification.GetPackageSpecificToken(null);
                IBuffer buffer = token.Id;
                byte[] bytes;
                using (var dataReader = DataReader.FromBuffer(buffer))
                {
                    bytes = new byte[buffer.Length];
                    dataReader.ReadBytes(bytes);
                }

                if (bytes.Length % 4 != 0)
                {
                    throw new ArgumentException("Invalid hardware id");
                }

                HardwareId[] hardwareIds = new HardwareId[bytes.Length / 4];
                for (int index = 0; index < hardwareIds.Length; index++)
                {
                    hardwareIds[index].type = (HardwareIdType)BitConverter.ToUInt16(bytes, index * 4);
                    hardwareIds[index].value = BitConverter.ToUInt16(bytes, index * 4 + 2);
                }

                string cpu = hardwareIds.Where(i => i.type == HardwareIdType.Processor).FirstOrDefault().value.ToString();
                string bios = hardwareIds.Where(i => i.type == HardwareIdType.SmBios).FirstOrDefault().value.ToString();
                string mac = hardwareIds.Where(i => i.type == HardwareIdType.NetworkAdapter).FirstOrDefault().value.ToString();

                deviceUniqueId = cpu + "-" + bios + "-" + mac;
                settings.Values["DeviceUniqueId"] = deviceUniqueId;
            }

            return deviceUniqueId;
        }

        struct HardwareId
        {
            public HardwareIdType type;
            public UInt16 value;
        }

        enum HardwareIdType
        {
            Invalid = 0,
            Processor = 1,
            Memory = 2,
            DiskDevice = 3,
            NetworkAdapter = 4,
            AudioAdapter = 5,
            DockingStation = 6,
            MobileBroadband = 7,
            Bluetooth = 8,
            SmBios = 9
        }
    }

    public static class SerializerUtility
    {
        public static string Serialize<T>(T obj)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));

            using (MemoryStream ms = new MemoryStream())
            {
                serializer.WriteObject(ms, obj);
                byte[] bytes = ms.ToArray();
                string str = Encoding.UTF8.GetString(bytes, 0, bytes.Length);

                return str;
            }
        }

        public static T Deserialize<T>(string objString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));

            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(objString)))
            {
                T obj = (T)serializer.ReadObject(ms);
                return obj;
            }
        }
    }

    public static class ConfigUtility
    {
        public static string GetValue(string elementName)
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            Dictionary<string, string> configs;
            if (settings.Values.ContainsKey("Configs") == true)
            {
                configs = SerializerUtility.Deserialize<Dictionary<string, string>>((string)settings.Values["Configs"]);
            }
            else
            {
                configs = new Dictionary<string, string>();
                string xml = FileIO.ReadTextAsync(Package.Current.InstalledLocation.GetFileAsync("Config.xml").AsTask().Result).AsTask().Result;
                XDocument doc = XDocument.Parse(xml);
                XElement root = doc.Root;

                foreach(var item in root.Elements())
                {
                    configs.Add(item.Name.LocalName, item.Value);
                }
                settings.Values["Configs"] = SerializerUtility.Serialize<Dictionary<string, string>>(configs);
            }

            return configs[elementName];
        } 
    }

    public static class BackgroundTaskUtility
    {
        public static void RegisterBackgroundTask(string taskEntryPoint, string taskName, IBackgroundTrigger trigger, IBackgroundCondition condition)
        {
            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {
                if (cur.Value.Name == taskName)
                {
                    return;
                }
            }

            BackgroundAccessStatus status = BackgroundExecutionManager.RequestAccessAsync().AsTask().Result;
            if (status == BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity || status == BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity)
            {
                var builder = new BackgroundTaskBuilder();

                builder.Name = taskName;
                builder.TaskEntryPoint = taskEntryPoint;
                builder.SetTrigger(trigger);

                if (condition != null)
                {
                    builder.AddCondition(condition);
                }

                builder.Register();
            }
            else
            {
                throw new Exception();
            }
        }

        public static void UnregisterBackgroundTask(string taskName)
        {
            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {
                if (cur.Value.Name == taskName)
                {
                    cur.Value.Unregister(false);
                }
            }
        }
    }
}
