"""Conceptual Blender digital twin for HelloblueGK's HB_NLP_RevolutionaryEngine.

Repository values represented:
  thrust: 3.5 MN; chamber pressure: 280 bar; nozzle length: 2.8 m;
  oxidizer: LOX; fuel: CH4; morphing nozzle: enabled;
  chamber alloy temperature limit: 3800 K.

This is a visualization model, not manufacturing CAD or certified analysis.
Run in Blender 5.2+: Scripting > New > paste/open this file > Run Script.
"""

import bpy
import math
from mathutils import Vector


ENGINE = {
    "name": "HB-NLP Revolutionary Engine",
    "thrust_n": 3_500_000.0,
    "chamber_pressure_bar": 280.0,
    "nozzle_length_m": 2.8,
    # 3800 K and 3500 K are material temperature limits in the engine definition
    # (chamber alloy and self-healing nozzle composite). Neither is a chamber
    # temperature; the engine design does not define one.
    "chamber_alloy_limit_k": 3800.0,
    "nozzle_composite_limit_k": 3500.0,
    "oxidizer": "LOX",
    "fuel": "CH4",
    "morphing_nozzle": True,
}


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.curves, bpy.data.meshes, bpy.data.materials,
                       bpy.data.cameras, bpy.data.lights):
        # Blender removes most orphans on save; avoid deleting data still in use.
        pass


def material(name, color, metallic=0.0, roughness=0.4, emission=None, strength=0.0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = color
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if emission is not None:
            bsdf.inputs["Emission Color"].default_value = emission
            bsdf.inputs["Emission Strength"].default_value = strength
    return mat


def smooth(obj):
    if obj.type == "MESH":
        for poly in obj.data.polygons:
            poly.use_smooth = True


def cylinder(name, radius, depth, z, mat, vertices=96):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=(0, 0, z))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    smooth(obj)
    bevel = obj.modifiers.new("Edge softening", "BEVEL")
    bevel.width = 0.045
    bevel.segments = 3
    return obj


def torus(name, major_radius, minor_radius, z, mat):
    bpy.ops.mesh.primitive_torus_add(major_radius=major_radius, minor_radius=minor_radius,
                                    major_segments=96, minor_segments=20,
                                    location=(0, 0, z))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    smooth(obj)
    return obj


def lathe(name, profile, mat, segments=128):
    """Create a surface of revolution from [(radius, z), ...]."""
    vertices = []
    faces = []
    for radius, z in profile:
        for i in range(segments):
            angle = 2.0 * math.pi * i / segments
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), z))
    rings = len(profile)
    for ring in range(rings - 1):
        for i in range(segments):
            nxt = (i + 1) % segments
            a = ring * segments + i
            b = ring * segments + nxt
            c = (ring + 1) * segments + nxt
            d = (ring + 1) * segments + i
            faces.append((a, b, c, d))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    smooth(obj)
    solidify = obj.modifiers.new("Wall thickness", "SOLIDIFY")
    solidify.thickness = 0.045
    return obj


