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

        #region Constant members
        // buffer size for the byte array
        private const int BUFFER_SIZE = 5242880;
        #endregion

        #region Static field members
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static AutoResetEvent connectDone = new AutoResetEvent(false);
        private static AutoResetEvent sendDone = new AutoResetEvent(false);
        #endregion

        #endregion // Static members

        #region Field members
        private Socket _clientSocket;
        private string _fileToSend;
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
            _fileToSend = ClientFunctions.FileToSend;
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
                // Remote endpoint is the server
                IPEndPoint remoteEP = new IPEndPoint(ServerIP, ServerPort);

                // Create a TCP/IP socket
                // Using IPv4 as the network protocol
                _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Begin to connect to the server.
                _clientSocket.BeginConnect(remoteEP, new AsyncCallback(this.ConnectCallback), _clientSocket);

                // Wait until the connection is done
                connectDone.WaitOne();

                // Start method which checks periodically if the Socket still connected
                new Thread(() => SocketConnected(_clientSocket))
                {
                    IsBackground = true
                }.Start();

                this.IsSocketConnected = true;

                // Begin to wait for logs request signal from the server.
                this.WaitForSignal();
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
                //this.StartConnectToServer();
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// Start listening for logs request
        /// </summary>
        public void WaitForSignal()
        {
            byte[] bytes = new byte[256];

            try
            {
                // Wait for log request
                _clientSocket.Receive(bytes);

                // Start new thread to copy and zip the logs
                Thread t = new Thread(() => this.CreateLogs())
                {
                    IsBackground = true
                };
                t.Start();

                log.Info("Starting copy and zip logs");
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        // Copy and zip logs
        private void CreateLogs()
        {
            ClientFunctions.CreateClientServerLogFolder();

            ClientFunctions.CopyLogs(ClientFunctions.ClientLogsPath, ClientFunctions.ClientLogsTempPath, true);
            ClientFunctions.CopyLogs(ClientFunctions.ClientConfPath, ClientFunctions.ClientConfTempPath, true);
            ClientFunctions.CopyLogs(ClientFunctions.ServerLogsPath, ClientFunctions.ServerLogsTempPath, true);

            ClientFunctions.ZipLogs(ClientFunctions.LogsTempPath, ClientFunctions.LogsTempPathZip);

            log.Info("Logs successfully created.");

            // Send logs to server
            this.Send();
            log.Info("Send logs to the server.");

            // Wait until logs has been sent
            sendDone.WaitOne();

            log.Info("Logs have been successfully sent to the server.");
            log.Info("");

            // Delete the temporary logs folder
            ClientFunctions.DeleteLogFolderAfterSent();

            this.WaitForSignal();
        }

        private void Send()
        {
            byte[] buffer = new byte[BUFFER_SIZE];

            // Send file information to the clients.
            this.SendFileInfo();

            // Blocking read file and send to the server asynchronously.
            using (FileStream fs = File.OpenRead(_fileToSend))
            {
                var sendBuffer = new byte[5242880];
                FileInfo fileInfo = new FileInfo(_fileToSend);
                long fileLen = fileInfo.Length;

                var bytesLeftToTransmit = fileLen;

                while (bytesLeftToTransmit > 0)
                {
                    var dataToSend = fs.Read(sendBuffer, 0, sendBuffer.Length);
                    bytesLeftToTransmit -= dataToSend;

                    //loop until the socket have sent everything in the buffer.
                    var offset = 0;
                    while (dataToSend > 0)
                    {
                        var bytesSent = _clientSocket.Send(sendBuffer, offset, dataToSend, SocketFlags.None);
                        dataToSend -= bytesSent;
                        offset += bytesSent;
                    }
                }
            }
            sendDone.Set();
        }

        /// <summary>
        /// Send log file size information to the server..
        /// </summary>
        private void SendFileInfo()
        {
            try
            {
                if (File.Exists(_fileToSend))
                {
                    FileInfo fileInfo = new FileInfo(_fileToSend);
                    long fileLen = fileInfo.Length;

                    byte[] fileLenByte = BitConverter.GetBytes(fileLen);
                    Socket handler = _clientSocket;
                    handler.Send(fileLenByte);

                    log.Info("Send log file size to the server.");
                }
                else
                {
                    log.Warn("Log zip file doenst exist. Try to create again");
                    this.CreateLogs();
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        private void SocketConnected(Socket s)
        {
            while (IsSocketConnected)
            {
                Thread.Sleep(5000);

                try
                {
                    Console.WriteLine("check");
                    bool part1 = s.Poll(1000, SelectMode.SelectRead);
                    bool part2 = (s.Available == 0);
                }
                catch (ObjectDisposedException)
                {
                    log.Warn("Socket disconnected. Try connecting to server");
                    IsSocketConnected = false;
                    this.StartConnectToServer();
                }
            }
        }
        #endregion
    }
}
