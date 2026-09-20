using System.Runtime.CompilerServices;

// Verification of control and decision logic often needs to reach a unit directly rather
// than drive it through an orchestration layer that injects randomness, timing, and console
// output. Widening those units to public would make them API surface the project has to
// keep; exposing them to the test assembly keeps them implementation detail while still
// letting the tests establish what the algorithm actually computes.
//
// The attribute lives in source because this project sets GenerateAssemblyInfo=false, so
// the MSBuild InternalsVisibleTo item would never be emitted.
[assembly: InternalsVisibleTo("HelloblueGK.Tests")]
