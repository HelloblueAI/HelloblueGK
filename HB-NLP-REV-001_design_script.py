
# HB-NLP Revolutionary Engine Design Script for Plasticity
# Engine: HB-NLP-REV-001

# Engine Chamber
chamber = create_cylinder(
    radius=1.25,
    height=3.0,
    material="Advanced Superalloy"
)

# Throat Section
throat = create_cylinder(
    radius=0.4,
    height=0.5,
    material="Advanced Superalloy"
)

# Nozzle (Conical)
nozzle = create_cone(
    base_radius=2.0,
    top_radius=0.4,
    height=6.0,
    material="Carbon-Carbon Composite"
)

# Injector System
injector = create_cylinder(
    radius=0.3,
    height=0.8,
    material="Titanium Alloy"
)

# Turbopump
turbopump = create_cylinder(
    radius=0.4,
    height=1.2,
    material="Inconel 718"
)

# Assembly
engine_assembly = union([chamber, throat, nozzle, injector, turbopump])

# Export
export_stl(engine_assembly, "HB-NLP-REV-001_3d_model.stl")
export_step(engine_assembly, "HB-NLP-REV-001_3d_model.step")
