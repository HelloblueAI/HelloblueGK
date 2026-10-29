# Real-Time Engine Control System

## Overview

This directory contains the real-time control system components that enable HelloblueGK to actually control rocket engines, not just simulate them.

## Architecture

```
Control/
├── RealTimeControlLoop.cs      # Base class for deterministic control loops
├── ThrottleController.cs        # Thrust control (100 Hz)
├── StartupSequenceController.cs # Engine startup sequence (10 Hz)
└── README.md                    # This file

Safety/
└── HardwareSafetyMonitor.cs     # Real-time safety monitoring (100 Hz)

Hardware/
├── ISensor.cs                   # Sensor interface
├── IActuator.cs                 # Actuator interface
└── IHardwareInterface.cs        # Communication protocol interface
```

## Components

### RealTimeControlLoop
Base class providing:
- Deterministic timing (configurable frequency)
- Timing violation detection
- Performance statistics
- Graceful startup/shutdown

### ThrottleController
Controls engine thrust via throttle actuator:
- **Frequency**: 100 Hz (10ms period)
- **Control**: PID-based closed-loop or open-loop
- **Safety**: Rate limiting, position limits
- **Inputs**: Thrust sensor, chamber pressure sensor
- **Output**: Throttle actuator position

### StartupSequenceController
Manages engine startup sequence:
- **Frequency**: 10 Hz (100ms period)
- **States**: Pre-checks → Purge → Fuel → Oxidizer → Ignition → Verify → Throttle Up → Running
- **Safety**: Timeout detection, state validation, fail-closed sensor and actuator gates
- **Abort**: Can abort at any time

Propellant is admitted and the igniter is fired only while the samples that authorize that command are still valid:

- The chamber stays in the ambient band until the spark. Combustion is a rise out of that band after the pulse, not another ambient sample.
- Fuel flow stays inside its band before the oxidizer valve opens, and again at the end of the oxidizer wait, before the sequence enters ignition. Ignition reads both flows again immediately before the spark. Those two fuel checks are not the same sample: the second runs on a later loop tick, after the chamber is read again, and it is the sample that authorizes the spark. Oxidizer flow is part of that pre-spark read only.
- The igniter pulse is a single 0.5 s wait. Both flows are read again when that wait ends, before combustion verification. Combustion verification waits a further second before its own flow read, so a flow that leaves the band during the pulse fail-closes without spending that extra second.
- A sensor that is not Ready, a non-finite or out-of-band sample, or a read timeout that is not the control-loop token fail-closes. The closed sequence retries the shutdown. An abort that already won stays aborted.
- Ignition's budget stays 3 seconds, and oxidizer admission's budget stays 5 seconds. At 10 Hz a healthy read fits in one 100 ms period. Ignition's pass, including the 0.5 s pulse, two chamber reads, and four flow reads, is about 1.1 s. Oxidizer admission's 1 s wait plus its sensor reads is about 1.7 s. The added reads fit in those budgets, so neither interlock is lengthened.

### HardwareSafetyMonitor
Real-time safety monitoring:
- **Frequency**: 100 Hz (10ms period)
- **Function**: Monitors critical sensors, triggers emergency shutdown
- **Limits**: Configurable safety limits per parameter
- **Response**: Hardware interlock activation

## Usage Example

```csharp
// Create hardware interfaces (would be actual hardware implementations)
var throttleActuator = new SimulatedThrottleActuator();
var thrustSensor = new SimulatedThrustSensor();
var pressureSensor = new SimulatedPressureSensor();

// Create throttle controller
var throttleController = new ThrottleController(
    throttleActuator,
    thrustSensor,
    pressureSensor,
    frequencyHz: 100
);

// Start controller
await throttleController.StartAsync();

// Set target thrust
throttleController.SetTargetThrust(1_500_000); // 1.5 MN

// Or set throttle directly
throttleController.SetThrottle(0.75); // 75% throttle

// Stop when done
await throttleController.StopAsync();
```

## Safety Considerations

⚠️ **CRITICAL**: This code is for controlling real rocket engines. Do not use without:

1. **Proper Safety Systems**: Hardware interlocks, emergency shutdown
2. **Testing**: Extensive testing on test stands before flight
3. **Certification**: Aerospace certification (DO-178C, etc.)
4. **Redundancy**: Multiple independent safety systems
5. **Expertise**: Aerospace engineering expertise required

## Next Steps

To make this fully functional:

1. **Implement Hardware Interfaces**: Create concrete implementations of `ISensor`, `IActuator`, `IHardwareInterface`
2. **Add Communication Protocols**: CAN bus, Modbus, Serial drivers
3. **Implement Gimbal Controller**: Thrust vectoring control
4. **Add Mixture Ratio Controller**: Fuel/oxidizer ratio control
5. **RTOS Integration**: Real-time operating system support
6. **Testing**: Hardware-in-the-loop testing
