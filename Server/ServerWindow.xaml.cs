using log4net;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Server
{
    /// <summary>
    /// Interaction logic for ServerWindow.xaml
    /// </summary>
    public partial class ServerWindow : Window
    {
        #region Static members

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static StringBuilder sb = new StringBuilder();

        private static int requestCount = 0;

        #endregion


        #region Field members

        AsynchronousServer server;

        #endregion


        #region Constructor

        public ServerWindow()
        {
            InitializeComponent();

            // Starts method to read the server configuration from the xml file. 
            // Creates a xml configuration file when it doesnt exist.
            ServerFunctions.ServerStartPreparation();

            server = new AsynchronousServer();
            this.DataContext = server;

            // Starts listening to client connections asynchronously in a new thread
            this.StartListeningToClients();
        }

        #endregion


        #region Functions

        private void StartListeningToClients()
        {
            if (!server.IsServerRunning)
            {
                try
                {
                    AsynchronousServer.ServerWindow = this;

                    Thread listener = new Thread(() => server.StartServerListeningClientConnections())
                    {
                        IsBackground = true
                    };
                    listener.Start();
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// Add the client IP address to the Listbox lbxServer.
        /// </summary>
        public void AddClient(IPEndPoint IpEndPoint, IPHostEntry IpHostEntry)
        {
            lbxServer.Items.Add(IpEndPoint.Address.ToString() + " - (" + IpHostEntry.HostName + ")");
            lbxServer.InvalidateArrange();
            lbxServer.UpdateLayout();
            btnRequestLogs.IsEnabled = true;
        }

        /// <summary>
        /// Remove the client IP addres from the Listbox lbxServer
        /// </summary>
        public void RemoveClient(string iPAddress, bool iPWithName)
        {
            List<object> removeSocket = new List<object>();

            if (iPWithName)
            {
                int index = iPAddress.IndexOf(" ");

                if (index > 0)
                {
                    iPAddress = iPAddress.Substring(0, index);
                }
            }

            foreach (object item in lbxServer.Items)
            {
                string address = item.ToString();

                int index = address.IndexOf(" ");

                if (index > 0)
                {
                    address = address.Substring(0, index);
                }

                if (string.Equals(iPAddress, address, StringComparison.OrdinalIgnoreCase))
                {
                    removeSocket.Add(item);
                }
            }

            foreach (object item in removeSocket)
            {
                lbxServer.Items.Remove(item);
            }

            lbxServer.InvalidateArrange();
            lbxServer.UpdateLayout();
        }

        /// <summary>
        /// Remove client socket with the transferred IP address if its disconnected
        /// </summary>
        public void RemoveClientSocket(string iPAddress, bool iPWithName)
        {
            List<Socket> removeSocket = new List<Socket>();

            foreach (Socket handler in server.ConnectedClients)
            {
                IPEndPoint ipEndPoint = handler.RemoteEndPoint as IPEndPoint;
                string address = ipEndPoint.Address.ToString();

                if (iPWithName)
                {
                    int index = iPAddress.IndexOf(" ");

                    if (index > 0)
                    {
                        iPAddress = iPAddress.Substring(0, index);
                    }
                }

                if (string.Equals(iPAddress, address, StringComparison.OrdinalIgnoreCase))
                {
                    removeSocket.Add(handler);
                }
            }

            foreach (Socket item in removeSocket)
            {
                server.ConnectedClients.Remove(item);

                try
                {
                    item.Shutdown(SocketShutdown.Both);
                    item.Close();
                }
                catch (ObjectDisposedException)
                {
                    // Do nothing because the socket is already closed
                }
            }
        }

        /// <summary>
        /// Needed for Server UI outputs
        /// </summary>
        public void UpdateLogStatus(string log)
        {
            sb.Append(log);
            tbOutput.Text = sb.ToString();
        }

        /// <summary>
        /// Reset the values of the progress bar and enable listbox and request logs button when the file is received.
        /// </summary>
        public void FileReceiveDone()
        {
            progressBar.Visibility = Visibility.Hidden;
            tbProgressText.Visibility = Visibility.Hidden;
            btnRequestLogs.IsEnabled = true;
            lbxServer.IsEnabled = true;
        }

        /// <summary>
        /// Delete Socket if its closed and reset the UI
        /// </summary>
        public void ResetUIAfterSocketException(Socket clientSocket)
        {
            IPEndPoint ipEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;
            string address = ipEndPoint.Address.ToString();

            log.Warn("Request problem from client: " + address);
            this.UpdateLogStatus(ServerFunctions.GetCurrentDateTime() + ServerFunctions.LogTextWarning + "Request problem from client: " + address);

            bool clientConnected = ServerFunctions.CheckClientConnected(clientSocket);

            if (!clientConnected)
            {             
                log.Warn("Selected client :" + address + " is not connected. Remove client from list.");
                this.UpdateLogStatus(ServerFunctions.GetCurrentDateTime() + ServerFunctions.LogTextWarning + "Selected client :" + address + " is not connected. Remove client from list.");
                this.UpdateLogStatus("\n");

                this.RemoveClient(address, false);
                this.RemoveClientSocket(address, false);

                try
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                }
                catch (ObjectDisposedException)
                {
                    // Socket is already closed. Do nothing
                }
            }

            progressBar.Visibility = Visibility.Hidden;
            tbProgressText.Visibility = Visibility.Hidden;
            btnRequestLogs.IsEnabled = true;
            lbxServer.IsEnabled = true;
        }

        #region Change the progressBar members

        /// <summary>
        /// Set the progress length of the ProgressBar
        /// </summary>
        public void SetProgressLength(long len)
        {
            btnRequestLogs.IsEnabled = false;

            progressBar.Maximum = len;
            progressBar.Value = 0;
            progressBar.Visibility = Visibility.Visible;
            tbProgressText.Visibility = Visibility.Visible;
        }

        public void ProgressBarIndeterminateMode(bool value)
        {
            progressBar.IsIndeterminate = value;

            if (value)
            {
                tbProgressText.Text = ServerFunctions.ProgressTextLogsRequested;
                tbProgressText.Visibility = Visibility.Visible;
                progressBar.Visibility = Visibility.Visible;
                btnRequestLogs.IsEnabled = false;
            }
            else
            {
                progressBar.Visibility = Visibility.Hidden;
                tbProgressText.Visibility = Visibility.Hidden;
            }
        }

        #endregion

        /// <summary>
        /// Enable / Disable Listbox 
        /// </summary>
        public void ListBoxEnabled(bool status)
        {
            lbxServer.IsEnabled = status;
        }

        /// <summary>
        /// Change the position of the progressBar
        /// </summary>
        public void ProgressChanged(int bytesRead)
        {
            progressBar.Value += bytesRead;
        }

        /// <summary>
        /// Request logs for the selected clients one by another.
        /// </summary>
        private void RequestLogsOneByOne(Socket handler)
        {
            lock (this)
            {
                requestCount++;

                SychronousServerFunctions serverFunctions;

                serverFunctions = new SychronousServerFunctions(handler);

                serverFunctions.SendLogsRequest();

                if (requestCount == server.SelectedClientsForCollectingLogs.Count)
                {
                    // Delete selected client for collecting logs list after the logs for these clients have been successfully received
                    server.SelectedClientsForCollectingLogs.Clear();

                    requestCount = 0;
                }
            }
        }

        #endregion


        #region Button event members

        private void Button_RequestLogs(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lbxServer.SelectedItems.Count != 0)
                {
                    // Check selected clients before log request if there are connected 
                    for (int i = 0; i < lbxServer.SelectedItems.Count; i++)
                    {
                        for (int j = 0; j < server.ConnectedClients.Count; j++)
                        {
                            string iPAddress = "";

                            IPEndPoint ipEndPoint = server.ConnectedClients[j].RemoteEndPoint as IPEndPoint;
                            string address = ipEndPoint.Address.ToString();

                            int index = lbxServer.SelectedItems[i].ToString().IndexOf(" ");

                            if (index > 0)
                            {
                                iPAddress = lbxServer.SelectedItems[i].ToString().Substring(0, index);
                            }

                            if (string.Equals(iPAddress, address, StringComparison.OrdinalIgnoreCase))
                            {
                                // Check if selected clients are still connected
                                bool isClientConnected = ServerFunctions.CheckClientConnected(server.ConnectedClients[j]);

                                if (!isClientConnected)
                                {
                                    log.Warn("Selected client :" + lbxServer.SelectedItems[i].ToString() + " is not connected. Remove client from list.");
                                    this.UpdateLogStatus(ServerFunctions.GetCurrentDateTime() + ServerFunctions.LogTextWarning + "Selected client :" + lbxServer.SelectedItems[i].ToString() + " is not connected. Remove client from list.");
                                    this.UpdateLogStatus("\n");

                                    this.RemoveClientSocket(lbxServer.SelectedItems[i].ToString(), true);

                                    this.RemoveClient(lbxServer.SelectedItems[i].ToString(), true);
                                }
                            }
                        }
                    }

                    // Check selected clients for log request
                    foreach (object item in lbxServer.SelectedItems)
                    {
                        // Iterate through the list with connected clients and check if its selected by the user.
                        // If yes: Add the client to the list for log request
                        foreach (Socket handler in server.ConnectedClients)
                        {
                            IPEndPoint ipEndPoint = handler.RemoteEndPoint as IPEndPoint;
                            string address = ipEndPoint.Address.ToString();

                            string selectedItem = item.ToString();
                            int index = selectedItem.IndexOf(" ");

                            if (index > 0)
                            {
                                selectedItem = selectedItem.Substring(0, index);
                            }

                            if (string.Equals(selectedItem, address, StringComparison.OrdinalIgnoreCase))
                            {
                                server.SelectedClientsForCollectingLogs.Add(handler);
                            }
                        }
                    }

                    foreach (Socket handler in server.SelectedClientsForCollectingLogs)
                    {
                        Thread logsRequester = new Thread(() => this.RequestLogsOneByOne(handler))
                        {
                            IsBackground = true
                        };
                        logsRequester.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        private void Button_RestartServer(object sender, RoutedEventArgs e)
        {
            log.Info("Server has been restarted by user");

            // Starts method to read the server configuration from the xml file. 
            // Creates a xml configuration file when it doesnt exist.
            ServerFunctions.ServerStartPreparation();

            Thread listener = new Thread(() => server.RestartServer())
            {
                IsBackground = true
            };
            listener.Start();
        }

        #endregion
    }
}
