using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Media;


namespace Server
{
    /// <summary>
    /// Class for managing Client connections
    /// </summary>
    public class AsynchronousServer : INotifyPropertyChanged
    {
        #region Static members

        // The maximum length of the pending connections queue.
        private const int SOCKET_LISTENER_BACKLOG = 100;


        #region Static field members

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Thread signal
        private static AutoResetEvent connectDone = new AutoResetEvent(false);

        #endregion


        // Needed to modify Server window
        public static ServerWindow ServerWindow { get; set; }

        #endregion // Static members


        #region Delegate members

        private delegate void AddClientHandler(IPEndPoint IpEndPoint, IPHostEntry IpHostEntry);
        private delegate void RemoveClientHandler(string iPAddress, bool iPWithName);
        private delegate void RemoveClientSocketHandler(string iPAddress, bool iPWithName);

        #endregion


        #region Field members

        private bool _isServerRunning = false;
        private string _serverStatus = "Down";
        private Brush _serverStatusForeground;
        private Socket _listener = null;

        #endregion


        #region Property members

        // List for connected clients
        public IList<Socket> ConnectedClients { get; set; }

        // List for selected clients for requesting logs
        public IList<Socket> SelectedClientsForCollectingLogs = new List<Socket>();

        public IPAddress ServerIP { get; set; }

        public int ServerPort { get; set; }

        public bool IsServerRunning
        {
            get { return _isServerRunning; }
            set
            {
                _isServerRunning = value;

                if (IsServerRunning)
                {
                    ServerStatus = "Running";
                    ServerStatusForeground = Brushes.GreenYellow;
                }
                else
                {
                    ServerStatus = "Down";
                    ServerStatusForeground = Brushes.Red;
                }
            }
        }

        public Brush ServerStatusForeground
        {
            get { return _serverStatusForeground; }
            set
            {
                _serverStatusForeground = value;
                NotifyPropertyChanged();
            }
        }

        public string ServerStatus
        {
            get { return _serverStatus; }
            set
            {
                _serverStatus = value;
                NotifyPropertyChanged();
            }
        }

        #endregion


        #region Constructor

        public AsynchronousServer()
        {
            ConnectedClients = new List<Socket>();
            ServerIP = ServerFunctions.ServerIP;
            ServerPort = ServerFunctions.ServerPort;
        }

        #endregion


        #region Functions

