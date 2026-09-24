using System.Text.RegularExpressions;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenControl.Actions;
using ScreenControl.Monitors;
using ScreenControl.Windows;
using ScreenXDeckPlugin;
using Serilog;

namespace ScreenXDeck.Tests;

[TestClass]
public sealed class IntegrationTests
{
    private static readonly Regex LocalIdPattern = new("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    [TestMethod]
    public void Actions_HaveUniqueValidLocalIds()
    {
        var monitors = new MonitorService();
        var windows = new WindowService();
        var serilogLogger = new LoggerConfiguration().CreateLogger();
        var integration = new ScreenXDeckIntegration(
            NullLogger<ScreenXDeckIntegration>.Instance,
            monitors,
            windows,
            serilogLogger);

        Assert.IsNotNull(integration.Actions);
        Assert.IsTrue(integration.Actions.Count > 0, "Integration must declare actions.");

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in integration.Actions)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(action.Id), "Action ID must not be blank.");
            Assert.IsTrue(action.Id.Length <= 64, $"Action ID '{action.Id}' exceeds 64 characters.");
            Assert.IsFalse(action.Id.Contains("::"), $"Action ID '{action.Id}' must not contain '::'.");
            Assert.IsTrue(
                LocalIdPattern.IsMatch(action.Id),
                $"Action ID '{action.Id}' must be kebab-case (e.g. 'set-brightness').");

            Assert.IsTrue(
                seenIds.Add(action.Id),
                $"Duplicate action ID found: '{action.Id}'. All action IDs must be unique across the plugin.");

            Assert.IsNotNull(action.Name, $"Action '{action.Id}' must have a localized Name.");
            Assert.IsNotNull(action.Description, $"Action '{action.Id}' must have a localized Description.");
            Assert.IsNotNull(action.Parameters, $"Action '{action.Id}' Parameters must not be null.");

