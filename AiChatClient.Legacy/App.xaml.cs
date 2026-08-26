using System.Configuration;
using System.Data;
using System.IO;
using System.Net.Http;
using System.Windows;
using AiChatClient.Config;
using AiChatClient.Data;
using AiChatClient.Services;
using AiChatClient.Services.Impl;
using AiChatClient.Services.Tools;
using AiChatClient.Services.Tools.Impl;
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
        private string DataBaseConnect => AppDataPathProvider.GetConnectionString(App.Config);

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

            services.Configure<AiOptions>(
                Config.GetSection("AI"));

            // 为每次数据库操作创建短生命周期 DbContext，避免整个 WPF 应用共享同一实例。
            services.AddDbContextFactory<AppDbContext>((x) =>
            {
                x.UseSqlite(DataBaseConnect);
            });

            services.AddScoped<IAIRoleRepository, AIRoleRepository>();
            services.AddScoped<IAIRoleService, AIRoleService>();

            services.AddScoped<IConversationRepository, ConversationRepository>();
            services.AddScoped<IConversationService, ConversationService>();

            services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
            services.AddScoped<IChatMessageService, ChatMessageService>();

            //services.AddSingleton<AiChatClient.Services.IConversationService, AiChatClient.Services.Impl.ConversationService>();
            services.AddSingleton<AiChatClient.Services.IChatProvider, AiChatClient.Services.Impl.OllamaChatProvider>();
            services.AddSingleton<AiChatClient.Services.IChatProvider, AiChatClient.Services.Impl.DeepSeekChatProvider>();

            services.AddSingleton<ITool, CalculatorTool>();
            services.AddSingleton<ITool, CurrentTimeTool>();
            services.AddSingleton<IToolResolver, ToolResolver>();

            services.AddSingleton<IChatProviderResolver, ChatProviderResolver>();
            // Markdown renderer service
            services.AddSingleton<IMarkdownRendererService, MarkdownRendererService>();
            // Dialog service
            services.AddSingleton<IDialogService, DialogService>();
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
                // 本机配置只用于开发者本地覆盖，禁止提交到代码仓库。
                .AddJsonFile(
                    "appsettings.Local.json",
                    optional: true,
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
