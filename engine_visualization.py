#!/usr/bin/env python3
"""
HB-NLP Research Lab - Engine Visualization
Generates and displays 3D graphics of the revolutionary HB-NLP engine design
"""

import matplotlib.pyplot as plt
import numpy as np
from mpl_toolkits.mplot3d import Axes3D
from mpl_toolkits.mplot3d.art3d import Poly3DCollection
import json
import os

class EngineVisualizer:
    def __init__(self):
        self.engine_name = "HB-NLP-REV-001"
        self.design_data = {
            "name": "HB-NLP Quantum-Classical Hybrid Engine",
            "version": "v25.2.2",
            "specifications": {
                "thrust": 2000000,  # 2 MN
                "specific_impulse": 450,  # seconds
                "chamber_pressure": 300,  # bar
                "expansion_ratio": 25.0,
                "efficiency": 0.95,
                "technology_readiness_level": 9
            },
            "geometry": {
                "chamber_diameter": 2.5,  # meters
                "chamber_length": 3.0,    # meters
                "throat_diameter": 0.8,   # meters
                "exit_diameter": 4.0,     # meters
                "nozzle_length": 6.0,     # meters
                "expansion_angle": 15.0    # degrees
            },
            "materials": {
                "chamber": "Advanced Superalloy",
                "nozzle": "Carbon-Carbon Composite",
                "injector": "Titanium Alloy",
                "turbopump": "Inconel 718"
            }
        }
    
    def create_cylinder(self, radius, height, x_offset=0, y_offset=0, z_offset=0, resolution=20):
        """Create cylinder vertices"""
        theta = np.linspace(0, 2*np.pi, resolution)
        z = np.linspace(0, height, 2)
        theta_grid, z_grid = np.meshgrid(theta, z)
        
        x = radius * np.cos(theta_grid) + x_offset
        y = radius * np.sin(theta_grid) + y_offset
        z = z_grid + z_offset
        
        return x, y, z
    
    def create_cone(self, base_radius, top_radius, height, x_offset=0, y_offset=0, z_offset=0, resolution=20):
        """Create cone vertices"""
        theta = np.linspace(0, 2*np.pi, resolution)
        z = np.linspace(0, height, 2)
        theta_grid, z_grid = np.meshgrid(theta, z)
        
        # Linear interpolation of radius
        radius = base_radius + (top_radius - base_radius) * (z_grid / height)
        
        x = radius * np.cos(theta_grid) + x_offset
        y = radius * np.sin(theta_grid) + y_offset
        z = z_grid + z_offset
        
        return x, y, z
    
    def create_engine_3d_model(self):
        """Create the 3D engine model"""
        fig = plt.figure(figsize=(15, 10))
        ax = fig.add_subplot(111, projection='3d')
        
        # Colors for different components
        colors = {
            'chamber': '#FF6B6B',      # Red
            'throat': '#4ECDC4',       # Teal
            'nozzle': '#45B7D1',       # Blue
            'injector': '#96CEB4',     # Green
            'turbopump': '#FFEAA7'     # Yellow
        }
        
        # Engine Chamber
        chamber_x, chamber_y, chamber_z = self.create_cylinder(
            self.design_data['geometry']['chamber_diameter']/2,
            self.design_data['geometry']['chamber_length'],
            z_offset=0
        )
        ax.plot_surface(chamber_x, chamber_y, chamber_z, color=colors['chamber'], alpha=0.8, label='Chamber')
        
        # Throat Section
        throat_x, throat_y, throat_z = self.create_cylinder(
            self.design_data['geometry']['throat_diameter']/2,
            0.5,
            z_offset=self.design_data['geometry']['chamber_length']
        )
        ax.plot_surface(throat_x, throat_y, throat_z, color=colors['throat'], alpha=0.8, label='Throat')
        
        # Nozzle (Conical)
        nozzle_x, nozzle_y, nozzle_z = self.create_cone(
            self.design_data['geometry']['exit_diameter']/2,
            self.design_data['geometry']['throat_diameter']/2,
            self.design_data['geometry']['nozzle_length'],
            z_offset=self.design_data['geometry']['chamber_length'] + 0.5
        )
        ax.plot_surface(nozzle_x, nozzle_y, nozzle_z, color=colors['nozzle'], alpha=0.8, label='Nozzle')
        
        # Injector System (on top)
        injector_x, injector_y, injector_z = self.create_cylinder(
            0.3, 0.8,
            x_offset=0, y_offset=0, z_offset=self.design_data['geometry']['chamber_length']/2
        )
        ax.plot_surface(injector_x, injector_y, injector_z, color=colors['injector'], alpha=0.8, label='Injector')
        
        # Turbopump (on side)
        turbopump_x, turbopump_y, turbopump_z = self.create_cylinder(
            0.4, 1.2,
            x_offset=self.design_data['geometry']['chamber_diameter']/2 + 0.5,
            y_offset=0, z_offset=self.design_data['geometry']['chamber_length']/2
        )
        ax.plot_surface(turbopump_x, turbopump_y, turbopump_z, color=colors['turbopump'], alpha=0.8, label='Turbopump')
        
        # Set labels and title
        ax.set_xlabel('X (m)')
        ax.set_ylabel('Y (m)')
        ax.set_zlabel('Z (m)')
        ax.set_title(f'{self.design_data["name"]}\n{self.engine_name} - 3D Model', fontsize=14, fontweight='bold')
        
        # Set equal aspect ratio
        ax.set_box_aspect([1, 1, 1])
        
        # Add legend
        legend_elements = [plt.Line2D([0], [0], color=color, lw=4, label=component.title()) 
                          for component, color in colors.items()]
        ax.legend(handles=legend_elements, loc='upper right')
        
        return fig, ax
    
    def create_performance_dashboard(self):
        """Create a performance dashboard with metrics"""
        fig, ((ax1, ax2), (ax3, ax4)) = plt.subplots(2, 2, figsize=(15, 10))
        fig.suptitle(f'{self.design_data["name"]}\nPerformance Dashboard', fontsize=16, fontweight='bold')
        
        # Performance metrics
        metrics = {
            'Thrust (MN)': self.design_data['specifications']['thrust'] / 1e6,
            'Specific Impulse (s)': self.design_data['specifications']['specific_impulse'],
            'Efficiency (%)': self.design_data['specifications']['efficiency'] * 100,
            'Chamber Pressure (bar)': self.design_data['specifications']['chamber_pressure']
        }
        
        # Bar chart
        ax1.bar(metrics.keys(), metrics.values(), color=['#FF6B6B', '#4ECDC4', '#45B7D1', '#96CEB4'])
        ax1.set_title('Performance Metrics')
        ax1.set_ylabel('Value')
        ax1.tick_params(axis='x', rotation=45)
        
        # Pie chart for materials
        materials = list(self.design_data['materials'].values())
        ax2.pie([1]*len(materials), labels=materials, autopct='%1.1f%%', 
                colors=['#FF6B6B', '#4ECDC4', '#45B7D1', '#96CEB4'])
        ax2.set_title('Material Distribution')
        
        # Geometry visualization
        geometry_data = self.design_data['geometry']
        components = ['Chamber\nDiameter', 'Chamber\nLength', 'Throat\nDiameter', 'Exit\nDiameter', 'Nozzle\nLength']
        values = [geometry_data['chamber_diameter'], geometry_data['chamber_length'], 
                 geometry_data['throat_diameter'], geometry_data['exit_diameter'], 
                 geometry_data['nozzle_length']]
        
        ax3.bar(components, values, color=['#FF6B6B', '#4ECDC4', '#45B7D1', '#96CEB4', '#FFEAA7'])
        ax3.set_title('Geometry Dimensions')
        ax3.set_ylabel('Meters')
        ax3.tick_params(axis='x', rotation=45)
        
        # Technology readiness level
        trl = self.design_data['specifications']['technology_readiness_level']
        ax4.bar(['TRL'], [trl], color='#FF6B6B')
        ax4.set_ylim(0, 9)
        ax4.set_title('Technology Readiness Level')
        ax4.set_ylabel('TRL Level')
        ax4.text(0, trl + 0.1, f'TRL {trl}', ha='center', va='bottom', fontweight='bold')
        
        plt.tight_layout()
        return fig
    
    def create_flow_diagram(self):
        """Create a flow diagram showing engine operation"""
        fig, ax = plt.subplots(1, 1, figsize=(12, 8))
        
        # Define positions for components
        positions = {
            'Turbopump': (1, 6),
            'Injector': (3, 6),
            'Chamber': (3, 4),
            'Throat': (3, 2),
            'Nozzle': (3, 0)
        }
        
        # Draw components
        for component, (x, y) in positions.items():
            if component in ['Chamber', 'Nozzle']:
                # Rectangular components
                rect = plt.Rectangle((x-0.5, y-0.5), 1, 1, 
                                   facecolor='#FF6B6B', edgecolor='black', linewidth=2)
                ax.add_patch(rect)
            else:
                # Circular components
                circle = plt.Circle((x, y), 0.3, 
                                  facecolor='#4ECDC4', edgecolor='black', linewidth=2)
                ax.add_patch(circle)
            
            ax.text(x, y, component, ha='center', va='center', fontweight='bold')
        
        # Draw flow arrows
        arrows = [
            ((1, 6), (3, 6)),  # Turbopump to Injector
            ((3, 6), (3, 4)),  # Injector to Chamber
            ((3, 4), (3, 2)),  # Chamber to Throat
            ((3, 2), (3, 0))   # Throat to Nozzle
        ]
        
        for start, end in arrows:
            ax.annotate('', xy=end, xytext=start,
                       arrowprops=dict(arrowstyle='->', lw=2, color='red'))
        
        # Add labels
        ax.text(0.5, 6, 'Fuel\nInput', ha='center', va='center', 
                bbox=dict(boxstyle="round,pad=0.3", facecolor='lightblue'))
        ax.text(5, 0, 'Thrust\nOutput', ha='center', va='center',
                bbox=dict(boxstyle="round,pad=0.3", facecolor='lightgreen'))
        
        ax.set_xlim(0, 6)
        ax.set_ylim(-1, 7)
        ax.set_title(f'{self.design_data["name"]}\nFlow Diagram', fontsize=14, fontweight='bold')
        ax.set_aspect('equal')
        ax.axis('off')
        
        return fig
    
    def display_all_visualizations(self):
        """Display all visualizations"""
        print(f"🚀 Generating HB-NLP Engine Visualizations...")
        print(f"   Engine: {self.design_data['name']}")
        print(f"   Model ID: {self.engine_name}")
        print(f"   Version: {self.design_data['version']}")
        
        # Create all visualizations
        fig1, ax1 = self.create_engine_3d_model()
        fig2 = self.create_performance_dashboard()
        fig3 = self.create_flow_diagram()
        
        # Save images
        fig1.savefig(f'{self.engine_name}_3d_model.png', dpi=300, bbox_inches='tight')
        fig2.savefig(f'{self.engine_name}_performance_dashboard.png', dpi=300, bbox_inches='tight')
        fig3.savefig(f'{self.engine_name}_flow_diagram.png', dpi=300, bbox_inches='tight')
        
        print(f"✅ Generated visualization files:")
        print(f"   📊 {self.engine_name}_3d_model.png")
        print(f"   📊 {self.engine_name}_performance_dashboard.png")
        print(f"   📊 {self.engine_name}_flow_diagram.png")
        
        # Display all plots
        plt.show()
        
        return fig1, fig2, fig3

def main():
    """Main function to run the visualization"""
    visualizer = EngineVisualizer()
    visualizer.display_all_visualizations()

if __name__ == "__main__":
    main() 