            var executor = action.CreateExecutor();
            Assert.IsNotNull(executor, $"Action '{action.Id}' executor must not be null.");
        }
    }

    [TestMethod]
    public void DeclaredVariables_ConformToStandards()
    {
        var monitors = new MonitorService();
        var windows = new WindowService();
        var serilogLogger = new LoggerConfiguration().CreateLogger();
        var integration = new ScreenXDeckIntegration(
            NullLogger<ScreenXDeckIntegration>.Instance,
            monitors,
            windows,
            serilogLogger);

        Assert.IsNotNull(integration.Variables);
        Assert.IsTrue(integration.Variables.Count > 0, "Integration must declare variables.");

        var seenVariableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variable in integration.Variables)
        {
            var id = variable.Name;
            Assert.IsFalse(string.IsNullOrWhiteSpace(id), $"Variable with null/empty Name: {variable}");
            Assert.IsTrue(
                seenVariableIds.Add(id),
                $"Duplicate variable Name: '{id}'.");
        }

        var brightnessVar = integration.Variables.FirstOrDefault(v => v.Name == "screenxdeck_brightness");
        Assert.IsNotNull(brightnessVar, "screenxdeck_brightness variable must exist.");
        Assert.AreEqual(VariableType.Numeric, brightnessVar.Type);
        Assert.AreEqual("%", brightnessVar.Unit);
        Assert.AreEqual(VariableSemanticKinds.Percentage, brightnessVar.SemanticKind);
        Assert.IsNotNull(brightnessVar.Write, "screenxdeck_brightness must be writable.");

        var displayModeVar = integration.Variables.FirstOrDefault(v => v.Name == "screenxdeck_display_mode");
        Assert.IsNotNull(displayModeVar, "screenxdeck_display_mode variable must exist.");
        Assert.AreEqual(VariableType.Text, displayModeVar.Type);
        Assert.IsNotNull(displayModeVar.Write, "screenxdeck_display_mode must be writable.");

        Assert.IsNull(
            integration.Variables.FirstOrDefault(v => v.Name == "screenxdeck_active_monitor"),
            "screenxdeck_active_monitor must be removed.");

        for (var i = 1; i <= 4; i++)
        {
            Assert.IsNull(
                integration.Variables.FirstOrDefault(v => v.Name == $"monitor_{i}_brightness"),
                $"Legacy monitor_{i}_brightness must not be declared.");

            var monitorBrightness = integration.Variables.FirstOrDefault(v => v.Name == $"screenxdeck_monitor_{i}_brightness");
            Assert.IsNotNull(monitorBrightness, $"screenxdeck_monitor_{i}_brightness must exist.");
            Assert.AreEqual(VariableType.Numeric, monitorBrightness.Type);
            Assert.IsNotNull(monitorBrightness.Write, $"screenxdeck_monitor_{i}_brightness must be writable.");

            Assert.IsNotNull(
                integration.Variables.FirstOrDefault(v => v.Name == $"screenxdeck_monitor_{i}_name"),
                $"screenxdeck_monitor_{i}_name must exist.");
            Assert.IsNotNull(
                integration.Variables.FirstOrDefault(v => v.Name == $"screenxdeck_monitor_{i}_connected"),
                $"screenxdeck_monitor_{i}_connected must exist.");
            Assert.IsNotNull(
                integration.Variables.FirstOrDefault(v => v.Name == $"screenxdeck_monitor_{i}_refresh_rate"),
                $"screenxdeck_monitor_{i}_refresh_rate must exist.");
        }
    }

    [TestMethod]
    public async Task Monitor2_Variables_CanBeReadAndWritten()
    {
        var monitors = new MonitorService();
        var windows = new WindowService();
        var serilogLogger = new LoggerConfiguration().CreateLogger();
        var integration = new ScreenXDeckIntegration(
            NullLogger<ScreenXDeckIntegration>.Instance,
            monitors,
            windows,
            serilogLogger);

        var brightness = await integration.ReadAsync("screenxdeck_monitor_2_brightness");
        var name = await integration.ReadAsync("screenxdeck_monitor_2_name");
        var connected = await integration.ReadAsync("screenxdeck_monitor_2_connected");
        var refreshRate = await integration.ReadAsync("screenxdeck_monitor_2_refresh_rate");

        Assert.IsNotNull(brightness);
        Assert.IsNotNull(name);
        Assert.IsNotNull(connected);
        Assert.IsNotNull(refreshRate);

        if (System.Windows.Forms.Screen.AllScreens.Length >= 2)
        {
            Assert.AreNotEqual(VariableReading.Unavailable, connected);
            Assert.AreNotEqual(VariableReading.Unavailable, name);
            // Non-DDC/CI brightness should successfully report via gamma ramp / software fallback
            Assert.AreNotEqual(VariableReading.Unavailable, brightness, "Monitor 2 brightness must not be Unavailable when connected.");

            var originalValue = brightness is { Value: double num } ? (int)num : 100;
            var writeResult = await integration.SetValueAsync("screenxdeck_monitor_2_brightness", 100);
            await integration.SetValueAsync("screenxdeck_monitor_1_brightness", 100);
            monitors.HideOverlays();
            Assert.IsNotNull(writeResult);
        }
    }

    [TestMethod]
    public void DisplayParameters_ReadMonitor_ClampsBetween1And4()
    {
        Assert.AreEqual(1, DisplayParameters.ReadMonitor(new Dictionary<string, object>()));
        Assert.AreEqual(1, DisplayParameters.ReadMonitor(new Dictionary<string, object> { ["monitor"] = 0 }));
        Assert.AreEqual(1, DisplayParameters.ReadMonitor(new Dictionary<string, object> { ["monitor"] = -5 }));
        Assert.AreEqual(3, DisplayParameters.ReadMonitor(new Dictionary<string, object> { ["monitor"] = 3 }));
        Assert.AreEqual(4, DisplayParameters.ReadMonitor(new Dictionary<string, object> { ["monitor"] = 4 }));
        Assert.AreEqual(4, DisplayParameters.ReadMonitor(new Dictionary<string, object> { ["monitor"] = 10 }));
        Assert.AreEqual(2, DisplayParameters.ReadMonitor(new Dictionary<string, object> { ["monitor"] = "2" }));
    }

    [TestMethod]
    public void DisplayParameters_ReadWindow_TrimsAndHandlesNullOrWhitespace()
    {
        Assert.IsNull(DisplayParameters.ReadWindow(new Dictionary<string, object>()));
        Assert.IsNull(DisplayParameters.ReadWindow(new Dictionary<string, object> { ["window"] = "   " }));
        Assert.AreEqual("Spotify", DisplayParameters.ReadWindow(new Dictionary<string, object> { ["window"] = "  Spotify  " }));
    }

    [TestMethod]
    public void DisplayParameters_ReadNumber_ParsesVariousNumericTypes()
    {
        var dict = new Dictionary<string, object>
        {
            ["int"] = 42,
            ["double"] = 3.14,
            ["str"] = "100.5",
            ["invalid"] = "not_a_number"
        };

        Assert.AreEqual(42.0, DisplayParameters.ReadNumber(dict, "int"));
        Assert.AreEqual(3.14, DisplayParameters.ReadNumber(dict, "double"));
        Assert.AreEqual(100.5, DisplayParameters.ReadNumber(dict, "str"));
        Assert.IsNull(DisplayParameters.ReadNumber(dict, "invalid"));
        Assert.IsNull(DisplayParameters.ReadNumber(dict, "missing"));
    }
}
