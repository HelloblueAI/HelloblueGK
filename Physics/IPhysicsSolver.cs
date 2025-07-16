namespace HB_NLP_Research_Lab.Physics
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