using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GgpGrpc.Models;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSI.Shared.Metrics;

namespace YetiVSI.Metrics
{
    public interface IProjectPropertiesMetricsParser
    {
        Task<VSIProjectProperties> GetStadiaProjectPropertiesAsync(IAsyncProject project);
    }

    public class ProjectPropertiesMetricsParser : IProjectPropertiesMetricsParser
    {
        public async Task<VSIProjectProperties> GetStadiaProjectPropertiesAsync(
            IAsyncProject project) =>
            new VSIProjectProperties
            {
                Debugging = new VSIProjectProperties.Types.Debugging
                {
                    DeployExecutableOnLaunch = await GetDeployExecutableOnLaunchAsync(project),
                    SurfaceEnforcementMode = await GetSurfaceEnforcementModeAsync(project),
                    LaunchWithRenderDoc = await GetLaunchWithRenderDocAsync(project),
                    LaunchWithRgp = await GetLaunchWithRgpAsync(project),
                    VulkanDriverVariant = await GetVulkanDriverVariantAsync(project),
                    StadiaEndpoint = await  GetStadiaEndpointAsync(project)
                }
            };

        async Task<VSIProjectProperties.Types.BoolValue> GetLaunchWithRenderDocAsync(
            IAsyncProject project)
        {
            try
            {
                return GetBoolValue(await project.GetLaunchWithRenderDocRawAsync());
            }
            catch (Exception e)
            {
                Trace.TraceError("Could not get 'Launch with RenderDoc' value.\r\n" +
                                 $"Message: {e.Message}.\r\nStack trace: {e.StackTrace}");
                return null;
            }
        }

        async Task<VSIProjectProperties.Types.BoolValue> GetLaunchWithRgpAsync(
            IAsyncProject project)
        {
            try
            {
                return GetBoolValue(await project.GetLaunchWithRgpRawAsync());
            }
            catch (Exception e)
            {
                Trace.TraceError("Could not get 'Launch with RGP' value.\r\n" +
                                 $"Message: {e.Message}.\r\nStack trace: {e.StackTrace}");
                return null;
            }
        }

        VSIProjectProperties.Types.BoolValue GetBoolValue(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return new VSIProjectProperties.Types.BoolValue
                {
                    IsDefault = true,
                    Value = VSIProjectProperties.Types.BoolValue.Types.Value.BoolNo
                };
            }

            if (!bool.TryParse(rawValue, out bool projectPropertyValue))
            {
                return new VSIProjectProperties.Types.BoolValue
                {
                    IsDefault = false,
                    Value = VSIProjectProperties.Types.BoolValue.Types.Value.BoolOther
                };
            }

            return new VSIProjectProperties.Types.BoolValue
            {
                IsDefault = false,
                Value = projectPropertyValue
                    ? VSIProjectProperties.Types.BoolValue.Types.Value.BoolYes
                    : VSIProjectProperties.Types.BoolValue.Types.Value.BoolNo
            };
        }

        async Task<VSIProjectProperties.Types.Debugging.Types.DeployExecutableOnLaunch?>
            GetDeployExecutableOnLaunchAsync(IAsyncProject project)
        {
            try
            {
                string rawValue = await project.GetDeployExecutableOnLaunchRawAsync();
                if (!Enum.TryParse(rawValue, true, out DeployOnLaunchSetting deployOnLaunch))
                {
                    return string.IsNullOrWhiteSpace(rawValue)
                        ? VSIProjectProperties.Types.Debugging.Types.DeployExecutableOnLaunch
                            .DeployDefault
                        : VSIProjectProperties.Types.Debugging.Types.DeployExecutableOnLaunch
                            .DeployOther;
                }

                switch (deployOnLaunch)
                {
                    case DeployOnLaunchSetting.ALWAYS:
                        return VSIProjectProperties.Types.Debugging.Types.DeployExecutableOnLaunch
                            .YesAlways;
                    case DeployOnLaunchSetting.DELTA:
                        return VSIProjectProperties.Types.Debugging.Types.DeployExecutableOnLaunch
                            .YesBinaryDiff;
                    case DeployOnLaunchSetting.FALSE:
                        return VSIProjectProperties.Types.Debugging.Types.DeployExecutableOnLaunch
                            .DeployNo;
                    default:
                        return VSIProjectProperties.Types.Debugging.Types.DeployExecutableOnLaunch
                            .DeployOther;
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Could not get 'Deploy executable on launch' value.\r\n" +
                                 $"Message: {e.Message}.\r\nStack trace: {e.StackTrace}");
                return null;
            }
        }

