using System.Configuration;
using System.Data;
using System.IO;
using System.Net.Http;
using System.Windows;
using AiChatClient.Data;
using AiChatClient.Services;
using AiChatClient.Services.Impl;
using AiChatClient.ViewModels;
using Microsoft.EntityFrameworkCore;
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
        private string DataBaseConnect => App.Config["ConnectionStrings:DefaultConnection"]!;

        private  ServiceProvider _serviceProvider;
        // 全局配置对象，整个程序随处调用
        public static IConfiguration Config { get; private set; }
        public static IServiceProvider Services { get; private set; }
        public App()
        {
          
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            services.AddDbContext<AppDbContext>((x) =>
            {
                x.UseSqlite(DataBaseConnect);
            });
            services.AddSingleton<AiChatClient.Services.IConversationService, AiChatClient.Services.Impl.ConversationService>();
            services.AddSingleton<AiChatClient.Services.IChatProvider, AiChatClient.Services.Impl.OllamaChatProvider>();
            // Markdown renderer service
            services.AddSingleton<IMarkdownRendererService, MarkdownRendererService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            
            services.AddHttpClient<IChatService, ChatService>();

          

            services.AddTransient<DatabaseInitializer>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // 构建配置读取器
            Config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build(); 
            var services = new ServiceCollection();

            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();
            Services = _serviceProvider;

            using (var scope = _serviceProvider.CreateScope())
            {
                var initializer =
                    scope.ServiceProvider
                    .GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync();
            }
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            mainWindow.Show();
        }
    }

}
