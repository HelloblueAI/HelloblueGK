using System.Diagnostics;
using HB_NLP_Research_Lab.Core.Control;
using HB_NLP_Research_Lab.Core.Hardware;

namespace HelloblueGK.Tests.Unit.Core;

public class StartupSequenceControllerTests
{
    private const double StandardAtmospherePascals = 101_325;

    [Fact]
    public void AmbientBand_AcceptsSeaLevelPressure_AndRejectsNonFinite()
    {
        StartupSequenceController.IsAmbientPressure(StandardAtmospherePascals).Should().BeTrue();
        StartupSequenceController.IsAmbientPressure(0).Should().BeTrue();
        StartupSequenceController.IsAmbientPressure(StartupSequenceController.MaxAmbientPressurePascals).Should().BeTrue();

        // The previous ceiling was 100 kPa, which is below standard atmosphere.
        StartupSequenceController.IsAmbientPressure(100_000).Should().BeTrue();
        StartupSequenceController.IsAmbientPressure(StartupSequenceController.MaxAmbientPressurePascals + 1).Should().BeFalse();
        StartupSequenceController.IsAmbientPressure(-1).Should().BeFalse();
        StartupSequenceController.IsAmbientPressure(double.NaN).Should().BeFalse();
        StartupSequenceController.IsAmbientPressure(double.PositiveInfinity).Should().BeFalse();
    }

    [Fact]
    public void AmbientTemperature_RejectsNonFinite()
    {
        StartupSequenceController.IsAmbientTemperature(293.15).Should().BeTrue();
        StartupSequenceController.IsAmbientTemperature(199).Should().BeFalse();
        StartupSequenceController.IsAmbientTemperature(401).Should().BeFalse();
        StartupSequenceController.IsAmbientTemperature(double.NaN).Should().BeFalse();
    }

    [Fact]
    public void Combustion_RequiresAPressureRiseOutOfTheAmbientBand()
    {
        StartupSequenceController.IsCombustionEstablished(StandardAtmospherePascals, 1500).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(50_000, 1500).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(120_000, 999).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(120_000, double.NaN).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(double.NaN, 1500).Should().BeFalse();

        StartupSequenceController.IsCombustionEstablished(120_000, 1000).Should().BeTrue();
        StartupSequenceController.IsCombustionEstablished(
            StartupSequenceController.MaximumChamberPressurePascals,
            StartupSequenceController.MaximumChamberTemperatureKelvin).Should().BeTrue();
    }

    [Fact]
    public void Combustion_RejectsAPeggedChamberSensor()
    {
        StartupSequenceController.IsCombustionEstablished(
            StartupSequenceController.MaximumChamberPressurePascals + 1,
            1_500).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(double.MaxValue, 1_500).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(
            200_000,
            StartupSequenceController.MaximumChamberTemperatureKelvin + 1).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(200_000, double.MaxValue).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(200_000, double.PositiveInfinity).Should().BeFalse();
    }

    [Fact]
    public void OperatingPressure_IsAboveTheAmbientBand()
    {
        StartupSequenceController.IsOperatingPressure(100_000).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(StartupSequenceController.MinOperatingPressurePascals - 1).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(StartupSequenceController.MinOperatingPressurePascals).Should().BeTrue();
        StartupSequenceController.IsOperatingPressure(StartupSequenceController.MaximumChamberPressurePascals)
            .Should().BeTrue();
        StartupSequenceController.IsOperatingPressure(StartupSequenceController.MaximumChamberPressurePascals + 1)
            .Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(double.MaxValue).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(double.NaN).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(double.PositiveInfinity).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(-1).Should().BeFalse();
    }

    [Fact]
    public void PropellantFlow_RejectsAMissingReading()
    {
        StartupSequenceController.HasMinimumPropellantFlow(0.01).Should().BeTrue();
        StartupSequenceController.HasMinimumPropellantFlow(0.009).Should().BeFalse();
        StartupSequenceController.HasMinimumPropellantFlow(double.NaN).Should().BeFalse();
        StartupSequenceController.HasMinimumPropellantFlow(double.NegativeInfinity).Should().BeFalse();
        StartupSequenceController.HasMinimumPropellantFlow(double.PositiveInfinity).Should().BeFalse();
    }

    [Fact]
    public void PropellantFlow_RejectsAPeggedSensor()
    {
        StartupSequenceController.IsPropellantFlowInBand(0.01, StartupSequenceController.MaximumFuelFlowKgPerSecond)
            .Should().BeTrue();
        StartupSequenceController.IsPropellantFlowInBand(
            StartupSequenceController.MaximumFuelFlowKgPerSecond,
            StartupSequenceController.MaximumFuelFlowKgPerSecond).Should().BeTrue();
        StartupSequenceController.IsPropellantFlowInBand(
            StartupSequenceController.MaximumOxidizerFlowKgPerSecond,
            StartupSequenceController.MaximumOxidizerFlowKgPerSecond).Should().BeTrue();

        StartupSequenceController.IsPropellantFlowInBand(
            StartupSequenceController.MaximumFuelFlowKgPerSecond + 1,
            StartupSequenceController.MaximumFuelFlowKgPerSecond).Should().BeFalse();
        StartupSequenceController.IsPropellantFlowInBand(
            double.MaxValue,
            StartupSequenceController.MaximumFuelFlowKgPerSecond).Should().BeFalse();
        StartupSequenceController.IsPropellantFlowInBand(double.NaN, StartupSequenceController.MaximumFuelFlowKgPerSecond)
            .Should().BeFalse();
        StartupSequenceController.IsPropellantFlowInBand(1, double.NaN).Should().BeFalse();
        StartupSequenceController.IsPropellantFlowInBand(1, double.PositiveInfinity).Should().BeFalse();
    }

    [Theory]
    [InlineData(ActuatorStatus.Ready, true)]
    [InlineData(ActuatorStatus.Fault, false)]
    [InlineData(ActuatorStatus.Error, false)]
    [InlineData(ActuatorStatus.Disabled, false)]
    [InlineData(ActuatorStatus.Unknown, false)]
    [InlineData(ActuatorStatus.Initializing, false)]
    [InlineData(ActuatorStatus.Moving, false)]
    [InlineData(ActuatorStatus.AtPosition, false)]
    public void ActuatorReadyForStartup_RequiresReady(ActuatorStatus status, bool ready)
    {
        StartupSequenceController.IsActuatorReadyForStartup(status).Should().Be(ready);
    }

