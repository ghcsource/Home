using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Web;

namespace Walkmap.Models
{
    public class GPSInfo
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string PositionSource { get; set; }
    }

    public static class TimeSyncer
    {
        private static Thread syncerThread;
        private static DateTime? ntpTime;
        private static DateTime? machineTime;

        public static void Start()
        {
            syncerThread = new Thread(DoWork);
            syncerThread.Start();
        }

        private static void DoWork()
        {
            while(true)
            {
                try
                {
                    DoWorkInternal();
                }
                catch (Exception ex)
                {

                }
                Thread.Sleep(3600000);
            }
        }

        private static ReaderWriterLockSlim rwlock = new ReaderWriterLockSlim();

        private static void DoWorkInternal()
        {
            int count = 1;
            while (count <= 5)
            {
                try
                {
                    DateTime networkTime = GetNetworkTime();

                    rwlock.EnterWriteLock();
                    try
                    {
                        ntpTime = networkTime;
                        machineTime = DateTime.UtcNow;

                        return;
                    }
                    finally
                    {
                        rwlock.ExitWriteLock();
                    }
                }
                catch (Exception ex)
                {

                }

                count++;
                Thread.Sleep(5000);
            }
        }

        private static object getTimeLocker = new object();

        public static DateTime GetTime()
        {
            if (syncerThread.IsAlive == false)
            {
                lock (getTimeLocker)
                {
                    if (syncerThread.IsAlive == false)
                    {
                        syncerThread = new Thread(DoWork);
                        syncerThread.Start();
                    }
                }
            }

            if(ntpTime == null)
            {
                return DateTime.UtcNow;
            }
            else
            {
                rwlock.EnterReadLock();
                try
                {
                    return ntpTime.Value.Add(DateTime.UtcNow - machineTime.Value);
                }
                finally
                {
                    rwlock.ExitReadLock();
                }
            }
        }

        public static DateTime GetNetworkTime()
        {
            const string ntpServer = "time.windows.com";

            var ntpData = new byte[48];
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(ipEndPoint);
                socket.ReceiveTimeout = 3000;
                socket.Send(ntpData);
                socket.Receive(ntpData);
            }

            const byte serverReplyTime = 40;

            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime;
        }

        // stackoverflow.com/a/3294698/162671
        private static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
    }
}