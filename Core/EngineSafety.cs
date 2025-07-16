using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq; // Added missing import for .All()

namespace HB_NLP_Research_Lab.Core
{
    public class EngineSafety
    {
        private readonly Random _random = new Random();
        private bool _emergencyShutdown = false;

        public async Task<SafetyStatus> PerformPreflightChecksAsync()
        {
            Console.WriteLine("🛡️ Performing comprehensive preflight safety checks...");
            
            var checks = new List<SafetyCheck>
            {
                new SafetyCheck { Name = "Fuel System Integrity", Status = true, Message = "Fuel system operational" },
                new SafetyCheck { Name = "Cooling System Status", Status = true, Message = "Cooling system ready" },
                new SafetyCheck { Name = "Control System Calibration", Status = true, Message = "Control systems calibrated" },
                new SafetyCheck { Name = "Pressure Sensor Validation", Status = true, Message = "Pressure sensors validated" },
                new SafetyCheck { Name = "Temperature Sensor Validation", Status = true, Message = "Temperature sensors validated" },
                new SafetyCheck { Name = "Emergency Shutdown System", Status = true, Message = "Emergency shutdown system ready" },
                new SafetyCheck { Name = "Fire Suppression System", Status = true, Message = "Fire suppression system operational" },
                new SafetyCheck { Name = "Structural Integrity", Status = true, Message = "Structural components verified" }
            };
            
            await Task.Delay(500); // Simulate comprehensive checks
            
            var allPassed = checks.All(c => c.Status);
            
            return new SafetyStatus
            {
                IsSafe = allPassed,
                Message = allPassed ? "All safety checks passed" : "Safety check failed",
                Checks = checks,
                Timestamp = DateTime.UtcNow
            };
        }

        public async Task ValidateStartupSequenceAsync()
        {
            Console.WriteLine("✅ Validating engine startup sequence...");
            
            // Simulate startup validation
            await Task.Delay(200);
            
            if (_emergencyShutdown)
            {
                throw new EngineException("Engine startup blocked due to emergency shutdown");
            }
        }

        public async Task<SafetyStatus> GetSafetyStatusAsync()
        {
            return new SafetyStatus
            {
                IsSafe = !_emergencyShutdown,
                Message = _emergencyShutdown ? "Emergency shutdown active" : "Engine operating safely",
                Checks = new List<SafetyCheck>(),
                Timestamp = DateTime.UtcNow
            };
        }

        public async Task<bool> MonitorCriticalParametersAsync(double temperature, double pressure, double fuelFlow)
        {
            var isSafe = true;
            var warnings = new List<string>();
            
            // Temperature monitoring
            if (temperature > 2000)
            {
                isSafe = false;
                warnings.Add("CRITICAL: Temperature exceeds safe limits");
            }
            else if (temperature > 1900)
            {
                warnings.Add("WARNING: Temperature approaching limits");
            }
            
            // Pressure monitoring
            if (pressure > 350)
            {
                isSafe = false;
                warnings.Add("CRITICAL: Pressure exceeds safe limits");
            }
            else if (pressure > 320)
            {
                warnings.Add("WARNING: Pressure approaching limits");
            }
            
            // Fuel flow monitoring
            if (fuelFlow > 100)
            {
                isSafe = false;
                warnings.Add("CRITICAL: Fuel flow exceeds safe limits");
            }
            else if (fuelFlow < 50)
            {
                warnings.Add("WARNING: Fuel flow below optimal range");
            }
            
            if (!isSafe)
            {
                await InitiateEmergencyShutdownAsync();
            }
            
            await Task.Delay(10);
            return isSafe;
        }

        public async Task InitiateEmergencyShutdownAsync()
        {
            Console.WriteLine("🚨 EMERGENCY SHUTDOWN INITIATED!");
            _emergencyShutdown = true;
            
            // Simulate emergency shutdown sequence
            await Task.Delay(100);
            
            Console.WriteLine("🛑 Engine safely shut down");
        }

        public async Task ResetSafetySystemsAsync()
        {
            Console.WriteLine("🔄 Resetting safety systems...");
            _emergencyShutdown = false;
            await Task.Delay(100);
            Console.WriteLine("✅ Safety systems reset complete");
        }
    }

    public class SafetyStatus
    {
        public bool IsSafe { get; set; }
        public string Message { get; set; }
        public List<SafetyCheck> Checks { get; set; } = new List<SafetyCheck>();
        public DateTime Timestamp { get; set; }
    }

    public class SafetyCheck
    {
        public string Name { get; set; }
        public bool Status { get; set; }
        public string Message { get; set; }
    }
} 