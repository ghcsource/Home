using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ClientProxyTray
{
    public static class Config
    {
        public class Server
        {
            public Server(string serverString)
            {
                Uri uri = new Uri(serverString);
                Host = uri.Host;
                Port = uri.Port;
                ConnectRequestTemp = string.Format("GET {0} HTTP/1.1\r\nHost: {1}\r\nFrom: {{0}}\r\n\r\n", uri.PathAndQuery + "connect", uri.Authority);
                SendRequestTemp = string.Format("POST {0} HTTP/1.1\r\nHost: {1}\r\nFrom: {{0}}\r\nContent-Type: application/octet-stream\r\nContent-Length: {{1}}\r\n\r\n", uri.PathAndQuery + "send", uri.Authority);
                ReceiveRequestTemp = string.Format("GET {0} HTTP/1.1\r\nHost: {1}\r\nFrom: {{0}}\r\n\r\n", uri.PathAndQuery + "receive", uri.Authority);
                DisconnectRequestTemp = string.Format("GET {0} HTTP/1.1\r\nHost: {1}\r\nFrom: {{0}}\r\n\r\n", uri.PathAndQuery + "disconnect", uri.Authority);
                SendThenReceiveRequestTemp = string.Format("POST {0} HTTP/1.1\r\nHost: {1}\r\nFrom: {{0}}\r\nContent-Type: application/octet-stream\r\nTransfer-Encoding: chunked\r\n\r\n", uri.PathAndQuery + "sendThenReceive", uri.Authority);
            }

            public string Host { get; }
            public int Port { get; }
            public string ConnectRequestTemp { get; }
            public string SendRequestTemp { get; }
            public string ReceiveRequestTemp { get; }
            public string DisconnectRequestTemp { get; }
            public string SendThenReceiveRequestTemp { get; }
        }

        static Config()
        {
            serverList = new List<Server>();
            List<string> serverStringList = ConfigurationManager.GetSection("serverList") as List<string>;
            foreach(var item in serverStringList)
            {
                serverList.Add(new Server(item));
            }

            localPort = int.Parse(ConfigurationManager.AppSettings["localPort"]);
            modifySystemProxy = bool.Parse(ConfigurationManager.AppSettings["modifySystemProxy"]);
            minWorkerThreads = int.Parse(ConfigurationManager.AppSettings["minWorkerThreads"]);
            minIOThreads = int.Parse(ConfigurationManager.AppSettings["minIOThreads"]);
        }

        public static Server GetServer()
        {
            int index = (new Random()).Next(serverList.Count);
            return serverList[index];
        }

        private static List<Server> serverList;

        public static readonly int localPort;

        public static readonly bool modifySystemProxy;

        public static readonly int minWorkerThreads;

        public static readonly int minIOThreads;
    }

    public static class Helper
    {
        public static readonly byte[] headerBound = new byte[] { 0x0D, 0x0A, 0x0D, 0x0A };

        public static int FindIndex(byte[] soruce, int length, byte[] pattern)
        {
            int result = -1;
            for (int i = 0; i < length; i++)
            {
                if (i + pattern.Length <= length)
                {
                    bool find = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (soruce[i + j] != pattern[j])
                        {
                            find = false;
                            break;
                        }
                    }
                    if (find == true)
                    {
                        result = i;
                        break;
                    }
                }
            }
            return result;
        }

        public static int FindIndex(List<byte> soruce, int length, byte[] pattern)
        {
            int result = -1;
            for (int i = 0; i < length; i++)
            {
                if (i + pattern.Length <= length)
                {
                    bool find = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (soruce[i + j] != pattern[j])
                        {
                            find = false;
                            break;
                        }
                    }
                    if (find == true)
                    {
                        result = i;
                        break;
                    }
                }
            }
            return result;
        }

        public static byte[] zeroChunk = new byte[] { 0x30, 0x0D, 0x0A, 0x0D, 0x0A };
        private static byte[] bound = new byte[] { 0x0D, 0x0A };

        public static byte[] GetChunk(byte[] data)
        {
            if(data == null || data.Length == 0)
            {
                return zeroChunk;
            }
            else
            {
                return Encoding.ASCII.GetBytes(Convert.ToString(data.Length, 16)).Concat(bound).Concat(data).Concat(bound).ToArray();
            }
        }
    }

    public static class Log
    {
        private static long serial = DateTime.Now.Ticks;
        private static object obj = new object();

        public static void Write(IEnumerable<byte> data, int length)
        {
            lock(obj)
            {
                serial++;
                File.WriteAllBytes("D:\\log\\" + serial, data.Take(length).ToArray());
            }
        }
    }

    public static class SystemProxy
    {
        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int lpdwBufferLength);

        private static void InternetSetOption(InternetPerConnOption[] options)
        {
            InternetPerConnOptionList internetPerConnOptionList = default(InternetPerConnOptionList);
            internetPerConnOptionList.Connection = null;
            internetPerConnOptionList.OptionCount = options.Length;
            internetPerConnOptionList.OptionError = 0;

            int optionsSize = 0;
            for (int i = 0; i < options.Length; i++)
            {
                optionsSize += Marshal.SizeOf(options[i]);
            }
            IntPtr optionsPtr = Marshal.AllocCoTaskMem(optionsSize);
            IntPtr optionPtr = optionsPtr;
            for (int j = 0; j < options.Length; j++)
            {
                Marshal.StructureToPtr(options[j], optionPtr, false);
                optionPtr = optionPtr + Marshal.SizeOf(options[j]);
            }

            internetPerConnOptionList.pOptions = optionsPtr;
            internetPerConnOptionList.Size = Marshal.SizeOf(internetPerConnOptionList);

            IntPtr listPtr = Marshal.AllocCoTaskMem(internetPerConnOptionList.Size);
            Marshal.StructureToPtr(internetPerConnOptionList, listPtr, false);
            bool flag = InternetSetOption(IntPtr.Zero, 75, listPtr, internetPerConnOptionList.Size);
            if (flag == true)
            {
                InternetSetOption(IntPtr.Zero, 95, IntPtr.Zero, 0);
            }

            for (int j = 0; j < options.Length; j++)
            {
                Marshal.FreeHGlobal(options[j].Value.pszValue);
            }
            Marshal.FreeCoTaskMem(optionsPtr);
            Marshal.FreeCoTaskMem(listPtr);
        }

        public static void Enable(string proxy)
        {
            InternetPerConnOption[] options = new InternetPerConnOption[2];
            options[0] = new InternetPerConnOption();
            options[0].dwOption = 1;
            options[0].Value.dwValue = 2;
            options[1] = new InternetPerConnOption();
            options[1].dwOption = 2;
            options[1].Value.pszValue = Marshal.StringToHGlobalAnsi(proxy);

            InternetSetOption(options);
        }

        public static void Disable()
        {
            InternetPerConnOption[] options = new InternetPerConnOption[1];
            options[0] = new InternetPerConnOption();
            options[0].dwOption = 1;
            options[0].Value.dwValue = 1;

            InternetSetOption(options);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct InternetPerConnOptionList
        {
            public int Size;

            public string Connection;

            public int OptionCount;

            public int OptionError;

            public IntPtr pOptions;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class InternetPerConnOption
        {
            public int dwOption;

            public OptionUnion Value;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct OptionUnion
        {
            [FieldOffset(0)]
            public int dwValue;

            [FieldOffset(0)]
            public IntPtr pszValue;

            [FieldOffset(0)]
            public System.Runtime.InteropServices.ComTypes.FILETIME ftValue;
        }
    }

    public class ServerListSection : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, XmlNode section)
        {
            List<string> serverList = new List<string>();

            foreach (XmlNode xn in section.ChildNodes)
            {
                serverList.Add(xn.SelectSingleNode("@value").InnerText);
            }

            return serverList;
        }
    }
}
