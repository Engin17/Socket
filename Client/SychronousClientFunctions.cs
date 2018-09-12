using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{

    public class SychronousClientFunctions
    {
        #region Static members

        // buffer size for the byte array
        private const int BUFFER_SIZE = 5242880;


        #region Static field members

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static AutoResetEvent sendDone = new AutoResetEvent(false);

        #endregion


        #endregion // Static members

        #region Field members

        private Socket _clientSocket;
        private string _fileToSend;

        #endregion


        #region Constructor

        public SychronousClientFunctions(Socket clientSocket)
        {
            _clientSocket = clientSocket;
            _fileToSend = ClientFunctions.FileToSend;
        }

        #endregion


        #region Fuction members

        /// <summary>
        /// Start listening for logs request
        /// </summary>
        public void WaitForSignal()
        {
            byte[] bytes = new byte[50];

            try
            {
                // Wait for server request
                _clientSocket.Receive(bytes);

                // Encode received task from server
                string receivedTask = Encoding.UTF8.GetString(bytes).Trim('\0');

                // Check if received task is a log request
                if (String.Compare(receivedTask, "RequestLogs", true) == 0)
                {
                    // Start new thread to copy and zip the logs
                    Thread t = new Thread(() => this.CreateLogs())
                    {
                        IsBackground = true
                    };
                    t.Start();

                    log.Info("Starting copy and zip logs");
                }
                // Check if received task is for connection test
                else if (String.Compare(receivedTask, "CheckStatus", true) == 0)
                {
                    this.WaitForSignal();
                }
                else
                {
                    return;
                }
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
            byte[] sendBuffer = new byte[BUFFER_SIZE];

            // Send file information to the clients.
            this.SendFileInfo();

            try
            {
                // Blocking read file and send to the server asynchronously.
                using (FileStream fs = File.OpenRead(_fileToSend))
                {
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
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
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

        #endregion
    }
}
