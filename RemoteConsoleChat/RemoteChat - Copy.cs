using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Data;
using System.ComponentModel;
using System.Reflection;

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
        public static Config chatConfig;

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
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnGameInitialize);

            Commands.ChatCommands.Add(new Command("RemoteConsoleChat.allow", remoteChat, "remotechat"));
            Commands.ChatCommands.Add(new Command("RemoteConsoleChat.allow", remoteChat, "rc"));
       }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
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
        private void OnGameInitialize(EventArgs args)
        {
            var path = Path.Combine(TShock.SavePath, "chatConfig.json");
            (chatConfig = Config.Read(path)).Write(path);
            if (chatConfig.chatOn)
            {
                Console.WriteLine("Start Listening");
                Communicate.setupPath();
                Console.WriteLine("Still Listening");
            }
        }
        private void OnJoin(GreetPlayerEventArgs e)
        {
            if (TShock.Players[e.Who] != null)
            {
                string message = "`1:" + TShock.Players[e.Who].Name + ":" + e.Who;
                Communicate.send(message);
            }

        }
        public void OnLeave(LeaveEventArgs e)
        {
            string message = "`1:" + " :" + e.Who;
            Communicate.send(message);
        }

        private void OnChat(ServerChatEventArgs args)
        {
            if (!args.Text.StartsWith("/"))
            {
                TSPlayer player = TShock.Players[args.Who];
                if (player != null)
                {
                    Communicate.setPlayer(player, args.Who);
                    string name = player.Name;
                    Communicate.send(String.Format("{0}->{1}", name, args.Text));
                    return;

                }
                args.Handled = false;
            }
        }

        private void remoteChat(CommandArgs args)
        {
            bool help = false;

            RemoteChatListArguments arguments = new RemoteChatListArguments(args.Parameters.ToArray());
            if (arguments.Contains("-help"))
                help = true;

            if (help)
            {
                args.Player.SendMessage("Syntax: /remotechate [-help] ", Color.Red);
                args.Player.SendMessage("Flags: ", Color.LightSalmon);
                args.Player.SendMessage("   -help     this information", Color.LightSalmon);
                return;
            }

            if (arguments.Contains("-start"))
            {

                try
                {
                    Console.WriteLine("Start Listening");
                    Communicate.setupPath();
                    Console.WriteLine("Still Listening");
                }
                catch
                {
                    Console.WriteLine("Unable to start clients chat");
                }
                return;
            }
            string input = string.Join(" ", args.Parameters);
 
            if (!Communicate.send(String.Format("{0}->{1}", args.Player.Name, input)))
                args.Player.SendErrorMessage("No one is listening to chat, please try later");
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
