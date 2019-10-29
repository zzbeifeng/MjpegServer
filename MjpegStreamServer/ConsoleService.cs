#region License
// /////////////////////////////////////////////////////////////////////////////
// Copyright (c) DIGIBIRD(http://www.digibird.com.cn)
// 北京小鸟科技股份有限公司保留所有代码著作权，如有任何疑问请访问官方网站与我们联系。
// 代码只针对特定客户使用，不得在未经允许或授权的情况下对外传播扩散，恶意传播者自行承担法律后果。
// 本代码仅用于北京小鸟科技股份有限公司的DSOC、DMIS等项目。
// 
// 创建日期:     2019年10月29日15:23
// 修改日期:     2019年10月29日15:23
// 文件名称:     ConsoleService.cs
// 创建作者:     LYJ
// 类 描 述 :  
// /////////////////////////////////////////////////////////////////////////////
#endregion

using System;
using System.Collections;
using System.IO;
using System.Threading;

namespace MjpegStreamServer
{
    public enum LogType
    {
        //
        // 摘要:
        //     LogType used for Errors.
        Error = 0,
        //
        // 摘要:
        //     LogType used for Asserts. (These could also indicate an error inside Unity itself.)
        Assert = 1,
        //
        // 摘要:
        //     LogType used for Warnings.
        Warning = 2,
        //
        // 摘要:
        //     LogType used for regular log messages.
        Log = 3,
        //
        // 摘要:
        //     LogType used for Exceptions.
        Exception = 4
    }

    public class ConsoleService
    {
        private float length;
        private Queue queue;
        private Queue dellQueue;
        private string lastDay;
        //private static readonly object lockobj = new object();
        private Thread writeThread;
        private bool running;
        //private static Semaphore sema = new Semaphore(0, 1);
        private string dataPath;

        private static ConsoleService _consoleService;
        public static ConsoleService GetInstance()
        {
            if (_consoleService == null)
            {
                _consoleService = new ConsoleService();
            }

            return _consoleService;
        } 

        public void Init()
        {

            dataPath = "./";

            queue = new Queue();
            dellQueue = new Queue();
            string dateStr = DateTime.Now.Date.ToString("yyyy-MM-dd");
            lastDay = dateStr.Substring(dateStr.Length - 2, 2);

            running = true;
            writeThread = new Thread(LogThread);
            writeThread.Start();
        }

        public void UnInit()
        {
            running = false;

            if (writeThread != null && writeThread.IsAlive)
            {
                writeThread.Join();
                writeThread = null;
            }

        }

        private void LogThread()
        {
            while (running)
            {
                //sema.WaitOne(5000);
                Thread.Sleep(2000);
                CheckLogs();
            }
        }

        public void LogMsg(string stackTrace, LogType type)
        {
            string _type = "";
            switch (type)
            {
                case LogType.Error:
                    _type = "error";
                    break;
                case LogType.Assert:
                    _type = "Assert";
                    break;
                case LogType.Warning:
                    _type = "Warning";
                    break;
                case LogType.Log:
                    _type = "Log";
                    break;
                case LogType.Exception:
                    _type = "Exception";
                    break;
                default:
                    break;
            }
            string msg = "[LOG]:" + stackTrace + "-" + "[LogType]:" + _type;

            lock (queue)
            {
                queue.Enqueue(msg);

                // sema.Release(1);
            }
        }

        private void CheckLogs()
        {

            lock (queue)
            {
                for (var i = 0; i < queue.Count; i++)
                {
                    dellQueue.Enqueue(queue.Dequeue());
                }
            }
            if (dellQueue.Count != 0)
            {
                string dateStr = DateTime.Now.Date.ToString("yyyy-MM-dd");
                string day = dateStr.Substring(dateStr.Length - 2, 2);

                string fname = dataPath + "/Log_" + day + ".txt";

                if (!day.Equals(lastDay))
                {
                    //清理 当前day 的日志文件
                    File.Delete(fname);
                }

                StreamWriter writer = new StreamWriter(fname, true, System.Text.Encoding.Default);

                foreach (var log in dellQueue)
                {
                    LogToFile(writer, log.ToString(), true, true);
                }
                dellQueue.Clear();

                writer.Close();
                writer.Dispose();

                lastDay = day;
            }
        }

        private void LogToFile(StreamWriter writer, string str, bool bwithTime, bool bAppendLineFeed)
        {
            if (str == null) return;
            try
            {

                if (bwithTime) writer.WriteLine("\r\n\r\n---------" + System.DateTime.Now.ToString());
                if (bAppendLineFeed) writer.WriteLine(str);
                else writer.Write(str);
            }
            catch
            {
                throw;
            }
        }

    }
}