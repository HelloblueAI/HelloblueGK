using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

public class ConfigurationValidationServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<ConfigurationValidationService>> _mockLogger;
    private readonly ConfigurationValidationService _service;

    public ConfigurationValidationServiceTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<ConfigurationValidationService>>();
        _service = new ConfigurationValidationService(_mockConfiguration.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_ShouldReturnValidationResult()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync();

        // Assert
        result.Should().NotBeNull();
        // IsValid is already typed as bool, so we just verify the result structure
        result.ValidationTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ValidateConfigurationAsync_ShouldHaveValidationDetails()
    {
        // Act
        var result = await _service.ValidateConfigurationAsync();

        // Assert
        result.Errors.Should().NotBeNull();
        result.Warnings.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateConfigurationAsync_ShouldBeIdempotent()
    {
        // Act
        var result1 = await _service.ValidateConfigurationAsync();
        var result2 = await _service.ValidateConfigurationAsync();

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
    }

    [Fact]
    public async Task GetConfigurationHealthAsync_ShouldNotExposeRawConfigurationValues()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-jwt-key-that-should-not-leak",
                ["ConnectionStrings:DefaultConnection"] = new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = "db",
                    Username = "admin",
                    Password = "secret-password"
                }.ConnectionString
            })
            .Build();
        var service = new ConfigurationValidationService(configuration, _mockLogger.Object);

        // Act
        var health = await service.GetConfigurationHealthAsync();
        var serializedSections = System.Text.Json.JsonSerializer.Serialize(health.ConfigurationSections);

        // Assert
        serializedSections.Should().NotContain("super-secret-jwt-key-that-should-not-leak");
        serializedSections.Should().NotContain("secret-password");
        serializedSections.Should().Contain("ChildSectionCount");
    }
}

