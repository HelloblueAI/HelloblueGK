#!/usr/bin/env python3
"""
HB-NLP Research Lab - Plasticity Engine Design Generator
Opens the revolutionary HB-NLP engine design in Plasticity v25.2.2
"""

import subprocess
import json
from pathlib import Path


class PlasticityEngineDesigner:
    """Plasticity Engine Designer for HB-NLP Revolutionary Engine Design."""

    def __init__(self):
        self.engine_name = "HB-NLP-REV-001"
        # Set up organized directory structure
        self.base_dir = Path(__file__).parent.parent.parent  # Go up to project root
        self.design_dir = self.base_dir / "Docs" / "Designs" / self.engine_name
        self.design_dir.mkdir(parents=True, exist_ok=True)
        
        # Same declaration as Aerospace/HB_NLP_RevolutionaryEngine.cs. Do not put a
        # technology readiness level, an efficiency, or a solver-convergence figure here:
        # this repository has no measurement that would support one.
        self.design_data = {
            "name": "HB-NLP Quantum-Classical Hybrid Engine",
            "model_id": self.engine_name,
            "version": "Advanced v2.5",
            "specifications": {
                "thrust": 3500000,  # N, 3.5 MN
                "specific_impulse": 351.5,  # seconds, ideal methane/LOX at this expansion
                "chamber_pressure": 280,  # bar
                "expansion_ratio": 28.5,  # area ratio
            },
            "geometry": {
                "chamber_diameter": 0.65,  # meters
                "chamber_length": 1.4,  # meters
                "throat_diameter": 0.293,  # meters
                "exit_diameter": 1.564192,  # meters, throat * sqrt(28.5)
                "nozzle_length": 2.8,  # meters
            },
            "materials": {
                "chamber": "Quantum-Enhanced Chamber Alloy",
                "nozzle": "Self-Healing Nozzle Composite",
                "injector": "not declared by the engine",
                "turbopump": "not declared by the engine",
            },
        }

    def create_design_file(self):
        """Create a JSON design file for Plasticity."""
        design_file = self.design_dir / "design.json"

        with open(design_file, "w", encoding="utf-8") as f:
            json.dump(self.design_data, f, indent=2)

        print(f"✅ Created design file: {design_file}")
        return design_file

    def generate_3d_model_script(self):
        """Generate a script to create the 3D model in Plasticity."""
        script_content = f"""
# HB-NLP Revolutionary Engine Design Script for Plasticity
# Engine: {self.engine_name}
#
# Diameters and lengths are the declaration in Aerospace/HB_NLP_RevolutionaryEngine.cs.
# Throat axial length, and the injector and turbopump sizes, are not declared.

# Engine Chamber
chamber = create_cylinder(
    radius={self.design_data['geometry']['chamber_diameter']/2},
    height={self.design_data['geometry']['chamber_length']},
    material="{self.design_data['materials']['chamber']}"
)

# Throat Section
throat = create_cylinder(
    radius={self.design_data['geometry']['throat_diameter']/2},
    height=0.5,
    material="{self.design_data['materials']['chamber']}"
)

# Nozzle (Conical)
nozzle = create_cone(
    base_radius={self.design_data['geometry']['exit_diameter']/2},
    top_radius={self.design_data['geometry']['throat_diameter']/2},
    height={self.design_data['geometry']['nozzle_length']},
    material="{self.design_data['materials']['nozzle']}"
)

# Injector System
injector = create_cylinder(
    radius=0.3,
    height=0.8,
    material="{self.design_data['materials']['injector']}"
)

# Turbopump
turbopump = create_cylinder(
    radius=0.4,
    height=1.2,
    material="{self.design_data['materials']['turbopump']}"
)

# Assembly
engine_assembly = union([chamber, throat, nozzle, injector, turbopump])

# Export
export_stl(engine_assembly, "{self.engine_name}_3d_model.stl")
export_step(engine_assembly, "{self.engine_name}_3d_model.step")
"""

        script_file = self.design_dir / "design_script.py"
        with open(script_file, "w", encoding="utf-8") as f:
            f.write(script_content)

        print(f"✅ Created Plasticity script: {script_file}")
        return script_file

    def open_in_plasticity(self):
        """Open the design in Plasticity software."""
        try:
            # Try to open Plasticity with the design file
            design_file = self.design_dir / "design.json"
            script_file = self.design_dir / "design_script.py"

            # Create files if they don't exist
            if not design_file.exists():
                self.create_design_file()
            if not script_file.exists():
                self.generate_3d_model_script()

            # Try to open with Plasticity (adjust path as needed)
            plasticity_paths = [
                "/Applications/Plasticity.app/Contents/MacOS/Plasticity",
                "C:\\Program Files\\Plasticity\\Plasticity.exe",
                "/usr/local/bin/plasticity",
                "plasticity",
            ]

            for path in plasticity_paths:
                try:
                    subprocess.run([path, str(design_file)], check=True)
                    print("✅ Opened design in Plasticity")
                    return True
                except (subprocess.CalledProcessError, FileNotFoundError):
                    continue

            print("⚠️  Plasticity not found. Please open manually:")
            print(f"   Design file: {design_file}")
            print(f"   Script file: {script_file}")
            return False

        except Exception as e:
            print(f"❌ Error opening in Plasticity: {e}")
            return False

    def create_design_summary(self):
        """Create a summary of the engine design."""
        summary = f"""
# HB-NLP Revolutionary Engine Design Summary

Concept design. These are declared inputs, not measurements, and no technology readiness level is claimed.

## Engine Specifications
- **Name**: {self.design_data['name']}
- **Model ID**: {self.design_data['model_id']}
- **Version**: {self.design_data['version']}
- **Thrust**: {self.design_data['specifications']['thrust']:,} N ({self.design_data['specifications']['thrust']/1e6:.1f} MN)
- **Specific Impulse**: {self.design_data['specifications']['specific_impulse']} s
- **Chamber Pressure**: {self.design_data['specifications']['chamber_pressure']} bar
- **Expansion Ratio**: {self.design_data['specifications']['expansion_ratio']}:1

## Geometry
- **Chamber Diameter**: {self.design_data['geometry']['chamber_diameter']} m
- **Chamber Length**: {self.design_data['geometry']['chamber_length']} m
- **Throat Diameter**: {self.design_data['geometry']['throat_diameter']} m
- **Exit Diameter**: {self.design_data['geometry']['exit_diameter']} m
- **Nozzle Length**: {self.design_data['geometry']['nozzle_length']} m

## Materials
- **Chamber**: {self.design_data['materials']['chamber']}
- **Nozzle**: {self.design_data['materials']['nozzle']}
- **Injector**: {self.design_data['materials']['injector']}
- **Turbopump**: {self.design_data['materials']['turbopump']}
"""

        summary_file = self.design_dir / "design_summary.md"
        with open(summary_file, "w", encoding="utf-8") as f:
            f.write(summary)

        print(f"✅ Created design summary: {summary_file}")
        return summary_file


def main():
    """Main function to run the Plasticity Engine Designer."""
    designer = PlasticityEngineDesigner()
    designer.create_design_file()
    designer.generate_3d_model_script()
    designer.create_design_summary()
    designer.open_in_plasticity()


if __name__ == "__main__":
    main()
