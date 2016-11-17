using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientProxyTray
{
    public class Proxy
    {
        private static TcpListener listener = null;

        public static void Start()
        {
            if (listener != null)
            {
                return;
            }

            listener = new TcpListener(IPAddress.Parse("127.0.0.1"), Config.localPort);
            listener.Start();

            Task.Run(() =>
            {
                while (true)
                {
                    TcpClient acceptedClient = listener.AcceptTcpClient();
                    Task.Factory.StartNew(DoWork, acceptedClient);
                }
            });
        }

        public static void Stop()
        {
            if (listener == null)
            {
                return;
            }
            listener.Stop();
            listener = null;
        }

        private static void DoHttps(TcpClient incomingClient, TcpClient sendClient, TcpClient receiveClient)
        {
            Config.Server server = Config.GetServer();

            NetworkStream incomingStream = incomingClient.GetStream();
            sendClient.Connect(server.Host, server.Port);
            NetworkStream sendStream = sendClient.GetStream();
            receiveClient.Connect(server.Host, server.Port);
            NetworkStream receiveStream = receiveClient.GetStream();

            byte[] buffer = new byte[4096];
            List<byte> header = new List<byte>();
            int headerEndIndex = -1;
            int receiveNumber;
            while (true)
            {
                receiveNumber = incomingStream.Read(buffer, 0, buffer.Length);
                if (receiveNumber == 0)
                {
                    throw new Exception();
                }
                header.AddRange(buffer.Take(receiveNumber));
                headerEndIndex = Helper.FindIndex(header, header.Count, Helper.headerBound);
                if (headerEndIndex == -1)
                {
                    continue;
                }
                else
                {
                    break;
                }
            }
            string firstLine = Encoding.ASCII.GetString(header.GetRange(0, header.IndexOf(0x0D)).ToArray());
            string httpsHost = firstLine.Split(' ')[1];

            byte[] connectionRequest = Encoding.ASCII.GetBytes(string.Format(server.ConnectRequestTemp, httpsHost));
            sendStream.Write(connectionRequest, 0, connectionRequest.Length);

            long key = 0;
            header.Clear();
            headerEndIndex = -1;
            while (true)
            {
                receiveNumber = sendStream.Read(buffer, 0, buffer.Length);
                if (receiveNumber == 0)
                {
                    throw new Exception();
                }
                header.AddRange(buffer.Take(receiveNumber));
                headerEndIndex = Helper.FindIndex(header, header.Count, Helper.headerBound);
                if (headerEndIndex == -1)
                {
                    continue;
                }
                else
                {
                    int eTagIndex = Helper.FindIndex(header, headerEndIndex, Encoding.ASCII.GetBytes("\r\nETag:"));
                    if (eTagIndex == -1)
                    {
                        throw new Exception();
                    }
                    eTagIndex = eTagIndex + 7;
                    key = long.Parse(Encoding.ASCII.GetString(header.GetRange(eTagIndex, header.IndexOf(0x0D, eTagIndex) - eTagIndex).ToArray()));
                    break;
                }
            }

            byte[] connectConfirm = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
            incomingStream.Write(connectConfirm, 0, connectConfirm.Length);

            Task.Run(async () =>
            {
                try
                {
                    byte[] readBuffer = new byte[4096];
                    int upReceiveNumber;
                    List<byte> upHeader = new List<byte>();

                    while (true)
                    {
                        upReceiveNumber = await incomingStream.ReadAsync(readBuffer, 0, readBuffer.Length);
                        if (upReceiveNumber == 0)
                        {
                            throw new Exception();
                        }

                        byte[] request = Encoding.ASCII.GetBytes(string.Format(server.SendRequestTemp, key, upReceiveNumber)).Concat(readBuffer.Take(upReceiveNumber)).ToArray();
                        sendStream.Write(request, 0, request.Length);

                        ClearResponse(sendStream);
                    }
                }
                catch
                {
                    //Disconnect(sendStream, key);
                    incomingClient.Close();
                    sendClient.Close();
                    receiveClient.Close();
                }
            });

            Task.Run(async () =>
            {
                try
                {
                    byte[] request = Encoding.ASCII.GetBytes(string.Format(server.ReceiveRequestTemp, key));
                    receiveStream.Write(request, 0, request.Length);

                    header.Clear();
                    headerEndIndex = -1;
                    int? chunkeLength = null;
                    List<byte> chunke = new List<byte>();
                    bool zeroChunke = false;

                    while (true)
                    {
                        byte[] readBuffer = buffer;
                        receiveNumber = await receiveStream.ReadAsync(readBuffer, 0, readBuffer.Length);
                        if (receiveNumber == 0)
                        {
                            throw new Exception();
                        }

                        if (headerEndIndex == -1)
                        {
                            header.AddRange(readBuffer.Take(receiveNumber));
                            headerEndIndex = Helper.FindIndex(header, header.Count, Helper.headerBound);

                            if (headerEndIndex == -1)
                            {
                                continue;
                            }
                            else
                            {
                                receiveNumber = header.Count - headerEndIndex - 4;
                                readBuffer = header.GetRange(headerEndIndex + 4, receiveNumber).ToArray();
                            }
                        }

                        for (int i = 0; i < receiveNumber; i++)
                        {
                            chunke.Add(readBuffer[i]);
                            if (chunkeLength == null)
                            {
                                if (readBuffer[i] == 0x0D)
                                {
                                    chunkeLength = Convert.ToInt32(Encoding.ASCII.GetString(chunke.GetRange(0, chunke.Count - 1).ToArray()), 16);
                                    if (chunkeLength == 0)
                                    {
                                        zeroChunke = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                if (chunke.Count == chunke.IndexOf(0x0D) + 2 + chunkeLength + 2)
                                {
                                    byte[] chunkeContent = chunke.GetRange(chunke.IndexOf(0x0D) + 2, chunkeLength.Value).ToArray();
                                    incomingStream.Write(chunkeContent, 0, chunkeContent.Length);
                                    chunkeLength = null;
                                    chunke.Clear();
                                }
                            }
                        }
                        if (zeroChunke == true)
                        {
                            throw new Exception();
                        }
                    }
                }
                catch
                {
                    incomingClient.Close();
                    sendClient.Close();
                    receiveClient.Close();
                }
            });
        }

        private static void DoHttp(TcpClient incomingClient, TcpClient outgoingClient)
        {
            Config.Server server = Config.GetServer();

            NetworkStream incomingStream = incomingClient.GetStream();
            outgoingClient.Connect(server.Host, server.Port);
            NetworkStream outgoingStream = outgoingClient.GetStream();

            byte[] buffer = new byte[4096];
            List<byte> header = new List<byte>();
            int headerEndIndex = -1;
            int receiveNumber;
            int contentLength = 0;
            int sentLength = 0;
            bool chunked = false;
            int? chunkeLength = null;
            List<byte> chunke = new List<byte>();
            bool zeroChunke = false;

            while (true)
            {
                byte[] readBuffer = buffer;
                receiveNumber = incomingStream.Read(readBuffer, 0, readBuffer.Length);
                if (receiveNumber == 0)
                {
                    throw new Exception();
                }

                if (headerEndIndex == -1)
                {
                    header.AddRange(readBuffer.Take(receiveNumber));
                    headerEndIndex = Helper.FindIndex(header, header.Count, Helper.headerBound);
                    if (headerEndIndex == -1)
                    {
                        continue;
                    }
                    else
                    {
                        string firstLine = Encoding.ASCII.GetString(header.GetRange(0, header.IndexOf(0x0D)).ToArray());
                        string[] part = firstLine.Split(' ');
                        string url = part[1];
                        Uri uri = new Uri(url);
                        string httpHost = uri.Authority;

                        int contentLengthIndex = Helper.FindIndex(header, headerEndIndex, Encoding.ASCII.GetBytes("Content-Length:"));
                        if (contentLengthIndex != -1)
                        {
                            int indexBegin = header.IndexOf(0x3A, contentLengthIndex) + 1;
                            int indexEnd = header.IndexOf(0x0D, contentLengthIndex);
                            byte[] lengthBytes = header.GetRange(indexBegin, indexEnd - indexBegin).ToArray();
                            contentLength = int.Parse(Encoding.ASCII.GetString(lengthBytes));
                        }
                        else
                        {
                            if (Helper.FindIndex(header, headerEndIndex, Encoding.ASCII.GetBytes("Transfer-Encoding: chunked")) != -1)
                            {
                                chunked = true;
                            }
                            else
                            {
                                contentLength = 0;
                            }
                        }

                        //string newFirstLine = part[0] + " " + uri.PathAndQuery + " " + part[2];
                        //List<byte> realHeaderList = Encoding.ASCII.GetBytes(newFirstLine).Concat(header.Take(headerEndIndex + 4).Skip(header.IndexOf(0x0D))).ToList();
                        //int connectionIndex = Helper.FindIndex(realHeaderList, realHeaderList.Count, Encoding.ASCII.GetBytes("\r\nProxy-Connection"));
                        //if(connectionIndex != -1)
                        //{
                        //    realHeaderList.RemoveRange(connectionIndex + 2, 6);
                        //}
                        //byte[] realHeader = realHeaderList.ToArray();
                        string newFirstLine = part[0] + " " + uri.PathAndQuery + " " + part[2];
                        byte[] realHeader = Encoding.ASCII.GetBytes(newFirstLine).Concat(header.Take(headerEndIndex + 4).Skip(header.IndexOf(0x0D))).ToArray();
                        byte[] request = Encoding.ASCII.GetBytes(string.Format(server.SendThenReceiveRequestTemp, httpHost)).Concat(Helper.GetChunk(realHeader)).ToArray();
                        outgoingStream.Write(request, 0, request.Length);

                        receiveNumber = header.Count - headerEndIndex - 4;
                        readBuffer = header.GetRange(headerEndIndex + 4, receiveNumber).ToArray();
                    }
                }

                if (chunked == false)
                {
                    if(receiveNumber > 0)
                    {
                        byte[] request = Helper.GetChunk(readBuffer.Take(receiveNumber).ToArray());
                        outgoingStream.Write(request, 0, request.Length);
                    }

                    sentLength = sentLength + receiveNumber;
                    if (sentLength >= contentLength)
                    {
                        break;
                    }
                }
                else
                {
                    for (int i = 0; i < receiveNumber; i++)
                    {
                        chunke.Add(readBuffer[i]);
                        if (chunkeLength == null)
                        {
                            if (readBuffer[i] == 0x0D)
                            {
                                chunkeLength = Convert.ToInt32(Encoding.ASCII.GetString(chunke.GetRange(0, chunke.Count - 1).ToArray()), 16);
                                if (chunkeLength == 0)
                                {
                                    zeroChunke = true;
                                    break;
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            if (chunke.Count == chunke.IndexOf(0x0D) + 2 + chunkeLength + 2)
                            {
                                byte[] request = Helper.GetChunk(chunke.ToArray());
                                outgoingStream.Write(request, 0, request.Length);

                                chunkeLength = null;
                                chunke.Clear();
                            }
                        }
                    }
                    if (zeroChunke == true)
                    {
                        byte[] request = Helper.GetChunk(Helper.zeroChunk);
                        outgoingStream.Write(request, 0, request.Length);

                        break;
                    }
                }
            }

            outgoingStream.Write(Helper.zeroChunk, 0, Helper.zeroChunk.Length);

            header.Clear();
            headerEndIndex = -1;
            chunkeLength = null;
            chunke.Clear();
            zeroChunke = false;

            while (true)
            {
                byte[] readBuffer = buffer;
                receiveNumber = outgoingStream.Read(readBuffer, 0, readBuffer.Length);
                if (receiveNumber == 0)
                {
                    throw new Exception();
                }

                if (headerEndIndex == -1)
                {
                    header.AddRange(readBuffer.Take(receiveNumber));
                    headerEndIndex = Helper.FindIndex(header, header.Count, Helper.headerBound);

                    if (headerEndIndex == -1)
                    {
                        continue;
                    }
                    else
                    {
                        receiveNumber = header.Count - headerEndIndex - 4;
                        readBuffer = header.GetRange(headerEndIndex + 4, receiveNumber).ToArray();
                    }
                }

                for (int i = 0; i < receiveNumber; i++)
                {
                    chunke.Add(readBuffer[i]);
                    if (chunkeLength == null)
                    {
                        if (readBuffer[i] == 0x0D)
                        {
                            chunkeLength = Convert.ToInt32(Encoding.ASCII.GetString(chunke.GetRange(0, chunke.Count - 1).ToArray()), 16);
                            if (chunkeLength == 0)
                            {
                                zeroChunke = true;
                                break;
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (chunke.Count == chunke.IndexOf(0x0D) + 2 + chunkeLength + 2)
                        {
                            byte[] chunkeContent = chunke.GetRange(chunke.IndexOf(0x0D) + 2, chunkeLength.Value).ToArray();
                            incomingStream.Write(chunkeContent, 0, chunkeContent.Length);
                            chunkeLength = null;
                            chunke.Clear();
                        }
                    }
                }
                if (zeroChunke == true)
                {
                    break;
                }
            }
        }

        private static void ClearResponse(NetworkStream stream)
        {
            byte[] buffer = new byte[4096];
            List<byte> header = new List<byte>();
            int headerEndIndex = -1;
            int receiveNumber;
            while (true)
            {
                receiveNumber = stream.Read(buffer, 0, buffer.Length);
                if (receiveNumber == 0)
                {
                    throw new Exception();
                }
                header.AddRange(buffer.Take(receiveNumber));
                headerEndIndex = Helper.FindIndex(header, header.Count, Helper.headerBound);
                if (headerEndIndex == -1)
                {
                    continue;
                }
                else
                {
                    break;
                }
            }
        }

        private static void DoWork(object obj)
        {
            TcpClient incomingClient = (TcpClient)obj;

            byte[] verb = new byte[7];
            int receiveNumber = incomingClient.Client.Receive(verb, verb.Length, SocketFlags.Peek);
            if (receiveNumber == 0)
            {
                throw new Exception();
            }

            if (Encoding.ASCII.GetString(verb) == "CONNECT")
            {
                TcpClient sendClient = new TcpClient();
                TcpClient receiveClient = new TcpClient();

                try
                {
                    DoHttps(incomingClient, sendClient, receiveClient);
                }
                catch
                {
                    incomingClient.Close();
                    sendClient.Close();
                    receiveClient.Close();
                }
            }
            else
            {
                TcpClient outgoingClient = new TcpClient();

                try
                {
                    DoHttp(incomingClient, outgoingClient);
                }
                finally
                {
                    incomingClient.Close();
                    outgoingClient.Close();
                }
            }
        }
    
        private static void Disconnect(TcpClient sendClient, long key, Config.Server server)
        {
            byte[] request = Encoding.ASCII.GetBytes(string.Format(server.DisconnectRequestTemp, key));
            sendClient.GetStream().Write(request, 0, request.Length);
        }
    }
}
