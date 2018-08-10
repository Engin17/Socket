﻿using log4net;
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

namespace Server
{
    public class SychronousServerFunctions
    {
        #region Static members

        #region Constant members
        // Buffer size for the byte array
        private const int BUFFER_SIZE = 5242880;
        #endregion

        #region Static field members
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static AutoResetEvent receiveDone = new AutoResetEvent(false);
        #endregion

        #endregion // Static members

        #region Delegate members
        private delegate void ProgressChangeHandler(int bytesRead);
        private delegate void SetProgressLengthHandler(long len);
        private delegate void ProgressBarIndeterminateSetHandler(bool mode);
        private delegate void SetStatusLogHandler(string log);
        private delegate void FileReceiveDoneHandler();
        #endregion

        #region Field members
        private Socket _clientSocket;
        private string _fileSavePath;
        private long _fileLength;
        private string _clientName;
        #endregion

        public SychronousServerFunctions(Socket clientSocket)
        {
            _clientSocket = clientSocket;
            _clientName = ServerFunctions.GetHostNameOfClient(_clientSocket);
            _fileSavePath = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\Templogs\");
        }

        /// <summary>
        /// Receive the file send by the client.
        /// </summary>
        private void Receive()
        {
            byte[] buffer = new byte[BUFFER_SIZE];
            BinaryWriter writer = null;
            long bytesLeftToReceive = _fileLength;

            // Stop progress bar indeterminate mode after copying and zipping progress is finished.
            AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new ProgressBarIndeterminateSetHandler(AsynchronousServer.ServerWindow.ProgressBarIndeterminateMode), false);

            // Prepare progress bar. Set the file length for the progress bar.
            AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new SetProgressLengthHandler(AsynchronousServer.ServerWindow.SetProgressLength), _fileLength);

            // Check and delte log file from this client if its already exists.
            try
            {
                var date = DateTime.Now;
                var dateOnly = date.Date;

                // Check if log zip folder already exists. If yes delete log zip folder, create new file
                if (File.Exists(_fileSavePath + dateOnly.ToString("yyyy/MM/dd") + "_" + _clientName + ".zip"))
                {
                    try
                    {
                        File.Delete(_fileSavePath + dateOnly.ToString("yyyy/MM/dd") + "_" + _clientName + ".zip");
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message, ex);
                    }
                }

                _fileSavePath += dateOnly.ToString("yyyy/MM/dd") + "_" + _clientName + ".zip";
                writer = new BinaryWriter(File.Open(_fileSavePath, FileMode.Create));

                do
                {
                    // Receive log file from client
                    var bytesRead = _clientSocket.Receive(buffer);
                    if (bytesRead == 0)
                    {
                        throw new InvalidOperationException("Remote endpoint disconnected");
                    }

                    // write to file
                    writer.Write(buffer, 0, bytesRead);
                    writer.Flush();

                    AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new ProgressChangeHandler(AsynchronousServer.ServerWindow.ProgressChanged), bytesRead);

                    bytesLeftToReceive -= bytesRead;
                }
                while (bytesLeftToReceive > 0);

                writer.Close();

                log.Info("Logs from Client " + _clientName + " successfully received.");
                log.Info("Logs for Client " + _clientName + " can be found under: " + _fileSavePath);

                AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new SetStatusLogHandler(AsynchronousServer.ServerWindow.UpdateLogStatus), ServerFunctions.GetCurrentDateTime() + ServerFunctions.LogTextInfo + " Logs from Client " + _clientName + " successfully received.");
                AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new SetStatusLogHandler(AsynchronousServer.ServerWindow.UpdateLogStatus), ServerFunctions.GetCurrentDateTime() + ServerFunctions.LogTextInfo + " Logs for Client " + _clientName + " can be found under: " + _fileSavePath);

                // Signal that all bytes have been received. Ready for request logs from client.
                receiveDone.Set();
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// Receive the file information send by the server.
        /// </summary>
        private void ReceiveFileInfo()
        {
            try
            {
                // Get the file length from the server.
                byte[] fileLenByte = new byte[8];
                _clientSocket.Receive(fileLenByte);
                _fileLength = BitConverter.ToInt64(fileLenByte, 0);

                AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new SetStatusLogHandler(AsynchronousServer.ServerWindow.UpdateLogStatus), ServerFunctions.GetCurrentDateTime() + ServerFunctions.LogTextInfo + " Log zip file size for Client " + _clientName + " is " + ServerFunctions.CheckZipSize(_fileLength) + " MB");
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        public void SendLogsRequest()
        {
            byte[] msg = Encoding.UTF8.GetBytes("Request Logs");
            byte[] bytes = new byte[256];

            try
            {
                // Send client a request trigger
                _clientSocket.Send(msg);

                AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new ProgressBarIndeterminateSetHandler(AsynchronousServer.ServerWindow.ProgressBarIndeterminateMode), true);

                log.Info("Logs from Client " + _clientName + " requested.");
                AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new SetStatusLogHandler(AsynchronousServer.ServerWindow.UpdateLogStatus), ServerFunctions.GetCurrentDateTime() + ServerFunctions.LogTextInfo + " Logs from Client " + _clientName + " requested.");

                // Receive the length information of the log zip folder from the client
                ReceiveFileInfo();

                // Receive the zip folder.
                Receive();
                receiveDone.WaitOne();

                // Notify the user whether receive the file completely.
                AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new FileReceiveDoneHandler(AsynchronousServer.ServerWindow.FileReceiveDone));
            }
            catch (Exception ex)
            {
                log.Info(ex.Message, ex);
            }

        }
    }
}