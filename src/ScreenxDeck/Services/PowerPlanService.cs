using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;

namespace ScreenxDeck.Services;

public sealed record PowerPlan(string Guid, string Name, bool IsActive);

public sealed class PowerPlanService
{
    private static readonly Regex PowerPlanLineRegex = new(
        @"(?<guid>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s+\((?<name>.*)\)\s*(?<active>\*)?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger _logger;

    public PowerPlanService(ILogger logger)
    {
        _logger = logger.ForContext<PowerPlanService>();
    }

    public IReadOnlyList<PowerPlan> GetAvailablePowerPlans()
    {
        string? output = RunPowerCfg("/list");
        if (output is null)
        {
            return [];
        }

        IReadOnlyList<PowerPlan> plans = ParsePowerPlans(output);
        if (plans.Count == 0)
        {
            _logger.Warning("powercfg.exe did not return any parseable power plans");
        }

        return plans;
    }

    public string? GetActivePowerPlanName()
    {
        string? output = RunPowerCfg("/getactivescheme");
        if (output is null)
        {
            return null;
        }

        PowerPlan? activePlan = ParsePowerPlans(output).FirstOrDefault();
        if (activePlan is null)
        {
            _logger.Warning("powercfg.exe did not return a parseable active power plan");
        }

        return activePlan?.Name;
    }

    public static IReadOnlyList<PowerPlan> ParsePowerPlans(string output)
    {
        var plans = new List<PowerPlan>();
        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = PowerPlanLineRegex.Match(line);
            if (!match.Success || !Guid.TryParse(match.Groups["guid"].Value, out Guid planGuid))
            {
                continue;
            }

            string name = match.Groups["name"].Value.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            plans.Add(new PowerPlan(planGuid.ToString("D"), name, match.Groups["active"].Success));
        }

        return plans;
    }

    public bool SetActivePowerPlan(string planGuid)
    {
        if (!Guid.TryParse(planGuid, out Guid parsedGuid))
        {
            _logger.Warning("Cannot activate power plan because its GUID is invalid");
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("/setactive");
        startInfo.ArgumentList.Add(parsedGuid.ToString("D"));

        bool success = RunPowerCfg(startInfo, parsedGuid) is not null;
        if (success)
        {
            _logger.Information("Activated Windows power plan {PlanGuid}", parsedGuid);
        }

        return success;
    }

    private string? RunPowerCfg(string argument)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(argument);
        return RunPowerCfg(startInfo, argument);
    }

    private string? RunPowerCfg(ProcessStartInfo startInfo, object operation)
    {
        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.Error("Could not start powercfg.exe for operation {Operation}", operation);
                return null;
            }

            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                _logger.Warning("powercfg.exe failed for operation {Operation} with exit code {ExitCode}: {Error}", operation, process.ExitCode, standardError.GetAwaiter().GetResult().Trim());
                return null;
            }

            return standardOutput.GetAwaiter().GetResult();
        }
        catch (Win32Exception ex)
        {
            _logger.Error(ex, "Could not start powercfg.exe for operation {Operation}", operation);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.Error(ex, "Could not complete powercfg.exe request for operation {Operation}", operation);
            return null;
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "Could not read powercfg.exe output for operation {Operation}", operation);
            return null;
        }
    }
}
