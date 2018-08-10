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
    public class AsynchronousServer : INotifyPropertyChanged
    {
        #region Static members
        // Maximum number of client connections
        private const int MAX_CONNECTED_CLIENT_SOCKETS = 100;

        #region Static field members

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Thread signal
        private static AutoResetEvent connectDone = new AutoResetEvent(false);

        #endregion

        public static ServerWindow ServerWindow { get; set; }
        #endregion // Static members

        #region Delegate members
        private delegate void AddClientHandler(IPEndPoint IpEndPoint, IPHostEntry IpHostEntry);
        #endregion

        #region Field members
        private readonly string _fileSavePath = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\Templogs\");      
        private bool _isServerRunning = false;
        private string _serverStatus = "Down";
        private Brush _serverStatusForeground;
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

        public AsynchronousServer()
        {
            ConnectedClients = new List<Socket>();
            ServerIP = ServerFunctions.ServerIP;
            ServerPort = ServerFunctions.ServerPort;
        }


        /// <summary>
        /// Server start to listen the client connection.
        /// </summary>
        public void StartServerListeningClientConnections()
        {
            // Creates one SocketPermission object for access restrictions
            SocketPermission permission = new SocketPermission(
            NetworkAccess.Accept,     // Allowed to accept connections 
            TransportType.Tcp,        // Defines transport types 
            ServerIP.ToString(),      // The IP addresses of local host 
            SocketPermission.AllPorts // Specifies all ports 
            );

            // Ensures the code to have permission to access a Socket 
            permission.Demand();

            // Establish the local endpoint for the socket. 
            IPEndPoint localEndPoint = new IPEndPoint(ServerIP, ServerPort);

            // Create a TCP/IP socket
            // Using IPv4 as the network protocol
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections. 
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(AsynchronousServer.MAX_CONNECTED_CLIENT_SOCKETS);

                log.Info("Log server is ready for client connections.");

                IsServerRunning = true;

                // Loop listening the client.
                while (true)
                {                  
                    // Start an asynchronous socket to listen for connections. 
                    listener.BeginAccept(new AsyncCallback(this.AcceptCallback), listener);

                    // Wait until a connection is made before continuing. 
                    connectDone.WaitOne();
                }
            }
            catch (Exception ex)
            {
                IsServerRunning = false;

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
                // Get the socket that handles the client request.
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                IPEndPoint ipEndPoint = handler.RemoteEndPoint as IPEndPoint;

                // Try to get the Hostname of the client which will connect to the server
                IPHostEntry entry = Dns.GetHostEntry(ipEndPoint.Address);

                if (entry == null)
                {
                    entry.HostName = "Unknown";
                    log.Warn("Couldnt get Hostname of the connected client.");
                }

                if (ipEndPoint != null)
                {
                    ServerWindow.Dispatcher.BeginInvoke(new AddClientHandler(ServerWindow.AddClient), ipEndPoint, entry);
                }

                // Add connected client to the connected client list
                ConnectedClients.Add(handler);

                log.Info("Client " + entry.HostName + " successfully connected to the server.");

                // Signal the connect thread to continue.  
                connectDone.Set();

            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

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

