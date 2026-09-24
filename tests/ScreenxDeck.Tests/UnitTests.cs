using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ScreenxDeck.Actions;
using ScreenxDeck.Core.Models;
using ScreenxDeck.Core.Native;
using ScreenxDeck.Integration;
using ScreenxDeck.Services;
using Serilog;
using Xunit;

using MacroDeck.Sdk;
using MacroDeck.Sdk.Variables;
using MacroDeck.Plugin.Hosting;
using Moq;

namespace ScreenxDeck.Tests;

public class UnitTests
{
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    [Fact]
    public async Task InspectVariablesCapabilityHandler_OwnerOfAndEagerDefinition()
    {
        var settingsService = new SettingsService(_logger);
        var displayManager = new DisplayManagerService(_logger);
        var brightnessService = new BrightnessService(_logger, displayManager, settingsService);
        var resService = new ResolutionRefreshService(_logger, displayManager);
        var topologyService = new TopologyService(_logger, displayManager, settingsService);
        var monitorControl = new MonitorControlService(_logger, displayManager);
        var profileService = new ProfileService(_logger, displayManager, brightnessService, resService, topologyService, monitorControl, settingsService);
        var windowManager = new WindowManagerService(_logger, displayManager, settingsService);

        using var integration = new ScreenxDeckIntegration(
            _logger,
            displayManager,
            brightnessService,
            resService,
            topologyService,
            monitorControl,
            profileService,
            windowManager,
            settingsService);

        var hostingAsm = typeof(MacroDeckPlugin).Assembly;
        var handlerType = hostingAsm.GetType("MacroDeck.Plugin.Hosting.Capabilities.Variables.VariablesCapabilityHandler");
        Assert.NotNull(handlerType);

        // Let's create an instance or inspect the methods
        var ctors = handlerType.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var c in ctors)
        {
            var p = string.Join(", ", c.GetParameters().Select(param => $"{param.ParameterType.Name} {param.Name}"));
            Console.WriteLine($"HANDLER CTOR: ({p})");
        }

