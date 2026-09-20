using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace HB_NLP_Research_Lab.AI
{
    public class ReinforcementLearningEngine
    {
        private readonly Dictionary<string, double> _qTable = new Dictionary<string, double>();
        private readonly RLConfig _config;
        private readonly Random _random;

        /// <param name="random">
        /// Supply a seeded instance to make exploration reproducible. Verification of an
        /// epsilon-greedy policy is only possible when the exploration draw is controlled,
        /// and a control algorithm whose behaviour cannot be reproduced cannot be reviewed.
        /// </param>
        public ReinforcementLearningEngine(RLConfig config, Random? random = null)
        {
            _config = config;
            _random = random ?? new Random();
        }

        public async Task<EngineAction> SelectOptimalActionAsync(EngineState state)
        {
            // Epsilon-greedy strategy
            if (_random.NextDouble() < _config.Epsilon)
            {
                return GetRandomAction();
            }
            else
            {
                return await GetBestActionAsync(state);
            }
        }

        public async Task UpdateQValueAsync(EngineState state, EngineAction action, double reward, EngineState nextState)
        {
            var stateKey = QKey(state, action);

            var currentQ = _qTable.TryGetValue(stateKey, out var stored) ? stored : 0.0;

            // The lookahead term is max over actions of Q(nextState, a), which is what makes
            // this Q-learning rather than a running average of immediate reward. It must be
            // read with the same (state, action) key schema the update writes: keying it on
            // the next state alone reads an entry no update ever writes, pinning the term at
            // zero and silently removing the agent's ability to learn delayed consequences.
            var maxNextQ = MaxQOver(nextState);

            // Q-learning update rule
            var newQ = currentQ + _config.LearningRate * (reward + _config.DiscountFactor * maxNextQ - currentQ);
            _qTable[stateKey] = newQ;
            
            await Task.Delay(1); // Simulate computation
        }

        /// <summary>
        /// Highest action-value currently estimated for <paramref name="state"/>. Unvisited
        /// pairs are treated as 0.0, matching the optimistic-at-zero initialisation used when
        /// an update first touches a pair.
        /// </summary>
        internal double MaxQOver(EngineState state)
        {
            var best = double.MinValue;

            foreach (var action in Enum.GetValues<EngineAction>())
            {
                var value = _qTable.TryGetValue(QKey(state, action), out var stored) ? stored : 0.0;

                if (value > best)
                {
                    best = value;
                }
            }

            return best;
        }

        /// <summary>
        /// Single definition of the Q-table key schema. Every read and write goes through
        /// this so the lookahead term and the greedy policy cannot drift onto different
        /// key formats, which is how the lookahead term came to be permanently zero.
        /// </summary>
        internal static string QKey(EngineState state, EngineAction action) => $"{state}_{action}";

        internal double QValueFor(EngineState state, EngineAction action) =>
            _qTable.TryGetValue(QKey(state, action), out var stored) ? stored : 0.0;

        public async Task TrainAsync(int episodes)
        {
            await Task.CompletedTask;
            Console.WriteLine("🎯 Training Reinforcement Learning Engine for Autonomous Control...");
            
            for (int episode = 0; episode < episodes; episode++)
            {
                var state = new EngineState
                {
                    Thrust = _random.NextDouble() * 1000 + 500,
                    Temperature = _random.NextDouble() * 500 + 1000,
                    Pressure = _random.NextDouble() * 100 + 50,
                    FuelFlow = _random.NextDouble() * 10 + 5
                };

                double totalReward = 0;
                
                for (int step = 0; step < 100; step++)
                {
                    var action = await SelectOptimalActionAsync(state);
                    var nextState = await SimulateActionAsync(state, action);
                    var reward = CalculateReward(state, action, nextState);
                    
                    await UpdateQValueAsync(state, action, reward, nextState);
                    
                    totalReward += reward;
                    state = nextState;
                    
                    await Task.Delay(5); // Simulate environment step
                }
                
                if (episode % 10 == 0)
                {
                    Console.WriteLine($"Episode {episode}: Total Reward = {totalReward:F2}");
                }
                
                // Decay epsilon
                _config.Epsilon *= 0.995;
            }
        }

        internal async Task<EngineAction> GetBestActionAsync(EngineState state)
        {
            var actions = Enum.GetValues<EngineAction>();
            var bestAction = actions[0];
            var bestQ = double.MinValue;
            
            foreach (var action in actions)
            {
                var qValue = QValueFor(state, action);
                
                if (qValue > bestQ)
                {
                    bestQ = qValue;
                    bestAction = action;
                }
            }
            
            await Task.Delay(1);
            return bestAction;
        }

        private EngineAction GetRandomAction()
        {
            var actions = Enum.GetValues<EngineAction>();
            return actions[_random.Next(actions.Length)];
        }

        internal async Task<EngineState> SimulateActionAsync(EngineState state, EngineAction action)
        {
            var nextState = new EngineState
            {
                Thrust = state.Thrust,
                Temperature = state.Temperature,
                Pressure = state.Pressure,
                FuelFlow = state.FuelFlow
            };
            
            switch (action)
            {
                case EngineAction.IncreaseThrust:
                    nextState.Thrust = Math.Min(state.Thrust * 1.1, 1500);
                    nextState.Temperature += 50;
                    nextState.FuelFlow += 0.5;
                    break;
                case EngineAction.DecreaseThrust:
                    nextState.Thrust = Math.Max(state.Thrust * 0.9, 300);
                    nextState.Temperature -= 30;
                    nextState.FuelFlow -= 0.3;
                    break;
                case EngineAction.IncreaseFuelFlow:
                    nextState.FuelFlow = Math.Min(state.FuelFlow * 1.2, 15);
                    nextState.Thrust *= 1.05;
                    nextState.Temperature += 20;
                    break;
                case EngineAction.DecreaseFuelFlow:
                    nextState.FuelFlow = Math.Max(state.FuelFlow * 0.8, 2);
                    nextState.Thrust *= 0.95;
                    nextState.Temperature -= 15;
                    break;
                case EngineAction.MaintainOptimal:
                    // Keep current state with slight variations
                    nextState.Temperature += (_random.NextDouble() - 0.5) * 10;
                    nextState.Pressure += (_random.NextDouble() - 0.5) * 5;
                    break;
            }
            
            await Task.Delay(1);
            return nextState;
        }

        internal static double CalculateReward(EngineState state, EngineAction action, EngineState nextState)
        {
            var reward = 0.0;
            
            // Reward for maintaining optimal thrust range
            if (nextState.Thrust >= 800 && nextState.Thrust <= 1200)
            {
                reward += 10;
            }
            else
            {
                reward -= 5;
            }
            
            // Penalty for excessive temperature
            if (nextState.Temperature > 1400)
            {
                reward -= 20;
            }
            
            // Reward for efficient fuel usage
            if (nextState.FuelFlow < 8)
            {
                reward += 5;
            }
            
            // Penalty for too low fuel flow
            if (nextState.FuelFlow < 3)
            {
                reward -= 10;
            }
            
            return reward;
        }
    }

    public class EngineState
    {
        public double Thrust { get; set; }
        public double Temperature { get; set; }
        public double Pressure { get; set; }
        public double FuelFlow { get; set; }

        public override string ToString()
        {
            return $"T:{Thrust:F0}_Temp:{Temperature:F0}_P:{Pressure:F0}_F:{FuelFlow:F1}";
        }
    }

    public enum EngineAction
    {
        IncreaseThrust,
        DecreaseThrust,
        IncreaseFuelFlow,
        DecreaseFuelFlow,
        MaintainOptimal
    }

    public class RLConfig
    {
        public double LearningRate { get; set; } = 0.1;
        public double DiscountFactor { get; set; } = 0.9;
        public double Epsilon { get; set; } = 0.1;
        public int MaxEpisodes { get; set; } = 1000;
    }
} 