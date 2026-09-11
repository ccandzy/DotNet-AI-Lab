using System.Configuration;
using System.Data;
using System.IO;
using System.Net.Http;
using System.Windows;
using AiChatClient.Config;
using AiChatClient.Data;
using AiChatClient.Plugins;
using AiChatClient.Services;
using AiChatClient.Services.Impl;
using AiChatClient.Services.Mcp;
using AiChatClient.Services.SemanticKernel;
using AiChatClient.Services.Rag;
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

        private ServiceProvider? _serviceProvider;
        // 全局配置对象，整个程序随处调用
        public static IConfiguration Config { get; private set; } = null!;
        public static IServiceProvider Services { get; private set; } = null!;
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
            services.Configure<EmbeddingOptions>(
                Config.GetSection("Embedding"));
            services.Configure<RagOptions>(
                Config.GetSection("Rag"));

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

            services.AddSingleton<CalculatorPlugin>();
            services.AddSingleton<TimePlugin>();
            services.AddSingleton<IKernelFactory, SemanticKernelFactory>();
            services.AddSingleton<IChatService>(serviceProvider =>
                new SemanticKernelChatService(
                    serviceProvider.GetRequiredService<IKernelFactory>()));
            services.AddSingleton<IDocumentLoader, MarkdownDocumentLoader>();
            services.AddSingleton<IEmbeddingService, SemanticKernelEmbeddingService>();
            services.AddSingleton<IVectorStore, InMemoryVectorStore>();
            services.AddSingleton<IRagService, RagService>();
            // MCP Client 持有一个 Server 子进程，因此整个应用只创建一个实例。
            // ServiceProvider 在应用退出时 Dispose，它会清理仍在运行的子进程。
            services.AddSingleton<IMcpClientService, McpClientService>();
            // Markdown renderer service
            services.AddSingleton<IMarkdownRendererService, MarkdownRendererService>();
            // Dialog service
            services.AddSingleton<IDialogService, DialogService>();
            services.AddScoped<MainViewModel>();
            services.AddScoped<MainWindow>();

            
            services.AddHttpClient(SemanticKernelFactory.HttpClientName);

          

            services.AddScoped<DatabaseInitializer>();
        }

        protected override async void OnStartup(
    StartupEventArgs e)
        {
            base.OnStartup(e);


            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var started = await StartupConfiguration.TryInitializeAsync(async () =>
            {
            Config = StartupConfiguration.Load();

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


            MainWindow = mainWindow;
            mainWindow.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            }, ex => MessageBox.Show(
                $"程序启动失败，请检查配置和数据库后重新启动。\n{ex.Message}",
                "启动失败", MessageBoxButton.OK, MessageBoxImage.Error));
            if (!started) Shutdown(1);
        }
        protected override void OnExit( ExitEventArgs e)
        {
            try { _appScope?.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            try { _serviceProvider?.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            try { (Config as IDisposable)?.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            base.OnExit(e);
        }
    }

}
