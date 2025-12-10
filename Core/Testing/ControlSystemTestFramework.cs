using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HB_NLP_Research_Lab.Core.Hardware;

namespace HB_NLP_Research_Lab.Core.Testing
{
    /// <summary>
    /// Testing framework for control systems
    /// Supports unit tests, integration tests, and hardware-in-the-loop (HIL) testing
    /// </summary>
    public class ControlSystemTestFramework
    {
        /// <summary>
        /// Run a control system test
        /// </summary>
        public static async Task<TestResult> RunTestAsync(ControlSystemTestCase testCase)
        {
            var result = new TestResult
            {
                TestName = testCase.Name,
                StartTime = DateTime.UtcNow,
                Status = TestStatus.Running
            };
            
            try
            {
                Console.WriteLine($"[Test] Running: {testCase.Name}");
                
                // Setup
                if (testCase.Setup != null)
                {
                    await testCase.Setup();
                }
                
                // Execute test
                await testCase.Execute();
                
                // Verify assertions
                foreach (var assertion in testCase.Assertions)
                {
                    var assertionResult = await assertion();
                    if (!assertionResult.Passed)
                    {
                        result.Status = TestStatus.Failed;
                        result.Failures.Add(assertionResult.Message);
                    }
                }
                
                if (result.Status == TestStatus.Running)
                {
                    result.Status = TestStatus.Passed;
                }
            }
            catch (Exception ex)
            {
                result.Status = TestStatus.Failed;
                result.Failures.Add($"Exception: {ex.Message}");
            }
            finally
            {
                // Cleanup
                if (testCase.Cleanup != null)
                {
                    await testCase.Cleanup();
                }
                
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;
            }
            
            Console.WriteLine($"[Test] {result.Status}: {testCase.Name} ({result.Duration.TotalMilliseconds:F2}ms)");
            
            return result;
        }
        
        /// <summary>
        /// Run multiple tests
        /// </summary>
        public static async Task<TestSuiteResult> RunTestSuiteAsync(List<ControlSystemTestCase> testCases)
        {
            var suiteResult = new TestSuiteResult
            {
                StartTime = DateTime.UtcNow
            };
            
            foreach (var testCase in testCases)
            {
                var result = await RunTestAsync(testCase);
                suiteResult.Results.Add(result);
            }
            
            suiteResult.EndTime = DateTime.UtcNow;
            suiteResult.Duration = suiteResult.EndTime - suiteResult.StartTime;
            suiteResult.PassedCount = suiteResult.Results.Count(r => r.Status == TestStatus.Passed);
            suiteResult.FailedCount = suiteResult.Results.Count(r => r.Status == TestStatus.Failed);
            
            Console.WriteLine($"[Test Suite] Completed: {suiteResult.PassedCount} passed, {suiteResult.FailedCount} failed");
            
            return suiteResult;
        }
    }
    
    public class ControlSystemTestCase
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Func<Task>? Setup { get; set; }
        public Func<Task> Execute { get; set; } = null!;
        public List<Func<Task<AssertionResult>>> Assertions { get; set; } = new();
        public Func<Task>? Cleanup { get; set; }
    }
    
    public class TestResult
    {
        public string TestName { get; set; } = string.Empty;
        public TestStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> Failures { get; set; } = new();
    }
    
    public class TestSuiteResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<TestResult> Results { get; set; } = new();
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
    }
    
    public enum TestStatus
    {
        NotRun,
        Running,
        Passed,
        Failed,
        Skipped
    }
    
    public class AssertionResult
    {
        public bool Passed { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    
    // Helper methods for creating assertions
    public static class Assertions
    {
        public static Func<Task<AssertionResult>> AssertTrue(bool condition, string message)
        {
            return () => Task.FromResult(new AssertionResult
            {
                Passed = condition,
                Message = condition ? "Passed" : message
            });
        }
        
        public static Func<Task<AssertionResult>> AssertEquals<T>(T expected, T actual, string message = "")
        {
            return () => Task.FromResult(new AssertionResult
            {
                Passed = Equals(expected, actual),
                Message = Equals(expected, actual) 
                    ? "Passed" 
                    : $"{message} Expected: {expected}, Actual: {actual}"
            });
        }
        
        public static Func<Task<AssertionResult>> AssertInRange(double value, double min, double max, string message = "")
        {
            return () => Task.FromResult(new AssertionResult
            {
                Passed = value >= min && value <= max,
                Message = (value >= min && value <= max)
                    ? "Passed"
                    : $"{message} Value {value} not in range [{min}, {max}]"
            });
        }
        
        public static Func<Task<AssertionResult>> AssertSensorValue(ISensor<double> sensor, double expected, double tolerance, string message = "")
        {
            return async () =>
            {
                var actual = await sensor.ReadAsync();
                var passed = Math.Abs(actual - expected) <= tolerance;
                return new AssertionResult
                {
                    Passed = passed,
                    Message = passed
                        ? "Passed"
                        : $"{message} Expected: {expected}±{tolerance}, Actual: {actual}"
                };
            };
        }
    }
}
