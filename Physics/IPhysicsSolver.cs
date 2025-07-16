namespace HelloblueGK.Physics
{
    public interface IPhysicsSolver
    {
        string Name { get; }
        void Initialize();
        PhysicsResult RunSimulation(object model);
    }

    public class PhysicsResult
    {
        public string Status { get; set; }
        public double[] Data { get; set; }
    }
} 