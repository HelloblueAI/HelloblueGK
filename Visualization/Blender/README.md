# Blender Digital Twin Visualization

A conceptual digital-twin visualization of the HelloblueGK reference engine, built in Blender. It
exists to communicate the engine's layout and operating point — not to analyse either.

The model represents **`HB_NLP_RevolutionaryEngine`**, defined in
[`Aerospace/HB_NLP_RevolutionaryEngine.cs`](../../Aerospace/HB_NLP_RevolutionaryEngine.cs).

## What this is not

This is a visualization. It is **not** manufacturing CAD, **not** CFD validation, **not**
flight-certified software, and **not** a substitute for engineering analysis. Geometry is modelled to
look like the described engine, not to be dimensionally correct or physically solved. No claim of
physical validation, manufacturing accuracy, or certification attaches to anything in this directory.

For which parts of this repository are verified engineering and which are simulation, see
[`Docs/VERIFICATION_SCOPE.md`](../../Docs/VERIFICATION_SCOPE.md). The engine represented here is a
concept design, not a built or tested engine.

## Values taken from the repository

Every figure in the telemetry panel traces to `HB_NLP_RevolutionaryEngine.cs`:

| Quantity | Value | Source |
|----------|-------|--------|
| Thrust | 3.5 MN | `Thrust = 3500000` |
| Chamber pressure | 280 bar | `ChamberPressure = 280` |
| Nozzle length | 2.8 m | `NozzleLength = 2.8` |
| Morphing nozzle | enabled | `MorphingNozzle = true` |
| Oxidizer | LOX | `PropulsionSystem.Oxidizer = "LOX"` |
| Fuel | Methane (CH4) | `PropulsionSystem.PrimaryFuel = "Methane"` |
| Chamber alloy temperature limit | 3,800 K | `Materials[0].TemperatureLimit` |
| Nozzle composite temperature limit | 3,500 K | `Materials[1].TemperatureLimit` |

Note that **3,800 K and 3,500 K are material limits, not gas temperatures.** The engine design
defines no chamber temperature, so the panel does not report one.

**The 3.5 MN thrust figure is what the code declares, not a figure the design supports.** It is
inconsistent with the same engine's declared geometry and chamber conditions by roughly a factor of
six: 280 bar through the declared 0.12 m throat gives about 587 kN in vacuum, and reaching 3.5 MN at
the declared expansion ratio would need a throat of 0.293 m. The panel shows the declared value
because that is what the model represents, but it should not be read as a performance claim. The
discrepancy is measured and gated by `Tests/Unit/Aerospace/EngineDesignConsistencyTests.cs` and
recorded in [`Docs/VERIFICATION_SCOPE.md`](../../Docs/VERIFICATION_SCOPE.md).

CH4 is likewise taken from the code rather than assumed: `PrimaryFuel = "Methane"` sits directly
above the oxidizer in the same propulsion block.

## Requirements

**Blender 5.2.2 or newer.** The script sets the render engine to `BLENDER_EEVEE` and falls back to
`BLENDER_EEVEE_NEXT`, uses the `Emission Color` input on Principled BSDF, and relies on Blender 5.x
layered animation data. Earlier releases name these differently.

## Opening the saved project

```bash
blender Visualization/Blender/HelloblueGK3D.blend
```

Or from Blender: **File → Open**, then select `HelloblueGK3D.blend`. The scene opens with the camera,
lighting, materials, and animation already in place — no scripting needed.

## Running the generator

The generator rebuilds the entire scene from code, which is useful after changing a value in
`ENGINE` at the top of the script.

> **It clears the scene first.** `clear_scene()` selects and deletes every object in the current
> file. Run it in a new file, or one you are willing to lose.

### From Blender's Scripting workspace

1. Open Blender and switch to the **Scripting** workspace.
2. **Text → Open**, select `hellobluegk_revolutionary_engine.py`.
3. Press **Run Script** (or `Alt`+`P`).
4. Switch to **Layout** and press `Numpad 0` for the camera view.

### From a terminal

```bash
blender --python Visualization/Blender/hellobluegk_revolutionary_engine.py
```

Add `--background` to run without a window, though there is little point unless you also pass render
arguments, since the script builds a scene rather than producing an image.

## What the generator creates

**Engine geometry**

- Injector assembly and combustion chamber, with an emissive hot-gas core
- Six regenerative cooling channel rings around the chamber
- Morphing nozzle, built as a surface of revolution from a seven-point profile and animated to
  expand and contract across frames 1 → 60 → 120
- Twin turbopump concept — one on the LOX side, one on the fuel side
- Propellant plumbing, as bezier-swept feed lines from each turbopump to the chamber
- Thrust frame ring

**Scene**

- Animated exhaust plume, pulsing over frames 1–24 with an emissive material
- Telemetry panel, as extruded 3D text carrying the values in the table above and the line
  `CONCEPT VISUALIZATION — NOT FLIGHT CERTIFIED`
- Test stand floor
- Two area lights: a warm key light and a blue fill
- Camera on a 58 mm lens aimed at the engine, set as the active scene camera
- Frame range 1–120, Eevee, 1920 × 1080 at 60% scale, against a near-black world

Materials are named for what they represent — `Nickel_Superalloy`, `Copper_Chamber`,
`Self_Healing_Nozzle_Composite`, `LOX_Flow`, `Fuel_Flow`, `Combustion_Core`, `Exhaust_3_5MN` — so
they are readable in the outliner. The names describe intent, not measured material properties.

## Files

| File | Contents |
|------|----------|
| `hellobluegk_revolutionary_engine.py` | Generator that builds the scene from scratch |
| `HelloblueGK3D.blend` | Saved project (356 KB) |

Blender backup and autosave files (`.blend1`, `.blend2`, `.blend~`) and the `renders/` and `cache/`
directories are ignored by git.

## License

Apache License 2.0, as with the rest of the repository — see [`LICENSE`](../../LICENSE).
