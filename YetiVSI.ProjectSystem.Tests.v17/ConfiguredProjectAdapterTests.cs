using Microsoft.VisualStudio.ProjectSystem;
using NSubstitute;

namespace YetiVSI.ProjectSystem.Tests
{
    partial class ConfiguredProjectAdapterTests
    {
        ConfiguredProject CreateConfiguredProjectBase()
        {
            var services = Substitute.For<ConfiguredProjectServices>();
            var unconfiguredProject = Substitute.For<UnconfiguredProject>();
            var project = Substitute.For<ConfiguredProject>();

            project.Services.Returns(services);
            project.UnconfiguredProject.Returns(unconfiguredProject);

            return project;
        }
    }
}
