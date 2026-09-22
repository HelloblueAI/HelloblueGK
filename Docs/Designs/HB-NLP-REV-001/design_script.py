# HB-NLP Revolutionary Engine Design Script for Plasticity
# Engine: HB-NLP-REV-001
#
# Diameters and lengths are the declaration in Aerospace/HB_NLP_RevolutionaryEngine.cs.
# Radii below are half of those diameters. Throat axial length, and the injector and
# turbopump sizes, are not declared by the engine; they are only so this sketch has
# those parts. This script does not analyse the engine.

# Engine Chamber
chamber = create_cylinder(
    radius=0.325,
    height=1.4,
    material="Quantum-Enhanced Chamber Alloy"
)

# Throat Section
throat = create_cylinder(
    radius=0.1465,
    height=0.5,
    material="Quantum-Enhanced Chamber Alloy"
)

# Nozzle (Conical)
nozzle = create_cone(
    base_radius=0.782096,
    top_radius=0.1465,
    height=2.8,
    material="Self-Healing Nozzle Composite"
)

# Injector System — size not declared by the engine
injector = create_cylinder(
    radius=0.3,
    height=0.8,
    material="not declared by the engine"
)

# Turbopump — size not declared by the engine
turbopump = create_cylinder(
    radius=0.4,
    height=1.2,
    material="not declared by the engine"
)

# Assembly
engine_assembly = union([chamber, throat, nozzle, injector, turbopump])

# Export
export_stl(engine_assembly, "HB-NLP-REV-001_3d_model.stl")
export_step(engine_assembly, "HB-NLP-REV-001_3d_model.step")
