using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Xml;


namespace Server
{

    public class ServerFunctions
    {
        #region Static members

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Needed during Server start preparation
        private static readonly string serverConfigurationPath = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\Templogs\Configuration\");
        private static readonly string serverConfigurationFileName = "logServer.conf.xml";

        public static IPAddress ServerIP { get; set; }
        public static int ServerPort { get; set; }

        public static string LogTextInfo => "INFO: ";
        public static string LogTextError => "ERROR: ";
        public static string LogTextWarning => "WARNING: ";

        public static string ProgressTextLogsRequested => "Logs requested. Please Wait...";

        #endregion


        #region Functions

        /// <summary>
        /// Method for server preparation.
        /// Load the IP address for the server if configuration file exists.
        /// Search IP address of the server and create a configuration file
        /// </summary>
        public static void ServerStartPreparation()
        {
            log.Info("********************************************************************************");

            log.Info("System informations:");

            // Determine Windows version
            EnvironmentInfo.GetOSVersionInfo();

            // Determine used .NET runtime version
            EnvironmentInfo.GetRunningNETRuntimeVersion();

            // Determine installed .NET Framework versions
            EnvironmentInfo.GetInstalledNETVersionFromRegistry();

            try
            {
                // Check if server xml configuration file exists
                if (File.Exists(ServerFunctions.serverConfigurationPath + ServerFunctions.serverConfigurationFileName))
                {
                    // Load the configuration xml and reads the configured IP address and port from the server
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(ServerFunctions.serverConfigurationPath + ServerFunctions.serverConfigurationFileName);

                    XmlNodeList elemListIPServer = xmlDoc.GetElementsByTagName("IPAddressServer");

                    for (int i = 0; i < elemListIPServer.Count; i++)
                    {
                        string ip;
                        ip = elemListIPServer[i].InnerXml;
                        ServerFunctions.ServerIP = IPAddress.Parse(ip);
                    }

                    XmlNodeList elemListPortServer = xmlDoc.GetElementsByTagName("PortServer");

                    for (int i = 0; i < elemListPortServer.Count; i++)
                    {
                        string port;
                        port = elemListPortServer[i].InnerXml;

                        ServerFunctions.ServerPort = Int32.Parse(port);
                    }

                    log.Info("Server configuration successfully loaded");
                    log.Info("Server runs with IP: " + ServerFunctions.ServerIP);
                    log.Info("Server runs with Port: " + ServerFunctions.ServerPort);
                }

                // Create server xml configuration file because its not created yet or its deleted
                // Search and set the IPv4 address from the computer before creating xml file
                else
                {
                    // Set default port for the server
                    ServerFunctions.ServerPort = 64500;

                    // Search for IPv4 IP addresses 
                    string hostName = Dns.GetHostName();
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(hostName);
                    for (int i = 0; i < ipHostInfo.AddressList.Length; ++i)
                    {
                        // If the current address is IPv4 then take this for the xml file and leave the loop
                        if (ipHostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                        {
                            ServerFunctions.ServerIP = ipHostInfo.AddressList[i];
                            break;
                        }
                    }

                    // If no IPv4 address is found
                    if (ServerFunctions.ServerIP == null)
                    {
                        ServerFunctions.ServerIP = IPAddress.Parse("127.0.0.1");
                        log.Error("No IPv4 address found:\nPlease open " + ServerFunctions.serverConfigurationFileName + " inside \n" + ServerFunctions.serverConfigurationPath + "\nand configure your IPv4 address");
                        MessageBox.Show("No IPv4 address found:\nPlease open " + ServerFunctions.serverConfigurationFileName + " inside \n" + ServerFunctions.serverConfigurationPath + "\nand configure your IPv4 address", "IPv4 not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    log.Info("The IP address for the LogServer is set to: " + ServerFunctions.ServerIP);
                    log.Info("The port for the LogServer is set to: " + ServerFunctions.ServerPort);
                    MessageBox.Show("The IP address for the LogServer is set to: " + ServerFunctions.ServerIP, "IPv4 address", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Settings for the xml
                    XmlWriterSettings settings = new XmlWriterSettings
                    {
                        Encoding = Encoding.UTF8,
                        ConformanceLevel = ConformanceLevel.Document,
                        OmitXmlDeclaration = false,
                        CloseOutput = true,
                        Indent = true,
                        IndentChars = "  ",
                        NewLineHandling = NewLineHandling.Replace
                    };

                    // Check if the configuration directory exists. If not create directory
                    if (!Directory.Exists(ServerFunctions.serverConfigurationPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(ServerFunctions.serverConfigurationPath);
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex.Message, ex);
                        }
                    }

                    // Create server xml configuration file
                    using (XmlWriter writer = XmlWriter.Create(ServerFunctions.serverConfigurationPath + ServerFunctions.serverConfigurationFileName, settings))
                    {
                        writer.WriteStartDocument();
                        writer.WriteStartElement("Configuration");

                        writer.WriteStartElement("IPAddressServer");
                        writer.WriteValue(ServerFunctions.ServerIP.ToString());
                        writer.WriteEndElement();

                        writer.WriteStartElement("PortServer");
                        writer.WriteValue(ServerFunctions.ServerPort);
                        writer.WriteEndElement();

                        writer.WriteEndDocument();
                    }

                    log.Info("Server configuration successfully created");
                    log.Info("Server runs with IP: " + ServerFunctions.ServerIP);
                    log.Info("Server runs with Port: " + ServerFunctions.ServerPort);
                }

            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// Get the current time for the log entry
        /// </summary>
        public static string GetCurrentDateTime()
        {
            DateTime current = new DateTime();
            current = DateTime.Now;

            return "\n [" + current + "] ";
        }

        /// <summary>
        /// Method for retrieving the Hostname of the client
        /// </summary>
        public static string GetHostNameOfClient(Socket clientSocket)
        {
            try
            {
                // Try to get the Hostname of the client which has sent the logs successfully to the server
                IPEndPoint ipEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;

                IPHostEntry entry = new IPHostEntry
                {
                    HostName = "Unknown"
                };

                try
                {
                    entry = Dns.GetHostEntry(ipEndPoint.Address);
                }
                catch (Exception)
                {
                    // Cannot determine the name of the Client. Do nothing, just skip.
                }

                return entry.HostName;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);

                log.Warn("Couldnt get Hostname of the connected client.");

                return "Unknown";
            }
        }

        /// <summary>
        /// Method to determine the IPHostEntry from the IPEndPoint
        /// </summary>
        public static IPHostEntry GetIPHostEntryOfClient(IPEndPoint endPoint)
        {
            IPHostEntry entry = new IPHostEntry
            {
                HostName = "Unknown"
            };

            // Try to get the entry of the client which will connect to the server
            try
            {
                entry = Dns.GetHostEntry(endPoint.Address);

                return entry;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);

                return entry;
            }
        }

        /// <summary>
        /// Method to check how big the log zip file is in megabytes
        /// </summary>
        public static double CheckZipSize(long fileSize)
        {
            // Calculate bytes in megabytes
            double size = Math.Round((fileSize / 1024d) / 1024d, 2);

            return size;
        }

        /// <summary>
        /// Method to check if the client which wants to connect again already connected.
        /// We can avoid clients to connected several times
        /// Iterate through already connected clients list and check if this client is in the list
        /// </summary>
        public static bool CheckClientStatus(IList<Socket> socketList, Socket socket, bool checkConnection)
        {
            IPEndPoint socket1 = socket.RemoteEndPoint as IPEndPoint;

            for (int i = 0; i < socketList.Count; i++)
            {
                IPEndPoint socket2 = socketList[i].RemoteEndPoint as IPEndPoint;

                if (socket1.Address.Equals(socket2.Address))
                {
                    // Check if socket is connected
                    if (checkConnection)
                    {
                        if (ServerFunctions.CheckClientConnected(socketList[i]))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Method to check if the socket is still connected
        /// </summary>
        public static bool CheckClientConnected(Socket socket)
        {
            byte[] msg = Encoding.UTF8.GetBytes("CheckStatus");

            bool blockingState = socket.Blocking;

            try
            {
                byte[] tmp = new byte[1];

                socket.Blocking = false;
                socket.Send(msg);

                return true;
            }
            catch (SocketException e)
            {
                // 10035 == WSAEWOULDBLOCK
                if (e.NativeErrorCode.Equals(10035))
                {
                    //Still Connected, but the Send would block

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            finally
            {
                socket.Blocking = blockingState;
            }
        }
        #endregion
    }
}
