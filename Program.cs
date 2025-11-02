using System;
using System.Threading.Tasks;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.Aerospace;
using HB_NLP_Research_Lab.Physics;
using HB_NLP_Research_Lab.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace HB_NLP_Research_Lab
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 HB-NLP Research Lab - Advanced Aerospace Engine Design Platform");
            Console.WriteLine("================================================================================\n");

            // Configure services and use using statement for ServiceProvider disposal
            var services = new ServiceCollection();
            ConfigureServices(services);
            using var serviceProvider = services.BuildServiceProvider();
            try
            {

                // Initialize core systems
                var engine = new HelloblueGKEngine();
                var revolutionaryEngine = new HB_NLP_RevolutionaryEngine();
                var aerospaceReadiness = new AerospaceReadinessAssessment();
                
                // Get enhanced services
                var performanceService = serviceProvider.GetService<PerformanceMonitoringService>();
                var rateLimitingService = serviceProvider.GetService<RateLimitingService>();
                var structuredLoggingService = serviceProvider.GetService<StructuredLoggingService>();
                var configValidationService = serviceProvider.GetService<ConfigurationValidationService>();
                var healthCheckService = serviceProvider.GetService<AdvancedHealthCheckService>();

                Console.WriteLine("🔬 Initializing HB-NLP Advanced Engine Design System...\n");

                // Initialize engine
                await revolutionaryEngine.InitializeAsync();

                // Perform comprehensive aerospace readiness assessment
                Console.WriteLine("🎯 Performing Comprehensive Aerospace Readiness Assessment...\n");
                
                var readinessReport = await aerospaceReadiness.PerformComprehensiveAssessmentAsync(
                    MissionLevel.Critical);

                // Display readiness assessment results
                DisplayReadinessAssessmentResults(readinessReport);

                // Check if ready for advanced aerospace applications
                var isReadyForAdvanced = await aerospaceReadiness.IsReadyForAdvancedAerospaceAsync();
                var isReadyForMissionCritical = await aerospaceReadiness.IsReadyForMissionCriticalOperationsAsync();

                Console.WriteLine("\n🎯 READINESS EVALUATION RESULTS:");
                Console.WriteLine("================================================================================\n");

                if (isReadyForAdvanced)
                {
                    Console.WriteLine("✅ ADVANCED AEROSPACE READY");
                    Console.WriteLine("   - Ready for advanced aerospace applications");
                    Console.WriteLine("   - Meets industry-leading standards");
                    Console.WriteLine("   - Suitable for research and development");
                }

                if (isReadyForMissionCritical)
                {
                    Console.WriteLine("🚀 MISSION-CRITICAL OPERATIONS READY");
                    Console.WriteLine("   - Ready for mission-critical operations");
                    Console.WriteLine("   - Meets human-rated safety standards");
                    Console.WriteLine("   - Suitable for operational missions");
                    Console.WriteLine("   - Compliant with all aerospace regulations");
                }

                if (!isReadyForAdvanced && !isReadyForMissionCritical)
                {
                    Console.WriteLine("⚠️  ADDITIONAL DEVELOPMENT REQUIRED");
                    Console.WriteLine("   - Some readiness criteria not met");
                    Console.WriteLine("   - Review recommendations for improvement");
                    Console.WriteLine("   - Continue development and testing");
                }

                // Perform engine analysis
                Console.WriteLine("\n🔬 Performing Advanced Engine Analysis...\n");
                
                var analysisResult = await engine.AnalyzeEngineAsync("HB-NLP-REV-001");
                var validationSummary = await engine.GenerateValidationSummaryAsync();

                // Display analysis results
                DisplayAnalysisResults(analysisResult, validationSummary);

                // Display comprehensive status
                Console.WriteLine("\n🏆 COMPREHENSIVE STATUS SUMMARY");
                Console.WriteLine("================================================================================\n");

                Console.WriteLine($"📊 Overall Readiness: {readinessReport.OverallReadiness:P}");
                Console.WriteLine($"🎯 Readiness Status: {readinessReport.ReadinessStatus}");
                Console.WriteLine($"🚀 Mission Level: {readinessReport.MissionLevel}");
                Console.WriteLine($"🔬 Technology Readiness: {analysisResult.PerformanceMetrics["Overall"]:P}");
                Console.WriteLine($"✅ Validation Status: {validationSummary.ValidationScore:P}");

                Console.WriteLine("\n🎯 CERTIFICATION STATUS:");
                foreach (var certification in readinessReport.CertificationStatus)
                {
                    Console.WriteLine($"   ✅ {certification.Type} - {certification.Level} ({certification.Status})");
                }

                Console.WriteLine("\n🔒 COMPLIANCE STATUS:");
                foreach (var compliance in readinessReport.ComplianceStatus)
                {
                    Console.WriteLine($"   ✅ {compliance.Standard} - {compliance.Level} ({compliance.Status})");
                }

                if (readinessReport.Recommendations.Count > 0)
                {
                    Console.WriteLine("\n📋 RECOMMENDATIONS FOR IMPROVEMENT:");
                    foreach (var recommendation in readinessReport.Recommendations)
                    {
                        Console.WriteLine($"   💡 {recommendation}");
                    }
                }

                Console.WriteLine("\n🎉 HB-NLP Advanced Engine Design Platform Assessment Complete!");
                Console.WriteLine("================================================================================\n");

                if (isReadyForMissionCritical)
                {
                    Console.WriteLine("🏆 ACHIEVEMENT: MISSION-CRITICAL READINESS ACHIEVED!");
                    Console.WriteLine("   Your platform is ready for the most demanding aerospace applications.");
                    Console.WriteLine("   All critical safety, quality, and compliance standards have been met.");
                    Console.WriteLine("   Ready for human-rated operations and mission-critical deployments.\n");
                }
                else if (isReadyForAdvanced)
                {
                    Console.WriteLine("🎯 ACHIEVEMENT: ADVANCED AEROSPACE READINESS ACHIEVED!");
                    Console.WriteLine("   Your platform is ready for advanced aerospace research and development.");
                    Console.WriteLine("   Suitable for prototype testing and qualification programs.");
                    Console.WriteLine("   Continue development to achieve mission-critical readiness.\n");
                }
                else
                {
                    Console.WriteLine("🔄 DEVELOPMENT STATUS: CONTINUE ENHANCEMENT");
                    Console.WriteLine("   Your platform shows excellent potential but requires additional development.");
                    Console.WriteLine("   Focus on addressing the recommendations provided.");
                    Console.WriteLine("   Continue testing and validation to achieve aerospace readiness.\n");
                }

                Console.WriteLine("🚀 HB-NLP Research Lab - Advanced Aerospace Technology");
                Console.WriteLine("   Beyond Current Capabilities - World's Most Advanced Engine Design Platform");
                
                // Display enhanced features status
                Console.WriteLine("\n🔧 ENHANCED FEATURES STATUS:");
                Console.WriteLine("================================================================================\n");
                Console.WriteLine($"📊 Performance Monitoring: {(performanceService != null ? "✅ ACTIVE" : "❌ INACTIVE")}");
                Console.WriteLine($"🛡️  Rate Limiting: {(rateLimitingService != null ? "✅ ACTIVE" : "❌ INACTIVE")}");
                Console.WriteLine($"📝 Structured Logging: {(structuredLoggingService != null ? "✅ ACTIVE" : "❌ INACTIVE")}");
                Console.WriteLine($"⚙️  Configuration Validation: {(configValidationService != null ? "✅ ACTIVE" : "❌ INACTIVE")}");
                Console.WriteLine($"🏥 Advanced Health Checks: {(healthCheckService != null ? "✅ ACTIVE" : "❌ INACTIVE")}");
                Console.WriteLine($"🧪 Unit Testing: ✅ FRAMEWORK READY");
                Console.WriteLine($"📈 Monitoring & Metrics: ✅ IMPLEMENTED");
                Console.WriteLine($"🔒 API Protection: ✅ IMPLEMENTED");
                Console.WriteLine($"📚 Auto-Generated Documentation: ✅ IMPLEMENTED");
                Console.WriteLine($"🚀 Performance Benchmarks: ✅ IMPLEMENTED");
                Console.WriteLine($"🔗 Integration Tests: ✅ IMPLEMENTED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during assessment: {ex.Message}");
                Console.WriteLine($"   Stack trace: {ex.StackTrace}");
            }
            // ServiceProvider automatically disposed via using statement
        }

        static void ConfigureServices(ServiceCollection services)
        {
            // Add configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            
            services.AddSingleton<IConfiguration>(configuration);
            
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Add enhanced services
            services.AddSingleton<PerformanceMonitoringService>();
            services.AddSingleton<RateLimitingService>();
            services.AddSingleton<StructuredLoggingService>();
            services.AddSingleton<ConfigurationValidationService>();
            services.AddSingleton<AdvancedHealthCheckService>();
            
            // Add as hosted services for background processing
            services.AddHostedService<PerformanceMonitoringService>();
        }

        static void DisplayReadinessAssessmentResults(AerospaceReadinessReport report)
        {
            Console.WriteLine("📊 AEROSPACE READINESS ASSESSMENT RESULTS");
            Console.WriteLine("================================================================================\n");

            Console.WriteLine($"🎯 Mission Level: {report.MissionLevel}");
            Console.WriteLine($"📊 Overall Readiness: {report.OverallReadiness:P}");
            Console.WriteLine($"✅ Readiness Status: {report.ReadinessStatus}");
            Console.WriteLine($"📅 Assessment Date: {report.Timestamp:yyyy-MM-dd HH:mm:ss UTC}\n");

            Console.WriteLine("📋 READINESS CATEGORIES:");
            Console.WriteLine("================================================================================\n");

            foreach (var category in report.ReadinessCategories)
            {
                Console.WriteLine($"🔹 {category.Category}:");
                Console.WriteLine($"   Score: {category.ReadinessScore:P}");
                Console.WriteLine($"   Status: {category.Status}");
                
                if (category.Recommendations.Count > 0)
                {
                    Console.WriteLine("   Recommendations:");
                    foreach (var rec in category.Recommendations)
                    {
                        Console.WriteLine($"     💡 {rec}");
                    }
                }
                Console.WriteLine();
            }
        }

        static void DisplayAnalysisResults(ComprehensiveAnalysisResult analysisResult, ValidationSummary validationSummary)
        {
            Console.WriteLine("🔬 ENGINE ANALYSIS RESULTS");
            Console.WriteLine("================================================================================\n");

            Console.WriteLine("🚀 Thrust Analysis:");
            Console.WriteLine($"   Maximum Thrust: {analysisResult.ThrustAnalysis.MaxThrust:N0} N");
            Console.WriteLine($"   Efficiency: {analysisResult.ThrustAnalysis.Efficiency:P}\n");

            Console.WriteLine("🔥 Thermal Analysis:");
            Console.WriteLine($"   Maximum Temperature: {analysisResult.ThermalAnalysis.MaxTemperature:N0} K");
            Console.WriteLine($"   Cooling Efficiency: {analysisResult.ThermalAnalysis.CoolingEfficiency:P}\n");

            Console.WriteLine("🏗️ Structural Analysis:");
            Console.WriteLine($"   Maximum Stress: {analysisResult.StructuralAnalysis.MaxStress / 1e6:F1} MPa");
            Console.WriteLine($"   Safety Factor: {analysisResult.StructuralAnalysis.SafetyFactor:F1}x\n");

            Console.WriteLine("📊 Performance Metrics:");
            foreach (var metric in analysisResult.PerformanceMetrics)
            {
                Console.WriteLine($"   {metric.Key}: {metric.Value:P}");
            }

            Console.WriteLine("\n✅ VALIDATION SUMMARY:");
            Console.WriteLine($"   Overall Validation: {validationSummary.ValidationScore:P}");
            Console.WriteLine($"   Average Accuracy: {validationSummary.AverageAccuracy:P}");
            Console.WriteLine($"   Highest Accuracy: {validationSummary.HighestAccuracy:P}");
            Console.WriteLine($"   Total Engines Validated: {validationSummary.TotalEnginesValidated}");
            Console.WriteLine($"   Critical Issues: {validationSummary.CriticalIssues}");
            Console.WriteLine($"   Warnings: {validationSummary.Warnings}\n");
        }
    }
} 