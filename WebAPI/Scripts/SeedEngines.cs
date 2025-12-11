using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace HB_NLP_Research_Lab.WebAPI.Scripts;

/// <summary>
/// Seeds the database with initial engine configurations
/// Includes real-world rocket engines: Raptor, Merlin, RS-25
/// </summary>
public static class EngineSeeder
{
    public static async Task SeedEnginesAsync(HelloblueGKDbContext context, ILogger logger)
    {
        try
        {
            // Check if engines already exist
            if (await context.Engines.AnyAsync())
            {
                logger.LogInformation("Engines already exist in database. Skipping seed.");
                return;
            }

            logger.LogInformation("Seeding initial engines...");

            var engines = new List<Engine>
            {
                // SpaceX Raptor Engine
                new Engine
                {
                    Name = "Raptor",
                    EngineType = "Raptor",
                    Thrust = 2200000, // Newtons (2.2 MN)
                    SpecificImpulse = 380, // seconds (vacuum)
                    ChamberPressure = 300, // bar
                    ExpansionRatio = 40,
                    Efficiency = 0.98,
                    Propellant = "LOX/Methane",
                    MixtureRatio = 3.6,
                    MassFlowRate = 650, // kg/s
                    Description = "SpaceX Raptor full-flow staged combustion rocket engine. Used on Starship and Super Heavy booster.",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },

                // SpaceX Merlin 1D Engine
                new Engine
                {
                    Name = "Merlin 1D",
                    EngineType = "Merlin",
                    Thrust = 845000, // Newtons (845 kN)
                    SpecificImpulse = 311, // seconds (sea level)
                    ChamberPressure = 97, // bar
                    ExpansionRatio = 16,
                    Efficiency = 0.99,
                    Propellant = "RP-1/LOX",
                    MixtureRatio = 2.36,
                    MassFlowRate = 280, // kg/s
                    Description = "SpaceX Merlin 1D engine. Used on Falcon 9 and Falcon Heavy first stage. Most reliable rocket engine in history.",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },

                // RS-25 (Space Shuttle Main Engine)
                new Engine
                {
                    Name = "RS-25",
                    EngineType = "RS-25",
                    Thrust = 2279000, // Newtons (2.279 MN)
                    SpecificImpulse = 452, // seconds (vacuum)
                    ChamberPressure = 204, // bar
                    ExpansionRatio = 69.5,
                    Efficiency = 0.99,
                    Propellant = "LH2/LOX",
                    MixtureRatio = 6.0,
                    MassFlowRate = 470, // kg/s
                    Description = "RS-25 (Space Shuttle Main Engine). Now used on SLS. Most efficient liquid hydrogen engine ever built.",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },

                // Blue Origin BE-4
                new Engine
                {
                    Name = "BE-4",
                    EngineType = "BE-4",
                    Thrust = 2400000, // Newtons (2.4 MN)
                    SpecificImpulse = 339, // seconds (sea level)
                    ChamberPressure = 135, // bar
                    ExpansionRatio = 20,
                    Efficiency = 0.97,
                    Propellant = "LOX/Methane",
                    MixtureRatio = 3.6,
                    MassFlowRate = 700, // kg/s
                    Description = "Blue Origin BE-4 engine. Used on New Glenn and Vulcan Centaur. Methane-fueled staged combustion engine.",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },

                // RD-180 (Russian Engine)
                new Engine
                {
                    Name = "RD-180",
                    EngineType = "RD-180",
                    Thrust = 4152000, // Newtons (4.152 MN)
                    SpecificImpulse = 338, // seconds (sea level)
                    ChamberPressure = 257, // bar
                    ExpansionRatio = 36.4,
                    Efficiency = 0.98,
                    Propellant = "RP-1/LOX",
                    MixtureRatio = 2.72,
                    MassFlowRate = 1200, // kg/s
                    Description = "RD-180 Russian engine. Used on Atlas V. Extremely powerful and reliable staged combustion engine.",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },

                // Raptor Vacuum (for upper stage)
                new Engine
                {
                    Name = "Raptor Vacuum",
                    EngineType = "Raptor",
                    Thrust = 3500000, // Newtons (3.5 MN)
                    SpecificImpulse = 380, // seconds (vacuum optimized)
                    ChamberPressure = 300, // bar
                    ExpansionRatio = 80, // Much larger nozzle for vacuum
                    Efficiency = 0.99,
                    Propellant = "LOX/Methane",
                    MixtureRatio = 3.6,
                    MassFlowRate = 650, // kg/s
                    Description = "SpaceX Raptor Vacuum engine. Optimized for upper stage operations in vacuum. Larger expansion ratio for maximum efficiency.",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            await context.Engines.AddRangeAsync(engines);
            await context.SaveChangesAsync();

            logger.LogInformation("Successfully seeded {Count} engines", engines.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding engines: {Error}", ex.Message);
            throw;
        }
    }
}
