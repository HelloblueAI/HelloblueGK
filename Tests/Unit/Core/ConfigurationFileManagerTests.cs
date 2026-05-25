using FluentAssertions;
using FileConfigurationManager = HB_NLP_Research_Lab.Core.Configuration.ConfigurationManager;

namespace HelloblueGK.Tests.Unit.Core;

public class ConfigurationFileManagerTests : IDisposable
{
    private readonly string _configDirectory;

    public ConfigurationFileManagerTests()
    {
        _configDirectory = Path.Combine(Path.GetTempPath(), $"hellobluegk-config-{Guid.NewGuid():N}");
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithTraversalName_ShouldRejectPath()
    {
        using var manager = new FileConfigurationManager(_configDirectory);

        var action = () => manager.LoadConfigurationAsync<TestConfiguration>("../outside");

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateConfigurationAsync_ShouldCompleteWithoutDeadlock()
    {
        using var manager = new FileConfigurationManager(_configDirectory);

        var updateTask = manager.UpdateConfigurationAsync<TestConfiguration>(
            "engine-control",
            config => config.Name = "updated");

        var completedTask = await Task.WhenAny(updateTask, Task.Delay(TimeSpan.FromSeconds(2)));

        completedTask.Should().Be(updateTask);
        await updateTask;
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDirectory))
        {
            Directory.Delete(_configDirectory, recursive: true);
        }
    }

    private sealed class TestConfiguration
    {
        public string Name { get; set; } = "default";
    }
}
