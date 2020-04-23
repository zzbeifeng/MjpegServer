using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;

/// <summary>
//  http://192.168.2.107:9191/preview/0/normal
/// </summary>
namespace MjpegStreamServer
{
    public class RestfulScreenData
    {
        [DefaultValue("0")]
        public string id { get; set; }
        [DefaultValue("0")]
        public string x { get; set; }
        [DefaultValue("0")]
        public string y { get; set; }
        [DefaultValue("0")]
        public string width { get; set; }
        [DefaultValue("0")]
        public string height { get; set; }
        [DefaultValue("0")]
        public string paddingLeft { get; set; }
        [DefaultValue("0")]
        public string paddingRight { get; set; }

    }

    class Program
    {
        public static string ServerIp = "";
        public static int port = 9191;
        public static int gridX = 1;
        public static int gridY = 1;
        public static int previewWidth = 1920;
        public static int previewheight = 1080;
        public static int frame = 15;
        public static List<RestfulScreenData> screenNodeList = new List<RestfulScreenData>();
        public static MjpegServer mjpegServer;

        static void Main(string[] args)
        {
            ConsoleService.GetInstance().Init();
            ConsoleService.GetInstance().LogMsg(" Start Mjpeg Server",LogType.Log);

            string configXml = "./AppScreensSettings.xml";

            if (args.Length >= 7)
            {
                port = int.Parse(args[0]);
                gridX = int.Parse(args[1]);
                gridY = int.Parse(args[2]);
                previewWidth = int.Parse(args[3]);
                previewheight = int.Parse(args[4]);
                frame = int.Parse(args[5]);
                configXml = args[6];

                ConsoleService.GetInstance().LogMsg(" Start Mjpeg Server args " + args[0] + " " +
                                                    args[1] + " " +
                                                    args[2] + " " +
                                                    args[3] + " " +
                                                    args[4] + " " +
                                                    args[5] + " " +
                                                    args[6], LogType.Log);

            }
            else
            {
                //warnning
                ConsoleService.GetInstance().LogMsg(" Start Mjpeg Server without args ", LogType.Log);
            }

            ConsoleService.GetInstance().LogMsg(" XmlDocument load "+configXml, LogType.Log);

            try
            {

                var xmlDoc = new XmlDocument();
                xmlDoc.Load(configXml);
                XmlElement root = xmlDoc.DocumentElement;
                XmlNodeList xmlScreenNodeList = root.SelectNodes("/AppSettings/Screes/Screen");
                foreach (XmlNode screenNode in xmlScreenNodeList)
                {
                    XmlNode curXmlNode = screenNode.FirstChild;
                    string id = curXmlNode.InnerText;
                    curXmlNode = curXmlNode.NextSibling;

                    string x = curXmlNode.InnerText;
                    curXmlNode = curXmlNode.NextSibling;

                    string y = curXmlNode.InnerText;
                    curXmlNode = curXmlNode.NextSibling;

                    string width = curXmlNode.InnerText;
                    curXmlNode = curXmlNode.NextSibling;

                    string height = curXmlNode.InnerText;

                    RestfulScreenData screenData = new RestfulScreenData();
                    screenData.id = id;
                    screenData.x = x;
                    screenData.y = y;
                    screenData.width = width;
                    screenData.height = height;
                    screenData.paddingRight = "0";
                    screenData.paddingLeft = "0";
                    screenNodeList.Add(screenData);
                }

                //启动预监服务器
                mjpegServer = new MjpegServer(port);
                mjpegServer.Start();

                ConsoleService.GetInstance().LogMsg(" Start PreviewImageService", LogType.Log);

                //启动预监画面服务
                PreviewImageService.GetInstance().Init(screenNodeList);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                ConsoleService.GetInstance().LogMsg("Start Error " +  e.Message,LogType.Error);

            }
            
            string cmd = Console.ReadLine();
            while (!cmd.Equals("bye"))
            {
                cmd = Console.ReadLine();
            }
        }
    }
}
