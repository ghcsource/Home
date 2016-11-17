using ClientProxyTray;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web;

namespace ServerProxy
{
    public class MyModule : IHttpModule
    {
        private static ConcurrentDictionary<long, TcpClient> socketDic = new ConcurrentDictionary<long, TcpClient>();
        private static long key = DateTime.Now.Ticks;
        private static object obj = new object();

        private static long GetNewKey()
        {
            lock (obj)
            {
                key++;
                return key;
            }
        }

        private class MyAsyncResult : IAsyncResult
        {
            public MyAsyncResult(bool isSync)
            {
                CompletedSynchronously = isSync;
            }

            public object AsyncState
            {
                get;
            }

            public WaitHandle AsyncWaitHandle
            {
                get;
            }

            public bool CompletedSynchronously
            {
                get;
            }

            public bool IsCompleted
            {
                get;
            }
        }

        public IAsyncResult OnBegin(object sender, EventArgs e, AsyncCallback cb, object extraData)
        {
            HttpApplication app = (HttpApplication)sender;
            HttpContext context = app.Context;

            string path = context.Request.AppRelativeCurrentExecutionFilePath;
            switch(path)
            {
                case "~/connect":
                    Connect(context);
                    return new MyAsyncResult(true);
                case "~/send":
                    Send(context);
                    return new MyAsyncResult(true);
                case "~/receive":
                    Receive(context);
                    return new MyAsyncResult(false);
                case "~/disconnect":
                    Disconnect(context);
                    return new MyAsyncResult(true);
                case "~/sendThenReceive":
                    SendThenReceive(context);
                    return new MyAsyncResult(true);
                default:
                    context.Response.Close();
                    return new MyAsyncResult(true);
            }
        }

        public void OnEnd(IAsyncResult result)
        {
            
        }

        private void Connect(HttpContext context)
        {
            string from = context.Request.Headers["From"].Trim();
            Uri uri = new Uri("http://" + from);

            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(uri.Host, uri.Port);

            long key = GetNewKey();
            socketDic[key] = tcpClient;
            context.Response.Headers["ETag"] = key.ToString();
            context.ApplicationInstance.CompleteRequest();
        }

        private void Disconnect(HttpContext context)
        {
            long key = long.Parse(context.Request.Headers["From"]);
            if (socketDic.ContainsKey(key) == true)
            {
                socketDic[key].Close();
            }
        }

        private void SendThenReceive(HttpContext context)
        {
            string from = context.Request.Headers["From"].Trim();
            Uri uri = new Uri("http://" + from);

            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(uri.Host, uri.Port);

            try
            {
                byte[] buffer = new byte[4096];
                int receiveNumber;
                while (true)
                {
                    receiveNumber = context.Request.InputStream.Read(buffer, 0, buffer.Length);
                    if (receiveNumber == 0)
                    {
                        break;
                    }
                    tcpClient.GetStream().Write(buffer, 0, receiveNumber);
                }

                List<byte> header = new List<byte>();
                int headerEndIndex = -1;
                int contentLength = 0;
                int sentLength = 0;
                bool chunked = false;
                int? chunkeLength = null;
                List<byte> chunke = new List<byte>();
                bool zeroChunke = false;

                while (true)
                {
                    byte[] readBuffer = buffer;
                    receiveNumber = tcpClient.GetStream().Read(readBuffer, 0, readBuffer.Length);
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
                                    if(Helper.FindIndex(header, headerEndIndex, Encoding.ASCII.GetBytes("Content-Type:")) != -1)
                                    {
                                        contentLength = int.MaxValue;
                                    }
                                    else
                                    {
                                        contentLength = 0;
                                    }
                                }
                            }

                            byte[] realHeader = header.Take(headerEndIndex + 4).ToArray();
                            context.Response.OutputStream.Write(realHeader, 0, realHeader.Length);

                            receiveNumber = header.Count - headerEndIndex - 4;
                            readBuffer = header.GetRange(headerEndIndex + 4, receiveNumber).ToArray();
                        }
                    }

                    if (chunked == false)
                    {
                        context.Response.OutputStream.Write(readBuffer, 0, receiveNumber);
                        context.Response.Flush();

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
                                    context.Response.OutputStream.Write(chunke.ToArray(), 0, chunke.Count);
                                    context.Response.Flush();
                                    chunkeLength = null;
                                    chunke.Clear();
                                }
                            }
                        }
                        if (zeroChunke == true)
                        {
                            context.Response.OutputStream.Write(Helper.zeroChunk, 0, Helper.zeroChunk.Length);
                            context.Response.Flush();
                            break;
                        }
                    }
                }
                context.Response.OutputStream.Close();
                context.Response.Flush();
                context.ApplicationInstance.CompleteRequest();
            }
            catch
            {
                context.Response.Close();
            }
            finally
            {
                tcpClient.Close();
            }
        }

        private void Send(HttpContext context)
        {
            long key = long.Parse(context.Request.Headers["From"]);
            TcpClient tcpClient = socketDic[key];

            try
            {
                byte[] readBuffer = context.Request.BinaryRead(context.Request.TotalBytes);
                tcpClient.GetStream().Write(readBuffer, 0, readBuffer.Length);
                context.ApplicationInstance.CompleteRequest();
            }
            catch (Exception ex)
            {
                TcpClient client;
                socketDic.TryRemove(key, out client);
                tcpClient.Close();
                context.Response.Close();
            }
        }

        private async void Receive(HttpContext context)
        {
            long key = long.Parse(context.Request.Headers["From"]);
            TcpClient tcpClient = socketDic[key];

            try
            {
                byte[] readBuffer = new byte[4096];
                while (true)
                {
                    int receiveNumber = await tcpClient.GetStream().ReadAsync(readBuffer, 0, readBuffer.Length);
                    if (receiveNumber == 0)
                    {
                        throw new Exception();
                    }

                    context.Response.OutputStream.Write(readBuffer, 0, receiveNumber);
                    context.Response.Flush();
                }
            }
            catch (Exception ex)
            {
                TcpClient client;
                socketDic.TryRemove(key, out client);
                tcpClient.Close();
                context.Response.Close();
            }
        }

        public void Init(HttpApplication context)
        {
            context.AddOnBeginRequestAsync(OnBegin, OnEnd);
        }

        public void Dispose()
        {
            
        }
    }
}
