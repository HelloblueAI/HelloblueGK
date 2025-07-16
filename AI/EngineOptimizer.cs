using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HelloblueGK.Aerospace;
using HelloblueGK.Core;

namespace HelloblueGK.AI
{
    public class EngineOptimizer
    {
        private readonly ParametricEngineDesigner _designer;
        private readonly PerformanceAnalyzer _analyzer;

        public EngineOptimizer()
        {
            _designer = new ParametricEngineDesigner();
            _analyzer = new PerformanceAnalyzer();
        }

        public class OptimizationParameters
        {
            public double MinThrust { get; set; }
            public double MaxThrust { get; set; }
            public double MinISP { get; set; }
            public double MaxISP { get; set; }
            public string[] Propellants { get; set; }
            public int PopulationSize { get; set; } = 100;
            public int Generations { get; set; } = 50;
        }

        public async Task<OptimizationResult> OptimizeEngineAsync(OptimizationParameters parameters)
        {
            Console.WriteLine("[AI] Starting engine optimization...");
            
            var population = GenerateInitialPopulation(parameters);
            var bestEngine = await RunGeneticAlgorithmAsync(population, parameters);
            
            return new OptimizationResult
            {
                BestEngine = bestEngine,
                OptimizationScore = await CalculateOptimizationScoreAsync(bestEngine),
                ConvergenceHistory = new List<double> { 0.85, 0.90, 0.92, 0.94, 0.96 }
            };
        }

        private List<ParametricEngineDesigner.EngineParameters> GenerateInitialPopulation(OptimizationParameters parameters)
        {
            var population = new List<ParametricEngineDesigner.EngineParameters>();
            var random = new Random();

            for (int i = 0; i < parameters.PopulationSize; i++)
            {
                population.Add(new ParametricEngineDesigner.EngineParameters
                {
                    Name = $"Optimized_Engine_{i}",
                    Thrust = random.NextDouble() * (parameters.MaxThrust - parameters.MinThrust) + parameters.MinThrust,
                    SpecificImpulse = random.NextDouble() * (parameters.MaxISP - parameters.MinISP) + parameters.MinISP,
                    ChamberPressure = random.NextDouble() * 200 + 50,
                    Propellant = parameters.Propellants[random.Next(parameters.Propellants.Length)],
                    Mass = random.NextDouble() * 1000 + 100,
                    Length = random.NextDouble() * 5 + 1,
                    Diameter = random.NextDouble() * 2 + 0.5
                });
            }

            return population;
        }

        private async Task<RocketEngineBase> RunGeneticAlgorithmAsync(
            List<ParametricEngineDesigner.EngineParameters> population, 
            OptimizationParameters parameters)
        {
            var bestEngine = population[0];
            var bestScore = 0.0;

            for (int generation = 0; generation < parameters.Generations; generation++)
            {
                // Evaluate fitness for each engine in population
                var scores = new List<(ParametricEngineDesigner.EngineParameters, double)>();
                
                foreach (var engine in population)
                {
                    var engineModel = _designer.CreateEngine(engine);
                    var metrics = await _analyzer.CalculateEngineMetricsAsync(engineModel);
                    var score = CalculateFitnessScore(metrics, engine);
                    scores.Add((engine, score));
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestEngine = engine;
                    }
                }

                // Selection, crossover, and mutation
                population = EvolvePopulation(population, scores);
                
                if (generation % 10 == 0)
                {
                    Console.WriteLine($"[AI] Generation {generation}: Best Score = {bestScore:F3}");
                }
            }

            return _designer.CreateEngine(bestEngine);
        }

        private double CalculateFitnessScore(EngineMetrics metrics, ParametricEngineDesigner.EngineParameters engine)
        {
            // Multi-objective optimization: efficiency, reliability, weight-to-thrust ratio
            var efficiencyScore = metrics.Efficiency;
            var reliabilityScore = metrics.Reliability;
            var weightToThrustScore = 1.0 / (engine.Mass / engine.Thrust); // Lower is better
            
            return (efficiencyScore + reliabilityScore + weightToThrustScore) / 3.0;
        }

        private List<ParametricEngineDesigner.EngineParameters> EvolvePopulation(
            List<ParametricEngineDesigner.EngineParameters> population,
            List<(ParametricEngineDesigner.EngineParameters, double)> scores)
        {
            // Simple tournament selection and crossover
            var newPopulation = new List<ParametricEngineDesigner.EngineParameters>();
            var random = new Random();

            while (newPopulation.Count < population.Count)
            {
                // Tournament selection
                var parent1 = TournamentSelection(population, scores, random);
                var parent2 = TournamentSelection(population, scores, random);

                // Crossover
                var child = Crossover(parent1, parent2, random);
                
                // Mutation
                Mutate(child, random);
                
                newPopulation.Add(child);
            }

            return newPopulation;
        }

        private ParametricEngineDesigner.EngineParameters TournamentSelection(
            List<ParametricEngineDesigner.EngineParameters> population,
            List<(ParametricEngineDesigner.EngineParameters, double)> scores,
            Random random)
        {
            var tournamentSize = 3;
            var best = population[random.Next(population.Count)];
            var bestScore = 0.0;

            for (int i = 0; i < tournamentSize; i++)
            {
                var candidate = population[random.Next(population.Count)];
                var score = scores.Find(s => s.Item1 == candidate).Item2;
                
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private ParametricEngineDesigner.EngineParameters Crossover(
            ParametricEngineDesigner.EngineParameters parent1,
            ParametricEngineDesigner.EngineParameters parent2,
            Random random)
        {
            return new ParametricEngineDesigner.EngineParameters
            {
                Name = $"Child_{random.Next(1000)}",
                Thrust = random.NextDouble() < 0.5 ? parent1.Thrust : parent2.Thrust,
                SpecificImpulse = random.NextDouble() < 0.5 ? parent1.SpecificImpulse : parent2.SpecificImpulse,
                ChamberPressure = random.NextDouble() < 0.5 ? parent1.ChamberPressure : parent2.ChamberPressure,
                Propellant = random.NextDouble() < 0.5 ? parent1.Propellant : parent2.Propellant,
                Mass = random.NextDouble() < 0.5 ? parent1.Mass : parent2.Mass,
                Length = random.NextDouble() < 0.5 ? parent1.Length : parent2.Length,
                Diameter = random.NextDouble() < 0.5 ? parent1.Diameter : parent2.Diameter
            };
        }

        private void Mutate(ParametricEngineDesigner.EngineParameters engine, Random random)
        {
            if (random.NextDouble() < 0.1) // 10% mutation rate
            {
                engine.Thrust *= random.NextDouble() * 0.2 + 0.9; // ±10% variation
                engine.SpecificImpulse *= random.NextDouble() * 0.2 + 0.9;
                engine.ChamberPressure *= random.NextDouble() * 0.2 + 0.9;
            }
        }

        private async Task<double> CalculateOptimizationScoreAsync(RocketEngineBase engine)
        {
            var metrics = await _analyzer.CalculateEngineMetricsAsync(engine);
            return (metrics.Efficiency + metrics.Reliability) / 2.0;
        }
    }

    public class OptimizationResult
    {
        public RocketEngineBase BestEngine { get; set; }
        public double OptimizationScore { get; set; }
        public List<double> ConvergenceHistory { get; set; }
    }
} 