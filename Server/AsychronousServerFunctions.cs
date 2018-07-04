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

namespace Server
{
    public class AsychronousServerFunctions
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static ManualResetEvent receiveDone = new ManualResetEvent(false);

        private delegate void ProgressChangeHandler(int bytesRead);
        private delegate void SetProgressLengthHandler(long len);
        private delegate void ProgressBarIndeterminateSetHandler(bool mode);
        private delegate void SetStatusLogHandler(string log);
        private delegate void FileReceiveDoneHandler();
        private delegate void RemoveItemHandler(string ipAddress);

        private Socket _clientSocket;
        private string _fileSavePath;
        private long _fileLength;
        private string _clientName;

        public AsychronousServerFunctions(Socket clientSocket)
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
            // Create the state object.
            StateObject state = new StateObject();
            state.workSocket = _clientSocket;

            // Stop progress bar indeterminate mode after copying and zipping progress is finished.
            AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new ProgressBarIndeterminateSetHandler(AsynchronousServer.ServerWindow.ProgressBarIndeterminateMode), false);

            // Prepare progress bar. Set the file length for the progress bar.
            AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new SetProgressLengthHandler(AsynchronousServer.ServerWindow.SetProgressLength), _fileLength);

            // Begin to receive the file from the server.
            try
            {
                var date = DateTime.Now;
                var dateOnly = date.Date;

                // Check if log zip folder already exists. If yes delete log zip folder.
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

                _clientSocket.BeginReceive(state.buffer, 0, StateObject.BUFFER_SIZE, 0, new AsyncCallback(this.ReceiveCallback), state);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// Callback when receive a file chunk from the server successfully.
        /// </summary>
        private void ReceiveCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the client socket.   
            // from the asynchronous state object. 
            StateObject state = (StateObject)ar.AsyncState;
            Socket clientSocket = state.workSocket;

            BinaryWriter writer;

            // Read data from the client. 
            int bytesRead = clientSocket.EndReceive(ar);

            if (bytesRead > 0)
            {
                try
                {
                    //If the file doesnt exist, create a file with the filename got from server. If the file exists, append to the file.
                    if (!File.Exists(_fileSavePath))
                    {
                        writer = new BinaryWriter(File.Open(_fileSavePath, FileMode.Create));
                    }
                    else
                    {
                        writer = new BinaryWriter(File.Open(_fileSavePath, FileMode.Append));
                    }

                    writer.Write(state.buffer, 0, bytesRead);
                    writer.Flush();
                    writer.Close();

                }
                catch (Exception ex)
                {
                    log.Error(ex.Message, ex);
                }

                // Notify the progressBar to change the position.
                AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new ProgressChangeHandler(AsynchronousServer.ServerWindow.ProgressChanged), bytesRead);

                // Recursively receive the rest file.
                try
                {
                    clientSocket.BeginReceive(state.buffer, 0, StateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message, ex);
                }
            }
            else
            {
                log.Info("Logs from Client " + _clientName + " successfully received.");
                log.Info("Logs for Client " + _clientName + " can be found under: " + _fileSavePath);

                AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new SetStatusLogHandler(AsynchronousServer.ServerWindow.UpdateLogStatus), ServerFunctions.GetCurrentDateTime() + ServerFunctions.LogTextInfo + " Logs from Client " + ServerFunctions.GetHostNameOfClient(clientSocket) + " successfully received.");
                AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new SetStatusLogHandler(AsynchronousServer.ServerWindow.UpdateLogStatus), ServerFunctions.GetCurrentDateTime() + ServerFunctions.LogTextInfo + " Logs for Client " + ServerFunctions.GetHostNameOfClient(clientSocket) + " can be found under: " + _fileSavePath);

                // Signal that all bytes have been received. Ready for request logs from client.
                receiveDone.Set();
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

                IPEndPoint ipEndPoint = _clientSocket.RemoteEndPoint as IPEndPoint;
                string address = ipEndPoint.Address.ToString();
                AsynchronousServer.ServerWindow.Dispatcher.BeginInvoke(new RemoveItemHandler(AsynchronousServer.ServerWindow.RemoveItem), address);

            }
            catch (Exception ex)
            {
                log.Info(ex.Message, ex);
            }

        }
    }
}
