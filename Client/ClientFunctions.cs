using Ionic.Zip;
using log4net;
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

namespace Client
{
    public class ClientFunctions
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string clientConfigurationPath = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\Templogs\Configuration\");
        private static readonly string clientConfigurationFileName = "logClient.conf.xml";

        private static string seeTecInstallPath = string.Empty;
        
        private static IPAddress clientIP;
    
        public static IPAddress ServerIP { get; set; }
        public static int ServerPort { get; set; }

        public static string LogsTempPath { get; } = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\TempLogs\Logs");
        public static string LogsTempPathZip { get; } = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\TempLogs\Logs\Logs.zip");
        public static string ClientLogsPath { get; } = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\log");
        public static string ClientConfPath { get; } = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\conf");
        public static string ClientLogsTempPath { get; } = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\TempLogs\Logs\ClientLogsConf\ClientLogs");
        public static string ClientConfTempPath { get; } = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\Templogs\Logs\ClientLogsConf\ClientConf");
        public static string ServerLogsTempPath { get; } = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\SeeTec\TempLogs\Logs\ServerLogs");
        public static string ServerLogsPath { get; set; } = string.Empty;
        public static string FileToSend { get; } = ClientFunctions.LogsTempPathZip;

        /// <summary>
        /// Method for preparation the clientSocket.
        /// Load the IP address for the clientSocket if configuration file exists.
        /// Search IP address of the clientSocket and create a configuration file
        /// </summary>
        public static void ClientStartPreparation()
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
                if (File.Exists(ClientFunctions.clientConfigurationPath + ClientFunctions.clientConfigurationFileName))
                {
                    // Load the configuration xml and read out the configured IP address and port for the clientSocket
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(ClientFunctions.clientConfigurationPath + ClientFunctions.clientConfigurationFileName);

                    XmlNodeList elemListIPClient = xmlDoc.GetElementsByTagName("IPAddressClient");

                    for (int i = 0; i < elemListIPClient.Count; i++)
                    {
                        string ip;
                        ip = elemListIPClient[i].InnerXml;
                        ClientFunctions.clientIP = IPAddress.Parse(ip);
                    }

                    XmlNodeList elemListIPServer = xmlDoc.GetElementsByTagName("IPAddressServer");

                    for (int i = 0; i < elemListIPServer.Count; i++)
                    {
                        string ip;
                        ip = elemListIPServer[i].InnerXml;
                        ClientFunctions.ServerIP = IPAddress.Parse(ip);
                    }

                    XmlNodeList elemListPortClient = xmlDoc.GetElementsByTagName("PortClient");

                    for (int i = 0; i < elemListPortClient.Count; i++)
                    {
                        string port;
                        port = elemListPortClient[i].InnerXml;

                        ClientFunctions.ServerPort = Int32.Parse(port);
                    }
                    log.Info("Client configuration successfully loaded");
                    log.Info("Client runs with IP: " + ClientFunctions.clientIP);
                    log.Info("Client will connect to server IP: " + ClientFunctions.ServerIP);
                    log.Info("Client connection runs with Port: " + ClientFunctions.ServerPort + " to the server");
                }

