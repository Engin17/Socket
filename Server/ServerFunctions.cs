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

        public static string LogTextInfo { get; } = "INFO: ";
        public static string LogTextError { get; } = "ERROR: ";
        public static string LogTextWarning { get; } = "WARNING: ";
        public static string LogText { get; } = "Log Server is ready to request logs! \n";

        public static string ProgressTextLogsRequested { get; } = "Loading Files. Please Wait...";


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
            OSVersionInfo.GetOSVersionInfo();

            // Determine used .NET runtime version
            ServerFunctions.GetRunningNETRuntimeVersion();

            // Determine installed .NET Framework versions
            ServerFunctions.GetInstalledNETVersionFromRegistry();

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

        public static void GetRunningNETRuntimeVersion()
        {
            log.Info("");
            log.Info(".NET Runtime Version: " + Environment.Version.ToString());
        }

        public static void GetInstalledNETVersionFromRegistry()
        {
            log.Info("Detect installed.NET framework versions:");

            try
            {
                // Opens the registry key for the .NET Framework entry.
                using (RegistryKey ndpKey =
                    RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, "").
                    OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
                {
                    // As an alternative, if you know the computers you will query are running .NET Framework 4.5 
                    // or later, you can use:
                    // using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, 
                    // RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
                    foreach (string versionKeyName in ndpKey.GetSubKeyNames())
                    {
                        if (versionKeyName.StartsWith("v"))
                        {

                            RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName);
                            string name = (string)versionKey.GetValue("Version", "");
                            string sp = versionKey.GetValue("SP", "").ToString();
                            string install = versionKey.GetValue("Install", "").ToString();
                            if (install == "") //no install info, must be later.
                                log.Info(versionKeyName + "  " + name);
                            else
                            {
                                if (sp != "" && install == "1")
                                {
                                    log.Info(versionKeyName + "  " + name + "  SP" + sp);
                                }

                            }
                            if (name != "")
                            {
                                continue;
                            }
                            foreach (string subKeyName in versionKey.GetSubKeyNames())
                            {
                                RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
                                name = (string)subKey.GetValue("Version", "");
                                if (name != "")
                                    sp = subKey.GetValue("SP", "").ToString();
                                install = subKey.GetValue("Install", "").ToString();
                                if (install == "") //no install info, must be later.
                                    log.Info(versionKeyName + "  " + name);
                                else
                                {
                                    if (sp != "" && install == "1")
                                    {
                                        log.Info("  " + subKeyName + "  " + name + "  SP" + sp);
                                    }
                                    else if (install == "1")
                                    {
                                        log.Info("  " + subKeyName + "  " + name);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }

            log.Info("");
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
    }
}
