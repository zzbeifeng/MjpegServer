﻿#region License
// /////////////////////////////////////////////////////////////////////////////
// 创建日期:     2019年10月24日11:37
// 修改日期:     2019年10月24日11:37
// 文件名称:     PreviewImageService.cs
// 创建作者:     LYJ
// 类 描 述 :  
// /////////////////////////////////////////////////////////////////////////////
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace MjpegStreamServer
{
    public class PreviewImageService
    {
        private static PreviewImageService instance;
        private static object lockObj = new object();
        private bool running;
        private Thread thread;
        private List<RestfulScreenData> screenDataList;
        private Dictionary<int, Bitmap> screenBitmapDict = new Dictionary<int, Bitmap>();
        public static PreviewImageService GetInstance()
        {
            lock (lockObj)
            {
                if (instance == null)
                {
                    instance = new PreviewImageService();
                }
            }

            return instance;
        }

        /// <summary>
        /// 设置预监屏幕单个输出左右偏移尺寸 是否要左右跨屏画面
        /// </summary>
        /// <param name="screenIndex"></param>
        /// <param name="paddingLeft"></param>
        /// <param name="paddingRight"></param>
        public void SetScreenPadding(int screenIndex,int paddingLeft,int paddingRight)
        {
            //配置
            if (screenDataList.Count <= screenIndex || screenIndex < 0)
            {
                return;
            }

            lock (screenDataList)
            {
                screenDataList[screenIndex].paddingRight = paddingRight.ToString();
                screenDataList[screenIndex].paddingLeft = paddingLeft.ToString();
            }
            
        }

        public void Init(List<RestfulScreenData> screenNodeList)
        {
            screenDataList = screenNodeList;
            int count = screenNodeList.Count;
            int previewGridCount = Program.gridX * Program.gridY;
            int previewTotal = ((count - 1) / previewGridCount) + 1;

            for (int previewIndex=0; previewIndex< previewTotal;previewIndex++)
            {
                Bitmap screenBmp = new Bitmap(Program.previewWidth, Program.previewheight);
                
                screenBitmapDict.Add(previewIndex,screenBmp);
            }

            running = true;

            //启动分屏幕抓取任务
            thread = new Thread(CaptureScreenTask);
            thread.Start();
        }

        public void UnInit()
        {
            foreach (var keyValuePair in screenBitmapDict)
            {
                keyValuePair.Value.Dispose();
            }
            screenBitmapDict.Clear();

            thread.Join(10);
        }

        private void CaptureScreenTask()
        {
            while (running)
            {
                //检查是否有连接
                lock (Program.mjpegServer.SocketList)
                {
                    if (Program.mjpegServer.SocketList.Count == 0)
                    {
                        Thread.Sleep(1000 / Program.frame);

                        continue;
                    }
                }

                int previewGridCount = Program.gridX * Program.gridY;

                int previewTotal = 0;
                lock (screenDataList)
                {
                    previewTotal = ((screenDataList.Count - 1) / previewGridCount) + 1;
                }

                for (int outputChannel = 0; outputChannel < previewTotal; outputChannel++)
                {
                    Bitmap outputPreviewBitmap = GetScreenPreviewBitmap(outputChannel);
                    lock (outputPreviewBitmap)
                    {
                        System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(outputPreviewBitmap);

                        //清空画布并以透明背景色填充
                        g.Clear(System.Drawing.Color.Transparent);

                        for (int oid = 1; oid <= previewGridCount; oid++)
                        {
                            int screenId = oid + outputChannel * previewGridCount;

                            RestfulScreenData restfulScreenData = GetRestfulScreenData(screenId);

                            if (restfulScreenData == null)
                            {
                                continue;
                            }

                            int previewStartId = ((screenId - 1) - outputChannel * previewGridCount);
                            int targetX = previewStartId % Program.gridX;
                            int targetY = previewStartId / Program.gridX;

                            //左右偏移
                            Rectangle captuRectangle = new Rectangle(int.Parse(restfulScreenData.x) - int.Parse(restfulScreenData.paddingLeft), 
                                int.Parse(restfulScreenData.y),
                                int.Parse(restfulScreenData.width) + int.Parse(restfulScreenData.paddingLeft) + int.Parse(restfulScreenData.paddingRight), 
                                int.Parse(restfulScreenData.height));

                            try
                            {

                                Bitmap screenBitmap = ImageHelper.CaptureScreen(captuRectangle);

                                //设置高质量插值法
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Default;

                                //设置高质量,低速度呈现平滑程度
                                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                                Rectangle targetRectangle = new System.Drawing.Rectangle(
                                    targetX * Program.previewWidth / Program.gridX,
                                    targetY * Program.previewheight / Program.gridY, Program.previewWidth / Program.gridX,
                                    Program.previewheight / Program.gridY);

                                ImageHelper.MakeThumbnail(targetRectangle, screenBitmap, g);

                                Console.WriteLine("###CaptureScreenTask previewStartId " + previewStartId + " targetRectangle.x " + targetRectangle.X + " targetRectangle.y " + targetRectangle.Y);

                                //是否分屏幕图片内存
                                screenBitmap.Dispose();

                            }
                            catch (Exception e)
                            {
                                ConsoleService.GetInstance().LogMsg(" CaptureScreenTask "+e.StackTrace, LogType.Error);
                            }
                           
                        }

                        g.Dispose();
                    }
                }

                Thread.Sleep(1000 / Program.frame);
            }
        }

        public Bitmap GetScreenPreviewBitmap(int channel)
        {
            Bitmap screenBitmap;

            lock (lockObj)
            {
                screenBitmapDict.TryGetValue(channel, out screenBitmap);
            }
          
            return screenBitmap;
        }

        public RestfulScreenData GetRestfulScreenData(int screenId)
        {
            RestfulScreenData screenData = null;
            foreach (var restfulScreenData in screenDataList)
            {
                if (int.Parse(restfulScreenData.id) == screenId)
                {
                    screenData = restfulScreenData;
                    break;
                }
            }

            return screenData;
        }
    }
}