                // Create clientSocket xml configuration file because its not created yet or its deleted
                // Search and set IPv4 address of the computer before creating xml file
                else
                {
                    // Set default port for the clientSocket and the IP for the server
                    ClientFunctions.ServerPort = 60100;
                    ClientFunctions.ServerIP = IPAddress.Parse("127.0.0.1");
                    MessageBox.Show("The IP for the ServerLogs server is set to: 127.0.0.1\nPlease open " + ClientFunctions.clientConfigurationFileName + " inside: \n" + ClientFunctions.clientConfigurationPath + "\nand configure the Update Server IP", "IP Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);

                    // Search for IPv4 IP addresses 
                    string hostName = Dns.GetHostName();
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(hostName);
                    for (int i = 0; i < ipHostInfo.AddressList.Length; ++i)
                    {
                        // If the current address is IPv4 then take this for the xml file and leave the loop
                        if (ipHostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                        {
                            ClientFunctions.clientIP = ipHostInfo.AddressList[i];
                            break;
                        }
                    }

                    // If no IPv4 address is found
                    if (ClientFunctions.clientIP == null)
                    {
                        ClientFunctions.clientIP = IPAddress.Parse("127.0.0.1");
                        MessageBox.Show("No IPv4 address found:\nPlease open " + ClientFunctions.clientConfigurationFileName + " inside \n" + ClientFunctions.clientConfigurationPath + "\nand configure your IPv4 address", "IPv4 not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    MessageBox.Show("The IP address for the LogClient is set to: " + ClientFunctions.clientIP, "IPv4 address", MessageBoxButton.OK, MessageBoxImage.Information);

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
                    if (!Directory.Exists(ClientFunctions.clientConfigurationPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(ClientFunctions.clientConfigurationPath);
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex.Message, ex);
                        }
                    }

                    // Create clientSocket xml configuration file
                    using (XmlWriter writer = XmlWriter.Create(ClientFunctions.clientConfigurationPath + ClientFunctions.clientConfigurationFileName, settings))
                    {
                        writer.WriteStartDocument();
                        writer.WriteStartElement("Configuration");

                        writer.WriteStartElement("IPAddressClient");
                        writer.WriteValue(clientIP.ToString());
                        writer.WriteEndElement();

                        writer.WriteStartElement("IPAddressServer");
                        writer.WriteValue(ClientFunctions.ServerIP.ToString());
                        writer.WriteEndElement();

                        writer.WriteStartElement("PortClient");
                        writer.WriteValue(ClientFunctions.ServerPort);
                        writer.WriteEndElement();

                        writer.WriteEndDocument();
                    }
                    log.Info("Client configuration successfully created");
                    log.Info("Client runs with IP: " + ClientFunctions.clientIP);
                    log.Info("Client will connect to server IP: " + ClientFunctions.ServerIP);
                    log.Info("Client connection runs with Port: " + ClientFunctions.ServerPort + " to the server");
                }

            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }

            // Find out where SeeTec is installed
            SeeTecInstallPath();
        }

        #region Copy and zip logs function
        /// <summary>
        /// Method for copy the client logs and conf to the temp logs folder
        /// </summary>
        public static void CopyLogs(string logPath, string logCopyTemp, bool copySubDirs)
        {
            try
            {
                // Get the subdirectories for the specified directory.
                DirectoryInfo dir = new DirectoryInfo(logPath);

                DirectoryInfo[] dirs = dir.GetDirectories();

                // If the destination directory doesn't exist, create it.
                if (!Directory.Exists(logCopyTemp))
                {
                    Directory.CreateDirectory(logCopyTemp);

                    // Get the files in the directory and copy them to the new location.
                    FileInfo[] files = dir.GetFiles();

                    foreach (FileInfo file in files)
                    {
                        string temppath = Path.Combine(logCopyTemp, file.Name);
                        file.CopyTo(temppath, false);
                    }

                    // If copying subdirectories, copy them and their contents to new location.
                    if (copySubDirs)
                    {
                        foreach (DirectoryInfo subdir in dirs)
                        {
                            string temppath = Path.Combine(logCopyTemp, subdir.Name);
                            CopyLogs(subdir.FullName, temppath, copySubDirs);
                        }
                    }
                }
                else
                {
                    // Delete old log folders
                    Directory.Delete(logCopyTemp, true);

                    // Call this method again after deleting old logs folder
                    CopyLogs(logPath, logCopyTemp, copySubDirs);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// Method to create server logs or client logs
        /// </summary>
        public static void ZipLogs(string logPath, string logTempZip)
        {
            // check if logs already exported
            FileInfo sFile = new FileInfo(logTempZip);
            bool fileExist = sFile.Exists;

            try
            {
                if (!fileExist)
                {
                    using (ZipFile zip = new ZipFile())
                    {
                        zip.AddDirectory(logPath);

                        // Set this buffer sizes because there is an bug in the zipping process from Ionic zip
                        // By using the default sizes sometimes it can happen that the zipping process freezes
                        zip.BufferSize = 1000000;
                        zip.CodecBufferSize = 1000000;
                        zip.Save(logTempZip);
                    }
                }
                else
                {
                    try
                    {
                        // Delete old log zip file
                        File.Delete(logTempZip);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message, ex);
                    }
                    // Call this method again after deleting old logs zip file
                    ZipLogs(logPath, logTempZip);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }
        #endregion

        /// <summary>
        /// Method to create the log folder for client and server logs
        /// </summary>
        public static void CreateClientServerLogFolder()
        {
            try
            {
                if (!Directory.Exists(ClientFunctions.LogsTempPath))
                {
                    Directory.CreateDirectory(ClientFunctions.LogsTempPath);
                }
            }
            catch (Exception ex)
            {

                log.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// Delete the temporary log folder after sending the logs to the server
        /// </summary>
        public static void DeleteLogFolderAfterSent()
        {
            try
            {
                if (Directory.Exists(ClientFunctions.LogsTempPath))
                {
                    Directory.Delete(ClientFunctions.LogsTempPath, true);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }
      
        /// <summary>
        /// Method to find the SeeTec installation path
        /// First check if Cayuga is installed in the default path
        /// If not installes in the default path then search for SeeTec folder
        /// </summary>
        public static void SeeTecInstallPath()
        {
            string defaultSeeTecInstallPath = @"C:\Program Files\SeeTec";

            try
            {
                // Check if Cayuga is installed in the default path
                // Yes: Set installation path as default
                // No: Search drives for SeeTec folder
                if (Directory.Exists(defaultSeeTecInstallPath))
                {
                    ClientFunctions.seeTecInstallPath = defaultSeeTecInstallPath;
                }
                else
                {
                    string[] dirsLevelOne;
                    string[] dirsLevelOne2;
                    string[] dirsLevelTwo;

                    // Check for available hard drives and iterate one by one over all hard drives 
                    foreach (DriveInfo d in DriveInfo.GetDrives().Where(d => d.IsReady))
                    {
                        try
                        {
                            // First search in the top directory for SeeTec folder
                            dirsLevelOne = Directory.GetDirectories(d.RootDirectory.FullName);

                            foreach (var item in dirsLevelOne)
                            {
                                // Check if SeeTec folder is found in the top directory
                                dirsLevelOne2 = Directory.GetDirectories(d.RootDirectory.FullName, "SeeTec");

                                // If SeeTec folder is found in the top directory then set the installation path
                                if (dirsLevelOne2.Length == 1)
                                {
                                    ClientFunctions.seeTecInstallPath = dirsLevelOne2[0];
                                }
                                else
                                {
                                    // If SeeTec folder is not in the top directory then search for the folder in the subdirectory for it
                                    dirsLevelTwo = Directory.GetDirectories(item, "SeeTec");

                                    // If SeeTec folder is found in the subdirectory then set the installation path
                                    if (dirsLevelTwo.Length == 1)
                                    {
                                        ClientFunctions.seeTecInstallPath = dirsLevelTwo[0];
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex.Message, ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Info(ex.Message, ex);
            }

            ClientFunctions.ServerLogsPath = Path.Combine(ClientFunctions.seeTecInstallPath, "log");

            log.Info("SeeTec server logs under: " + ClientFunctions.ServerLogsPath);
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
    }
}
