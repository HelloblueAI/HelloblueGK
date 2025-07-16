using System;

namespace HelloblueGK.Physics
{
    public class ThermalSolverStub : IPhysicsSolver
    {
        public string Name => "Stub Thermal Solver";

        public void Initialize()
        {
            // Placeholder for thermal solver initialization
            Console.WriteLine("[Thermal] Initializing thermal solver...");
        }

        public PhysicsResult RunSimulation(object model)
        {
            // Placeholder for thermal simulation
            Console.WriteLine("[Thermal] Running thermal simulation...");
            return new PhysicsResult { Status = "Success", Data = new double[] { 100.0, 200.0, 300.0 } };
        }
    }
} 