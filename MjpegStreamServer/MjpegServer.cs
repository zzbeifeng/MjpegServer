#region License
// /////////////////////////////////////////////////////////////////////////////
// 创建日期:     2019年10月23日20:22
// 修改日期:     2019年10月23日20:22
// 文件名称:     MjpegServer.cs
// 创建作者:     LYJ
// 类 描 述 :  
// /////////////////////////////////////////////////////////////////////////////
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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

        private List<Socket> _socketList;
        public List<Socket> SocketList
        {
            get { return _socketList; }
        }
        
        public bool Running
        {
            get { return running; }
        }

        public MjpegServer(int port)
        {
            _socketList = new List<Socket>();

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

        private static ImageCodecInfo GetImageCodecInfo(ImageFormat imageFormat)
        {
            ImageCodecInfo[] imageCodecInfoArr = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo imageCodecInfo in imageCodecInfoArr)
            {
                if (imageCodecInfo.FormatID == imageFormat.Guid)
                {
                    return imageCodecInfo;
                }
            }
            return null;
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
                
                web_client.SendBufferSize = 1024;
                web_client.SendTimeout = 10000;
                
                socket.BeginAccept(new AsyncCallback(OnAccept), socket);

                lock (_socketList)
                {
                    if (_socketList.IndexOf(web_client) == -1)
                    {
                        _socketList.Add(web_client);
                    }
                }

                byte[] recv_Buffer = new byte[1024 * 640];
                int recv_Count = web_client.Receive(recv_Buffer);
                string recv_request = Encoding.UTF8.GetString(recv_Buffer, 0, recv_Count);
                Console.WriteLine("Data Request:" + recv_request); //将请求显示到界面

                ConsoleService.GetInstance().LogMsg("Data Request:" + recv_request, LogType.Log);

                string routePath = RouteHandle(recv_request);

                ConsoleService.GetInstance().LogMsg("Data Request routePath :" + routePath, LogType.Log);

                Console.WriteLine("Data Request routePath :" + routePath);

                //解析地址首字段 /preview/0/normal  , /setting/padding/0/left/right , /heart/

                string[] routePathParams = routePath.Split('/');
                //参数问题
                if (routePathParams.Length<2)
                {
                    web_client.Close(10);
                }
                else
                {
                    string requestCmd = routePathParams[1];

                    if (requestCmd.Equals("preview"))
                    {
                        //预监
                        DoPreview(web_client, routePathParams);
                    }
                    else if(requestCmd.Equals("setting"))
                    {
                        //设置
                        DoSetting(web_client, routePathParams);
                    }
                    else if (requestCmd.Equals("heart"))
                    {
                        //设置
                        DoHeart(web_client, routePathParams);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("处理http请求断开" + Environment.NewLine + "\t" + ex.Message);
            }
            finally
            {
                lock (_socketList)
                {
                    if (_socketList.IndexOf(web_client) >= 0)
                    {
                        _socketList.Remove(web_client);
                    }
                }
                web_client.Close(10);
                web_client.Dispose();

                ConsoleService.GetInstance().LogMsg("web_client.Close ", LogType.Log);

            }
        }

        private void DoHeart(Socket web_client, string[] routePathParams)
        { 
            //报文头部
            string head = Header();
            byte[] headBytes = Encoding.UTF8.GetBytes(head);
            web_client.Send(headBytes);

            byte[] result = Encoding.ASCII.GetBytes("SUCCESS");
            web_client.Send(result);
            web_client.Close(10);
            web_client.Dispose();
        }

        private void DoSetting(Socket web_client, string[] routePathParams)
        {

            if (routePathParams.Length < 4)
            {
                return;
            }
            
            // /setting/padding/0/left/right
            int previewOutputChannel = int.Parse(routePathParams[3]) - 1;
            int paddingLeft = int.Parse(routePathParams[4]);
            int paddingRight = int.Parse(routePathParams[5]);

            ConsoleService.GetInstance().LogMsg("DoSetting previewOutputChannel " + previewOutputChannel +
                                                " paddingLeft " + paddingLeft +
                                                " paddingRight " + paddingRight, LogType.Log);

            PreviewImageService.GetInstance().SetScreenPadding(previewOutputChannel, paddingLeft,paddingRight);


            //报文头部
            string head = Header();
            byte[] headBytes = Encoding.UTF8.GetBytes(head);
            web_client.Send(headBytes);

            byte[] result = Encoding.ASCII.GetBytes("SUCCESS");
            web_client.Send(result);
            web_client.Close(10);
            web_client.Dispose();
        }

        private void DoPreview(Socket web_client, string[] routePathParams)
        {
            if (routePathParams.Length < 4)
            {
                return;
            }

            try
            {
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

                        EncoderParameters encoderParameters = new EncoderParameters(1);
                        EncoderParameter encoderParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 50L);
                        encoderParameters.Param[0] = encoderParameter;

                        MemoryStream ms = new MemoryStream();

                        screenPreviewBitmap.Save(ms, GetImageCodecInfo(ImageFormat.Jpeg), encoderParameters);

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
                web_client.Dispose();

                ConsoleService.GetInstance().LogMsg("web_client.Close ", LogType.Log);
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