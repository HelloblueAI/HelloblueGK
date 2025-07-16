#!/usr/bin/env python3
"""
HB-NLP Research Lab - Aerospace Engine Design Image Generator
Generates a technical diagram of the advanced aerospace engine design
"""

import matplotlib.pyplot as plt
import matplotlib.patches as patches
from matplotlib.patches import FancyBboxPatch, Circle, Rectangle, Polygon
import numpy as np

def create_engine_diagram():
    """Create a technical aerospace engine diagram"""
    
    # Create figure and axis
    fig, ax = plt.subplots(1, 1, figsize=(12, 8))
    ax.set_xlim(0, 12)
    ax.set_ylim(0, 8)
    ax.set_aspect('equal')
    
    # Remove axes
    ax.axis('off')
    
    # Engine components
    components = [
        # Combustion Chamber
        {'type': 'rectangle', 'x': 2, 'y': 3, 'width': 2, 'height': 2, 
         'color': 'red', 'alpha': 0.7, 'label': 'Combustion\nChamber'},
        
        # Nozzle
        {'type': 'polygon', 'points': [[4, 2.5], [8, 2], [8, 4], [4, 3.5]], 
         'color': 'orange', 'alpha': 0.7, 'label': 'Nozzle'},
        
        # Fuel Injector
        {'type': 'circle', 'x': 1.5, 'y': 4, 'radius': 0.3, 
         'color': 'blue', 'alpha': 0.8, 'label': 'Fuel\nInjector'},
        
        # Oxidizer Injector
        {'type': 'circle', 'x': 1.5, 'y': 2, 'radius': 0.3, 
         'color': 'cyan', 'alpha': 0.8, 'label': 'Oxidizer\nInjector'},
        
        # Turbopump
        {'type': 'rectangle', 'x': 0.5, 'y': 1, 'width': 1, 'height': 1, 
         'color': 'green', 'alpha': 0.7, 'label': 'Turbopump'},
        
        # Cooling Channels
        {'type': 'rectangle', 'x': 1.8, 'y': 2.8, 'width': 0.1, 'height': 1.4, 
         'color': 'purple', 'alpha': 0.6, 'label': 'Cooling\nChannels'},
    ]
    
    # Draw components
    for comp in components:
        if comp['type'] == 'rectangle':
            rect = Rectangle((comp['x'], comp['y']), comp['width'], comp['height'],
                           facecolor=comp['color'], alpha=comp['alpha'], 
                           edgecolor='black', linewidth=1)
            ax.add_patch(rect)
            ax.text(comp['x'] + comp['width']/2, comp['y'] + comp['height']/2, 
                   comp['label'], ha='center', va='center', fontsize=8, fontweight='bold')
            
        elif comp['type'] == 'polygon':
            poly = Polygon(comp['points'], facecolor=comp['color'], alpha=comp['alpha'],
                          edgecolor='black', linewidth=1)
            ax.add_patch(poly)
            ax.text(6, 3, comp['label'], ha='center', va='center', fontsize=8, fontweight='bold')
            
        elif comp['type'] == 'circle':
            circle = Circle((comp['x'], comp['y']), comp['radius'], 
                          facecolor=comp['color'], alpha=comp['alpha'],
                          edgecolor='black', linewidth=1)
            ax.add_patch(circle)
            ax.text(comp['x'], comp['y'], comp['label'], ha='center', va='center', 
                   fontsize=7, fontweight='bold')
    
    # Add flow arrows
    arrows = [
        # Fuel flow
        {'start': (0.8, 4), 'end': (1.2, 4), 'color': 'blue', 'label': 'Fuel'},
        # Oxidizer flow
        {'start': (0.8, 2), 'end': (1.2, 2), 'color': 'cyan', 'label': 'Oxidizer'},
        # Exhaust flow
        {'start': (8, 3), 'end': (10, 3), 'color': 'red', 'label': 'Exhaust'},
    ]
    
    for arrow in arrows:
        ax.annotate('', xy=arrow['end'], xytext=arrow['start'],
                   arrowprops=dict(arrowstyle='->', color=arrow['color'], lw=2))
        ax.text((arrow['start'][0] + arrow['end'][0])/2, 
               (arrow['start'][1] + arrow['end'][1])/2 + 0.2,
               arrow['label'], ha='center', fontsize=8, fontweight='bold',
               color=arrow['color'])
    
    # Add title and specifications
    ax.text(6, 7.5, 'HB-NLP Advanced Aerospace Engine Design', 
           ha='center', fontsize=16, fontweight='bold')
    
    # Add specifications
    specs = [
        'Thrust: 2,300 kN',
        'ISP: 350 s', 
        'Chamber Pressure: 300 bar',
        'Propellant: Methane/LOX',
        'Expansion Ratio: 40:1'
    ]
    
    for i, spec in enumerate(specs):
        ax.text(10, 6 - i*0.5, spec, fontsize=10, fontweight='bold')
    
    # Add technical details
    ax.text(1, 0.5, 'Advanced Features:\n• CFD Analysis\n• Thermal Management\n• Structural Optimization\n• AI-Driven Design', 
           fontsize=9, bbox=dict(boxstyle="round,pad=0.3", facecolor="lightgray", alpha=0.8))
    
    plt.tight_layout()
    plt.savefig('HB-NLP-Advanced-Engine-Design.png', dpi=300, bbox_inches='tight')
    plt.close()
    
    print("Engine diagram generated: HB-NLP-Advanced-Engine-Design.png")

if __name__ == "__main__":
    create_engine_diagram() 