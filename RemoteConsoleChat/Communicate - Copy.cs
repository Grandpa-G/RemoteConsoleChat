using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
// http://www.codeproject.com/Articles/1608/Asynchronous-socket-communication

namespace RemoteConsoleChat
{

    // State object for reading client data asynchronously
    public class Communicate
    {
        private static TShockAPI.TSPlayer currentPlayer;
        private static int playerIndex;
        public static void setPlayer(TShockAPI.TSPlayer player, int index)
        {
            currentPlayer = player;
            playerIndex = index;

        }
        public static void setupPath()
        {
            IPAddress[] aryLocalAddr = null;
            string strHostName = "";
            IPAddress ipAddress = Terraria.Netplay.serverIP;
             try
            {
                // NOTE: DNS lookups are nice and all but quite time consuming.
                strHostName = Dns.GetHostName();
                IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);
                aryLocalAddr = ipEntry.AddressList;
 
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error trying to get local address {0} ", ex.Message);
            }

            // Verify we got an IP address. Tell the user if we did
            if (aryLocalAddr == null || aryLocalAddr.Length < 1)
            {
                Console.WriteLine("Unable to get local address");
                return;
            }
            Console.WriteLine("Listening on : [{0}] {1}", strHostName, aryLocalAddr[0]);
            // Create the socket object
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint epServer = new IPEndPoint(IPAddress.Parse(RemoteChat.chatConfig.ipServer), RemoteChat.chatConfig.chatPort);
            listener.Bind(epServer);
            // Create the listener socket in this machines IP address
 //           Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
 //           listener.Bind(new IPEndPoint(aryLocalAddr[0], RemoteChat.chatConfig.chatPort));
             listener.Listen(10);

            // Setup a callback to be notified of connection requests
            listener.BeginAccept(new AsyncCallback(Communicate.OnConnectRequest), listener);

        }
        static Socket client = null;
        static Socket listener;
        public static void OnConnectRequest(IAsyncResult ar)
        {
            listener = (Socket)ar.AsyncState;
            client = listener.EndAccept(ar);
//            Console.WriteLine("Client {0}, joined", listener.RemoteEndPoint);
            Console.WriteLine("Client {0}, joined", client.RemoteEndPoint);

            // Get current date and time.
            DateTime now = DateTime.Now;
            string strDateLine = "Welcome " + now.ToString("G") + " Version: " + Assembly.GetExecutingAssembly().GetName().Version + ":" + client.RemoteEndPoint;

            // Convert to byte array and send.
            Byte[] byteDateLine = System.Text.Encoding.ASCII.GetBytes(strDateLine.ToCharArray());
            client.Send(byteDateLine, byteDateLine.Length, 0);

            listener.BeginAccept(new AsyncCallback(OnConnectRequest), listener);
            try
            {
                AsyncCallback recieveData = new AsyncCallback(OnRecievedData);
                client.BeginReceive(m_byBuff, 0, m_byBuff.Length, SocketFlags.None, recieveData, client);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message +  " Setup Recieve Callback failed!");
            }
 
        }
        public static bool send(string message)
        {
            if (listener == null)
                return false;
 
            // Convert to byte array and send.
            Byte[] byteDateLine =  System.Text.Encoding.ASCII.GetBytes(message.ToCharArray());
            client.Send(byteDateLine, byteDateLine.Length, 0);

            SetupRecieveCallback(client);
            return true;
        }
        private static byte[] m_byBuff = new byte[256];    // Recieved data buffer
        public static void SetupRecieveCallback(Socket sock)
        {
            try
            {
                AsyncCallback recieveData = new AsyncCallback(OnRecievedData);
                sock.BeginReceive(m_byBuff, 0, m_byBuff.Length, SocketFlags.None, recieveData, sock);
            }
            catch (Exception ex)
            {
               Console.WriteLine(ex.Message + " Setup Recieve Callback failed!");
            }
        }
        public static void OnRecievedData(IAsyncResult ar)
        {
            // Socket was the passed in object
            Socket sock = (Socket)ar.AsyncState;

            // Check if we got any data
            try
            {
                int nBytesRec = sock.EndReceive(ar);
                int pIndex = 0;
                if (nBytesRec > 0)
                {
                    // Wrote the data to the List
                    string sRecieved = Encoding.ASCII.GetString(m_byBuff, 0, nBytesRec);
//    if(RemoteChat.chatConfig.debugChat)
                    Console.WriteLine(sRecieved);
                    if (sRecieved.StartsWith("`0:"))
                    {
                        string pi = sRecieved.Substring(3, 4);
                        if (Int32.TryParse(pi, out pIndex))
                        {
                            TShockAPI.TShock.Players[pIndex].SendMessage(RemoteChat.chatConfig.consoleName + ":" + sRecieved.Substring(8), Color.AliceBlue);
                            // If the connection is still usable restablish the callback
                            Communicate.send(String.Format("{0}->{1}", TShockAPI.TShock.Players[pIndex].Name, sRecieved.Substring(8)));
                            SetupRecieveCallback(sock);
                        }
                        else
                            Console.WriteLine("Bad player {0}", sRecieved);
                    }
                }
                else
                {
                    // If no data was recieved then the connection is probably dead
                    Console.WriteLine("Client {0}, disconnected", sock.RemoteEndPoint);
                    if (sock != null)
                    {
                        sock.Shutdown(SocketShutdown.Both);
                        sock.Close();
                    }
                }
            }
            catch (Exception)
            {
                // If no data was recieved then the connection is probably dead
                Console.WriteLine("Client {0}, disconnected", sock.RemoteEndPoint);
                if (sock != null)
                {
                    sock.Shutdown(SocketShutdown.Both);
                    sock.Close();
                }
                listener = null;
            }
        }

    }
}