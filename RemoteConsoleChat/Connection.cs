/*
 * Created by SharpDevelop.
 * User: Jayan Nair
 * Date: 02/01/2005
 * Time: 2:54 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;
using System.Reflection;
using System.Drawing;
using System.Collections.Concurrent;
using System.Text;

using TShockAPI;

namespace RemoteConsoleChat
{
    /// <summary>
    /// Description of SocketServer.	
    /// </summary>
    public class SocketServer
    {
        public AsyncCallback pfnWorkerCallBack;
        private Socket m_mainSocket;
        private int adminConnection = -1;
        private Socket consoleSocket;

        // We know how many items we want to insert into the ConcurrentDictionary. 
        // So set the initial capacity to some prime number above that, to ensure that 
        // the ConcurrentDictionary does not need to be resized while initializing it. 
        static int initialCapacity = 101;

        // The higher the concurrencyLevel, the higher the theoretical number of operations 
        // that could be performed concurrently on the ConcurrentDictionary.  However, global 
        // operations like resizing the dictionary take longer as the concurrencyLevel rises.  
        // For the purposes of this example, we'll compromise at numCores * 2. 
        int numProcs = Environment.ProcessorCount;
        static int concurrencyLevel = Environment.ProcessorCount * 2;

        // Construct the dictionary with the desired concurrencyLevel and initialCapacity
        ConcurrentDictionary<string, Socket> channelSockets = new ConcurrentDictionary<string, Socket>(concurrencyLevel, initialCapacity);


        // An ArrayList is used to keep track of worker sockets that are designed
        // to communicate with each connected client. Make it a synchronized ArrayList
        // For thread safety
        private System.Collections.ArrayList m_workerSocketListx =
                ArrayList.Synchronized(new System.Collections.ArrayList());
        private string[] m_playerChannelList = new string[TShock.Config.MaxSlots];


        // The following variable will keep track of the cumulative 
        // total number of clients connected at any time. Since multiple threads
        // can access this variable, modifying this variable should be done
        // in a thread safe manner
        private int m_clientCount = 0;

        public SocketServer()
        {
            for (int i = 0; i < m_playerChannelList.Length; i++)
                m_playerChannelList[i] = null;
            Connect();
        }

        public void Connect()
        {
            try
            {
                // Create the listening socket...
                m_mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint ipLocal = new IPEndPoint(IPAddress.Parse(RemoteChat.statConfig.ipServer), RemoteChat.statConfig.chatPort);
                // Bind to local IP Address...
                m_mainSocket.Bind(ipLocal);
                // Start listening...
                m_mainSocket.Listen(4);
                // Create the call back for any client connections...
                m_mainSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
            }
        }
        public void x()
        {
            foreach (var cs in channelSockets)
            {
                Console.WriteLine("Value of the Dictionary Item is: {0}:{1}", cs.Key, cs.Value);
            }
            for (int i = 0; i < m_playerChannelList.Length; i++)
                if (m_playerChannelList[i] != null)
                    Console.WriteLine(i + ":" + m_playerChannelList[i]);
        }
        // This is the call back function, which will be invoked when a client is connected
        public void OnClientConnect(IAsyncResult asyn)
        {
            try
            {
                // Here we complete/end the BeginAccept() asynchronous call
                // by calling EndAccept() - which returns the reference to
                // a new Socket object
                Socket workerSocket = m_mainSocket.EndAccept(asyn);

                // Now increment the client count for this client 
                // in a thread safe manner
                Interlocked.Increment(ref m_clientCount);
                adminConnection = m_clientCount;
                // Add the workerSocket reference to our ArrayList

                channelSockets.GetOrAdd(workerSocket.RemoteEndPoint.ToString(), workerSocket);
                if (channelSockets.Count == 1)
                    consoleSocket = workerSocket;

                // Send a welcome message to client
                string msg = "Welcome client " + m_clientCount + "\n";
                Console.WriteLine("Client {0}-{1}, joined", workerSocket.LocalEndPoint, workerSocket.RemoteEndPoint);

                // Get current date and time.
                DateTime now = DateTime.Now;
                string strDateLine = "`3:" + "``Welcome " + now.ToString("G") + " Version: " + Assembly.GetExecutingAssembly().GetName().Version + "|" + workerSocket.RemoteEndPoint;

                // Convert to byte array and send.
                Byte[] byteDateLine = System.Text.Encoding.UTF8.GetBytes(strDateLine.ToCharArray());
                                SendMessageConsole(strDateLine);

                // Let the worker Socket do the further processing for the 
                // just connected client
                WaitForData(workerSocket, m_clientCount);

                // Since the main Socket is now free, it can go back and wait for
                // other clients who are attempting to connect
                m_mainSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\n OnClientConnection: Socket has been closed\n");
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
            }

        }
        public class SocketPacket
        {
            // Constructor which takes a Socket and a client number
            public SocketPacket(System.Net.Sockets.Socket socket, int clientNumber)
            {
                m_currentSocket = socket;
                m_clientNumber = clientNumber;
            }
            public System.Net.Sockets.Socket m_currentSocket;
            public int m_clientNumber;
            // Buffer to store the data sent by the client
            public byte[] dataBuffer = new byte[1024];
        }
        // Start waiting for data from the client
        public void WaitForData(System.Net.Sockets.Socket soc, int clientNumber)
        {
            try
            {
                if (pfnWorkerCallBack == null)
                {
                    // Specify the call back function which is to be 
                    // invoked when there is any write activity by the 
                    // connected client
                    pfnWorkerCallBack = new AsyncCallback(OnDataReceived);
                }
                SocketPacket theSocPkt = new SocketPacket(soc, clientNumber);

                soc.BeginReceive(theSocPkt.dataBuffer, 0, theSocPkt.dataBuffer.Length, SocketFlags.None, pfnWorkerCallBack, theSocPkt);
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
            }
        }
        // This the call back function which will be invoked when the socket
        // detects any client writing of data on the stream
        public void OnDataReceived(IAsyncResult asyn)
        {
            SocketPacket socketData = (SocketPacket)asyn.AsyncState;
            try
            {
                // Complete the BeginReceive() asynchronous call by EndReceive() method
                // which will return the number of characters written to the stream 
                // by the client

                int iRx = socketData.m_currentSocket.EndReceive(asyn);

                if (iRx < 3)
                    return;     // not our packet.

                char[] chars = new char[iRx];
                // Extract the characters as a buffer
                System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                int charLen = d.GetChars(socketData.dataBuffer, 0, iRx, chars, 0);

                System.String sRecieved = new System.String(chars);

                string msg = "" + socketData.m_clientNumber + ":";

                byte[] byteData = null;

                Socket workerSocket = (Socket)socketData.m_currentSocket;
                int pIndex = 0;
                string action = "";
                try
                {
                    action = sRecieved.Substring(0, 3);
                }
                catch
                {
                    return;     // packet too small
                }
                string message = "";
                if (sRecieved.Length > 8)
                    message = sRecieved.Substring(8);

                Color chatColor = new Color();
                chatColor.R = (byte)RemoteChat.statConfig.consoleChatRGB[0];
                chatColor.G = (byte)RemoteChat.statConfig.consoleChatRGB[1];
                chatColor.B = (byte)RemoteChat.statConfig.consoleChatRGB[2];
                Color privateChatColor = new Color();
                privateChatColor.R = (byte)RemoteChat.statConfig.consolePrivateChatRGB[0];
                privateChatColor.G = (byte)RemoteChat.statConfig.consolePrivateChatRGB[1];
                privateChatColor.B = (byte)RemoteChat.statConfig.consolePrivateChatRGB[2];
                int playerIndex;
                string[] s;
//                                Console.WriteLine("|" + action + "|" + sRecieved);
                switch (action)
                {
                    case "`0:":     //private chat from console to player
                        string pi = sRecieved.Substring(3, 4);
                        if (Int32.TryParse(pi, out pIndex))
                        {

                            TShockAPI.TShock.Players[pIndex].SendMessage(RemoteChat.statConfig.consolePrivateChatPrefix + " " + RemoteChat.statConfig.consoleName + ": " + sRecieved.Substring(8), privateChatColor);
                            msg = String.Format("{0}: {1}", RemoteChat.statConfig.consoleName, message);
                            byteData = System.Text.Encoding.UTF8.GetBytes(msg);
                            workerSocket.Send(byteData);
                            m_playerChannelList[pIndex] = workerSocket.RemoteEndPoint.ToString();
                        }
                        else
                            Console.WriteLine("Bad player {0}", sRecieved);
                        break;

                    case "`1:":
                        break;
                    case "`2:":
                        msg = String.Format("{0}: {1}", RemoteChat.statConfig.consoleName, message);
//                        byteData = System.Text.Encoding.ASCII.GetBytes(msg);
                        SendMessageConsole(msg);

                        TShockAPI.TSPlayer.All.SendMessage(msg, chatColor);
                        break;
                    case "`3:":     //response from welcome from console
                        consoleSocket = workerSocket;
                        break;

                    case "`4:":     // player index from table and channel index
                        s = sRecieved.Split('`');
                        playerIndex = Int32.Parse(s[2]);
                        m_playerChannelList[playerIndex] = s[3];
                        break;
                    case "`8:":     // send to main console history
                        s = sRecieved.Split('`');
                        SendMessageConsole("`8:``" + RemoteChat.statConfig.consoleName + " " + s[3]);
                        break;
                    case "`9:":
                        s = sRecieved.Split('`');
                        playerIndex = Int32.Parse(s[2]);
                        if (playerIndex >= 0)
                            m_playerChannelList[playerIndex] = null;

                        //                       Console.WriteLine(workerSocket.RemoteEndPoint.ToString() + " disconnected.");
                        Socket ws;
                        channelSockets.TryRemove(workerSocket.RemoteEndPoint.ToString(), out ws);
                        break;
                }

                // Continue the waiting for data on the Socket
                WaitForData(socketData.m_currentSocket, socketData.m_clientNumber);

            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Socket has been closed\n");
            }
            catch (SocketException se)
            {
                if (se.ErrorCode == 10054) // Error code for Connection reset by peer
                {
                    string msg = "Client " + socketData.m_clientNumber + " Disconnected" + "\n";
                    Console.WriteLine(msg);

                }
                else
                {
                    Console.WriteLine(se.Message);
                }
            }
        }
        public void clearConnections()
        {
            channelSockets.Clear();
            Socket workerSocket;
            foreach (var cs in channelSockets)
            {
                workerSocket = cs.Value;
                workerSocket.Close();
            }
            for (int i = 0; i < m_playerChannelList.Length; i++)
                m_playerChannelList[i] = null;
            Console.WriteLine("All connections cleared.");
        }
        public bool SendMessageConsole(string msg)
        {
            if (consoleSocket == null || !consoleSocket.Connected)
                return false;

            try
            {
                byte[] byData = System.Text.Encoding.UTF8.GetBytes(msg);
                if (consoleSocket == null)
                    return false;
                if (consoleSocket.Connected)
                {
                    consoleSocket.Send(byData);
                }
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
                return false;
            }
            return true;
        }

        public bool SendMessage(int playerIndex, string msg)
        {
            string connectionEndPoint;
            if (playerIndex < 0)
                return false;
            connectionEndPoint = m_playerChannelList[playerIndex];

            if (connectionEndPoint == null)
                return false;

            try
            {
                byte[] byData = System.Text.Encoding.UTF8.GetBytes(msg);
                Socket workerSocket = null;
                if (!channelSockets.TryGetValue(connectionEndPoint, out workerSocket))
                    return false;

                if (workerSocket == null)
                    return false;

                if (workerSocket.Connected)
                {
                    //                   Console.WriteLine("S>" + connectionEndPoint + " from:" + playerIndex + ":" + msg);
                    workerSocket.Send(byData);
                }
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
                return false;
            }
            return true;
        }

        void StopListening()
        {
            CloseSockets();
        }

        public void CloseConnection()
        {
            CloseSockets();
        }
        void CloseSockets()
        {
            if (m_mainSocket != null)
            {
                m_mainSocket.Close();
            }
            Socket workerSocket = null;
            foreach (var cs in channelSockets)
            {
                workerSocket = cs.Value;
                workerSocket.Close();
            }
            channelSockets.Clear();
            for (int i = 0; i < m_playerChannelList.Length; i++)
                m_playerChannelList[i] = null;
        }
    }
}
