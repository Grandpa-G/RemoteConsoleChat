using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Data;
using System.ComponentModel;
using System.Reflection;
using System.Drawing;

using Terraria;
using TShockAPI;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Threading;
using TerrariaApi.Server;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;

namespace RemoteConsoleChat
{
    [ApiVersion(1, 17)]
    public class RemoteChat : TerrariaPlugin
    {
        public TcpListener listner;
        public Config chatConfig;
        public static Config statConfig;
        public static bool chatActive = false;

        public override string Name
        {
            get { return "RemoteConsoleChat"; }
        }
        public override string Author
        {
            get { return "Granpa-G"; }
        }
        public override string Description
        {
            get { return "Provides a remote console chat interface."; }
        }
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }
        public RemoteChat(Main game)
            : base(game)
        {
            Order = -1;
        }
        public override void Initialize()
        {
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave, 100);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnGameInitialize);

            Commands.ChatCommands.Add(new Command("RemoteConsoleChat.allow", remoteChat, "remotechat"));
            Commands.ChatCommands.Add(new Command("RemoteConsoleChat.allow", remoteChat, "rc"));
 
       }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnGameInitialize);
                try
                {
                    listner.Stop();
                    if (listner != null)
                        listner = null;
                }
                catch { }
            }
            base.Dispose(disposing);
        }
        SocketServer connection;
        Thread communication = null;
        private void OnGameInitialize(EventArgs args)
        {
            var ConfigPath = Path.Combine(TShock.SavePath, "chatConfig.json");
            try
            {
                chatConfig = Config.Read(ConfigPath).Write(ConfigPath);
            }
            catch (Exception ex)
            {
                chatConfig = new Config();
                TShock.Log.ConsoleError("[RemoteChat] An exception occurred while parsing the RemoteChat config!\n{0}".SFormat(ex.ToString()));
            }

            statConfig = chatConfig;
            if (chatConfig.chatOn)
            {
                communication = new Thread(() =>
                                {
                                    Thread.CurrentThread.IsBackground = false;
                                    Console.WriteLine("Start Listening for chat");
                                    connection = new SocketServer();
                                });
                communication.Start();
                chatActive = true;
            }
        }
        private void OnJoin(GreetPlayerEventArgs e)
        {
            if (TShock.Players[e.Who] != null)
            {
                string message = "`1:" + TShock.Players[e.Who].Name + ":" + TShock.Players[e.Who].Index;
                connection.SendMessageConsole(message);
                message = "`8:``" + TShock.Players[e.Who].Name + " has joined. IP:" + TShock.Players[e.Who].IP;
                connection.SendMessageConsole(message);
            }
            else
            {
                string message = "`1:" + " :0";
                connection.SendMessageConsole(message);
            }

        }
        private void OnLeave(LeaveEventArgs e)
        {
            if (TShock.Players[e.Who] != null)
            {
                string message = "`1:" + " :" + TShock.Players[e.Who].Index;
                connection.SendMessageConsole(message);
                message = "`8:``" + TShock.Players[e.Who].Name + " has left. IP:" + TShock.Players[e.Who].IP;
                connection.SendMessageConsole(message);
            }
            else
            {
                string message = "`1:" + " :0";
                connection.SendMessageConsole(message);
            }
        }

        private void OnChat(ServerChatEventArgs args)
        {
            args.Handled = true;
            if (!chatActive)
                return;

            TSPlayer player = TShock.Players[args.Who];
            if (player != null)
            {
                string name = player.Name;
                string text;
                if (args.Text.ToLower().StartsWith("/login"))
                {
                    string[] msg = args.Text.Split(' ');
                    text = String.Format("{0} {1} *****", msg[0], msg[1]);
                }
                else
                    text = args.Text;

                if (args.Text.StartsWith("/"))
                {
                    string message = "`8:``" + player.Group.Prefix + " " + player.Name + ": " + text;
                    connection.SendMessageConsole(message);
                }
                else
                {
                    connection.SendMessageConsole(String.Format("{0}: {1}", player.Group.Prefix + " " + name, args.Text));
                    return;
                }
            }
        }

        private void remoteChat(CommandArgs args)
        {
            RemoteChatListArguments arguments = new RemoteChatListArguments(args.Parameters.ToArray());

            if (arguments.Contains("-help"))
            {
                args.Player.SendMessage("Syntax: /remotechat [-help] ", Color.Red);
                args.Player.SendMessage("Flags: ", Color.LightSalmon);
                args.Player.SendMessage("   -help     this information", Color.LightSalmon);
                return;
            }

            if (arguments.Contains("-x"))
            {
                connection.x();
                return;
            }
            if (arguments.Contains("-clear"))
            {
                connection.clearConnections();
                return;
            }
            if (arguments.Contains("-stop"))
            {
                try
                {
                    if (connection != null)
                        connection.CloseConnection();
                    chatActive = false;
                }
                catch
                {
                    Console.WriteLine("Unable to close chat connections");
                }
                return;
            }
            if (arguments.Contains("-start"))
            {
                try
                {
                    if (communication == null)
                    {
                        communication.Start();
                        chatActive = true;
                    }
                    else
                    {
                        Console.WriteLine("Already Listening");
                        return;
                    }
                    Console.WriteLine("Start Listening");
                }
                catch
                {
                    Console.WriteLine("Unable to start clients chat");
                }
                return;
            }
            string message = string.Join(" ", args.Parameters);

            Color c = new Color();
            c.R = (byte)TShockAPI.TShock.Config.SuperAdminChatRGB[0];
            c.G = (byte)TShockAPI.TShock.Config.SuperAdminChatRGB[1];
            c.B = (byte)TShockAPI.TShock.Config.SuperAdminChatRGB[2];
            if (!connection.SendMessage(args.Player.Index, String.Format("{0}: {1}", args.Player.Name, message)))
                TShockAPI.TShock.Players[args.Player.Index].SendMessage(chatConfig.consoleName + " has left chat.", c);

            return;

        }
    }
    #region application specific commands
    public class RemoteChatListArguments : InputArguments
    {
        public string Verbose
        {
            get { return GetValue("-verbose"); }
        }
        public string VerboseShort
        {
            get { return GetValue("-v"); }
        }

        public string Help
        {
            get { return GetValue("-help"); }
        }


        public RemoteChatListArguments(string[] args)
            : base(args)
        {
        }

        protected bool GetBoolValue(string key)
        {
            string adjustedKey;
            if (ContainsKey(key, out adjustedKey))
            {
                bool res;
                bool.TryParse(_parsedArguments[adjustedKey], out res);
                return res;
            }
            return false;
        }
    }
    #endregion

}