        // Also check VariableDescriptorMapper.LocalIdOf
        var mapperType = hostingAsm.GetType("MacroDeck.Plugin.Hosting.Capabilities.Variables.VariableDescriptorMapper");
        if (mapperType != null)
        {
            var localIdOfMethod = mapperType.GetMethod("LocalIdOf", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Console.WriteLine($"VariableDescriptorMapper.LocalIdOf exists: {localIdOfMethod != null}");
            if (localIdOfMethod != null)
            {
                var vDef = integration.Variables.First();
                var result = localIdOfMethod.Invoke(null, [vDef]);
                Console.WriteLine($"LocalIdOf({vDef.Name}) => {result}");
            }
        }

        var subsType = hostingAsm.GetType("MacroDeck.Plugin.Hosting.Capabilities.Variables.VariableSubscriptions");
        foreach (var c in subsType!.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
        {
            var p = string.Join(", ", c.GetParameters().Select(param => $"{param.ParameterType.Name} {param.Name}"));
            Console.WriteLine($"SUBS CTOR: ({p})");
        }
        var subs = Activator.CreateInstance(subsType, new object[] { _logger });
        var metaType = ctors[0].GetParameters()[1].ParameterType;
        var meta = Activator.CreateInstance(metaType);
        var handler = Activator.CreateInstance(handlerType, new object[] { new[] { integration }, meta!, subs!, _logger });
        var declareMethod = handlerType.GetMethod("DeclareCapabilities");
        var caps = (System.Collections.IEnumerable)declareMethod!.Invoke(handler, null)!;
        int count = 0;
        foreach (var cap in caps)
        {
            count++;
            if (count <= 3)
            {
                Console.WriteLine($"CAPABILITY: {cap}");
            }
        }
        var invokeMethod = handlerType.GetMethod("InvokeAsync");
        Console.WriteLine($"InvokeMethod: {invokeMethod}");

        // Also check if SupportsPush is true or false
        var supportsPushProp = typeof(IVariableProvider).GetProperty("SupportsPush");
        Console.WriteLine($"integration.SupportsPush: {supportsPushProp?.GetValue(integration)}");

        var protoAsm = System.Reflection.Assembly.Load("MacroDeck.Plugin.Protocol");
        var varsOpType = protoAsm.GetType("MacroDeck.Plugin.Protocol.Capabilities.CapabilityOperations+Variables");
        Console.WriteLine($"VarsOpType: {varsOpType}");
        var getMethod = handlerType.GetMethod("GetAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Console.WriteLine($"GetMethod: {getMethod}");
        // Let's find what Arguments type it uses:
        var getArgsType = protoAsm.GetTypes().FirstOrDefault(t => t.Name.Contains("VariablesGet") || t.Name.Contains("VariableGet"));
        Console.WriteLine($"GetArgsType: {getArgsType?.FullName}");
        var ownerOfMethod = handlerType.GetMethod("OwnerOf", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var ownerHyphen = ownerOfMethod!.Invoke(handler, new object[] { "screenxdeck-brightness" });
        var ownerUnderscore = ownerOfMethod!.Invoke(handler, new object[] { "screenxdeck_brightness" });
        Console.WriteLine($"OwnerOf('screenxdeck-brightness'): {ownerHyphen != null}");
        Console.WriteLine($"OwnerOf('screenxdeck_brightness'): {ownerUnderscore != null}");

        var ownerMethod = handlerType.GetMethod("OwnerOf", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var bytes = ownerMethod!.GetMethodBody()?.GetILAsByteArray();
        Console.WriteLine($"OwnerOf IL bytes length: {bytes?.Length}");

        var invocType = hostingAsm.GetType("MacroDeck.Plugin.Hosting.Capabilities.CapabilityInvocation");
        var invoc = Activator.CreateInstance(invocType!)!;
        invocType!.GetProperty("Kind")!.SetValue(invoc, "variables");
        invocType!.GetProperty("LocalId")!.SetValue(invoc, "screenxdeck-brightness");
        invocType!.GetProperty("Operation")!.SetValue(invoc, "get");
        
        var task = (Task)invokeMethod!.Invoke(handler, new object[] { invoc, CancellationToken.None })!;
        await task;
        var resProp = task.GetType().GetProperty("Result")!;
        var res = resProp.GetValue(task)!;
        Console.WriteLine($"INVOKE (LocalId='screenxdeck-brightness', Op='get'): Result = {res}");
    }

    [Fact]
    public async Task ScreenxDeckIntegration_ImplementsVariableProviderAndExposesAllVariables()
    {
        var settingsService = new SettingsService(_logger);
        var displayManager = new DisplayManagerService(_logger);
        var brightnessService = new BrightnessService(_logger, displayManager, settingsService);
        var resService = new ResolutionRefreshService(_logger, displayManager);
        var topologyService = new TopologyService(_logger, displayManager, settingsService);
        var monitorControl = new MonitorControlService(_logger, displayManager);
        var profileService = new ProfileService(_logger, displayManager, brightnessService, resService, topologyService, monitorControl, settingsService);
        var windowManager = new WindowManagerService(_logger, displayManager, settingsService);

        using var integration = new ScreenxDeckIntegration(
            _logger,
            displayManager,
            brightnessService,
            resService,
            topologyService,
            monitorControl,
            profileService,
            windowManager,
            settingsService);

        // 1. Verify IVariableProvider interface
        Assert.IsAssignableFrom<IVariableProvider>(integration);
        var provider = (IVariableProvider)integration;

        // 2. Verify the 28 stable, high-value variables
        var variables = provider.Variables;
        Assert.NotNull(variables);
        Assert.Equal(28, variables.Count);
        Assert.False(provider.SupportsCatalog);
        Assert.All(variables, variable => Assert.Equal(VariableMaterialization.Eager, variable.Materialization));
        Assert.Equal(28, variables.Select(variable => variable.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var v in variables)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Id), $"Variable {v.Name} has null or empty Id");
            Assert.False(string.IsNullOrWhiteSpace(v.Name), "Variable has null or empty Name");
            Assert.Equal(v.Name!.Replace('_', '-'), v.Id);
            Assert.DoesNotContain('_', v.Id);
        }

        // Verify none of the removed variables exist
        string[] removedVars =
        [
            "screenxdeck_display1_width",
            "screenxdeck_display1_height",
            "screenxdeck_display2_width",
            "screenxdeck_display2_height",
            "screenxdeck_display3_width",
            "screenxdeck_display3_height",
            "screenxdeck_display4_width",
            "screenxdeck_display4_height",
            "screenxdeck_saved_profiles_count",
            "screenxdeck_saved_arrangements_count",
            "screenxdeck_active_window_process",
            "screenxdeck_active_window_display",
            "screenxdeck_total_resolution",
            "screenxdeck_display1_orientation",
            "screenxdeck_display1_ddc_supported",
            "screenxdeck_display2_ddc_supported",
            "screenxdeck_display3_ddc_supported",
            "screenxdeck_display4_ddc_supported",
            "screenxdeck_display1_aspect_ratio",
            "screenxdeck_display2_aspect_ratio",
            "screenxdeck_display3_aspect_ratio",
            "screenxdeck_display4_aspect_ratio",
            "screenxdeck_display1_is_primary",
            "screenxdeck_display2_is_primary",
            "screenxdeck_display3_is_primary",
            "screenxdeck_display4_is_primary",
            "screenxdeck_display1_name",
            "screenxdeck_display1_device",
            "screenxdeck_display1_brightness_method",
            "screenxdeck_display2_name",
            "screenxdeck_display2_device",
            "screenxdeck_display2_brightness_method",
            "screenxdeck_display3_name",
            "screenxdeck_display3_device",
            "screenxdeck_display3_brightness_method",
            "screenxdeck_display4_name",
            "screenxdeck_display4_device",
            "screenxdeck_display4_brightness_method"
        ];
        foreach (var removed in removedVars)
        {
            Assert.DoesNotContain(variables, v => v.Name == removed);
        }

        // 3. Verify screenxdeck_brightness definition & slider capability
        var brightnessDef = variables.FirstOrDefault(v => v.Name == "screenxdeck_brightness");
        Assert.NotNull(brightnessDef);
        Assert.Equal(VariableType.Numeric, brightnessDef.Type);
        Assert.Equal("%", brightnessDef.Unit);
        Assert.Equal(VariableSemanticKinds.Percentage, brightnessDef.SemanticKind);
        Assert.NotNull(brightnessDef.Write);

        // 4. Test ReadAsync with direct name, plugin prefix, and hyphen normalization
        var readingDirect = await provider.ReadAsync("screenxdeck_brightness");
        Assert.NotEqual(VariableReading.Unavailable, readingDirect);
        Assert.Equal(0.0, readingDirect.Min);
        Assert.Equal(100.0, readingDirect.Max);

        var readingPrefixed = await provider.ReadAsync("com.screenxdeck.cjhackeryt.screenxdeck_brightness");
        Assert.NotEqual(VariableReading.Unavailable, readingPrefixed);
        Assert.Equal(0.0, readingPrefixed.Min);
        Assert.Equal(100.0, readingPrefixed.Max);

        var readingHyphen = await provider.ReadAsync("screenxdeck-brightness");
        Assert.NotEqual(VariableReading.Unavailable, readingHyphen);

        // 5. Test every variable to ensure NONE are VariableReading.Unavailable
        foreach (var def in variables)
        {
            var r = await provider.ReadAsync(def.Id!);
            Assert.NotEqual(VariableReading.Unavailable, r);
            Assert.NotNull(r.Value);
        }

        // 6. Verify connection state and disconnected resolution for every monitor ordinal
        for (int display = 1; display <= 4; display++)
        {
            var monitor = displayManager.ConnectedMonitors.FirstOrDefault(item => item.DisplayIndex == display);
            var connectedDefinition = variables.Single(variable => variable.Name == $"screenxdeck_display{display}_connected");
            Assert.Equal(VariableType.Boolean, connectedDefinition.Type);

            var connectedReading = await provider.ReadAsync(connectedDefinition.Id!);
            Assert.Equal(monitor != null, Assert.IsType<bool>(connectedReading.Value));

            string expectedResolution = monitor != null
                ? $"{monitor.CurrentWidth}x{monitor.CurrentHeight}"
                : "Not connected";
            var resolutionReading = await provider.ReadAsync($"screenxdeck_display{display}_resolution");
            Assert.Equal(expectedResolution, resolutionReading.Value);
        }

        // 7. Test SetValueAsync with prefix and direct name (Macro Deck slider write)
        var writeResult = await provider.SetValueAsync("com.screenxdeck.cjhackeryt.screenxdeck_brightness", 65);
        Assert.Equal(VariableWriteStatus.Applied, writeResult.Status);
    }

    [Fact]
    public async Task InitializeAsync_RemovesLegacyEagerVariablesOnlyOnce()
    {
        var settingsService = new SettingsService(_logger);
        var displayManager = new DisplayManagerService(_logger);
        var brightnessService = new BrightnessService(_logger, displayManager, settingsService);
        var resService = new ResolutionRefreshService(_logger, displayManager);
        var topologyService = new TopologyService(_logger, displayManager, settingsService);
        var monitorControl = new MonitorControlService(_logger, displayManager);
        var profileService = new ProfileService(_logger, displayManager, brightnessService, resService, topologyService, monitorControl, settingsService);
        var windowManager = new WindowManagerService(_logger, displayManager, settingsService);

        using var integration = new ScreenxDeckIntegration(
            _logger,
            displayManager,
            brightnessService,
            resService,
            topologyService,
            monitorControl,
            profileService,
            windowManager,
            settingsService);

        var legacyCatalogVariableId = Guid.NewGuid();
        var connectedVariableId = Guid.NewGuid();
        var eagerVariableId = Guid.NewGuid();
        IReadOnlyList<VariableHandle> handles =
        [
            new VariableHandle(legacyCatalogVariableId, "screenxdeck_display4_name", VariableType.Text, "Display 4", null)
            {
                DefinitionId = "screenxdeck-display4-name"
            },
            new VariableHandle(connectedVariableId, "screenxdeck_display4_connected", VariableType.Boolean, false, null)
            {
                DefinitionId = "screenxdeck-display4-connected"
            },
            new VariableHandle(eagerVariableId, "screenxdeck_brightness", VariableType.Numeric, 100, 0)
            {
                DefinitionId = "screenxdeck-brightness"
            }
        ];

        var variablesApi = new Mock<IVariableApi>();
        variablesApi.Setup(api => api.GetAllAsync()).ReturnsAsync(handles);
        variablesApi.Setup(api => api.DeleteAsync(legacyCatalogVariableId)).Returns(Task.CompletedTask);
        variablesApi.Setup(api => api.DeleteAsync(eagerVariableId)).Returns(Task.CompletedTask);
        var context = new Mock<IIntegrationContext>();
        context.SetupGet(value => value.Variables).Returns(variablesApi.Object);

        string? originalDataDirectory = Environment.GetEnvironmentVariable("MACRO_DECK_PLUGIN_DATA_DIRECTORY");
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"screenxdeck-migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        Environment.SetEnvironmentVariable("MACRO_DECK_PLUGIN_DATA_DIRECTORY", dataDirectory);

        try
        {
            await integration.InitializeAsync(context.Object);
            await integration.InitializeAsync(context.Object);

            variablesApi.Verify(api => api.GetAllAsync(), Times.Once);
            variablesApi.Verify(api => api.DeleteAsync(legacyCatalogVariableId), Times.Once);
            variablesApi.Verify(api => api.DeleteAsync(connectedVariableId), Times.Never);
            variablesApi.Verify(api => api.DeleteAsync(eagerVariableId), Times.Never);
            Assert.True(File.Exists(Path.Combine(dataDirectory, "catalog-variables-v2.marker")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MACRO_DECK_PLUGIN_DATA_DIRECTORY", originalDataDirectory);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }


    [Fact]
    public void Gdi32_GammaRamp_CalculatesValidValuesAcrossAllPercentages()
    {
        // 100% brightness (neutral gamma 1.0)
        var ramp100 = Gdi32.CreateGammaRampForBrightness(100);
        Assert.NotNull(ramp100.Red);
        Assert.NotNull(ramp100.Green);
        Assert.NotNull(ramp100.Blue);
        Assert.Equal(256, ramp100.Red.Length);
        Assert.Equal(0, ramp100.Red[0]);
        Assert.Equal(65535, ramp100.Red[255]); // Full dynamic endpoint

        // 75% brightness
        var ramp75 = Gdi32.CreateGammaRampForBrightness(75);
        Assert.True(ramp75.Red[128] < ramp100.Red[128]);
        Assert.True(ramp75.Red[128] > 0);

        // 50% brightness (works and midtones are darkened)
        var ramp50 = Gdi32.CreateGammaRampForBrightness(50);
        Assert.Equal(0, ramp50.Red[0]);
        Assert.True(ramp50.Red[128] < ramp75.Red[128]);
        Assert.True(ramp50.Red[128] > 0);

        // 25% brightness (working below 50%)
        var ramp25 = Gdi32.CreateGammaRampForBrightness(25);
        Assert.Equal(0, ramp25.Red[0]);
        Assert.True(ramp25.Red[128] < ramp50.Red[128]);
        Assert.True(ramp25.Red[128] > 0);

        // 0% brightness (works below 50% and dark floor check)
        var ramp0 = Gdi32.CreateGammaRampForBrightness(0);
        Assert.Equal(0, ramp0.Red[0]);
        Assert.Equal(65535, ramp0.Red[255]); // Keeps top endpoint for driver heuristic compliance
        Assert.True(ramp0.Red[128] > 0);
        Assert.True(ramp0.Red[128] < ramp25.Red[128]);
    }

    [Fact]
    public void BrightnessPresetAction_IncludesZeroPercent()
    {
        var settingsService = new SettingsService(_logger);
        var displayManager = new DisplayManagerService(_logger);
        var brightnessService = new BrightnessService(_logger, displayManager, settingsService);

        var action = new BrightnessPresetAction(brightnessService, _logger);
        var presetParam = action.Parameters.FirstOrDefault(p => p.Name == "preset");
        Assert.NotNull(presetParam);
        Assert.NotNull(presetParam.Options);

        // Verify "0" is available in the options
        Assert.Contains(presetParam.Options, opt => opt.Value == "0" && opt.Label == "0%");
        Assert.Contains(presetParam.Options, opt => opt.Value == "25" && opt.Label == "25%");
        Assert.Contains(presetParam.Options, opt => opt.Value == "50" && opt.Label == "50%");
        Assert.Contains(presetParam.Options, opt => opt.Value == "75" && opt.Label == "75%");
        Assert.Contains(presetParam.Options, opt => opt.Value == "100" && opt.Label == "100%");
    }

    [Fact]
    public void DisplayResolution_EqualityAndToString()
    {
        var resA = new DisplayResolution(1920, 1080);
        var resB = new DisplayResolution(1920, 1080);
        var resC = new DisplayResolution(2560, 1440);

        Assert.Equal(resA, resB);
        Assert.NotEqual(resA, resC);
        Assert.Equal("1920x1080", resA.ToString());
        Assert.Equal("2560x1440", resC.ToString());
    }

    [Fact]
    public void PluginSettings_SerializesAndDeserializesCorrectly()
    {
        var settings = new PluginSettings
        {
            ActiveProfileName = "Gaming",
            AutoPinApplications = ["notepad", "obs64"]
        };
        settings.SavedProfiles.Add(new DisplayProfile
        {
            Name = "Gaming",
            DisplayMode = "Extend",
            Monitors =
            [
                new MonitorProfileEntry
                {
                    DisplayIndex = 1,
                    DeviceName = @"\\.\DISPLAY1",
                    FriendlyName = "Gaming Monitor",
                    Width = 2560,
                    Height = 1440,
                    RefreshRate = 144,
                    Brightness = 80,
                    IsPrimary = true
                }
            ]
        });

        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        Assert.False(string.IsNullOrWhiteSpace(json));

        var deserialized = JsonSerializer.Deserialize<PluginSettings>(json);
        Assert.NotNull(deserialized);
        Assert.Equal("Gaming", deserialized.ActiveProfileName);
        Assert.Equal(2, deserialized.AutoPinApplications.Count);
        Assert.Single(deserialized.SavedProfiles);
        Assert.Equal(144, deserialized.SavedProfiles[0].Monitors[0].RefreshRate);
        Assert.Equal(80, deserialized.SavedProfiles[0].Monitors[0].Brightness);
    }

    [Fact]
    public void ScreenxDeckIntegration_RegistersAll27ActionsWithUniqueIds()
    {
        var settingsService = new SettingsService(_logger);
        var displayManager = new DisplayManagerService(_logger);
        var brightnessService = new BrightnessService(_logger, displayManager, settingsService);
        var resService = new ResolutionRefreshService(_logger, displayManager);
        var topologyService = new TopologyService(_logger, displayManager, settingsService);
        var monitorControl = new MonitorControlService(_logger, displayManager);
        var profileService = new ProfileService(_logger, displayManager, brightnessService, resService, topologyService, monitorControl, settingsService);
        var windowManager = new WindowManagerService(_logger, displayManager, settingsService);

        using var integration = new ScreenxDeckIntegration(
            _logger,
            displayManager,
            brightnessService,
            resService,
            topologyService,
            monitorControl,
            profileService,
            windowManager,
            settingsService);

        var actions = integration.Actions;
        Assert.NotEmpty(actions);
        Assert.Equal(27, actions.Count);

        // Verify each action definition
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in actions)
        {
            Assert.False(string.IsNullOrWhiteSpace(action.Id), $"Action {action.GetType().Name} has empty Id");
            Assert.False(action.Name.IsEmpty, $"Action {action.Id} has empty Name");
            Assert.False(action.Description.IsEmpty, $"Action {action.Id} has empty Description");
            Assert.NotNull(action.CreateExecutor());
            Assert.True(ids.Add(action.Id), $"Duplicate action Id detected: '{action.Id}'");
        }
    }

    [Fact]
    public void ManifestJson_MatchesPackageIdAndVersion()
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            // If running in test directory, look up into src/ScreenxDeck
            manifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/ScreenxDeck/manifest.json"));
        }

        Assert.True(File.Exists(manifestPath), $"manifest.json not found at {manifestPath}");
        string json = File.ReadAllText(manifestPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("com.screenxdeck.cjhackeryt", root.GetProperty("id").GetString());
        Assert.Equal("ScreenxDeck", root.GetProperty("name").GetString());
        Assert.Equal("1.0.0", root.GetProperty("version").GetString());
        Assert.Equal("runtimes/win-x64/ScreenxDeck.dll", root.GetProperty("entrypoints").GetProperty("win-x64").GetProperty("executable").GetString());
        Assert.Equal("FrameworkDependent", root.GetProperty("entrypoints").GetProperty("win-x64").GetProperty("runtime").GetProperty("kind").GetString());
        Assert.True(root.TryGetProperty("publisher", out var publisherProp));
        Assert.Equal("CJHackerYT", publisherProp.GetProperty("name").GetString());
        Assert.True(root.TryGetProperty("compatibility", out var compatProp));
        Assert.Equal(">=3.0.0-beta.13", compatProp.GetProperty("macroDeck").GetString());
    }
}
