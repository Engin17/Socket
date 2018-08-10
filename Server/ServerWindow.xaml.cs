using log4net;
using System;
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
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static StringBuilder sb = new StringBuilder();

        private static int flag = 0;

        AsynchronousServer server;

        public ServerWindow()
        {
            InitializeComponent();

            // Starts method to read the server configuration from the xml file. 
            // Creates a xml configuration file when it doesnt exist.
            ServerFunctions.ServerStartPreparation();

            server = new AsynchronousServer();
            this.DataContext = server;

            // Starts listening to client connections asynchronously
            this.StartListeningToClients();
        }

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
        /// Remove the client address which disconnect from the server.
        /// </summary>
        public void RemoveItem(string ipAddress)
        {
            int[] indexes = new int[lbxServer.SelectedItems.Count];
            int index = 0;
            int currentIndex = 0;

            foreach (object item in lbxServer.SelectedItems)
            {

                int end = item.ToString().IndexOf(' ');

                if (string.Equals(item.ToString().Substring(0, end), ipAddress, StringComparison.OrdinalIgnoreCase))
                {
                    index = lbxServer.Items.IndexOf(item);
                    indexes[currentIndex] = index;

                    currentIndex++;
                }
            }

            foreach (int i in indexes)
            {
                lbxServer.Items.RemoveAt(i);
            }

            lbxServer.InvalidateArrange();
            lbxServer.UpdateLayout();
        }

        public void UpdateLogStatus(string log)
        {
            sb.Append(log);
            tbOutput.Text = sb.ToString();
        }

        /// <summary>
        /// Reset the values of the progress bar when the file is received.
        /// </summary>
        public void FileReceiveDone()
        {
            progressBar.Visibility = Visibility.Hidden;
            tbProgressText.Visibility = Visibility.Hidden;
            btnRequestLogs.IsEnabled = true;
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

        /// <summary>
        /// Change the position of the progressBar
        /// </summary>
        public void ProgressChanged(int bytesRead)
        {
            progressBar.Value += bytesRead;
        }

        #endregion

        private void Button_RequestLogs(object sender, RoutedEventArgs e)
        {
            try
            {
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
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        private void RequestLogsOneByOne(Socket handler)
        {
            lock (this)
            {
                flag++;

                SychronousServerFunctions serverFunctions;

                serverFunctions = new SychronousServerFunctions(handler);

                serverFunctions.SendLogsRequest();

                if (flag == server.SelectedClientsForCollectingLogs.Count)
                {
                    // Delete selected client for collecting logs list after the logs for these clients have been successfully received
                    server.SelectedClientsForCollectingLogs.Clear();

                    flag = 0;
                }
            }
        }

        private void Button_RestartServer(object sender, RoutedEventArgs e)
        {

        }
    }
}
