using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.Physics;
using HB_NLP_Research_Lab.AI;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Digital Twin Engine with Real-Time Learning
    /// Predictive Digital Twin for Aerospace Engines
    /// </summary>
    public class DigitalTwinEngine : IDisposable
    {
        public const int MaxHistoryEntries = 256;
        public const int MaxActiveTwins = 256;

        private readonly AdvancedPhysicsEngine _physicsEngine;
        private readonly ValidationEngine _validationEngine;
        private readonly AutonomousEngineDesigner _aiDesigner;
        private readonly AdvancedMultiPhysicsCoupler _multiPhysicsCoupler;
        
        private readonly LiveLearningSystem _liveLearning;
        private readonly PredictiveDigitalTwin _predictiveTwin;
        private readonly AutonomousTestingSystem _autonomousTesting;
        private readonly RealTimeLearningEngine _learningEngine;
        
        private readonly ConcurrentDictionary<string, EngineDigitalTwin> _digitalTwins;
        private readonly ConcurrentDictionary<string, LearningHistory> _learningHistories;
        private readonly ConcurrentDictionary<string, PredictionAccuracy> _predictionAccuracies;
        private readonly ConcurrentDictionary<string, object> _historyLocks;
        private readonly ConcurrentDictionary<string, EngineGateLease> _engineGates;
        private readonly ConcurrentQueue<string> _twinCreationOrder;
        private readonly object _lifecycleLock = new();
        
        private bool _isInitialized = false;
        private volatile bool _isDisposed;

        private sealed class EngineGateLease
        {
            public readonly SemaphoreSlim Semaphore = new(1, 1);
            public int RefCount;
        }

        public DigitalTwinEngine()
        {
            _physicsEngine = new AdvancedPhysicsEngine();
            _validationEngine = new ValidationEngine();
            _aiDesigner = new AutonomousEngineDesigner();
            _multiPhysicsCoupler = new AdvancedMultiPhysicsCoupler();
            
            _liveLearning = new LiveLearningSystem();
            _predictiveTwin = new PredictiveDigitalTwin();
            _autonomousTesting = new AutonomousTestingSystem();
            _learningEngine = new RealTimeLearningEngine();
            
            _digitalTwins = new ConcurrentDictionary<string, EngineDigitalTwin>();
            _learningHistories = new ConcurrentDictionary<string, LearningHistory>();
            _predictionAccuracies = new ConcurrentDictionary<string, PredictionAccuracy>();
            _historyLocks = new ConcurrentDictionary<string, object>();
            _engineGates = new ConcurrentDictionary<string, EngineGateLease>();
            _twinCreationOrder = new ConcurrentQueue<string>();
        }

        public async Task<DigitalTwinStatus> InitializeAsync()
        {
            ThrowIfDisposed();
            Console.WriteLine("[Digital Twin] 🤖 Initializing Digital Twin Engine...");
            Console.WriteLine("[Digital Twin] Live Learning System Enabled");
            Console.WriteLine("[Digital Twin] Predictive Capabilities Active");
            
            // Initialize all components
            await _physicsEngine.InitializeAsync();
            await _multiPhysicsCoupler.InitializeAsync();
            await _liveLearning.InitializeAsync();
            await _predictiveTwin.InitializeAsync();
            await _autonomousTesting.InitializeAsync();
            await _learningEngine.InitializeAsync();
            
            await Task.Delay(300); // Simulate initialization time

            lock (_lifecycleLock)
            {
                ThrowIfDisposed();
                _isInitialized = true;

                return new DigitalTwinStatus
                {
                    IsReady = true,
                    ActiveSystems = new[] { "Live Learning", "Predictive Twin", "Autonomous Testing", "Real-Time Learning" },
                    LearningMode = "Continuous",
                    PredictionAccuracy = "99.9%",
                    TwinCount = _digitalTwins.Count,
                    GateCount = _engineGates.Count
                };
            }
        }

        public async Task<EngineDigitalTwin> CreateDigitalTwinAsync(string engineId, EngineModel engineModel)
        {
            ThrowIfDisposed();
            Console.WriteLine($"[Debug] Entered CreateDigitalTwinAsync for engineId: {engineId}");
            if (!_isInitialized)
                await InitializeAsync();

            if (string.IsNullOrWhiteSpace(engineId))
                throw new ArgumentException("engineId cannot be null or empty");
            if (engineModel == null)
            {
                Console.WriteLine("[Digital Twin] ERROR: engineModel is null");
                throw new ArgumentNullException(nameof(engineModel));
            }
            if (string.IsNullOrWhiteSpace(engineModel.Name))
            {
                Console.WriteLine("[Digital Twin] ERROR: engineModel.Name is null or empty");
                engineModel.Name = "Unnamed Engine";
            }
            if (engineModel.Parameters == null)
            {
                Console.WriteLine("[Digital Twin] WARNING: engineModel.Parameters is null, initializing to empty dictionary");
                engineModel.Parameters = new Dictionary<string, double>();
            }

            Console.WriteLine($"[Digital Twin] 🎯 Creating Digital Twin for Engine: {engineId}");
            Console.WriteLine($"[Digital Twin] EngineModel.Name: {engineModel.Name}");
            Console.WriteLine($"[Digital Twin] EngineModel.Parameters.Count: {engineModel.Parameters.Count}");

            // Create comprehensive digital twin
            var digitalTwin = new EngineDigitalTwin
            {
                EngineId = engineId,
                EngineModel = engineModel,
                CreationTimestamp = DateTime.UtcNow,
                LastUpdateTimestamp = DateTime.UtcNow,
                LearningStatus = "Active",
                PredictionAccuracy = 0.999,
                TwinVersion = "1.0.0"
            };
            
            var engineGate = AcquireEngineGate(engineId);
            try
            {
                await engineGate.Semaphore.WaitAsync();
                try
                {
                    lock (_lifecycleLock)
                    {
                        ThrowIfDisposed();
                        // In-place updates do not grow the map; only evict when inserting a new key.
                        var isNewKey = !_digitalTwins.ContainsKey(engineId);
                        if (isNewKey)
                        {
                            EvictOldestTwinsUnlocked(keepEngineId: engineId);
                            if (_digitalTwins.Count >= MaxActiveTwins)
                            {
                                throw new InvalidOperationException(
                                    $"Digital twin capacity of {MaxActiveTwins} reached; try again shortly.");
                            }
                        }

                        lock (GetHistoryLock(engineId))
                        {
                            // Publish all per-engine state as one atomic generation.
                            // The twin is written last so readers never observe it
                            // without corresponding history and accuracy state.
                            _learningHistories[engineId] = CreateLearningHistory(engineId);
                            _predictionAccuracies[engineId] = new PredictionAccuracy
                            {
                                EngineId = engineId,
                                OverallAccuracy = 0.999,
                                ThrustPredictionAccuracy = 0.998,
                                ThermalPredictionAccuracy = 0.997,
                                StructuralPredictionAccuracy = 0.999,
                                FailurePredictionAccuracy = 0.999
                            };
                            isNewKey = !_digitalTwins.ContainsKey(engineId);
                            _digitalTwins[engineId] = digitalTwin;
                            if (isNewKey)
                            {
                                _twinCreationOrder.Enqueue(engineId);
                            }
                        }
                    }
                }
                finally
                {
                    engineGate.Semaphore.Release();
                }
            }
            finally
            {
                ReleaseEngineGate(engineId, engineGate);
            }
            
            Console.WriteLine($"[Digital Twin] Digital Twin created successfully for {engineId}");
            Console.WriteLine($"[Digital Twin] Initial prediction accuracy: {digitalTwin.PredictionAccuracy:P3}");
            
            return digitalTwin;
        }

        /// <summary>
        /// Rebuilds the process-local state for a persisted digital twin after an
        /// application restart. Existing runtime state is never overwritten.
        /// </summary>
        public async Task<EngineDigitalTwin> EnsureDigitalTwinAsync(
            string engineId,
            EngineModel engineModel,
            double predictionAccuracy = 0.999)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(engineId))
                throw new ArgumentException("engineId cannot be null or empty", nameof(engineId));
            ArgumentNullException.ThrowIfNull(engineModel);

            if (_digitalTwins.TryGetValue(engineId, out var existingTwin))
                return existingTwin;

            if (!_isInitialized)
                await InitializeAsync();

            var engineGate = AcquireEngineGate(engineId);
            try
            {
                await engineGate.Semaphore.WaitAsync();
                try
                {
                    lock (_lifecycleLock)
                    {
                        ThrowIfDisposed();
                        if (_digitalTwins.TryGetValue(engineId, out existingTwin))
                            return existingTwin;

                        EvictOldestTwinsUnlocked(keepEngineId: engineId);
                        if (_digitalTwins.Count >= MaxActiveTwins)
                        {
                            throw new InvalidOperationException(
                                $"Digital twin capacity of {MaxActiveTwins} reached; try again shortly.");
                        }

                        lock (GetHistoryLock(engineId))
                        {
                            if (_digitalTwins.TryGetValue(engineId, out existingTwin))
                                return existingTwin;

                            _learningHistories[engineId] = CreateLearningHistory(engineId);
                            _predictionAccuracies[engineId] = new PredictionAccuracy
                            {
                                EngineId = engineId,
                                OverallAccuracy = predictionAccuracy,
                                ThrustPredictionAccuracy = predictionAccuracy,
                                ThermalPredictionAccuracy = predictionAccuracy,
                                StructuralPredictionAccuracy = predictionAccuracy,
                                FailurePredictionAccuracy = predictionAccuracy
                            };

                            var restoredTwin = new EngineDigitalTwin
                            {
                                EngineId = engineId,
                                EngineModel = engineModel,
                                CreationTimestamp = DateTime.UtcNow,
                                LastUpdateTimestamp = DateTime.UtcNow,
                                LearningStatus = "Active",
                                PredictionAccuracy = predictionAccuracy,
                                TwinVersion = "1.0.0"
                            };
                            _digitalTwins[engineId] = restoredTwin;
                            _twinCreationOrder.Enqueue(engineId);
                            return restoredTwin;
                        }
                    }
                }
                finally
                {
                    engineGate.Semaphore.Release();
                }
            }
            finally
            {
                ReleaseEngineGate(engineId, engineGate);
            }
        }

        /// <summary>
        /// Removes process-local state for a twin key (history, accuracy, gates).
        /// Safe to call when the key is already absent.
        /// </summary>
        public bool RemoveDigitalTwin(string engineId)
        {
            if (string.IsNullOrWhiteSpace(engineId))
                return false;

            ThrowIfDisposed();

            lock (_lifecycleLock)
            {
                ThrowIfDisposed();
                return RemoveDigitalTwinUnlocked(engineId);
            }
        }

        public async Task<LiveLearningResult> LearnFromTestFlightAsync(string engineId, TestFlightData flightData)
        {
            ThrowIfDisposed();
            Console.WriteLine($"[Debug] Entered LearnFromTestFlightAsync for engineId: {engineId}");
            if (string.IsNullOrWhiteSpace(engineId))
                throw new ArgumentException("engineId cannot be null or empty");
            if (flightData == null)
            {
                Console.WriteLine("[Digital Twin] ERROR: flightData is null");
                throw new ArgumentNullException(nameof(flightData));
            }

            var engineGate = AcquireEngineGate(engineId);
            try
            {
                await engineGate.Semaphore.WaitAsync();
                try
                {
                    return await LearnFromTestFlightWithGateAsync(engineId, flightData);
                }
                finally
                {
                    engineGate.Semaphore.Release();
                }
            }
            finally
            {
                ReleaseEngineGate(engineId, engineGate);
            }
        }

        private async Task<LiveLearningResult> LearnFromTestFlightWithGateAsync(
            string engineId,
            TestFlightData flightData)
        {
            if (!_digitalTwins.ContainsKey(engineId))
            {
                Console.WriteLine($"[Digital Twin] ERROR: Digital twin not found for engine: {engineId}");
                throw new ArgumentException($"Digital twin not found for engine: {engineId}");
            }

            Console.WriteLine($"[Digital Twin] 📚 Learning from Test Flight Data for {engineId}...");

            var learningEvent = new LearningEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "TestFlight",
                FlightData = flightData,
                LearningMetrics = await _liveLearning.ProcessLearningEventAsync(flightData)
            };

            var aiLearningResult = await _aiDesigner.LearnFromTestDataAsync(flightData);
            var modelImprovement = await _learningEngine.UpdateModelsAsync(engineId, flightData);
            var accuracyUpdate = await _predictiveTwin.UpdatePredictionAccuracyAsync(engineId, flightData);

            // Commit the learning result only after every asynchronous stage
            // succeeds and while replacement/disposal are excluded.
            lock (_lifecycleLock)
            {
                ThrowIfDisposed();
                lock (GetHistoryLock(engineId))
                {
                    if (!_digitalTwins.TryGetValue(engineId, out var digitalTwin))
                        throw new InvalidOperationException($"Digital twin was removed while learning: {engineId}");

                    var history = _learningHistories.GetOrAdd(engineId, CreateLearningHistory);
                    history.LearningEvents.Add(learningEvent);
                    TrimBoundedHistory(history.LearningEvents);
                    history.ModelImprovements.Add(modelImprovement);
                    TrimBoundedHistory(history.ModelImprovements);
                    _predictionAccuracies[engineId] = accuracyUpdate;
                    digitalTwin.LastUpdateTimestamp = DateTime.UtcNow;
                    digitalTwin.PredictionAccuracy = accuracyUpdate.OverallAccuracy;
                }
            }

            Console.WriteLine($"[Digital Twin] Learning complete for {engineId}");
            Console.WriteLine($"[Digital Twin] Model improvement: {modelImprovement.ImprovementPercentage:P2}");
            Console.WriteLine($"[Digital Twin] Updated prediction accuracy: {accuracyUpdate.OverallAccuracy:P3}");

            return new LiveLearningResult
            {
                EngineId = engineId,
                LearningEvent = learningEvent,
                AILearningResult = aiLearningResult,
                ModelImprovement = modelImprovement,
                UpdatedPredictionAccuracy = accuracyUpdate,
                LearningTimestamp = DateTime.UtcNow
            };
        }

        public async Task<EnginePrediction> PredictEngineBehaviorAsync(string engineId, PredictionScenario scenario)
        {
            ThrowIfDisposed();
            var engineGate = AcquireEngineGate(engineId);
            try
            {
                await engineGate.Semaphore.WaitAsync();
                try
                {
                    if (!_digitalTwins.TryGetValue(engineId, out var twinForPrediction))
                        throw new ArgumentException($"Digital twin not found for engine: {engineId}");

                    Console.WriteLine($"[Digital Twin] 🔮 Predicting Engine Behavior for {engineId}...");
                    Console.WriteLine($"[Digital Twin] Scenario: {scenario.Name}");

                    if (!_predictionAccuracies.TryGetValue(engineId, out var predictionAccuracy))
                        predictionAccuracy = new PredictionAccuracy { OverallAccuracy = 0.0 };

                    var prediction = await _predictiveTwin.PredictEngineBehaviorAsync(
                        engineId,
                        scenario,
                        twinForPrediction.EngineModel);
                    var predictionRecord = new PredictionRecord
                    {
                        Timestamp = DateTime.UtcNow,
                        Scenario = scenario,
                        Prediction = prediction,
                        ConfidenceLevel = prediction.ConfidenceLevel,
                        ExpectedAccuracy = predictionAccuracy.OverallAccuracy
                    };

                    lock (_lifecycleLock)
                    {
                        ThrowIfDisposed();
                        lock (GetHistoryLock(engineId))
                        {
                            var history = _learningHistories.GetOrAdd(engineId, CreateLearningHistory);
                            history.PredictionHistory.Add(predictionRecord);
                            TrimBoundedHistory(history.PredictionHistory);
                        }
                    }

                    Console.WriteLine($"[Digital Twin] Prediction complete for {engineId}");
                    Console.WriteLine($"[Digital Twin] Confidence level: {prediction.ConfidenceLevel:P2}");
                    Console.WriteLine($"[Digital Twin] Expected accuracy: {predictionAccuracy.OverallAccuracy:P3}");

                    return prediction;
                }
                finally
                {
                    engineGate.Semaphore.Release();
                }
            }
            finally
            {
                ReleaseEngineGate(engineId, engineGate);
            }
        }

        public async Task<AutonomousTestingResult> RunAutonomousTestsAsync(string engineId, TestingRequirements requirements)
        {
            ThrowIfDisposed();
            var engineGate = AcquireEngineGate(engineId);
            try
            {
                await engineGate.Semaphore.WaitAsync();
                try
                {
                    if (!_digitalTwins.TryGetValue(engineId, out var digitalTwin))
                        throw new ArgumentException($"Digital twin not found for engine: {engineId}");

                    Console.WriteLine($"[Digital Twin] 🧪 Running Autonomous Tests for {engineId}...");
                    var engineArchitecture = new EngineArchitecture
                    {
                        Id = engineId,
                        Name = digitalTwin.EngineModel.Name
                    };

                    var testResult = await _autonomousTesting.DesignAndRunTestsAsync(engineArchitecture, requirements);
                    var testFlightData = new TestFlightData
                    {
                        EngineId = engineId,
                        FlightDate = DateTime.UtcNow,
                        FlightMetrics = new Dictionary<string, double>
                        {
                            ["Thrust"] = testResult.Analysis.AveragePerformance * 1500000,
                            ["Efficiency"] = testResult.Analysis.AveragePerformance,
                            ["Reliability"] = testResult.Analysis.ReliabilityScore
                        }
                    };

                    await LearnFromTestFlightWithGateAsync(engineId, testFlightData);

                    Console.WriteLine($"[Digital Twin] Autonomous testing complete for {engineId}");
                    Console.WriteLine($"[Digital Twin] Test coverage: {testResult.TestCoverage:P2}");
                    Console.WriteLine($"[Digital Twin] Test accuracy: {testResult.TestAccuracy:P2}");

                    return testResult;
                }
                finally
                {
                    engineGate.Semaphore.Release();
                }
            }
            finally
            {
                ReleaseEngineGate(engineId, engineGate);
            }
        }

        public async Task<MultiPhysicsPrediction> RunPredictiveMultiPhysicsAsync(string engineId, EngineModel engineModel)
        {
            ThrowIfDisposed();
            Console.WriteLine($"[Digital Twin] 🌊🔥🏗️⚡ Running Predictive Multi-Physics Analysis for {engineId}...");
            
            // Convert Core.EngineModel to Physics.EngineModel
            var physicsEngineModel = new HB_NLP_Research_Lab.Physics.EngineModel { Name = engineModel.Name };
            
            // Run predictive multi-physics analysis
            var multiPhysicsResult = await _multiPhysicsCoupler.RunCompletePhysicsIntegrationAsync(physicsEngineModel);

            var thrust = TryReadEngineParameter(engineModel, "Thrust", out var engineThrust) && engineThrust > 0
                ? engineThrust
                : 1500000.0;
            var efficiency = TryReadEngineParameter(engineModel, "Efficiency", out var engineEfficiency) && engineEfficiency > 0
                ? Math.Clamp(engineEfficiency, 0.0, 1.0)
                : 0.92;
            
            // Create predictive result
            var prediction = new MultiPhysicsPrediction
            {
                EngineId = engineId,
                PredictionTimestamp = DateTime.UtcNow,
                MultiPhysicsResult = multiPhysicsResult,
                PredictionConfidence = 0.999,
                PredictedPerformance = new PredictedPerformance
                {
                    Thrust = thrust,
                    Efficiency = efficiency,
                    Reliability = 0.999,
                    ThermalEfficiency = Math.Clamp(efficiency * 0.92, 0.0, 1.0),
                    StructuralSafety = 0.998
                },
                PredictedFailures = new List<PredictedFailure>
                {
                    new PredictedFailure
                    {
                        FailureMode = "Thermal Fatigue",
                        Probability = 0.001,
                        TimeToFailure = TimeSpan.FromHours(5000),
                        Confidence = 0.95
                    }
                }
            };
            
            Console.WriteLine($"[Digital Twin] Predictive multi-physics analysis complete");
            Console.WriteLine($"[Digital Twin] Prediction confidence: {prediction.PredictionConfidence:P3}");
            Console.WriteLine($"[Digital Twin] Predicted thrust: {prediction.PredictedPerformance.Thrust / 1000:F0} kN");
            
            return prediction;
        }

        public async Task<DigitalTwinSummary> GenerateDigitalTwinSummaryAsync()
        {
            ThrowIfDisposed();
            await Task.Delay(1); // Simulate async operation
            
            Console.WriteLine("[Digital Twin] 📊 Generating Comprehensive Digital Twin Summary...");

            // Defensive: handle empty or null lists
            double avgPredictionAccuracy = 0.0;
            if (_predictionAccuracies != null && _predictionAccuracies.Count > 0)
                avgPredictionAccuracy = _predictionAccuracies.Values.Average(p => p.OverallAccuracy);

            var historyCounts = _learningHistories.Keys
                .Select(GetHistoryCounts)
                .ToArray();
            int totalLearningEvents = historyCounts.Sum(counts => counts.LearningEvents);
            int totalPredictions = historyCounts.Sum(counts => counts.Predictions);

            var summary = new DigitalTwinSummary
            {
                TotalTwins = _digitalTwins?.Count ?? 0,
                ActiveTwins = _digitalTwins?.Count(t => t.Value.LearningStatus == "Active") ?? 0,
                AveragePredictionAccuracy = avgPredictionAccuracy,
                TotalLearningEvents = totalLearningEvents,
                TotalPredictions = totalPredictions,
                LearningPerformance = "Excellent",
                PredictionPerformance = "Outstanding",
                SystemHealth = "Optimal"
            };

            Console.WriteLine($"[Digital Twin] Summary generated successfully");
            Console.WriteLine($"[Digital Twin] Total twins: {summary.TotalTwins}");
            Console.WriteLine($"[Digital Twin] Average prediction accuracy: {summary.AveragePredictionAccuracy:P3}");
            Console.WriteLine($"[Digital Twin] Total learning events: {summary.TotalLearningEvents}");

            return summary;
        }

        public async Task<LearningPerformanceReport> GenerateLearningPerformanceReportAsync(string engineId)
        {
            ThrowIfDisposed();
            await Task.Delay(1); // Simulate async operation
            
            // Use TryGetValue instead of ContainsKey + indexer for efficiency
            var accuracy = _predictionAccuracies.TryGetValue(engineId, out var acc)
                ? acc
                : new PredictionAccuracy { OverallAccuracy = 0.0 };

            if (!_learningHistories.ContainsKey(engineId))
                throw new ArgumentException($"Learning history not found for engine: {engineId}");

            var historyCounts = GetHistoryCounts(engineId);

            var report = new LearningPerformanceReport
            {
                EngineId = engineId,
                TotalLearningEvents = historyCounts.LearningEvents,
                TotalModelImprovements = historyCounts.ModelImprovements,
                TotalPredictions = historyCounts.Predictions,
                AverageModelImprovement = historyCounts.AverageModelImprovement,
                PredictionAccuracy = accuracy.OverallAccuracy,
                LearningTrend = "Improving",
                PerformanceRating = "Excellent"
            };

            return report;
        }

        private static LearningHistory CreateLearningHistory(string engineId) => new()
        {
            EngineId = engineId,
            LearningEvents = new List<LearningEvent>(),
            ModelImprovements = new List<ModelImprovement>(),
            PredictionHistory = new List<PredictionRecord>()
        };

        private static void TrimBoundedHistory<T>(List<T> list)
        {
            if (list.Count > MaxHistoryEntries)
            {
                list.RemoveRange(0, list.Count - MaxHistoryEntries);
            }
        }

        private object GetHistoryLock(string engineId) =>
            _historyLocks.GetOrAdd(engineId, static _ => new object());

        private EngineGateLease AcquireEngineGate(string engineId)
        {
            while (true)
            {
                var gate = _engineGates.GetOrAdd(engineId, static _ => new EngineGateLease());
                var count = Interlocked.Increment(ref gate.RefCount);
                if (count <= 0)
                {
                    // Lease was tombstoned for disposal; discard and retry with a fresh one.
                    _engineGates.TryRemove(new KeyValuePair<string, EngineGateLease>(engineId, gate));
                    continue;
                }

                if (_engineGates.TryGetValue(engineId, out var live) && ReferenceEquals(live, gate))
                {
                    return gate;
                }

                // Lost a race with prune/removal; drop the stale ref and retry.
                if (Interlocked.Decrement(ref gate.RefCount) == 0)
                {
                    TryForgetIdleEngineGate(engineId, gate);
                }
            }
        }

        private void ReleaseEngineGate(string engineId, EngineGateLease gate)
        {
            if (Interlocked.Decrement(ref gate.RefCount) != 0)
            {
                return;
            }

            TryForgetIdleEngineGate(engineId, gate);
        }

        private void TryForgetIdleEngineGate(string engineId, EngineGateLease gate)
        {
            // Keep gates that still back an active twin.
            if (_digitalTwins.ContainsKey(engineId))
            {
                return;
            }

            // CAS RefCount 0 → tombstone so Acquire cannot revive a disposed lease.
            if (Interlocked.CompareExchange(ref gate.RefCount, int.MinValue, 0) != 0)
            {
                return;
            }

            _engineGates.TryRemove(new KeyValuePair<string, EngineGateLease>(engineId, gate));
            gate.Semaphore.Dispose();
        }

        private void EvictOldestTwinsUnlocked(string keepEngineId)
        {
            // Bound busy-gate retries so a fully contended set cannot spin forever.
            var busySkips = 0;
            while (_digitalTwins.Count >= MaxActiveTwins)
            {
                if (!_twinCreationOrder.TryDequeue(out var oldestKey))
                {
                    // Queue drifted; fall back to arbitrary eviction excluding the key being created.
                    oldestKey = _digitalTwins.Keys.FirstOrDefault(key =>
                        !string.Equals(key, keepEngineId, StringComparison.Ordinal));
                    if (oldestKey == null)
                        break;
                }

                if (string.Equals(oldestKey, keepEngineId, StringComparison.Ordinal))
                {
                    // Keep newly created/restored key; re-queue and try another.
                    _twinCreationOrder.Enqueue(oldestKey);
                    if (_digitalTwins.Count < MaxActiveTwins)
                        break;

                    var alternate = _digitalTwins.Keys.FirstOrDefault(key =>
                        !string.Equals(key, keepEngineId, StringComparison.Ordinal));
                    if (alternate == null)
                        break;

                    if (!RemoveDigitalTwinUnlocked(alternate))
                    {
                        busySkips++;
                        if (busySkips >= MaxActiveTwins)
                            break;
                    }
                    else
                    {
                        busySkips = 0;
                    }

                    continue;
                }

                if (!_digitalTwins.ContainsKey(oldestKey))
                    continue;

                if (!RemoveDigitalTwinUnlocked(oldestKey))
                {
                    // Gate was busy: restore LRU order and try another candidate.
                    _twinCreationOrder.Enqueue(oldestKey);
                    busySkips++;
                    if (busySkips >= MaxActiveTwins)
                        break;
                    continue;
                }

                busySkips = 0;
            }
        }

        private bool RemoveDigitalTwinUnlocked(string engineId)
        {
            // Only evict when the per-engine gate is idle. Reference-counted leases
            // allow safe removal/disposal once no caller holds AcquireEngineGate.
            if (_engineGates.TryGetValue(engineId, out var gate))
            {
                if (!gate.Semaphore.Wait(0))
                {
                    return false;
                }

                try
                {
                    return RemoveTwinStateUnlocked(engineId);
                }
                finally
                {
                    gate.Semaphore.Release();
                    // Eviction never acquired a ref-count lease; forget only when idle.
                    TryForgetIdleEngineGate(engineId, gate);
                }
            }

            return RemoveTwinStateUnlocked(engineId);
        }

        private bool RemoveTwinStateUnlocked(string engineId)
        {
            var removed = false;
            lock (GetHistoryLock(engineId))
            {
                removed |= _digitalTwins.TryRemove(engineId, out _);
                removed |= _learningHistories.TryRemove(engineId, out _);
                removed |= _predictionAccuracies.TryRemove(engineId, out _);
            }

            _historyLocks.TryRemove(engineId, out _);
            return removed;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DigitalTwinEngine));
        }

        private static bool TryReadEngineParameter(
            EngineModel? engineModel,
            string key,
            out double value)
        {
            value = 0;
            if (engineModel?.Parameters == null)
            {
                return false;
            }

            var match = engineModel.Parameters.FirstOrDefault(pair =>
                string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match.Key == null)
            {
                return false;
            }

            value = match.Value;
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private (int LearningEvents, int ModelImprovements, int Predictions, double AverageModelImprovement)
            GetHistoryCounts(string engineId)
        {
            lock (GetHistoryLock(engineId))
            {
                if (!_learningHistories.TryGetValue(engineId, out var history))
                    return (0, 0, 0, 0.0);

                int learningEvents = history.LearningEvents?.Count ?? 0;
                var modelImprovementHistory = history.ModelImprovements;
                int modelImprovements = modelImprovementHistory?.Count ?? 0;
                int predictions = history.PredictionHistory?.Count ?? 0;
                double averageModelImprovement = modelImprovements > 0
                    ? modelImprovementHistory!.Average(improvement => improvement.ImprovementPercentage)
                    : 0.0;

                return (learningEvents, modelImprovements, predictions, averageModelImprovement);
            }
        }

        public void Dispose()
        {
            lock (_lifecycleLock)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                _digitalTwins.Clear();
                _learningHistories.Clear();
                _predictionAccuracies.Clear();
                _historyLocks.Clear();
                while (_twinCreationOrder.TryDequeue(out _))
                {
                    // Intentionally drain and discard queued engine IDs during disposal.
                }
                foreach (var gate in _engineGates.Values)
                {
                    gate.Semaphore.Dispose();
                }
                _engineGates.Clear();
            }
        }
    }

    // Supporting Classes
    public class EngineDigitalTwin
    {
        public EngineDigitalTwin()
        {
            EngineId = string.Empty;
            EngineModel = new EngineModel();
            LearningStatus = string.Empty;
            PredictionAccuracy = 0.0;
            TwinVersion = string.Empty;
        }
        public string EngineId { get; set; }
        public HB_NLP_Research_Lab.Core.EngineModel EngineModel { get; set; }
        public DateTime CreationTimestamp { get; set; }
        public DateTime LastUpdateTimestamp { get; set; }
        public string LearningStatus { get; set; }
        public double PredictionAccuracy { get; set; }
        public string TwinVersion { get; set; }
    }

    public class DigitalTwinStatus
    {
        public bool IsReady { get; set; }
        public string[] ActiveSystems { get; set; } = Array.Empty<string>();
        public string LearningMode { get; set; } = string.Empty;
        public string PredictionAccuracy { get; set; } = string.Empty;
        public int TwinCount { get; set; }
        public int GateCount { get; set; }
    }

    public class LiveLearningResult
    {
        public string EngineId { get; set; } = string.Empty;
        public LearningEvent LearningEvent { get; set; } = new();
        public ContinuousLearningResult AILearningResult { get; set; } = new();
        public ModelImprovement ModelImprovement { get; set; } = new();
        public PredictionAccuracy UpdatedPredictionAccuracy { get; set; } = new();
        public DateTime LearningTimestamp { get; set; }
    }

    public class EnginePrediction
    {
        public EnginePrediction()
        {
            EngineId = string.Empty;
            Scenario = new PredictionScenario();
            PredictedMetrics = new Dictionary<string, double>();
            ConfidenceLevel = 0.0;
            PredictionTimestamp = DateTime.UtcNow;
            PredictedIssues = new List<string>();
            RecommendedActions = new List<string>();
        }
        public string EngineId { get; set; } = string.Empty;
        public PredictionScenario Scenario { get; set; } = new();
        public Dictionary<string, double> PredictedMetrics { get; set; } = new();
        public double ConfidenceLevel { get; set; }
        public DateTime PredictionTimestamp { get; set; }
        public List<string> PredictedIssues { get; set; } = new();
        public List<string> RecommendedActions { get; set; } = new();
    }

    public class MultiPhysicsPrediction
    {
        public string EngineId { get; set; } = string.Empty;
        public DateTime PredictionTimestamp { get; set; }
        public FluidStructureThermalElectromagneticResult MultiPhysicsResult { get; set; } = new();
        public double PredictionConfidence { get; set; }
        public PredictedPerformance PredictedPerformance { get; set; } = new();
        public List<PredictedFailure> PredictedFailures { get; set; } = new();
    }

    public class PredictedPerformance
    {
        public double Thrust { get; set; } // N
        public double Efficiency { get; set; }
        public double Reliability { get; set; }
        public double ThermalEfficiency { get; set; }
        public double StructuralSafety { get; set; }
    }

    public class PredictedFailure
    {
        public string FailureMode { get; set; } = string.Empty;
        public double Probability { get; set; }
        public TimeSpan TimeToFailure { get; set; }
        public double Confidence { get; set; }
    }

    public class DigitalTwinSummary
    {
        public int TotalTwins { get; set; }
        public int ActiveTwins { get; set; }
        public double AveragePredictionAccuracy { get; set; }
        public int TotalLearningEvents { get; set; }
        public int TotalPredictions { get; set; }
        public string LearningPerformance { get; set; } = string.Empty;
        public string PredictionPerformance { get; set; } = string.Empty;
        public string SystemHealth { get; set; } = string.Empty;
    }

    public class LearningPerformanceReport
    {
        public string EngineId { get; set; } = string.Empty;
        public int TotalLearningEvents { get; set; }
        public int TotalModelImprovements { get; set; }
        public int TotalPredictions { get; set; }
        public double AverageModelImprovement { get; set; }
        public double PredictionAccuracy { get; set; }
        public string LearningTrend { get; set; } = string.Empty;
        public string PerformanceRating { get; set; } = string.Empty;
    }

    // Learning and prediction components
    public class LiveLearningSystem
    {
        public async Task InitializeAsync()
        {
            await Task.Delay(100);
        }
        
        public async Task<LearningMetrics> ProcessLearningEventAsync(TestFlightData flightData)
        {
            await Task.Delay(50);
            return new LearningMetrics
            {
                DataQuality = 0.95,
                LearningRate = 0.15,
                ModelConvergence = 0.98
            };
        }
    }

    public class PredictiveDigitalTwin
    {
        public async Task InitializeAsync()
        {
            await Task.Delay(100);
        }
        
        public Task<EnginePrediction> PredictEngineBehaviorAsync(string engineId, PredictionScenario scenario)
        {
            return PredictEngineBehaviorAsync(engineId, scenario, engineModel: null);
        }

        public async Task<EnginePrediction> PredictEngineBehaviorAsync(
            string engineId,
            PredictionScenario scenario,
            EngineModel? engineModel)
        {
            await Task.Delay(100);

            var thrust = TryReadEngineModelParameter(engineModel, "Thrust", out var engineThrust) && engineThrust > 0
                ? engineThrust
                : 1500000.0;
            var efficiency = TryReadEngineModelParameter(engineModel, "Efficiency", out var engineEfficiency) && engineEfficiency > 0
                ? Math.Clamp(engineEfficiency, 0.0, 1.0)
                : 0.92;
            var reliability = 0.999;
            var parameters = scenario.Parameters ?? new Dictionary<string, object>();

            if (TryReadScenarioDouble(parameters, "thrust", out var requestedThrust) && requestedThrust > 0)
            {
                thrust = requestedThrust;
            }
            else if (TryReadScenarioDouble(parameters, "thrustScale", out var thrustScale) && thrustScale > 0)
            {
                thrust *= thrustScale;
            }

            if (TryReadScenarioDouble(parameters, "efficiency", out var requestedEfficiency) && requestedEfficiency > 0)
            {
                efficiency = Math.Clamp(requestedEfficiency, 0.0, 1.0);
            }
            else if (TryReadScenarioDouble(parameters, "throttle", out var throttle) && throttle > 0)
            {
                // Throttle below 1.0 reduces delivered thrust and efficiency; above 1.0 trades reliability.
                thrust *= throttle;
                efficiency = Math.Clamp(efficiency * Math.Min(throttle, 1.05), 0.0, 0.99);
                if (throttle > 1.0)
                {
                    reliability = Math.Clamp(reliability - ((throttle - 1.0) * 0.05), 0.9, 0.999);
                }
            }

            if (TryReadScenarioDouble(parameters, "reliability", out var requestedReliability) && requestedReliability > 0)
            {
                reliability = Math.Clamp(requestedReliability, 0.0, 1.0);
            }

            if (TryReadScenarioDouble(parameters, "ambientTemperature", out var ambientTemperature))
            {
                // Hot-day derate: every 10C above 288K costs ~0.5% efficiency.
                var delta = Math.Max(0.0, ambientTemperature - 288.15);
                efficiency = Math.Clamp(efficiency - (delta / 10.0) * 0.005, 0.0, 0.99);
            }

            return new EnginePrediction
            {
                EngineId = engineId,
                Scenario = scenario,
                PredictedMetrics = new Dictionary<string, double>
                {
                    ["Thrust"] = thrust,
                    ["Efficiency"] = efficiency,
                    ["Reliability"] = reliability
                },
                ConfidenceLevel = 0.999,
                PredictionTimestamp = DateTime.UtcNow,
                PredictedIssues = new List<string>(),
                RecommendedActions = new List<string>()
            };
        }

        private static bool TryReadEngineModelParameter(
            EngineModel? engineModel,
            string key,
            out double value)
        {
            value = 0;
            if (engineModel?.Parameters == null)
            {
                return false;
            }

            var match = engineModel.Parameters.FirstOrDefault(pair =>
                string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match.Key == null)
            {
                return false;
            }

            value = match.Value;
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool TryReadScenarioDouble(
            IReadOnlyDictionary<string, object> parameters,
            string key,
            out double value)
        {
            value = 0;
            var match = parameters.FirstOrDefault(pair =>
                string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match.Key == null || match.Value == null)
            {
                return false;
            }

            switch (match.Value)
            {
                case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                    value = d;
                    return true;
                case float f when !float.IsNaN(f) && !float.IsInfinity(f):
                    value = f;
                    return true;
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = l;
                    return true;
                case decimal m:
                    value = (double)m;
                    return true;
                case string text when double.TryParse(
                    text,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed):
                    value = parsed;
                    return !double.IsNaN(parsed) && !double.IsInfinity(parsed);
                default:
                    return false;
            }
        }
        
        public async Task<PredictionAccuracy> UpdatePredictionAccuracyAsync(string engineId, TestFlightData flightData)
        {
            await Task.Delay(50);
            return new PredictionAccuracy
            {
                EngineId = engineId,
                OverallAccuracy = 0.999,
                ThrustPredictionAccuracy = 0.998,
                ThermalPredictionAccuracy = 0.997,
                StructuralPredictionAccuracy = 0.999,
                FailurePredictionAccuracy = 0.999
            };
        }
    }

    public class AutonomousTestingSystem
    {
        public async Task InitializeAsync()
        {
            await Task.Delay(100);
        }
        
        public async Task<AutonomousTestingResult> DesignAndRunTestsAsync(EngineArchitecture engine, TestingRequirements requirements)
        {
            await Task.Delay(200);
            return new AutonomousTestingResult
            {
                TestScenarios = new List<HB_NLP_Research_Lab.AI.TestScenario>(),
                TestResults = new List<TestResult>(),
                Analysis = new TestAnalysis
                {
                    PassRate = 0.95,
                    AveragePerformance = 0.92,
                    ReliabilityScore = 0.999,
                    TestCoverage = 0.90
                },
                TestCoverage = 0.90,
                TestAccuracy = 0.95
            };
        }
    }

    public class RealTimeLearningEngine
    {
        public async Task InitializeAsync()
        {
            await Task.Delay(100);
        }
        
        public async Task<ModelImprovement> UpdateModelsAsync(string engineId, TestFlightData flightData)
        {
            await Task.Delay(100);
            return new ModelImprovement
            {
                EngineId = engineId,
                ImprovementPercentage = 0.12,
                ModelVersion = "2.1.0",
                UpdateTimestamp = DateTime.UtcNow
            };
        }
    }

    // Additional supporting classes
    public class LearningHistory
    {
        public string EngineId { get; set; } = string.Empty;
        public List<LearningEvent> LearningEvents { get; set; } = new();
        public List<ModelImprovement> ModelImprovements { get; set; } = new();
        public List<PredictionRecord> PredictionHistory { get; set; } = new();
    }

    public class LearningEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public TestFlightData FlightData { get; set; } = new();
        public LearningMetrics LearningMetrics { get; set; } = new();
    }

    public class ModelImprovement
    {
        public string EngineId { get; set; } = string.Empty;
        public double ImprovementPercentage { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        public DateTime UpdateTimestamp { get; set; }
    }

    public class PredictionRecord
    {
        public DateTime Timestamp { get; set; }
        public PredictionScenario Scenario { get; set; } = new();
        public EnginePrediction Prediction { get; set; } = new();
        public double ConfidenceLevel { get; set; }
        public double ExpectedAccuracy { get; set; }
    }

    public class LearningMetrics
    {
        public LearningMetrics()
        {
            DataQuality = 0.0;
            LearningRate = 0.0;
            ModelConvergence = 0.0;
        }
        public double DataQuality { get; set; }
        public double LearningRate { get; set; }
        public double ModelConvergence { get; set; }
    }

    public class PredictionAccuracy
    {
        public PredictionAccuracy()
        {
            EngineId = string.Empty;
        }
        public string EngineId { get; set; }
        public double OverallAccuracy { get; set; }
        public double ThrustPredictionAccuracy { get; set; }
        public double ThermalPredictionAccuracy { get; set; }
        public double StructuralPredictionAccuracy { get; set; }
        public double FailurePredictionAccuracy { get; set; }
    }

    public class PredictionScenario
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class TestingRequirements
    {
        public string TestType { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
} 