# Experimental Engine Control Components

## Overview

This directory contains experimental simulation and research control components. They are not validated, certified, or approved for operational hardware. They must not be connected to a real engine. Software tests in this repository are not physical or operational validation.

## Architecture

```
Control/
├── RealTimeControlLoop.cs      # Base class for timed control-loop research code
├── ThrottleController.cs        # Simulated thrust-command loop (100 Hz)
├── StartupSequenceController.cs # Simulated startup-sequence state machine (10 Hz)
└── README.md                    # This file

Safety/
└── HardwareSafetyMonitor.cs     # Simulated safety-monitoring research loop (100 Hz)

# Future research gaps (not present evidence of hardware readiness):
# - Sensor interfaces
# - Actuator interfaces
# - Hardware communication interfaces
```

## Components

### RealTimeControlLoop
Base class providing:
- Configurable loop timing
- Timing violation detection
- Performance statistics
- Startup/shutdown hooks for research use

### ThrottleController
Research loop that issues simulated throttle commands via an `IActuator`:
- **Frequency**: 100 Hz (10ms period)
- **Control**: PID-based closed-loop or open-loop
- **Limits**: Rate limiting, position limits
- **Inputs**: Thrust sensor, chamber pressure sensor abstractions
- **Output**: Throttle actuator position commands in simulation

### StartupSequenceController
Simulated engine startup-sequence state machine:
- **Frequency**: 10 Hz (100ms period)
- **States**: Pre-checks → Purge → Fuel → Oxidizer → Ignition → Verify → Throttle Up → Running
- **Gates**: Timeout detection, state validation, fail-closed sensor and actuator gates in software
- **Abort**: Can abort at any time within the simulation

In this simulated sequence, propellant admission and igniter commands are authorized only while the samples that authorize that command are still valid:

- The chamber stays in the ambient band until the spark. Combustion is a rise out of that band after the pulse, not another ambient sample.
- Fuel flow stays inside its band before the oxidizer valve opens, and again at the end of the oxidizer wait, before the sequence enters ignition. Ignition reads both flows again immediately before the spark. Those two fuel checks are not the same sample: the second runs on a later loop tick, after the chamber is read again, and it is the sample that authorizes the spark. Oxidizer flow is part of that pre-spark read only.
- The igniter pulse is a single 0.5 s wait. Both flows, and the chamber, are read again when that wait ends, before combustion verification. The chamber may still be ambient or already rising. A pegged, negative, non-finite, or not-Ready sample fail-closes. Combustion verification waits a further second before its own chamber and flow reads, so a sample that leaves its band during the pulse fail-closes without spending that extra second.
- A sensor that is not Ready, a non-finite or out-of-band sample, or a read timeout that is not the control-loop token fail-closes. The closed sequence retries the shutdown. An abort that already won stays aborted.
- Ignition's budget stays 3 seconds, and oxidizer admission's budget stays 5 seconds. At 10 Hz a healthy read fits in one 100 ms period. Ignition's pass, including the 0.5 s pulse, four chamber reads, and four flow reads, is about 1.3 s. Oxidizer admission's 1 s wait plus its sensor reads is about 1.7 s. The added reads fit in those budgets, so neither interlock is lengthened.

### HardwareSafetyMonitor
Simulated safety-monitoring research loop:
- **Frequency**: 100 Hz (10ms period)
- **Function**: Monitors configured sensor abstractions and can request a simulated emergency shutdown path
- **Limits**: Configurable software limits per parameter
- **Response**: Software-side interlock request only; not a hardware safety system

### RedundantControlSystem
Voting helpers (majority, median, average, mid-value, consensus, fault detection) are retained for internal/offline testing. Runtime operation stays disabled until real output sources, timestamps, quorum rules, bounds, rate limits, and single-writer actuation are designed. `StartAsync` throws `NotSupportedException` and does not start loops or command actuators.

## Usage Example

```csharp
// Simulated sensor/actuator stand-ins for research only — not operational hardware
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

## Scope and Limits

- These components are experimental simulation and research code.
- They are not validated, certified, or approved for operational hardware.
- They must not be connected to a real engine.
- Passing software tests here is not physical or operational validation.
- Do not treat this directory as flight-ready, certified, or hardware-qualified software.

## Future Research Gaps

The items below are research gaps, not evidence that hardware control is ready:

1. Concrete `ISensor`, `IActuator`, and `IHardwareInterface` implementations suitable for a specific test stand (still not permission to connect to a real engine)
2. Communication protocol drivers (for example CAN, Modbus, or serial) as research topics
3. Gimbal / thrust-vectoring research controllers
4. Mixture-ratio research controllers
5. RTOS integration experiments
6. Hardware-in-the-loop research setups under appropriate lab controls
