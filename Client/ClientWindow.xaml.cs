using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Interaction logic for ClientWindow.xaml
    /// </summary>
    public partial class ClientWindow : Window
    {
        AsynchronousClient client;

        public ClientWindow()
        {
            InitializeComponent();

            // Starts method to read the client configuration from the xml file. 
            // Create a xml configuration file if it doenst exist
            ClientFunctions.ClientStartPreparation();

            client = new AsynchronousClient();
        }

        private void StartConnectingToServer()
        {
            Thread threadClient = new Thread(() => client.StartConnectToServer())
            {
                IsBackground = true
            };
            threadClient.Start();

            //AsynchronousClient.Connected = true;
        }

        private void Button_ConnectClient(object sender, RoutedEventArgs e)
        {
            this.StartConnectingToServer();
        }
    }
}
