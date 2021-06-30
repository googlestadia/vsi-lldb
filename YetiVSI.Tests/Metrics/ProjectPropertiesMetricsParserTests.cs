using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using YetiVSI.Metrics;
using YetiVSI.ProjectSystem.Abstractions;
using YetiVSI.Shared.Metrics;
using DeployExecutableOnLaunch =
    YetiVSI.Shared.Metrics.VSIProjectProperties.Types.Debugging.Types.DeployExecutableOnLaunch;
using SurfaceEnforcement =
    YetiVSI.Shared.Metrics.VSIProjectProperties.Types.Debugging.Types.SurfaceEnforcement;
using VulkanDriverVariant =
    YetiVSI.Shared.Metrics.VSIProjectProperties.Types.Debugging.Types.VulkanDriverVariant;
using StadiaEndpoint =
    YetiVSI.Shared.Metrics.VSIProjectProperties.Types.Debugging.Types.StadiaEndpoint;
using BoolValue = YetiVSI.Shared.Metrics.VSIProjectProperties.Types.BoolValue;
using ValueType = YetiVSI.Shared.Metrics.VSIProjectProperties.Types.BoolValue.Types.Value;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class ProjectPropertiesMetricsParserTests
    {
        IAsyncProject _project;

        ProjectPropertiesMetricsParser _target;

        [SetUp]
        public void SetUp()
        {
            _project = Substitute.For<IAsyncProject>();
            _target = new ProjectPropertiesMetricsParser();
        }

        [TestCase("", DeployExecutableOnLaunch.DeployDefault, TestName = "DefaultEmpty")]
        [TestCase("  ", DeployExecutableOnLaunch.DeployDefault, TestName = "DefaultWhitespace")]
        [TestCase("false", DeployExecutableOnLaunch.DeployNo, TestName = "DeployNo")]
        [TestCase("delta", DeployExecutableOnLaunch.YesBinaryDiff, TestName = "YesBinaryDiff")]
        [TestCase("always", DeployExecutableOnLaunch.YesAlways, TestName = "YesAlways")]
        [TestCase("random val", DeployExecutableOnLaunch.DeployOther, TestName = "YesAlways")]
        public async Task DebuggingDeployExecutableOnLaunchAsync(
            string input, DeployExecutableOnLaunch? output)
        {
            SetupProject(deployExecutableOnLaunch: input);
            VSIProjectProperties result = await _target.GetStadiaProjectPropertiesAsync(_project);
            Assert.That(result.Debugging.DeployExecutableOnLaunch, Is.EqualTo(output));
            Assert.That(result.Debugging.SurfaceEnforcementMode,
                        Is.EqualTo(SurfaceEnforcement.SurfaceDefault));
            Assert.That(result.Debugging.LaunchWithRenderDoc, Is.EqualTo(DefaultBoolValue));
            Assert.That(result.Debugging.LaunchWithRgp, Is.EqualTo(DefaultBoolValue));
            Assert.That(result.Debugging.VulkanDriverVariant,
                        Is.EqualTo(VulkanDriverVariant.VulkanDefault));
            Assert.That(result.Debugging.StadiaEndpoint,
                        Is.EqualTo(StadiaEndpoint.EndpointDefault));
        }

        [TestCase("", SurfaceEnforcement.SurfaceDefault, TestName = "DefaultEmpty")]
        [TestCase("  ", SurfaceEnforcement.SurfaceDefault, TestName = "DefaultWhitespace")]
        [TestCase("block", SurfaceEnforcement.SurfaceBlock, TestName = "SurfaceBlock")]
        [TestCase("off", SurfaceEnforcement.SurfaceOff, TestName = "SurfaceOff")]
        [TestCase("warn", SurfaceEnforcement.SurfaceWarn, TestName = "SurfaceWarn")]
        [TestCase("random val", SurfaceEnforcement.SurfaceOther, TestName = "SurfaceOther")]
        public async Task DebuggingSurfaceEnforcementModeAsync(
            string input, SurfaceEnforcement? output)
        {
            SetupProject(surfaceEnforcementMode: input);
            VSIProjectProperties result = await _target.GetStadiaProjectPropertiesAsync(_project);
            Assert.That(result.Debugging.DeployExecutableOnLaunch,
                        Is.EqualTo(DeployExecutableOnLaunch.DeployDefault));
            Assert.That(result.Debugging.SurfaceEnforcementMode, Is.EqualTo(output));
            Assert.That(result.Debugging.LaunchWithRenderDoc, Is.EqualTo(DefaultBoolValue));
            Assert.That(result.Debugging.LaunchWithRgp, Is.EqualTo(DefaultBoolValue));
            Assert.That(result.Debugging.VulkanDriverVariant,
                        Is.EqualTo(VulkanDriverVariant.VulkanDefault));
            Assert.That(result.Debugging.StadiaEndpoint,
                        Is.EqualTo(StadiaEndpoint.EndpointDefault));
        }

        [TestCase("", ValueType.BoolNo, true, TestName = "DefaultEmpty")]
        [TestCase("  ", ValueType.BoolNo, true, TestName = "DefaultWhitespace")]
        [TestCase("false", ValueType.BoolNo, false, TestName = "BoolNo")]
        [TestCase("true", ValueType.BoolYes, false, TestName = "BoolYes")]
        [TestCase("rand val", ValueType.BoolOther, false, TestName = "BoolOther")]
        public async Task DebuggingLaunchWithRenderDocAsync(string input, ValueType? output,
                                                            bool? isDefault)
        {
            SetupProject(launchWithRenderDoc: input);
            VSIProjectProperties result = await _target.GetStadiaProjectPropertiesAsync(_project);
            Assert.That(result.Debugging.DeployExecutableOnLaunch,
                        Is.EqualTo(DeployExecutableOnLaunch.DeployDefault));
            Assert.That(result.Debugging.SurfaceEnforcementMode,
                        Is.EqualTo(SurfaceEnforcement.SurfaceDefault));
            Assert.That(result.Debugging.LaunchWithRenderDoc,
                        Is.EqualTo(new BoolValue { IsDefault = isDefault, Value = output }));
            Assert.That(result.Debugging.LaunchWithRgp, Is.EqualTo(DefaultBoolValue));
            Assert.That(result.Debugging.VulkanDriverVariant,
                        Is.EqualTo(VulkanDriverVariant.VulkanDefault));
            Assert.That(result.Debugging.StadiaEndpoint,
                        Is.EqualTo(StadiaEndpoint.EndpointDefault));
        }

        [TestCase("", ValueType.BoolNo, true, TestName = "DefaultEmpty")]
        [TestCase("  ", ValueType.BoolNo, true, TestName = "DefaultWhitespace")]
        [TestCase("false", ValueType.BoolNo, false, TestName = "BoolNo")]
        [TestCase("true", ValueType.BoolYes, false, TestName = "BoolYes")]
        [TestCase("rand val", ValueType.BoolOther, false, TestName = "BoolOther")]
        public async Task DebuggingLaunchWithRgpAsync(string input, ValueType? output,
                                                      bool? isDefault)
        {
            SetupProject(launchWithRgp: input);
            VSIProjectProperties result = await _target.GetStadiaProjectPropertiesAsync(_project);
            Assert.That(result.Debugging.DeployExecutableOnLaunch,
                        Is.EqualTo(DeployExecutableOnLaunch.DeployDefault));
            Assert.That(result.Debugging.SurfaceEnforcementMode,
                        Is.EqualTo(SurfaceEnforcement.SurfaceDefault));
            Assert.That(result.Debugging.LaunchWithRenderDoc, Is.EqualTo(DefaultBoolValue));
            Assert.That(result.Debugging.LaunchWithRgp,
                        Is.EqualTo(new BoolValue { IsDefault = isDefault, Value = output }));
            Assert.That(result.Debugging.VulkanDriverVariant,
                        Is.EqualTo(VulkanDriverVariant.VulkanDefault));
            Assert.That(result.Debugging.StadiaEndpoint,
                        Is.EqualTo(StadiaEndpoint.EndpointDefault));
        }

        [TestCase("", VulkanDriverVariant.VulkanDefault, TestName = "DefaultEmpty")]
        [TestCase("  ", VulkanDriverVariant.VulkanDefault, TestName = "DefaultWhitespace")]
        [TestCase("opt", VulkanDriverVariant.Optimized, TestName = "Optimized")]
        [TestCase("optprintasserts", VulkanDriverVariant.PrintingAssertions,
                  TestName = "PrintingAssertions")]
        [TestCase("dbgtrapasserts", VulkanDriverVariant.TrappingAssertions,
                  TestName = "TrappingAssertions")]
        [TestCase("random val", VulkanDriverVariant.VulkanOther, TestName = "VulkanOther")]
        public async Task DebuggingVulkanDriverVariantAsync(
            string input, VulkanDriverVariant? output)
        {
            SetupProject(vulkanDriverVariant: input);
            VSIProjectProperties result = await _target.GetStadiaProjectPropertiesAsync(_project);
            Assert.That(result.Debugging.DeployExecutableOnLaunch,
                        Is.EqualTo(DeployExecutableOnLaunch.DeployDefault));
            Assert.That(result.Debugging.SurfaceEnforcementMode,
                        Is.EqualTo(SurfaceEnforcement.SurfaceDefault));
            Assert.That(result.Debugging.LaunchWithRenderDoc, Is.EqualTo(DefaultBoolValue));
            Assert.That(result.Debugging.LaunchWithRgp, Is.EqualTo(DefaultBoolValue));
            Assert.That(result.Debugging.VulkanDriverVariant, Is.EqualTo(output));
            Assert.That(result.Debugging.StadiaEndpoint,
                        Is.EqualTo(StadiaEndpoint.EndpointDefault));
        }

        [TestCase("", StadiaEndpoint.EndpointDefault, TestName = "DefaultEmpty")]
        [TestCase("  ", StadiaEndpoint.EndpointDefault, TestName = "DefaultWhitespace")]
        [TestCase("anyEndpoint", StadiaEndpoint.AnyEndpoint, TestName = "AnyEndpoint")]
        [TestCase("playerEndpoint", StadiaEndpoint.PlayerWebEndpoint,
                  TestName = "PlayerWebEndpoint")]
        [TestCase("testClient", StadiaEndpoint.TestClient, TestName = "TestClient")]
        [TestCase("random val", StadiaEndpoint.EndpointOther, TestName = "EndpointOther")]
        public async Task DebuggingStadiaEndpointAsync(string input, StadiaEndpoint? output)
        {
            SetupProject(stadiaEndpoint: input);
            VSIProjectProperties result = await _target.GetStadiaProjectPropertiesAsync(_project);
            Assert.That(result.Debugging.DeployExecutableOnLaunch,
                        Is.EqualTo(DeployExecutableOnLaunch.DeployDefault));
            Assert.That(result.Debugging.SurfaceEnforcementMode,
                        Is.EqualTo(SurfaceEnforcement.SurfaceDefault));
            Assert.That(result.Debugging.LaunchWithRenderDoc, Is.EqualTo(DefaultBoolValue));
            Assert.That(result.Debugging.LaunchWithRgp, Is.EqualTo(DefaultBoolValue));
            Assert.That(result.Debugging.VulkanDriverVariant,
                        Is.EqualTo(VulkanDriverVariant.VulkanDefault));
            Assert.That(result.Debugging.StadiaEndpoint, Is.EqualTo(output));
        }

        void SetupProject(string deployExecutableOnLaunch = "", string surfaceEnforcementMode = "",
                          string launchWithRenderDoc = "", string launchWithRgp = "",
                          string vulkanDriverVariant = "", string stadiaEndpoint = "")
        {
            _project.GetDeployExecutableOnLaunchRawAsync()
                .Returns(Task.FromResult(deployExecutableOnLaunch));
            _project.GetSurfaceEnforcementModeRawAsync()
                .Returns(Task.FromResult(surfaceEnforcementMode));
            _project.GetLaunchWithRenderDocRawAsync().Returns(Task.FromResult(launchWithRenderDoc));
            _project.GetLaunchWithRgpRawAsync().Returns(Task.FromResult(launchWithRgp));
            _project.GetVulkanDriverVariantRawAsync().Returns(Task.FromResult(vulkanDriverVariant));
            _project.GetStadiaEndpointRawAsync().Returns(Task.FromResult(stadiaEndpoint));
        }

        BoolValue DefaultBoolValue => new BoolValue { IsDefault = true, Value = ValueType.BoolNo };
    }
}
