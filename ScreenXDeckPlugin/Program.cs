using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Serilog;
using Microsoft.Extensions.DependencyInjection;
using ScreenControl;
using ScreenControl.Monitors;
using ScreenControl.Windows;
using ScreenXDeckPlugin;

var builder = MacroDeckPlugin.CreatePlugin(args);
builder.Services.AddSingleton<IMonitorService, MonitorService>();
builder.Services.AddSingleton<IWindowService, WindowService>();
var plugin = builder
    .UseMacroDeckLogging()
    .UseLocalization(Strings.LocalizationCatalog)
    .RegisterIntegration<ScreenXDeckIntegration>()
    .Build();

await plugin.RunAsync();
