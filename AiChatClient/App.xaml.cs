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
using Repositories;
using Repositories.Impl;
using Services;
using Services.Impl;

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
        private IServiceScope? _appScope;
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

            services.AddScoped<IAIRoleRepository, AIRoleRepository>();
            services.AddScoped<IAIRoleService, AIRoleService>();


            services.AddSingleton<AiChatClient.Services.IConversationService, AiChatClient.Services.Impl.ConversationService>();
            services.AddSingleton<AiChatClient.Services.IChatProvider, AiChatClient.Services.Impl.OllamaChatProvider>();
            // Markdown renderer service
            services.AddSingleton<IMarkdownRendererService, MarkdownRendererService>();
            services.AddScoped<MainViewModel>();
            services.AddScoped<MainWindow>();

            
            services.AddHttpClient<IChatService, ChatService>();

          

            services.AddScoped<DatabaseInitializer>();
        }

        protected override async void OnStartup(
    StartupEventArgs e)
        {
            base.OnStartup(e);


            Config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(
                    "appsettings.json",
                    optional: false,
                    reloadOnChange: true)
                .Build();


            var services = new ServiceCollection();

            ConfigureServices(services);


            _serviceProvider =
                services.BuildServiceProvider();


            Services = _serviceProvider;


            _appScope = _serviceProvider.CreateScope();


            var initializer =
                _appScope.ServiceProvider
                .GetRequiredService<DatabaseInitializer>();

            await initializer.InitializeAsync();


            var vm =
                _appScope.ServiceProvider
                .GetRequiredService<MainViewModel>();

            await vm.InitializeAsync();


            var mainWindow =
                _appScope.ServiceProvider
                .GetRequiredService<MainWindow>();


            mainWindow.Show();
        }
        protected override void OnExit( ExitEventArgs e)
        {
            _appScope?.Dispose();

            _serviceProvider?.Dispose();

            base.OnExit(e);
        }
    }

}