    [Theory]
    [InlineData(SensorStatus.Ready, true)]
    [InlineData(SensorStatus.Reading, false)]
    [InlineData(SensorStatus.Unknown, false)]
    [InlineData(SensorStatus.Initializing, false)]
    [InlineData(SensorStatus.Error, false)]
    [InlineData(SensorStatus.Disconnected, false)]
    [InlineData(SensorStatus.Calibrating, false)]
    public void SensorReadyForStartup_RequiresReady(SensorStatus status, bool ready)
    {
        StartupSequenceController.IsSensorReadyForStartup(status).Should().Be(ready);
    }

    [Theory]
    [InlineData("fuel", ActuatorStatus.Disabled)]
    [InlineData("oxidizer", ActuatorStatus.Fault)]
    [InlineData("igniter", ActuatorStatus.Error)]
    [InlineData("oxidizer", ActuatorStatus.Unknown)]
    [InlineData("igniter", ActuatorStatus.AtPosition)]
    [InlineData("fuel", ActuatorStatus.Moving)]
    [InlineData("oxidizer", ActuatorStatus.Initializing)]
    public async Task PreStartup_ActuatorNotReady_FailsClosedBeforePurge(string actuator, ActuatorStatus status)
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        ActuatorFor(actuator, fuel, oxidizer, igniter).Status = status;

        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = StandardAtmospherePascals },
            temperature: new StubSensor { Value = 293.15 });

        controller.MoveToState(StartupState.PreStartupChecks);
        await controller.ExecuteOnceAsync();
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Purge);
        controller.CurrentState.Should().NotBe(StartupState.FuelFlowInitiation);
        fuel.Positions.Should().NotContain(0.1);
        oxidizer.Positions.Should().NotContain(0.1);
        igniter.Positions.Should().NotContain(1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task PreStartup_ReadyActuators_AdvancesToPurgeWithoutOpeningAValve()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = StandardAtmospherePascals },
            temperature: new StubSensor { Value = 293.15 });

        controller.MoveToState(StartupState.PreStartupChecks);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Purge);
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Theory]
    [InlineData("pressure", SensorStatus.Disconnected)]
    [InlineData("pressure", SensorStatus.Error)]
    [InlineData("pressure", SensorStatus.Unknown)]
    [InlineData("pressure", SensorStatus.Initializing)]
    [InlineData("pressure", SensorStatus.Calibrating)]
    [InlineData("pressure", SensorStatus.Reading)]
    [InlineData("temperature", SensorStatus.Disconnected)]
    [InlineData("temperature", SensorStatus.Error)]
    [InlineData("temperature", SensorStatus.Reading)]
    public async Task PreStartup_SensorNotReady_DoesNotTreatCachedAmbientAsAPass(
        string sensor,
        SensorStatus status)
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var pressure = new StubSensor { Name = "chamber pressure", Value = StandardAtmospherePascals };
        var temperature = new StubSensor { Name = "chamber temperature", Value = 293.15 };
        SensorFor(sensor, pressure, temperature).Status = status;

        var controller = CreateController(fuel, oxidizer, igniter, pressure, temperature);

        controller.MoveToState(StartupState.PreStartupChecks);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Purge);
        fuel.Positions.Should().NotContain(0.1);
        oxidizer.Positions.Should().NotContain(0.1);
        igniter.Positions.Should().NotContain(1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task FuelFlow_DisconnectedSensor_DoesNotOpenFuel()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Name = "fuel flow", Value = 1, Status = SensorStatus.Disconnected });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        fuel.Positions.Should().NotContain(0.1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task FuelFlow_SensorDisconnectsAfterOpen_DoesNotTreatCachedFlowAsEstablished()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor
            {
                Name = "fuel flow",
                Value = 1,
                StatusOnRead = SensorStatus.Disconnected
            });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        fuel.Positions.Should().Contain(0.1);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task OxidizerFlow_DisconnectedSensor_DoesNotOpenOxidizer()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            oxidizerFlow: new StubSensor
            {
                Name = "oxidizer flow",
                Value = 1,
                Status = SensorStatus.Disconnected
            });

        controller.MoveToState(StartupState.OxidizerFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Ignition);
        oxidizer.Positions.Should().NotContain(0.1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task OxidizerFlow_SensorDisconnectsAfterOpen_DoesNotTreatCachedFlowAsEstablished()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            oxidizerFlow: new StubSensor
            {
                Name = "oxidizer flow",
                Value = 1,
                StatusOnRead = SensorStatus.Error
            });

        controller.MoveToState(StartupState.OxidizerFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Ignition);
        oxidizer.Positions.Should().Contain(0.1);
        oxidizer.LastPosition.Should().Be(0);
        fuel.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Theory]
    [InlineData("pressure", SensorStatus.Disconnected)]
    [InlineData("temperature", SensorStatus.Disconnected)]
    [InlineData("pressure", SensorStatus.Reading)]
    [InlineData("temperature", SensorStatus.Calibrating)]
    public async Task Combustion_SensorNotReady_DoesNotTreatCachedCombustionAsVerified(
        string sensor,
        SensorStatus status)
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var pressure = new StubSensor { Name = "chamber pressure", Value = 200_000 };
        var temperature = new StubSensor { Name = "chamber temperature", Value = 1_500 };
        SensorFor(sensor, pressure, temperature).Status = status;

        var controller = CreateController(fuel, oxidizer, igniter, pressure, temperature);

        controller.MoveToState(StartupState.CombustionVerification);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.ThrottleUp);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Theory]
    [InlineData(SensorStatus.Disconnected, 200_000)]
    [InlineData(SensorStatus.Reading, 200_000)]
    [InlineData(SensorStatus.Disconnected, 120_000)]
    [InlineData(SensorStatus.Error, 120_000)]
    public async Task ThrottleUp_SensorNotReady_DoesNotKeepWaitingOrDeclareRunning(
        SensorStatus status,
        double pressure)
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Name = "chamber pressure",
                Value = pressure,
                Status = status
            });

        controller.MoveToState(StartupState.ThrottleUp);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Running);
        controller.CurrentState.Should().NotBe(StartupState.ThrottleUp);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task PreStartup_AbortDuringSensorRead_StaysAbortedWhenOxidizerIsFaulted()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer", Status = ActuatorStatus.Fault };
        var igniter = new RecordingActuator { Name = "igniter" };
        StartupSequenceController? controller = null;
        var pressure = new StubSensor
        {
            OnReading = () =>
            {
                controller!.AbortStartup();
                return Task.FromResult(StandardAtmospherePascals);
            }
        };
        controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure,
            temperature: new StubSensor { Value = 293.15 });

        controller.MoveToState(StartupState.PreStartupChecks);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.HasError.Should().BeFalse();
        fuel.Positions.Should().NotContain(0.1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task PreStartup_AbortDuringDisconnectedSensorRead_StaysAborted()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        StartupSequenceController? controller = null;
        var pressure = new StubSensor
        {
            Name = "chamber pressure",
            Status = SensorStatus.Disconnected,
            OnReading = () =>
            {
                controller!.AbortStartup();
                return Task.FromResult(StandardAtmospherePascals);
            }
        };
        controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure,
            temperature: new StubSensor { Value = 293.15 });

        controller.MoveToState(StartupState.PreStartupChecks);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.HasError.Should().BeFalse();
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task FuelFlow_FaultedOxidizer_DoesNotOpenFuel()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer", Status = ActuatorStatus.Fault };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        fuel.Positions.Should().NotContain(0.1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task OxidizerFlow_FaultedIgniter_DoesNotOpenOxidizer()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter", Status = ActuatorStatus.Fault };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            oxidizerFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.OxidizerFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Ignition);
        oxidizer.Positions.Should().NotContain(0.1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task Ignition_FaultedIgniter_DoesNotSpark()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter", Status = ActuatorStatus.Disabled };
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Ignition);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.CombustionVerification);
        igniter.Positions.Should().NotContain(1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task FuelFlow_ReadyActuatorsAndInBandFlow_OpensFuelAndAdvances()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.OxidizerFlowInitiation);
        fuel.Positions.Should().Contain(0.1);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_ChamberLeftAmbient_DoesNotOpenFuel()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Name = "chamber pressure", Value = 200_000 },
            temperature: new StubSensor { Name = "chamber temperature", Value = 293.15 },
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        fuel.Positions.Should().NotContain(0.1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task FuelFlow_HotChamber_DoesNotOpenFuel()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Name = "chamber pressure", Value = StandardAtmospherePascals },
            temperature: new StubSensor { Name = "chamber temperature", Value = 1_500 },
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        fuel.Positions.Should().NotContain(0.1);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_DisconnectedChamber_DoesNotTreatCachedAmbientAsSafeToOpen()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Name = "chamber pressure",
                Value = StandardAtmospherePascals,
                Status = SensorStatus.Disconnected
            },
            temperature: new StubSensor { Name = "chamber temperature", Value = 293.15 },
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        fuel.Positions.Should().NotContain(0.1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task FuelFlow_ChamberLeavesAmbientDuringTheFlowWait_DoesNotAdvance()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var pressureReads = 0;
        var pressure = new StubSensor
        {
            Name = "chamber pressure",
            Value = StandardAtmospherePascals,
            OnReading = () =>
            {
                pressureReads++;
                var sample = pressureReads == 1
                    ? StandardAtmospherePascals
                    : StartupSequenceController.MaximumChamberPressurePascals + 1;
                return Task.FromResult(sample);
            }
        };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure,
            temperature: new StubSensor { Name = "chamber temperature", Value = 293.15 },
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        fuel.Positions.Should().Contain(0.1);
        fuel.LastPosition.Should().Be(0);
        oxidizer.Positions.Should().NotContain(0.1);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_ChamberReadTimeout_DoesNotOpenFuel()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Name = "chamber pressure",
                Value = StandardAtmospherePascals,
                OnReading = () => throw new TaskCanceledException("pressure timeout")
            },
            temperature: new StubSensor { Name = "chamber temperature", Value = 293.15 },
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        fuel.Positions.Should().NotContain(0.1);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_ChamberReadFailure_StillEscapesThePass()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Name = "chamber pressure",
                OnReading = () => throw new InvalidOperationException("pressure read failed")
            },
            temperature: new StubSensor { Name = "chamber temperature", Value = 293.15 },
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        var act = () => controller.ExecuteOnceAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        controller.CurrentState.Should().Be(StartupState.FuelFlowInitiation);
        fuel.Positions.Should().NotContain(0.1);
    }

    [Fact]
    public async Task FuelFlow_AbortDuringChamberRead_StaysAbortedAndDoesNotOpenFuel()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        StartupSequenceController? controller = null;
        var pressure = new StubSensor
        {
            Name = "chamber pressure",
            OnReading = () =>
            {
                controller!.AbortStartup();
                return Task.FromResult(StandardAtmospherePascals);
            }
        };
        controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure,
            temperature: new StubSensor { Name = "chamber temperature", Value = 293.15 },
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.HasError.Should().BeFalse();
        fuel.Positions.Should().NotContain(0.1);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task OxidizerFlow_PeggedChamber_DoesNotOpenOxidizer()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Name = "chamber pressure",
                Value = StartupSequenceController.MaximumChamberPressurePascals
            },
            temperature: new StubSensor { Name = "chamber temperature", Value = 293.15 },
            oxidizerFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.OxidizerFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Ignition);
        oxidizer.Positions.Should().NotContain(0.1);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task OxidizerFlow_AmbientChamberAndInBandFlow_OpensOxidizerAndAdvances()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            oxidizerFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.OxidizerFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Ignition);
        oxidizer.Positions.Should().Contain(0.1);
        igniter.Positions.Should().NotContain(1);
        fuel.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task Ignition_ChamberAlreadyHot_DoesNotSpark()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Name = "chamber pressure", Value = StandardAtmospherePascals },
            temperature: new StubSensor { Name = "chamber temperature", Value = 1_200 });

        controller.MoveToState(StartupState.Ignition);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.CombustionVerification);
        igniter.Positions.Should().NotContain(1);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task Ignition_AmbientChamber_SparksThenAdvances()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Ignition);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.CombustionVerification);
        igniter.Positions.Should().Contain(1);
        igniter.LastPosition.Should().Be(0);
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task PreStartup_NonFinitePressure_ClosesValvesOnThisPass()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            new StubSensor { Value = double.NaN });

        controller.MoveToState(StartupState.PreStartupChecks);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.HasError.Should().BeTrue();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_ActuatorCommandTimeout_FailClosesAndDoesNotContinue()
    {
        var fuel = new RecordingActuator
        {
            OnCommanding = position =>
            {
                if (position > 0)
                    throw new TaskCanceledException("actuator timeout");

                return Task.CompletedTask;
            }
        };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        var act = () => controller.ExecuteOnceAsync();
        await act.Should().NotThrowAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        fuel.LastPosition.Should().Be(0);
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_ActuatorCommandTimeout_LeavesTheControlLoopRunning()
    {
        var fuel = new RecordingActuator
        {
            OnCommanding = position =>
            {
                if (position > 0)
                    throw new TaskCanceledException("actuator timeout");

                return Task.CompletedTask;
            }
        };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter, fuelFlow: new StubSensor { Value = 1 });
        controller.MoveToState(StartupState.FuelFlowInitiation);

        await controller.StartAsync();
        var continued = await WaitUntilAsync(
            () => controller.IsRunning
                && controller.CurrentState == StartupState.Error
                && fuel.CloseAttempts >= 2,
            TimeSpan.FromSeconds(5));
        continued.Should().BeTrue();
        controller.IsRunning.Should().BeTrue();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);

        await controller.StopAsync();
        controller.IsRunning.Should().BeFalse();
        controller.Dispose();
    }

    [Fact]
    public async Task FuelFlow_CancelledActuatorCommand_PropagatesAndDoesNotFailClosed()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);
        controller.MoveToState(StartupState.FuelFlowInitiation);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var act = () => controller.ExecuteOnceAsync(cancelled.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        controller.CurrentState.Should().Be(StartupState.FuelFlowInitiation);
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_InFlightCommandPastTheDeadline_FailClosesWithoutWaiting()
    {
        // The open command never returns and does not observe cancellation. The fuel
        // deadline is 5 seconds. The pass starts with a fraction of that left, so a
        // timeout that is only checked at the top of the next pass never runs.
        var openStarted = 0;
        var fuel = new RecordingActuator
        {
            OnCommanding = position =>
            {
                if (position <= 0)
                    return Task.CompletedTask;

                Interlocked.Increment(ref openStarted);
                return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            }
        };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Value = 1 });
        controller.MoveToState(StartupState.FuelFlowInitiation);
        controller.SetStateEnteredAt(DateTime.UtcNow - TimeSpan.FromMilliseconds(4_800));

        var started = Stopwatch.StartNew();
        await controller.ExecuteOnceAsync();
        started.Stop();

        started.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(800));
        openStarted.Should().Be(1);
        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        fuel.LastPosition.Should().Be(0);
        fuel.Positions.Should().Contain(0.0);
        oxidizer.Positions.Any(position => position > 0).Should().BeFalse();
        igniter.Positions.Any(position => position > 0).Should().BeFalse();
    }

    [Fact]
    public async Task FuelFlow_InFlightCommandPastTheDeadline_LeavesTheControlLoopRunning()
    {
        var fuel = new RecordingActuator
        {
            OnCommanding = position => position > 0
                ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task
                : Task.CompletedTask
        };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter, fuelFlow: new StubSensor { Value = 1 });
        controller.MoveToState(StartupState.FuelFlowInitiation);

        // Start resets the state clock, so this pass uses the full fuel deadline.
        // The open command does not return. The loop has to fail closed on that
        // deadline instead of staying inside the command.
        await controller.StartAsync();
        var continued = await WaitUntilAsync(
            () => controller.IsRunning
                && controller.CurrentState == StartupState.Error
                && fuel.CloseAttempts >= 2,
            TimeSpan.FromSeconds(8));
        continued.Should().BeTrue();

        // A deadline that escaped as cancellation would stop the loop after one
        // extra close. A live loop keeps commanding the valves shut.
        var closesAfterDeadline = fuel.CloseAttempts;
        await Task.Delay(500);
        fuel.CloseAttempts.Should().BeGreaterThan(closesAfterDeadline);
        controller.IsRunning.Should().BeTrue();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);

        await controller.StopAsync();
        controller.IsRunning.Should().BeFalse();
        controller.Dispose();
    }

    [Fact]
    public async Task PreStartup_SensorReadTimeout_FailClosesAndDoesNotAdvance()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Name = "chamber pressure",
                OnReading = () => Task.FromException<double>(new TaskCanceledException("sensor timeout"))
            });
        controller.MoveToState(StartupState.PreStartupChecks);

        var act = () => controller.ExecuteOnceAsync();
        await act.Should().NotThrowAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Purge);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task PreStartup_TemperatureReadTimeout_FailClosesAndDoesNotAdvance()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = StandardAtmospherePascals },
            temperature: new StubSensor
            {
                Name = "chamber temperature",
                OnReading = () => Task.FromException<double>(new TaskCanceledException("sensor timeout"))
            });
        controller.MoveToState(StartupState.PreStartupChecks);

        var act = () => controller.ExecuteOnceAsync();
        await act.Should().NotThrowAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Purge);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task PreStartup_SensorReadTimeout_LeavesTheControlLoopRunning()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                OnReading = () => Task.FromException<double>(new TaskCanceledException("sensor timeout"))
            });
        controller.MoveToState(StartupState.PreStartupChecks);

        await controller.StartAsync();
        var continued = await WaitUntilAsync(
            () => controller.IsRunning
                && controller.CurrentState == StartupState.Error
                && fuel.CloseAttempts >= 2,
            TimeSpan.FromSeconds(5));
        continued.Should().BeTrue();
        controller.IsRunning.Should().BeTrue();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);

        await controller.StopAsync();
        controller.IsRunning.Should().BeFalse();
        controller.Dispose();
    }

    [Fact]
    public async Task PreStartup_CancelledSensorRead_PropagatesAndDoesNotFailClosed()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                OnReading = () => Task.FromException<double>(new TaskCanceledException("loop stop"))
            });
        controller.MoveToState(StartupState.PreStartupChecks);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var act = () => controller.ExecuteOnceAsync(cancelled.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        controller.CurrentState.Should().Be(StartupState.PreStartupChecks);
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task PreStartup_SensorReadFailure_StillEscapesThePass()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                OnReading = () => Task.FromException<double>(new InvalidOperationException("pressure read failed"))
            });
        controller.MoveToState(StartupState.PreStartupChecks);

        var act = () => controller.ExecuteOnceAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();

        controller.CurrentState.Should().Be(StartupState.PreStartupChecks);
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task PreStartup_SensorReadTimeoutAfterAbort_StaysAborted()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        StartupSequenceController? controller = null;
        var pressure = new StubSensor
        {
            Name = "chamber pressure",
            OnReading = () =>
            {
                controller!.AbortStartup();
                return Task.FromException<double>(new TaskCanceledException("sensor timeout"));
            }
        };
        controller = CreateController(fuel, oxidizer, igniter, pressure);
        controller.MoveToState(StartupState.PreStartupChecks);

        var act = () => controller.ExecuteOnceAsync();
        await act.Should().NotThrowAsync();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.HasError.Should().BeFalse();
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task FuelFlow_SensorReadTimeoutAfterOpen_FailClosesAndDoesNotContinue()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor
            {
                Name = "fuel flow",
                OnReading = () => Task.FromException<double>(new TaskCanceledException("sensor timeout"))
            });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        var act = () => controller.ExecuteOnceAsync();
        await act.Should().NotThrowAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        fuel.Positions.Should().Contain(0.1);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task OxidizerFlow_SensorReadTimeoutAfterOpen_FailClosesAndDoesNotContinue()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            oxidizerFlow: new StubSensor
            {
                Name = "oxidizer flow",
                OnReading = () => Task.FromException<double>(new TaskCanceledException("sensor timeout"))
            });

        controller.MoveToState(StartupState.OxidizerFlowInitiation);
        var act = () => controller.ExecuteOnceAsync();
        await act.Should().NotThrowAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Ignition);
        oxidizer.Positions.Should().Contain(0.1);
        oxidizer.LastPosition.Should().Be(0);
        fuel.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task ThrottleUp_SensorReadTimeout_DoesNotDeclareTheEngineRunning()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Name = "chamber pressure",
                Value = StartupSequenceController.MinOperatingPressurePascals,
                OnReading = () => Task.FromException<double>(new TaskCanceledException("sensor timeout"))
            });

        controller.MoveToState(StartupState.ThrottleUp);
        var act = () => controller.ExecuteOnceAsync();
        await act.Should().NotThrowAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Running);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_RejectedValveCommand_DoesNotContinueWhenFlowLooksEstablished()
    {
        var fuel = new RecordingActuator { RejectWhen = static _ => true };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task Ignition_RejectedIgniterShutdown_DoesNotLeaveTheSequenceRunning()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator { RejectWhen = static position => position == 0 };
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Ignition);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.CombustionVerification);
        controller.CurrentState.Should().NotBe(StartupState.Running);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.Positions.Should().Contain(1);
        igniter.Positions.Should().Contain(0);
    }

    [Fact]
    public async Task FuelFlow_PeggedSensor_DoesNotContinueAndClosesTheValve()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Value = double.MaxValue });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task Combustion_PeggedTemperature_DoesNotContinueAndClosesTheValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = 200_000 },
            temperature: new StubSensor { Value = double.MaxValue });

        controller.MoveToState(StartupState.CombustionVerification);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.ThrottleUp);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task ThrottleUp_PeggedPressure_DoesNotDeclareRunningAndClosesTheValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = double.MaxValue });

        controller.MoveToState(StartupState.ThrottleUp);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task ThrottleUp_PressureStillBelowOperating_KeepsTheValvesAsTheyWere()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = 120_000 });

        controller.MoveToState(StartupState.ThrottleUp);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.ThrottleUp);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task Ignition_FuelFaultsDuringSpark_DoesNotEnterCombustion()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator
        {
            Name = "igniter",
            OnCommanding = position =>
            {
                if (position >= 1)
                    fuel.Status = ActuatorStatus.Fault;

                return Task.CompletedTask;
            }
        };
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Ignition);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.CombustionVerification);
        controller.IsStartupComplete.Should().BeFalse();
        igniter.Positions.Should().Contain(1.0);
        igniter.LastPosition.Should().Be(0);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
    }

    [Theory]
    [InlineData("fuel", ActuatorStatus.Fault)]
    [InlineData("oxidizer", ActuatorStatus.Error)]
    [InlineData("igniter", ActuatorStatus.AtPosition)]
    public async Task Combustion_ActuatorNotReady_DoesNotAdvanceAndClosesTheValves(
        string actuator,
        ActuatorStatus status)
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        ActuatorFor(actuator, fuel, oxidizer, igniter).Status = status;
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = 200_000 },
            temperature: new StubSensor { Value = 1_500 });

        controller.MoveToState(StartupState.CombustionVerification);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.ThrottleUp);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task Combustion_ValveFaultsDuringTheWait_DoesNotTreatTheReadingAsVerified()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Value = 200_000,
                OnReading = () =>
                {
                    fuel.Status = ActuatorStatus.Fault;
                    return Task.FromResult(200_000d);
                }
            },
            temperature: new StubSensor { Value = 1_500 });

        controller.MoveToState(StartupState.CombustionVerification);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.ThrottleUp);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Theory]
    [InlineData("fuel", ActuatorStatus.Fault)]
    [InlineData("oxidizer", ActuatorStatus.Disabled)]
    [InlineData("igniter", ActuatorStatus.Moving)]
    public async Task ThrottleUp_ActuatorNotReady_DoesNotDeclareRunning(
        string actuator,
        ActuatorStatus status)
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        ActuatorFor(actuator, fuel, oxidizer, igniter).Status = status;
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = StartupSequenceController.MinOperatingPressurePascals });

        controller.MoveToState(StartupState.ThrottleUp);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Running);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task ThrottleUp_ValveFaultsBeforeTheRunningTransition_DoesNotDeclareRunning()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Value = StartupSequenceController.MinOperatingPressurePascals,
                OnReading = () =>
                {
                    oxidizer.Status = ActuatorStatus.Fault;
                    return Task.FromResult(StartupSequenceController.MinOperatingPressurePascals);
                }
            });

        controller.MoveToState(StartupState.ThrottleUp);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Running);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task ThrottleUp_ReadyActuatorsAndOperatingPressure_DeclaresRunning()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = StartupSequenceController.MinOperatingPressurePascals });

        controller.MoveToState(StartupState.ThrottleUp);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Running);
        controller.IsStartupComplete.Should().BeTrue();
        controller.HasError.Should().BeFalse();
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task Running_ReadyActuatorsAndInBandSamples_StaysRunningWithoutAValveCommand()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateRunningController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Name = "chamber pressure",
                Value = StartupSequenceController.MaximumChamberPressurePascals
            },
            temperature: new StubSensor
            {
                Name = "chamber temperature",
                Value = StartupSequenceController.MaximumChamberTemperatureKelvin
            },
            fuelFlow: new StubSensor
            {
                Name = "fuel flow",
                Value = StartupSequenceController.MaximumFuelFlowKgPerSecond
            },
            oxidizerFlow: new StubSensor
            {
                Name = "oxidizer flow",
                Value = StartupSequenceController.MinimumPropellantFlow
            });

        controller.MoveToState(StartupState.Running);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Running);
        controller.IsStartupComplete.Should().BeTrue();
        controller.HasError.Should().BeFalse();
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Theory]
    [InlineData("fuel", ActuatorStatus.Fault)]
    [InlineData("oxidizer", ActuatorStatus.Error)]
    [InlineData("igniter", ActuatorStatus.AtPosition)]
    public async Task Running_ActuatorNotReady_ClosesTheValves(
        string actuator,
        ActuatorStatus status)
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        ActuatorFor(actuator, fuel, oxidizer, igniter).Status = status;
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Running);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.IsStartupComplete.Should().BeFalse();
        controller.HasError.Should().BeTrue();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Theory]
    [InlineData("pressure", "pegged")]
    [InlineData("pressure", "nan")]
    [InlineData("pressure", "below-operating")]
    [InlineData("temperature", "pegged")]
    [InlineData("temperature", "nan")]
    [InlineData("temperature", "cold")]
    [InlineData("fuel", "pegged")]
    [InlineData("fuel", "stopped")]
    [InlineData("oxidizer", "pegged")]
    [InlineData("oxidizer", "negative")]
    public async Task Running_SampleOutsideTheAuthorizedBand_ClosesTheValves(string sensor, string sample)
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateRunningController(
            fuel,
            oxidizer,
            igniter,
            pressure: sensor == "pressure" ? RunningSensor("chamber pressure", sample) : null,
            temperature: sensor == "temperature" ? RunningSensor("chamber temperature", sample) : null,
            fuelFlow: sensor == "fuel" ? RunningSensor("fuel flow", sample) : null,
            oxidizerFlow: sensor == "oxidizer" ? RunningSensor("oxidizer flow", sample) : null);

        controller.MoveToState(StartupState.Running);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.Running);
        controller.IsStartupComplete.Should().BeFalse();
        controller.HasError.Should().BeTrue();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Theory]
    [InlineData("pressure", SensorStatus.Disconnected)]
    [InlineData("temperature", SensorStatus.Error)]
    [InlineData("fuel", SensorStatus.Unknown)]
    [InlineData("oxidizer", SensorStatus.Reading)]
    public async Task Running_SensorNotReady_ClosesTheValves(string sensor, SensorStatus status)
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        StubSensor NotReady(string name, double value) => new()
        {
            Name = name,
            Value = value,
            Status = status
        };
        var controller = CreateRunningController(
            fuel,
            oxidizer,
            igniter,
            pressure: sensor == "pressure"
                ? NotReady("chamber pressure", StartupSequenceController.MinOperatingPressurePascals)
                : null,
            temperature: sensor == "temperature"
                ? NotReady("chamber temperature", StartupSequenceController.MinCombustionTemperatureKelvin)
                : null,
            fuelFlow: sensor == "fuel" ? NotReady("fuel flow", 1) : null,
            oxidizerFlow: sensor == "oxidizer" ? NotReady("oxidizer flow", 1) : null);

        controller.MoveToState(StartupState.Running);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task Running_PressureReadTimeout_ClosesTheValvesAndRetriesTheShutdown()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateRunningController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Name = "chamber pressure",
                OnReading = () => Task.FromException<double>(new TaskCanceledException("sensor timeout"))
            });

        controller.MoveToState(StartupState.Running);
        var act = () => controller.ExecuteOnceAsync();
        await act.Should().NotThrowAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);

        await controller.ExecuteOnceAsync();
        fuel.CloseAttempts.Should().BeGreaterThanOrEqualTo(2);
        controller.CurrentState.Should().Be(StartupState.Error);
    }

    [Fact]
    public async Task Running_PressureReadTimeoutAfterAbort_StaysAborted()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        StartupSequenceController? controller = null;
        var pressure = new StubSensor
        {
            Name = "chamber pressure",
            OnReading = () =>
            {
                controller!.AbortStartup();
                return Task.FromException<double>(new TaskCanceledException("sensor timeout"));
            }
        };
        controller = CreateRunningController(fuel, oxidizer, igniter, pressure: pressure);
        controller.MoveToState(StartupState.Running);

        var act = () => controller.ExecuteOnceAsync();
        await act.Should().NotThrowAsync();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.HasError.Should().BeFalse();
        fuel.Positions.Should().OnlyContain(position => position == 0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task Running_CancelledPressureRead_PropagatesAndDoesNotFailClosed()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateRunningController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                OnReading = () => Task.FromException<double>(new TaskCanceledException("loop stop"))
            });
        controller.MoveToState(StartupState.Running);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var act = () => controller.ExecuteOnceAsync(cancelled.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        controller.CurrentState.Should().Be(StartupState.Running);
        controller.HasError.Should().BeFalse();
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task Running_PressureReadFailure_StillEscapesThePass()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateRunningController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                OnReading = () => Task.FromException<double>(new InvalidOperationException("pressure read failed"))
            });
        controller.MoveToState(StartupState.Running);

        var act = () => controller.ExecuteOnceAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();

        controller.CurrentState.Should().Be(StartupState.Running);
        controller.HasError.Should().BeFalse();
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task Running_ValveFaultsDuringThePressureRead_ClosesTheValves()
    {
        var fuel = new RecordingActuator { Name = "fuel" };
        var oxidizer = new RecordingActuator { Name = "oxidizer" };
        var igniter = new RecordingActuator { Name = "igniter" };
        var controller = CreateRunningController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                Name = "chamber pressure",
                Value = StartupSequenceController.MinOperatingPressurePascals,
                OnReading = () =>
                {
                    oxidizer.Status = ActuatorStatus.Fault;
                    return Task.FromResult(StartupSequenceController.MinOperatingPressurePascals);
                }
            });

        controller.MoveToState(StartupState.Running);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task Stop_FromRunning_ClosesThePropellantValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateRunningController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Running);
        await controller.StartAsync();
        await controller.StopAsync();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
        controller.Dispose();
    }

    [Fact]
    public async Task Stop_FromIdle_DoesNotCommandTheValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);

        await controller.StartAsync();
        await controller.StopAsync();

        controller.CurrentState.Should().Be(StartupState.Idle);
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
        controller.Dispose();
    }

    [Fact]
    public void AbortStartup_FromRunning_ClosesThePropellantValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Running);
        controller.AbortStartup();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_AbortDuringOpen_DoesNotResumeOrLeaveTheValveOpen()
    {
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StartupSequenceController? controller = null;
        var fuel = new RecordingActuator
        {
            OnCommanding = async position =>
            {
                if (position <= 0)
                    return;

                opened.TrySetResult();
                await releaseOpen.Task;
            }
        };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        var pass = controller.ExecuteOnceAsync();
        await opened.Task;
        controller.AbortStartup();
        releaseOpen.TrySetResult();
        await pass;

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);

        var commandsAtAbort = fuel.CommandCount;
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        fuel.Positions.Skip(commandsAtAbort).Should().OnlyContain(position => position == 0);
        oxidizer.Positions.SkipWhile(position => position == 0).Should().BeEmpty();
    }

    [Fact]
    public async Task Ignition_AbortDuringSpark_DoesNotLeaveTheIgniterOnOrContinue()
    {
        var sparked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSpark = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StartupSequenceController? controller = null;
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator
        {
            OnCommanding = async position =>
            {
                if (position <= 0)
                    return;

                sparked.TrySetResult();
                await releaseSpark.Task;
            }
        };
        controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Ignition);
        var pass = controller.ExecuteOnceAsync();
        await sparked.Task;
        controller.AbortStartup();
        releaseSpark.TrySetResult();
        await pass;

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.CurrentState.Should().NotBe(StartupState.CombustionVerification);
        igniter.LastPosition.Should().Be(0);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task ClosedSequence_FuelCloseThrows_StillClosesTheOtherValvesAndRetries()
    {
        var fuelCloses = 0;
        var fuel = new RecordingActuator
        {
            OnCommanding = position =>
            {
                if (position <= 0)
                {
                    fuelCloses++;
                    throw new InvalidOperationException("fuel valve rejected close");
                }

                return Task.CompletedTask;
            }
        };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Error);
        await controller.ExecuteOnceAsync();

        fuelCloses.Should().Be(1);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
        controller.CurrentState.Should().Be(StartupState.Error);

        await controller.ExecuteOnceAsync();

        fuelCloses.Should().Be(2);
        oxidizer.CommandCount.Should().Be(2);
        igniter.CommandCount.Should().Be(2);
    }

    [Fact]
    public async Task ClosedSequence_RejectedFuelClose_StillClosesTheOtherValvesAndRetries()
    {
        var fuel = new RecordingActuator { RejectClose = true };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Aborted);
        await controller.ExecuteOnceAsync();

        fuel.CloseAttempts.Should().Be(1);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);

        await controller.ExecuteOnceAsync();

        fuel.CloseAttempts.Should().Be(2);
        oxidizer.CommandCount.Should().Be(2);
        igniter.CommandCount.Should().Be(2);
        controller.CurrentState.Should().Be(StartupState.Aborted);
    }

    [Fact]
    public void AbortStartup_WhenFuelCloseThrows_StillClosesTheOtherValves()
    {
        var fuel = new RecordingActuator
        {
            OnCommanding = position =>
            {
                if (position <= 0)
                    throw new InvalidOperationException("fuel valve rejected close");

                return Task.CompletedTask;
            }
        };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Running);
        controller.AbortStartup();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task Stop_WhenFuelCloseThrows_StillClosesTheOtherValves()
    {
        var fuel = new RecordingActuator
        {
            OnCommanding = position =>
            {
                if (position <= 0)
                    throw new InvalidOperationException("fuel valve rejected close");

                return Task.CompletedTask;
            }
        };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateRunningController(fuel, oxidizer, igniter);
        controller.MoveToState(StartupState.Running);

        try
        {
            await controller.StartAsync();
            var stop = async () => await controller.StopAsync();
            await stop.Should().ThrowAsync<InvalidOperationException>();

            controller.CurrentState.Should().Be(StartupState.Aborted);
            controller.IsStartupComplete.Should().BeFalse();
            oxidizer.LastPosition.Should().Be(0);
            igniter.LastPosition.Should().Be(0);
        }
        finally
        {
            controller.Dispose();
        }
    }

    [Fact]
    public async Task Stop_WhenLoopFaultsAndValveCloseFails_PreservesBothFailures()
    {
        var pressureRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loopFailure = new InvalidOperationException("pressure read failed");
        var fuel = new RecordingActuator { RejectClose = true };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor
            {
                OnReading = () =>
                {
                    pressureRead.TrySetResult();
                    return Task.FromException<double>(loopFailure);
                }
            });

        controller.MoveToState(StartupState.PreStartupChecks);

        try
        {
            await controller.StartAsync();
            await pressureRead.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var stop = async () => await controller.StopAsync();
            var exception = await stop.Should().ThrowAsync<AggregateException>();

            exception.Which.InnerExceptions.Should().Contain(loopFailure);
            exception.Which.InnerExceptions.Should().ContainSingle(inner =>
                inner is InvalidOperationException
                && inner.Message == "[Startup Sequence] Shutdown did not close every valve.");
            fuel.CloseAttempts.Should().BeGreaterThanOrEqualTo(2);
            oxidizer.LastPosition.Should().Be(0);
            igniter.LastPosition.Should().Be(0);
        }
        finally
        {
            controller.Dispose();
        }
    }

    [Fact]
    public void AbortStartup_FromIdle_DoesNotCommandTheValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.AbortStartup();

        controller.CurrentState.Should().Be(StartupState.Idle);
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    private static StubSensor SensorFor(string sensor, StubSensor pressure, StubSensor temperature) =>
        sensor switch
        {
            "pressure" => pressure,
            "temperature" => temperature,
            _ => throw new ArgumentOutOfRangeException(nameof(sensor), sensor, "Unknown startup sensor.")
        };

    private static RecordingActuator ActuatorFor(
        string actuator,
        RecordingActuator fuel,
        RecordingActuator oxidizer,
        RecordingActuator igniter) =>
        actuator switch
        {
            "fuel" => fuel,
            "oxidizer" => oxidizer,
            "igniter" => igniter,
            _ => throw new ArgumentOutOfRangeException(nameof(actuator), actuator, "Unknown startup actuator.")
        };

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return true;

            await Task.Delay(10);
        }

        return predicate();
    }

    private static StubSensor RunningSensor(string name, string sample) =>
        new()
        {
            Name = name,
            Value = sample switch
            {
                "pegged" => double.MaxValue,
                "nan" => double.NaN,
                "below-operating" => 120_000,
                "cold" => 100,
                "stopped" => 0,
                "negative" => -1,
                _ => throw new ArgumentOutOfRangeException(nameof(sample), sample, "Unknown running sample.")
            }
        };

    private static StartupSequenceController CreateRunningController(
        IActuator fuel,
        IActuator oxidizer,
        IActuator igniter,
        ISensor<double>? pressure = null,
        ISensor<double>? temperature = null,
        ISensor<double>? fuelFlow = null,
        ISensor<double>? oxidizerFlow = null)
    {
        return CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure ?? new StubSensor
            {
                Name = "chamber pressure",
                Value = StartupSequenceController.MinOperatingPressurePascals
            },
            temperature ?? new StubSensor
            {
                Name = "chamber temperature",
                Value = StartupSequenceController.MinCombustionTemperatureKelvin
            },
            fuelFlow ?? new StubSensor { Name = "fuel flow", Value = 1 },
            oxidizerFlow ?? new StubSensor { Name = "oxidizer flow", Value = 1 });
    }

    private static StartupSequenceController CreateController(
        IActuator fuel,
        IActuator oxidizer,
        IActuator igniter,
        ISensor<double>? pressure = null,
        ISensor<double>? temperature = null,
        ISensor<double>? fuelFlow = null,
        ISensor<double>? oxidizerFlow = null)
    {
        return new StartupSequenceController(
            fuel,
            oxidizer,
            igniter,
            pressure ?? new StubSensor { Name = "chamber pressure", Value = StandardAtmospherePascals },
            temperature ?? new StubSensor { Name = "chamber temperature", Value = 293.15 },
            fuelFlow ?? new StubSensor(),
            oxidizerFlow ?? new StubSensor());
    }

    private sealed class RecordingActuator : IActuator
    {
        public string ActuatorId => "actuator";
        public string Name { get; init; } = "actuator";
        public ActuatorType Type => ActuatorType.Valve;
        public ActuatorStatus Status { get; set; } = ActuatorStatus.Ready;
        public double MinPosition => 0;
        public double MaxPosition => 1;
        public double ResponseTimeSeconds => 0.1;
        public double MaxRateOfChange => 1;
        public bool IsEnabled => true;
        public int CommandCount { get; private set; }
        public int CloseAttempts { get; private set; }
        public double LastPosition { get; private set; } = double.NaN;
        public List<double> Positions { get; } = new();
        public Func<double, Task>? OnCommanding { get; init; }
        public bool RejectClose { get; init; }

        /// <summary>
        /// When this returns true, the command is recorded and rejected.
        /// </summary>
        public Predicate<double>? RejectWhen { get; init; }

        public event EventHandler<ActuatorPositionChangedEventArgs>? PositionChanged;

        public async Task<bool> SetPositionAsync(double position, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (OnCommanding != null)
                await OnCommanding(position);

            CommandCount++;
            LastPosition = position;
            Positions.Add(position);
            PositionChanged?.Invoke(this, new ActuatorPositionChangedEventArgs());
            if (position <= 0)
                CloseAttempts++;

            return !(RejectClose && position <= 0)
                && (RejectWhen == null || !RejectWhen(position));
        }

        public Task<double> GetPositionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LastPosition);

        public Task<bool> EnableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DisableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class StubSensor : ISensor<double>
    {
        public string SensorId => "sensor";
        public string Name { get; init; } = "sensor";
        public SensorType Type => SensorType.Pressure;
        public SensorStatus Status { get; set; } = SensorStatus.Ready;
        public double MinValue => 0;
        public double MaxValue => 1;
        public int MaxFrequencyHz => 10;
        public DateTime LastReadingTime => DateTime.UtcNow;

        public event EventHandler<SensorReadingChangedEventArgs<double>>? ReadingChanged;

        public double Value { get; set; }
        public Func<Task<double>>? OnReading { get; init; }

        /// <summary>
        /// Applied at the start of a read, before <see cref="OnReading"/>.
        /// A sensor can be Ready when the valve opens and disconnected by the time the sample returns.
        /// </summary>
        public SensorStatus? StatusOnRead { get; init; }

        public Task<double> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (StatusOnRead is SensorStatus status)
                Status = status;

            if (OnReading != null)
                return OnReading();

            ReadingChanged?.Invoke(this, new SensorReadingChangedEventArgs<double>());
            return Task.FromResult(Value);
        }

        public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
