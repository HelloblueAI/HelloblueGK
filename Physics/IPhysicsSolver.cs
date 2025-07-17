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
        public PhysicsResult()
        {
            Status = string.Empty;
            Data = new double[0];
            ErrorMessage = string.Empty;
        }

        public string Status { get; set; }
        public double[] Data { get; set; }
        public string ErrorMessage { get; set; }
    }
} 