using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    // State object for reading client data asynchronously  
    public class StateObject
    {
        // Client  socket. 
        public Socket workSocket = null;

        // Size of receive buffer.
        public const int BUFFER_SIZE = 5242880;

        // Receive buffer. 
        public byte[] buffer = new byte[BUFFER_SIZE];

        public bool connected = false;
    }
}
