using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace Client
{
    public class AsynchronousClient
    {
        #region Static members


        #region Static field members

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static AutoResetEvent connectDone = new AutoResetEvent(false);

        #endregion


        #endregion // Static members


        #region Field members

        private Socket _clientSocket;

        #endregion


        #region Property members

        public IPAddress ServerIP { get; set; }
        public int ServerPort { get; set; }
        public Socket ClientSocket { get; set; }
        public bool IsSocketConnected { get; set; }

        #endregion


        #region Constructor

        public AsynchronousClient()
        {
            ServerIP = ClientFunctions.ServerIP;
            ServerPort = ClientFunctions.ServerPort;
        }

        #endregion


        #region Function members

        /// <summary>
        /// Start connect to the server.
        /// </summary>
        public void StartConnectToServer()
        {
            if (ServerIP == null)
            {
                log.Warn("Client has no configured IP adress");
                log.Warn("Client cannot connect to the server. Please open the clientSocket configuration and enter the clientSocket IP");
            }

            try
            {
                // Creates one SocketPermission object for access restrictions
                SocketPermission permission = new SocketPermission(
                NetworkAccess.Connect,    // Allowed to accept connections 
                TransportType.Tcp,        // Defines transport types 
                ServerIP.ToString(),      // The IP addresses of local host 
                SocketPermission.AllPorts // Specifies all ports 
                );

                // Ensures the code to have permission to access a Socket 
                permission.Demand();

                // Remote endpoint is the server
                IPEndPoint remoteEP = new IPEndPoint(ServerIP, ServerPort);

                // Create a TCP/IP socket
                // Using IPv4 as the network protocol
                _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Begin to connect to the server.
                _clientSocket.BeginConnect(remoteEP, new AsyncCallback(this.ConnectCallback), _clientSocket);

                // Wait until the connection is done
                connectDone.WaitOne();

                // Start method which checks periodically if the Socket is still connected
                new Thread(() => SocketConnected(_clientSocket))
                {
                    IsBackground = true

                }.Start();

                this.IsSocketConnected = true;

                // Begin to wait for logs request signal from the server.
                new SychronousClientFunctions(_clientSocket).WaitForSignal();
            }
            catch (Exception ex)
            {
                log.Info(ex.Message, ex);
            }
        }

        /// <summary>
        /// Callback when the clientSocket connect to the server successfully.
        /// </summary>
        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket clientSocket = (Socket)ar.AsyncState;

                // Complete the connection.
                clientSocket.EndConnect(ar);

                // Signal that the connection has been made. 
                connectDone.Set();

                log.Info("Client successfully connected to the server.");
                log.Info("");
            }
            catch (SocketException)
            {
                // TODO: maybe we dont need no sleep here
                Thread.Sleep(3000);
                this.StartConnectToServer();
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// Method to check if the client socket is connected to the server
        /// </summary>
        private void SocketConnected(Socket clientSocket)
        {
            while (IsSocketConnected)
            {
                // TODO: Replace this with Sytem.Timers.Timer. Check every 30 minutes
                Thread.Sleep(10000);

                // This is how you can determine whether a socket is still connected.
                bool blockingState = clientSocket.Blocking;

                try
                {
                    try
                    {
                        byte[] tmp = new byte[1];

                        clientSocket.Blocking = false;
                        clientSocket.Send(tmp, 0, 0);
                        clientSocket.Blocking = blockingState;
                    }
                    catch (SocketException e)
                    {
                        // 10035 == WSAEWOULDBLOCK
                        if (e.NativeErrorCode.Equals(10035))
                        {
                            //Still Connected, but the Send would block
                        }
                        else
                        {
                            log.Warn("Socket disconnected. Try reconnect to server: error code" + " " + e.NativeErrorCode);
                            IsSocketConnected = false;
                            this.StartConnectToServer();
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    log.Warn("Socket disconnected. Try reconnect to server");
                    IsSocketConnected = false;
                    this.StartConnectToServer();
                }
                finally
                {
                    clientSocket.Blocking = blockingState;
                }
            }
        }
        #endregion
    }
}
