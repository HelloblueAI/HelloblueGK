# Documentation Directory

Live docs for running and contributing live here. Point-in-time status write-ups have been
retired; [`VERIFICATION_SCOPE.md`](VERIFICATION_SCOPE.md) is the current account of what this
project verifies, and git history holds the rest.

New here? See [CONTRIBUTING.md](../CONTRIBUTING.md).

## Directory Structure

### 📄 VERIFICATION_SCOPE.md
Which parts of this project are verified engineering and which are simulation scaffolding.
Start here to calibrate any other claim in the documentation.

### 📁 Project/
High-level project documentation.
- `DEMO.md` - Demonstration and usage guide
- `PROFESSIONAL_DEMO_STRATEGY.md` - How to present the project to a technical audience
- `PROJECT_GAPS_AND_IMPROVEMENTS.md` - Known gaps and improvement ideas
- `SHORT_TERM_PLAN.md` - Near-term development plan
- `SHOWCASE_GUIDE.md` - Guide for presenting the project

### 📁 Technical/
Technical documentation, installation, limitations, and validation reports.
- `ENTERPRISE_DEPLOYMENT.md` - Deployment patterns for larger environments
- `INSTALL_DOTNET.md` - .NET installation instructions
- `REAL_TIME_ENGINE_CONTROL.md` - Real-time engine control implementation plan
- `TECHNICAL_LIMITATIONS_AND_ROADMAP.md` - Known limitations and future development roadmap
- `TESTING_LOCALLY.md` - Reproduce the CI checks on your own machine before pushing
- `VALIDATION_AND_BENCHMARKS.md` - Performance validation results and benchmark data

### 📁 Deployment/
Deployment guides and operational checklists, including [`DEPLOY_TO_RENDER.md`](Deployment/DEPLOY_TO_RENDER.md).

### 📁 Design/
Design descriptions for units inside the certification boundary. These are traceability
evidence: the certification gate verifies that the design elements they name exist.
- `AdvancedCFDSolver.md` - Design description for the CFD solver

### 📁 Security/
- `SECURITY_INCIDENT_RESPONSE.md` - Record of a past credential-exposure alert and its response

### 📁 Communication/
Project updates, research templates, and communication notes.

### 📁 Designs/
Engine designs, specifications, and related documentation.
- `HB-NLP-REV-001/` - Complete design package for the HB-NLP-REV-001 engine
  - `design_summary.md` - Detailed design documentation
  - `design.json` - Machine-readable design specifications
  - `design_script.py` - Automated design generation script
  - `3d_model.png` - 3D visualization of the engine
  - `flow_diagram.png` - Flow and process diagrams
  - `performance_dashboard.png` - Performance metrics visualization

## Related Directories

### ../Scripts/
Utility scripts for visualization, integration, and automation.
- `Visualization/` - Scripts for generating visualizations and images
- `Integration/` - Integration scripts for external tools (e.g., Plasticity)

### ../Assets/
Project assets including images and media files.
- `Images/` - General project images and graphics

## Navigation

- Return to [Project Root](../README.md)
- View [PlasticityDemo Documentation](../PlasticityDemo/README.md)
