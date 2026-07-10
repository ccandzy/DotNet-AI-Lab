using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AiChatClient.Services;
using AiChatClient.Services.Impl;

namespace AiChatClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IOllamaService _ollamaService;
        public MainWindow(IOllamaService ollamaService)
        {
            _ollamaService = ollamaService;     
            InitializeComponent();
            Test4();
        }
        //private readonly AiHttpClient _aiHttpClient;

        //public MainWindow(AiHttpClient aiHttpClient)
        //{
        //    InitializeComponent();

        //    _aiHttpClient = aiHttpClient;

        //    Test2();
        //}

      private async void Test4()
        {
            var result = await _ollamaService.GenerateAsync("hello");
            MessageBox.Show(result);
        }   
    }
}