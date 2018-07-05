using System.Threading;
using System.Windows;


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

            this.StartConnectingToServer();
        }

        private void StartConnectingToServer()
        {
            Thread threadClient = new Thread(() => client.StartConnectToServer())
            {
                IsBackground = true
            };
            threadClient.Start();
        }
    }
}