def pipe(name, points, radius, mat):
    curve = bpy.data.curves.new(name + "Curve", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = 5
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for bp, point in zip(spline.bezier_points, points):
        bp.co = point
        bp.handle_left_type = "AUTO"
        bp.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def text_object(name, body, location, size=0.23, mat=None, extrude=0.008):
    bpy.ops.object.text_add(location=location, rotation=(math.radians(78), 0, 0))
    obj = bpy.context.object
    obj.name = name
    obj.data.body = body
    obj.data.align_x = "LEFT"
    obj.data.size = size
    obj.data.extrude = extrude
    if mat:
        obj.data.materials.append(mat)
    return obj


def add_plume(hot, plume):
    profile = [(0.22, -1.55), (0.48, -2.0), (0.82, -3.4), (0.24, -6.0)]
    obj = lathe("Exhaust_Plume_3_5MN", profile, plume, 96)
    obj.scale = (0.75, 0.75, 1.0)
    obj.keyframe_insert("scale", frame=1)
    obj.scale = (1.06, 1.06, 1.04)
    obj.keyframe_insert("scale", frame=8)
    obj.scale = (0.82, 0.82, 0.98)
    obj.keyframe_insert("scale", frame=16)
    obj.scale = (1.0, 1.0, 1.02)
    obj.keyframe_insert("scale", frame=24)
    # Blender 5.x stores action curves through layered animation data.
    # Default Bezier interpolation is intentionally retained for compatibility.
    return obj


def build_engine():
    clear_scene()

    steel = material("Nickel_Superalloy", (0.12, 0.15, 0.19, 1), 0.9, 0.2)
    copper = material("Copper_Chamber", (0.62, 0.16, 0.045, 1), 0.75, 0.25)
    carbon = material("Self_Healing_Nozzle_Composite", (0.025, 0.03, 0.04, 1), 0.45, 0.25)
    lox = material("LOX_Flow", (0.04, 0.42, 1.0, 1), 0.2, 0.2,
                   (0.02, 0.22, 1.0, 1), 3.0)
    fuel = material("Fuel_Flow", (0.1, 1.0, 0.38, 1), 0.1, 0.25,
                    (0.05, 1.0, 0.2, 1), 2.0)
    hot = material("Combustion_Core", (1.0, 0.08, 0.01, 1), 0.0, 0.15,
                   (1.0, 0.025, 0.002, 1), 8.0)
    plume = material("Exhaust_3_5MN", (0.08, 0.32, 1.0, 0.72), 0.0, 0.1,
                     (0.04, 0.25, 1.0, 1), 12.0)
    white = material("HUD_Text", (0.35, 0.85, 1.0, 1), 0.0, 0.25,
                     (0.1, 0.55, 1.0, 1), 2.0)

    # Injector, combustion chamber and regenerative cooling bands.
    cylinder("Injector_Assembly", 0.82, 0.30, 2.65, steel)
    cylinder("Combustion_Chamber", 0.72, 1.65, 1.72, copper)
    cylinder("Hot_Gas_Core", 0.60, 1.56, 1.72, hot)
    for z in (1.05, 1.32, 1.59, 1.86, 2.13, 2.40):
        torus("Cooling_Channel", 0.73, 0.035, z, lox)

    # Repository specifies a 2.8 m morphing nozzle. Z spans 0.80 to -2.00.
    nozzle_profile = [
        (0.70, 0.90), (0.52, 0.55), (0.28, 0.28),
        (0.34, -0.15), (0.52, -0.65), (0.78, -1.20), (1.16, -1.90)
    ]
    nozzle = lathe("Morphing_Nozzle_2_8m", nozzle_profile, carbon)
    nozzle.scale = (0.96, 0.96, 1.0)
    nozzle.keyframe_insert("scale", frame=1)
    nozzle.scale = (1.04, 1.04, 1.0)
    nozzle.keyframe_insert("scale", frame=60)
    nozzle.scale = (0.96, 0.96, 1.0)
    nozzle.keyframe_insert("scale", frame=120)

    # Twin turbopump/preburner concept implied by the engine architecture.
    for x, flow_mat, label in ((-1.18, lox, "LOX"), (1.18, fuel, "FUEL")):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32,
                                             location=(x, 0, 2.55), scale=(0.52, 0.52, 0.42))
        pump = bpy.context.object
        pump.name = label + "_Turbopump"
        pump.data.materials.append(steel)
        smooth(pump)
        pipe(label + "_Feed", [(x * 1.4, 0, 3.75), (x, 0, 3.2),
                                (x, 0, 2.55), (x * 0.45, 0, 2.55)], 0.10, flow_mat)

    torus("Thrust_Frame", 1.45, 0.10, 2.75, steel)
    add_plume(hot, plume)

    # Telemetry panel uses the actual repository baseline values.
    panel = (
        "HELLOBLUEGK // DIGITAL TWIN\n"
        "HB-NLP REVOLUTIONARY ENGINE\n\n"
        f"THRUST             {ENGINE['thrust_n']/1e6:.1f} MN\n"
        f"CHAMBER PRESSURE   {ENGINE['chamber_pressure_bar']:.0f} bar\n"
        f"NOZZLE LENGTH      {ENGINE['nozzle_length_m']:.1f} m\n"
        f"OXIDIZER           {ENGINE['oxidizer']}\n"
        f"FUEL               {ENGINE['fuel']}\n"
        f"ALLOY TEMP LIMIT   {ENGINE['chamber_alloy_limit_k']:.0f} K\n"
        f"NOZZLE TEMP LIMIT  {ENGINE['nozzle_composite_limit_k']:.0f} K\n"
        "MORPHING NOZZLE    ACTIVE\n\n"
        "CONCEPT VISUALIZATION — NOT FLIGHT CERTIFIED"
    )
    text_object("Telemetry_HUD", panel, (2.4, 0.9, 2.8), 0.20, white)

    # Ground, lighting and camera.
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, -6.1))
    ground = bpy.context.object
    ground.name = "Test_Stand_Floor"
    ground.data.materials.append(material("Floor", (0.012, 0.016, 0.025, 1), 0.15, 0.5))

    bpy.ops.object.light_add(type="AREA", location=(4, -4, 6))
    bpy.context.object.data.energy = 1400
    bpy.context.object.data.shape = "DISK"
    bpy.context.object.data.size = 5
    bpy.ops.object.light_add(type="AREA", location=(-4, 2, 1))
    bpy.context.object.data.energy = 900
    bpy.context.object.data.color = (0.12, 0.35, 1.0)
    bpy.context.object.data.size = 4

    bpy.ops.object.camera_add(location=(10.5, -13.5, 6.2))
    camera = bpy.context.object
    camera.name = "Digital_Twin_Camera"
    direction = Vector((0, 0, 0.1)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 58
    bpy.context.scene.camera = camera

    world = bpy.context.scene.world
    world.color = (0.003, 0.006, 0.015)
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = 120
    # Blender 5.2 exposes Eevee as BLENDER_EEVEE; some earlier releases used
    # BLENDER_EEVEE_NEXT. Prefer the current identifier and fall back safely.
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 60

    print("HelloblueGK HB-NLP Revolutionary Engine digital twin created.")
    print(ENGINE)


build_engine()