        /// <summary>
        /// Server start to listen the client connection.
        /// </summary>
        public void StartServerListeningClientConnections()
        {
            try
            {
                // WE have to start the server delayed
                Thread.Sleep(2000);

                // Creates one SocketPermission object for access restrictions
                SocketPermission permission = new SocketPermission (
                NetworkAccess.Accept,     // Allowed to accept connections 
                TransportType.Tcp,        // Defines transport types 
                ServerIP.ToString(),      // The IP addresses of local host 
                SocketPermission.AllPorts // Specifies all ports 
                );

                // Ensures the code to have permission to access a Socket 
                permission.Demand();

                // Establish the server endpoint for the socket. 
                IPEndPoint serverEndPoint = new IPEndPoint(ServerIP, ServerPort);

                // Create a TCP/IP socket
                // Using IPv4 as the network protocol
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Bind the socket to the server endpoint and listen for incoming connections. 
                _listener.Bind(serverEndPoint);
                _listener.Listen(AsynchronousServer.SOCKET_LISTENER_BACKLOG);

                log.Info("Log server is ready for client connections.");
                log.Info("");

                //Start method which checks periodically if the server is running
                new Thread(() => CheckServer())
                {
                    IsBackground = true

                }.Start();

                IsServerRunning = true;

                // Loop server listening to client connections.
                while (IsServerRunning)
                {
                    // Start an asynchronous socket to listen for connections. 
                    _listener.BeginAccept(new AsyncCallback(this.AcceptCallback), _listener);

                    // Wait until a connection is made before continuing. 
                    connectDone.WaitOne();
                }
            }
            catch (SocketException ex)
            {
                log.Error(ex.Message, ex);
                log.Warn("Log Server listener closed. Try to restart Server");

                // Socket is not listening --> Restart Server
                this.RestartServer();
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// Callback when one client successfully connected to the server.
        /// </summary>
        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                // Signal the connect thread to continue.  
                connectDone.Set();

                // Dont accept connection if the server is not running and the listener is null
                if (!IsServerRunning || _listener == null)
                {
                    log.Error("Server is not running");
                    return;
                }

                // Get the socket that handles the client request.
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                IPEndPoint ipEndPoint = handler.RemoteEndPoint as IPEndPoint;

                // Try to get the Hostname of the client which will connect to the server
                IPHostEntry entry = ServerFunctions.GetIPHostEntryOfClient(ipEndPoint);

                // Check if the client which wants to connect is already connected. 
                // If Client is not connected then try to connect to the server
                if (!ServerFunctions.CheckClientStatus(this.ConnectedClients, handler, false) && ipEndPoint != null)
                {
                    // Add connected Client to the listbox
                    ServerWindow.Dispatcher.Invoke(new AddClientHandler(ServerWindow.AddClient), ipEndPoint, entry);
                    
                    // Add connected client to the connected client list
                    this.ConnectedClients.Add(handler);

                    log.Info("Client " + entry.HostName + " successfully connected to the server.");
                    log.Info("");
                }
                else
                {
                    // Check if the already connected client is still connected. 
                    // If the already connected client is connected then close the same client which tries to connect again
                    if (ServerFunctions.CheckClientStatus(this.ConnectedClients, handler, true))
                    {
                        log.Info("Client " + entry.HostName + " already connected. Dont connect to server.");

                        // Close socket because this socket is already connected
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                    }
                    else
                    {
                        // Delete the already connected client and connect the new client which tries to connect
                        this.DeleteAndAddConnectedClient(this.ConnectedClients, handler);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// Method to check if the server socket is listening and if the client sockets are still connected to the server
        /// </summary>
        private void CheckServer()
        {
            while (IsServerRunning)
            {
                // TODO: Replace this with Sytem.Timers.Timer and intervall should be 30 minutes
                Thread.Sleep(10000);

                try
                {
                    // Check if Socket is listening. Listening = 1
                    Int32 optVal = (Int32)_listener.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.AcceptConnection);

                    if (optVal <= 0)
                    {
                        log.Warn("The server is down. Try to restart the server.");

                        // Socket is not listening --> Restart Server
                        this.RestartServer();
                    }
                }
                catch (SocketException ex)
                {
                    log.Error(ex.Message, ex);
                    log.Warn("The server is down. Try to restart the server.");

                    // Socket is not listening --> Restart Server
                    this.RestartServer();
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// Method which which deletes the disconnected client and connect the new client which tries to connect
        /// </summary>
        private void DeleteAndAddConnectedClient(IList<Socket> socketList, Socket socket)
        {
            IPEndPoint s1 = socket.RemoteEndPoint as IPEndPoint;

            for (int i = 0; i < socketList.Count; i++)
            {
                try
                {
                    IPEndPoint s2 = socketList[i].RemoteEndPoint as IPEndPoint;

                    if (s1.Address.Equals(s2.Address))
                    {
                        ServerWindow.Dispatcher.Invoke(new RemoveClientSocketHandler(ServerWindow.RemoveClient), s1.Address.ToString(), false);

                        ServerWindow.Dispatcher.Invoke(new RemoveClientHandler(ServerWindow.RemoveClientSocket), s1.Address.ToString(), false);

                        // Add connected Client to the listbox
                        // Try to get the Hostname of the client which will connect to the server
                        IPEndPoint ipEndPoint = socket.RemoteEndPoint as IPEndPoint;
                        IPHostEntry entry = ServerFunctions.GetIPHostEntryOfClient(ipEndPoint);

                        // Add connected Client to the listbox
                        ServerWindow.Dispatcher.Invoke(new AddClientHandler(ServerWindow.AddClient), ipEndPoint, entry);

                        // Add connected client to the connected client list
                        this.ConnectedClients.Add(socket);

                        log.Info("Client " + entry.HostName + " successfully reconnected to the server.");
                        log.Info("");

                        return;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// Close server listener for client connections and start a new listener for client connections
        /// </summary>
        public void RestartServer()
        {
            IsServerRunning = false;

            if (_listener != null)
            {
                _listener.Close();
            }

            _listener = null;

            ServerIP = ServerFunctions.ServerIP;
            ServerPort = ServerFunctions.ServerPort;

            this.StartServerListeningClientConnections();
        }

        #endregion


        #region Notify property changed  

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}

