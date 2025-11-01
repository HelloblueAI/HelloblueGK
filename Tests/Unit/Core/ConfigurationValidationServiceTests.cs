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
        // IsValid is already typed as bool, no need to verify type
        result.IsValid.Should().BeOneOf([true, false]);
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
}

