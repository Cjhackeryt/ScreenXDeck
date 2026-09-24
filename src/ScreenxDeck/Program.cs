using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Serilog;
using ScreenxDeck.Integration;
using ScreenxDeck.Services;

namespace ScreenxDeck;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = MacroDeckPlugin.CreatePlugin(args);
        builder.UseMacroDeckLogging();
        builder.UseLocalization(Strings.LocalizationCatalog);

        builder.ConfigureServices((context, services) =>
        {
            services.AddSingleton<SettingsService>();
            services.AddSingleton<DisplayManagerService>();
            services.AddSingleton<BrightnessService>();
            services.AddSingleton<ResolutionRefreshService>();
            services.AddSingleton<TopologyService>();
            services.AddSingleton<MonitorControlService>();
            services.AddSingleton<ProfileService>();
            services.AddSingleton<WindowManagerService>();
        });

        builder.RegisterIntegration<ScreenxDeckIntegration>();

        var app = builder.Build();
        await app.RunAsync();
    }
}
