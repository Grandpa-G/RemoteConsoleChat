using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using Terraria;
using System.ComponentModel;

namespace RemoteConsoleChat
{
    public class Config
    {
        public string consolePrivateChatPrefix;
        public bool debugChat;
        public int chatPort;
        public bool chatOn;
        public string consoleName;
        public int[] consolePrivateChatRGB;
        public int[] consoleChatRGB;
        public string ipServer;

        public Config Write(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            return this;
        }

        public static Config Read(string path)
        {
            if (!File.Exists(path))
            {
                 Config.WriteConfig(path);
            }
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
        }

        public static void WriteConfig(string file)
        {
            Config cfg = new Config();
            cfg.debugChat = false;
            cfg.ipServer = Terraria.Netplay.serverListenIP.ToString();
            cfg.chatPort = 25565;
            cfg.chatOn = true;
            cfg.consoleName = "Console";

            cfg.consoleChatRGB = new int[] { 255, 0, 0 };
            cfg.consolePrivateChatRGB = new int[] { 255, 255, 0 };
            cfg.consolePrivateChatPrefix = "[Private]";

            cfg.Write(file);
        }
    }
}

