import math
import os
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
FONT_PATH = REPO_ROOT / "Assets/UI/Fonts/Cinzel.ttf"
MODEL_PATH = REPO_ROOT / "Assets/Environment/MercatoVecchio/ProductionKit/Models/AlbergoFiorentinoSign.glb"
BLEND_PATH = REPO_ROOT / "ArtSource/Blender/AlbergoFiorentinoSign.blend"
PREVIEW_FRONT = REPO_ROOT / "Temp/AlbergoFiorentinoSign_Front.png"
PREVIEW_BACK = REPO_ROOT / "Temp/AlbergoFiorentinoSign_Back.png"


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    principled = mat.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    metallic_input = principled.inputs.get("Metallic") or principled.inputs.get("Metallic IOR Level")
    if metallic_input is None:
        raise RuntimeError("Blender Principled BSDF exposes no metallic input")
    metallic_input.default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    return mat


def assign_material(obj, mat):
    if hasattr(obj.data, "materials"):
        obj.data.materials.append(mat)


def smooth_mesh(obj):
    if obj.type != "MESH":
        return
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def apply_bevel(obj, width, segments=3):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    modifier = obj.modifiers.new("Hand-finished bevel", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def parent_to_root(obj, root):
    obj.parent = root
    return obj


def add_box(name, location, dimensions, mat, root, bevel=0.02):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    assign_material(obj, mat)
    if bevel > 0:
        apply_bevel(obj, bevel)
    return parent_to_root(obj, root)


def add_cylinder(name, radius, depth, mat, root, bevel=0.015):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=64,
        radius=radius,
        depth=depth,
        location=(0.0, 0.0, 0.0),
        rotation=(0.0, math.radians(90.0), 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    assign_material(obj, mat)
    if bevel > 0:
        apply_bevel(obj, bevel, 4)
    smooth_mesh(obj)
    return parent_to_root(obj, root)


def add_torus(name, location, major_radius, minor_radius, mat, root):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=32,
        minor_segments=8,
        major_radius=major_radius,
        minor_radius=minor_radius,
        location=location,
        rotation=(0.0, math.radians(90.0), 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    assign_material(obj, mat)
    smooth_mesh(obj)
    return parent_to_root(obj, root)


def add_rod_between(name, start, end, radius, mat, root):
    start_v = Vector(start)
    end_v = Vector(end)
    delta = end_v - start_v
    midpoint = (start_v + end_v) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius, depth=delta.length, location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0.0, 0.0, 1.0)).rotation_difference(delta.normalized())
    assign_material(obj, mat)
    smooth_mesh(obj)
    return parent_to_root(obj, root)


def create_text(name, face_x, back, font, mat, root):
    bpy.ops.object.text_add(location=(0.0, 0.0, 0.0))
    obj = bpy.context.object
    obj.name = name
    curve = obj.data
    curve.body = "ALBERGO\nFIORENTINO"
    curve.font = font
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    curve.size = 0.24
    curve.space_character = 1.02
    curve.space_line = 0.82
    curve.extrude = 0.008
    curve.bevel_depth = 0.0015
    curve.bevel_resolution = 0
    curve.resolution_u = 3
    assign_material(obj, mat)

    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    obj.name = name
    obj.select_set(False)

    target_width = 1.02
    target_height = 0.40
    scale = min(target_width / obj.dimensions.x, target_height / obj.dimensions.y)
    obj.scale *= scale
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.dissolve_limited(angle_limit=math.radians(0.5), use_dissolve_boundaries=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    decimate = obj.modifiers.new("Game-ready lettering", "DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.ratio = 0.35
    decimate.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    obj.select_set(False)

    local_x = Vector((0.0, 1.0 if back else -1.0, 0.0))
    local_y = Vector((0.0, 0.0, 1.0))
    local_z = local_x.cross(local_y)
    orientation = Matrix((local_x, local_y, local_z)).transposed().to_4x4()
    obj.matrix_world = Matrix.Translation((face_x, 0.0, -0.19)) @ orientation
    return parent_to_root(obj, root)


def add_lily_piece_sphere(name, location, scale, rotation_x, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=10, location=location, rotation=(rotation_x, 0.0, 0.0))
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)
    assign_material(obj, mat)
    smooth_mesh(obj)
    return obj


def create_lily(name, face_x, mat, root):
    pieces = [
        add_lily_piece_sphere(name + "_Center", (face_x, 0.0, 0.25), (0.018, 0.085, 0.19), 0.0, mat),
        add_lily_piece_sphere(name + "_Left", (face_x, -0.13, 0.22), (0.018, 0.065, 0.16), math.radians(-42.0), mat),
        add_lily_piece_sphere(name + "_Right", (face_x, 0.13, 0.22), (0.018, 0.065, 0.16), math.radians(42.0), mat),
    ]
    stem = add_box(name + "_Stem", (face_x, 0.0, 0.065), (0.036, 0.055, 0.22), mat, root=None, bevel=0.01)
    band = add_box(name + "_Band", (face_x, 0.0, 0.135), (0.036, 0.34, 0.045), mat, root=None, bevel=0.012)
    pieces.extend((stem, band))

    bpy.ops.object.select_all(action="DESELECT")
    for piece in pieces:
        piece.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    lily = bpy.context.object
    lily.name = name
    lily.select_set(False)
    return parent_to_root(lily, root)


def create_sign():
    chestnut = material("Sign Chestnut", (0.085, 0.022, 0.010), 0.0, 0.38)
    brass = material("Worn Florentine Brass", (0.50, 0.255, 0.055), 0.76, 0.31)
    ivory = material("Warm Ivory Lettering", (0.88, 0.70, 0.38), 0.18, 0.38)
    iron = material("Hand-forged Iron", (0.035, 0.03, 0.026), 0.64, 0.46)

    root = bpy.data.objects.new("InnFacade_SignAssembly", None)
    bpy.context.collection.objects.link(root)

    add_cylinder("InnFacade_SignRim", 0.72, 0.12, brass, root, 0.025)
    add_cylinder("InnFacade_SignMedallion", 0.65, 0.16, chestnut, root, 0.035)
    add_torus("InnFacade_SignTrim_North", (-0.087, 0.0, 0.0), 0.585, 0.016, brass, root)
    add_torus("InnFacade_SignTrim_South", (0.087, 0.0, 0.0), 0.585, 0.016, brass, root)

    add_box("InnFacade_SignBracket", (0.0, 0.775, 0.89), (0.13, 1.55, 0.13), iron, root, 0.025)
    add_box("InnFacade_SignWallPlate", (0.0, 1.53, 0.89), (0.42, 0.09, 0.48), iron, root, 0.035)
    add_rod_between("InnFacade_SignBrace_Left", (-0.16, 1.49, 0.64), (-0.16, 0.72, 0.89), 0.032, iron, root)
    add_rod_between("InnFacade_SignBrace_Right", (0.16, 1.49, 0.64), (0.16, 0.72, 0.89), 0.032, iron, root)
    add_torus("InnFacade_SignHanger_Left", (0.0, -0.27, 0.75), 0.105, 0.026, iron, root)
    add_torus("InnFacade_SignHanger_Right", (0.0, 0.27, 0.75), 0.105, 0.026, iron, root)

    font = bpy.data.fonts.load(str(FONT_PATH))
    create_lily("InnFacade_SignLily_North", -0.095, brass, root)
    create_lily("InnFacade_SignLily_South", 0.095, brass, root)
    create_text("InnFacade_SignText_North", -0.097, False, font, ivory, root)
    create_text("InnFacade_SignText_South", 0.097, True, font, ivory, root)
    return root


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_preview_lighting():
    world = bpy.context.scene.world or bpy.data.worlds.new("Sign Preview World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.032, 0.038, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.42

    for name, location, energy, size, color in (
        ("Key", (-2.4, -3.2, 3.3), 900.0, 3.0, (1.0, 0.72, 0.46)),
        ("Fill", (2.8, -1.2, 1.0), 520.0, 2.5, (0.48, 0.67, 1.0)),
        ("Rim", (0.0, 2.5, 2.8), 800.0, 2.0, (1.0, 0.45, 0.21)),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        data.color = color
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        look_at(light, (0.0, 0.0, 0.15))


def render_preview(path, location):
    camera_data = bpy.data.cameras.get("Preview Camera")
    if camera_data is None:
        camera_data = bpy.data.cameras.new("Preview Camera")
        camera = bpy.data.objects.new("Preview Camera", camera_data)
        bpy.context.collection.objects.link(camera)
    else:
        camera = bpy.data.objects.get("Preview Camera")
    camera.location = location
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.55
    look_at(camera, (0.0, 0.25, 0.18))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = str(path)
    scene.render.image_settings.color_mode = "RGBA"
    bpy.ops.render.render(write_still=True)


def export_sign(root):
    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    PREVIEW_FRONT.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(MODEL_PATH),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
    )


def main():
    reset_scene()
    root = create_sign()
    export_sign(root)
    add_preview_lighting()
    render_preview(PREVIEW_FRONT, (-4.1, 0.0, 0.35))
    render_preview(PREVIEW_BACK, (4.1, 0.0, 0.35))

    mesh_objects = [obj for obj in root.children_recursive if obj.type == "MESH"]
    for obj in mesh_objects:
        obj.data.calc_loop_triangles()
    triangles = sum(len(obj.data.loop_triangles) for obj in mesh_objects)
    for obj in sorted(mesh_objects, key=lambda item: len(item.data.loop_triangles), reverse=True):
        print(f"[AlbergoFiorentinoSign]   {obj.name}: {len(obj.data.loop_triangles)} triangles")
    print(f"[AlbergoFiorentinoSign] Exported {len(mesh_objects)} mesh objects, approximately {triangles} triangles")
    print(f"[AlbergoFiorentinoSign] Model: {MODEL_PATH}")
    print(f"[AlbergoFiorentinoSign] Front preview: {PREVIEW_FRONT}")
    print(f"[AlbergoFiorentinoSign] Back preview: {PREVIEW_BACK}")


if __name__ == "__main__":
    main()
