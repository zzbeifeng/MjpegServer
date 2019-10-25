#region License
// /////////////////////////////////////////////////////////////////////////////
// Copyright (c) DIGIBIRD(http://www.digibird.com.cn)
// 北京小鸟科技股份有限公司保留所有代码著作权，如有任何疑问请访问官方网站与我们联系。
// 代码只针对特定客户使用，不得在未经允许或授权的情况下对外传播扩散，恶意传播者自行承担法律后果。
// 本代码仅用于北京小鸟科技股份有限公司的DSOC、DMIS等项目。
// 
// 创建日期:     2019年10月23日20:22
// 修改日期:     2019年10月23日20:22
// 文件名称:     MjpegServer.cs
// 创建作者:     LYJ
// 类 描 述 :  
// /////////////////////////////////////////////////////////////////////////////
#endregion

using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MjpegStreamServer
{
    public class MjpegServer
    {
        private Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);  //侦听socket
        private bool running = true;

        public bool Running
        {
            get { return running; }
        }

        private int port;
        public MjpegServer(int port)
        {
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        public void Start()
        {
            _socket.Listen(100);
            _socket.BeginAccept(new AsyncCallback(OnAccept), _socket);
            running = true;
        }

        public void Stop()
        {
            _socket.Disconnect(true);
        }
        /// <summary>
        /// http头部
        /// </summary>
        public string Header()
        {
            string header = "HTTP/1.1 200 OK\r\n" +
                            "Content-Type:multipart/x-mixed-replace; boundary=--unihub\r\n"+
                            "Cache-Control:no-store\r\n\r\n"; 

            return header;
        }

        /// <summary>
        /// 图片头部
        /// </summary>
        public  string PayloadHeader(long contentLength)
        {
            string header = "\r\n--unihub\r\n" +
                            "Content-Type:image/jpeg\r\n" +
                            "Content-Length:" + contentLength + "\r\n\r\n";

            return header;
        }

        /// <summary>
        /// 接受处理http的请求
        /// </summary>
        /// <param name="ar"></param>
        private void OnAccept(IAsyncResult ar)
        {
            Socket web_client = null;
            try
            {
                Socket socket = ar.AsyncState as Socket;
                web_client = socket.EndAccept(ar);

                web_client.SendBufferSize = (1024 * 1024);
                web_client.SendTimeout = 10000;

                socket.BeginAccept(new AsyncCallback(OnAccept), socket);

                byte[] recv_Buffer = new byte[1024 * 640];
                int recv_Count = web_client.Receive(recv_Buffer);
                string recv_request = Encoding.UTF8.GetString(recv_Buffer, 0, recv_Count);
                Console.WriteLine("Data Request:" + recv_request); //将请求显示到界面

                string routePath = RouteHandle(recv_request);

                string[] routePathParams = routePath.Split('/');
                if (routePathParams.Length<3)
                {
                    web_client.Close(10);
                    return;
                }

                //预监路径输出路数索引
                string previewOutputChannel = routePathParams[2];

                Console.WriteLine("Data Request previewOutputChannel :" + previewOutputChannel);

                //报文头部
                string head = Header();
                byte[] headBytes = Encoding.UTF8.GetBytes(head);
                web_client.Send(headBytes);

                //根据预监输出图像路数索引值去预监图像服务查找图像数据
                while (web_client.Connected)
                {
                    Bitmap screenPreviewBitmap = PreviewImageService.GetInstance().GetScreenPreviewBitmap(int.Parse(previewOutputChannel));
                    lock (screenPreviewBitmap)
                    {
                        if (screenPreviewBitmap == null)
                        {
                            web_client.Close();
                            return;
                        }

                        MemoryStream ms = new MemoryStream();
                        screenPreviewBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        byte[] screenBitmapBytes = ms.GetBuffer();
                        ms.Close();
                        ms.Dispose();

                        byte[] payloadarray = Encoding.ASCII.GetBytes(PayloadHeader(screenBitmapBytes.Length));
                        web_client.Send(payloadarray);
                        //图像数据
                        web_client.Send(screenBitmapBytes);

                    }

                    Thread.Sleep(1000 / Program.frame);
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("处理http请求断开" + Environment.NewLine + "\t" + ex.Message);
            }
            finally
            {
                web_client.Close(10);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private string RouteHandle(string request)
        {
            string retRoute = "";
            string[] strs = request.Split(new string[] { "\r\n" }, StringSplitOptions.None); 
            
            if (strs.Length > 0) 
            {
                string[] items = strs[0].Split(' '); 
                string pageName = items[1];
                string post_data = strs[strs.Length - 1]; 
                
                retRoute = pageName + (post_data.Length > 0 ? "?" + post_data : "");
            }

            return retRoute;

        }

    }
}