using System.Configuration;
using System.Data;
using System.IO;
using System.Net.Http;
using System.Windows;
using AiChatClient.Services;
using AiChatClient.Services.Impl;
using AiChatClient.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiChatClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;
        // 全局配置对象，整个程序随处调用
        public static IConfiguration Config { get; private set; }
        public App()
        {
            var services = new ServiceCollection();

            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            
            services.AddHttpClient<IChatService, ChatService>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // 构建配置读取器
            Config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            mainWindow.Show();
        }
    }

}
