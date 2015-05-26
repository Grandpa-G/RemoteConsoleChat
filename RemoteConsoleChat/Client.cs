using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace RemoteConsoleChat
{
    public class Client
    {
        public TcpClient sock;
       public byte[] readBuffer;

        public Client(TcpClient c)
        {
            sock = c;
        }
        public void cleanUp()
        {
            try
            {
                sock.Close();
                readBuffer = null;

                if (sock != null)
                {
                    sock = null;
                }
            }
            catch { }
        }

    }
}