        async Task<VSIProjectProperties.Types.Debugging.Types.SurfaceEnforcement?>
            GetSurfaceEnforcementModeAsync(IAsyncProject project)
        {
            try
            {
                string rawValue = await project.GetSurfaceEnforcementModeRawAsync();
                if (!Enum.TryParse(rawValue, true,
                                   out SurfaceEnforcementSetting surfaceEnforcement))
                {
                    return string.IsNullOrWhiteSpace(rawValue)
                        ? VSIProjectProperties.Types.Debugging.Types.SurfaceEnforcement
                            .SurfaceDefault
                        : VSIProjectProperties.Types.Debugging.Types.SurfaceEnforcement
                            .SurfaceOther;
                }

                switch (surfaceEnforcement)
                {
                    case SurfaceEnforcementSetting.Block:
                        return VSIProjectProperties.Types.Debugging.Types.SurfaceEnforcement
                            .SurfaceBlock;
                    case SurfaceEnforcementSetting.Off:
                        return VSIProjectProperties.Types.Debugging.Types.SurfaceEnforcement
                            .SurfaceOff;
                    case SurfaceEnforcementSetting.Warn:
                        return VSIProjectProperties.Types.Debugging.Types.SurfaceEnforcement
                            .SurfaceWarn;
                    default:
                        return VSIProjectProperties.Types.Debugging.Types.SurfaceEnforcement
                            .SurfaceOther;
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Could not get 'Stadia instance surface enforcement' value.\r\n" +
                                 $"Message: {e.Message}.\r\nStack trace: {e.StackTrace}");
                return null;
            }
        }

        async Task<VSIProjectProperties.Types.Debugging.Types.VulkanDriverVariant?>
            GetVulkanDriverVariantAsync(IAsyncProject project)
        {
            try
            {
                string rawValue = await project.GetVulkanDriverVariantRawAsync();
                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    return VSIProjectProperties.Types.Debugging.Types.VulkanDriverVariant
                        .VulkanDefault;
                }

                if (rawValue == "opt")
                {
                    return VSIProjectProperties.Types.Debugging.Types.VulkanDriverVariant.Optimized;
                }

                if (rawValue == "optprintasserts")
                {
                    return VSIProjectProperties.Types.Debugging.Types.VulkanDriverVariant
                        .PrintingAssertions;
                }

                if (rawValue == "dbgtrapasserts")
                {
                    return VSIProjectProperties.Types.Debugging.Types.VulkanDriverVariant
                        .TrappingAssertions;
                }

                return VSIProjectProperties.Types.Debugging.Types.VulkanDriverVariant.VulkanOther;
            }
            catch (Exception e)
            {
                Trace.TraceError("Could not get 'Vulkan driver variant' value.\r\n" +
                                 $"Message: {e.Message}.\r\nStack trace: {e.StackTrace}");
                return null;
            }
        }

        async Task<VSIProjectProperties.Types.Debugging.Types.StadiaEndpoint?>
            GetStadiaEndpointAsync(IAsyncProject project)
        {
            try
            {
                string rawValue = await project.GetStadiaEndpointRawAsync();
                if (!Enum.TryParse(rawValue, true, out StadiaEndpoint endpoint))
                {
                    return string.IsNullOrWhiteSpace(rawValue)
                        ? VSIProjectProperties.Types.Debugging.Types.StadiaEndpoint.EndpointDefault
                        : VSIProjectProperties.Types.Debugging.Types.StadiaEndpoint.EndpointOther;
                }

                switch (endpoint)
                {
                    case StadiaEndpoint.AnyEndpoint:
                        return VSIProjectProperties.Types.Debugging.Types.StadiaEndpoint
                            .AnyEndpoint;
                    case StadiaEndpoint.PlayerEndpoint:
                        return VSIProjectProperties.Types.Debugging.Types.StadiaEndpoint
                            .PlayerWebEndpoint;
                    case StadiaEndpoint.TestClient:
                        return VSIProjectProperties.Types.Debugging.Types.StadiaEndpoint.TestClient;
                    default:
                        return VSIProjectProperties.Types.Debugging.Types.StadiaEndpoint
                            .EndpointOther;
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Could not get 'Stadia Endpoint' value.\r\n" +
                                 $"Message: {e.Message}.\r\nStack trace: {e.StackTrace}");
                return null;
            }
        }
    }
}
