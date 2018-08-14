using log4net;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace Server
{
    public class ServerFunctions
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Needed during Server start preparation
        private static readonly string serverConfigurationPath = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\Templogs\Configuration\");
        private static readonly string serverConfigurationFileName = "logServer.conf.xml";

        public static IPAddress ServerIP { get; set; }
        public static int ServerPort { get; set; }

        public static string LogTextInfo => "INFO: ";
        public static string LogTextError => "ERROR: ";
        public static string LogTextWarning => "WARNING: ";
        public static string LogText => "Log Server is ready to request logs! \n";

        public static string ProgressTextLogsRequested => "Loading Files. Please Wait...";

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
                    ServerFunctions.ServerPort = 60100;

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
                IPHostEntry entry = Dns.GetHostEntry(ipEndPoint.Address);

                return entry.HostName;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);

                return null;
            }
        }

        /// <summary>
        /// Method to check how big the logs zip file is in MB
        /// </summary>
        public static double CheckZipSize(long fileSize)
        {
            // Calculate bytes in mega bytes
            double size = Math.Round((fileSize / 1024d) / 1024d, 2);

            return size;
        }

        /// <summary>
        /// Method to check if the client which wants to connect again already connected.
        /// We can avoid clients to connected several times
        /// Iterate through already connected clients list and check if this client is in the list
        /// </summary>
        public static bool CheckClientAlreadyConnected(IList<Socket> socketList, Socket socket)
        {
            IPEndPoint s1 = socket.RemoteEndPoint as IPEndPoint;

            for (int i = 0; i < socketList.Count; i++)
            {
                IPEndPoint s2 = socketList[i].RemoteEndPoint as IPEndPoint;

                if (s1.Address.Equals(s2.Address))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
