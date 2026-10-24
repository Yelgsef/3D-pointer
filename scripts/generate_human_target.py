"""Generate an original, low-poly rigged human FBX with Blender 4.x."""

import argparse
import math
import os
import sys

import bpy
from mathutils import Vector


def material(name, color, roughness=0.78):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    principled = mat.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Roughness"].default_value = roughness
    return mat


def add_ellipsoid(name, center, radii, bone, mat, direction=None):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=16, ring_count=12, radius=1.0, location=center
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = radii
    if direction is not None:
        obj.rotation_mode = "QUATERNION"
        obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(
            Vector(direction).normalized()
        )
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.materials.append(mat)
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def capsule(name, start, end, radius, bone, mat, depth=None):
    start, end = Vector(start), Vector(end)
    direction = end - start
    return add_ellipsoid(
        name,
        (start + end) * 0.5,
        (radius, depth or radius, direction.length * 0.66 + radius * 0.34),
        bone,
        mat,
        direction,
    )


def create_armature():
    armature = bpy.data.armatures.new("HumanArmature")
    rig = bpy.data.objects.new("Armature", armature)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    bones = {
        "Root": ((0, 0, 0), (0, 0, 0.92), None),
        "Hips": ((0, 0, 0.88), (0, 0, 1.10), "Root"),
        "Spine": ((0, 0, 1.10), (0, 0, 1.34), "Hips"),
        "Chest": ((0, 0, 1.34), (0, 0, 1.50), "Spine"),
        "Neck": ((0, 0, 1.50), (0, 0, 1.68), "Chest"),
        "Head": ((0, 0, 1.68), (0, 0, 1.96), "Neck"),
        "LeftShoulder": ((0, 0, 1.47), (-0.30, 0, 1.47), "Chest"),
        "LeftUpperArm": ((-0.30, 0, 1.47), (-0.42, 0, 1.18), "LeftShoulder"),
        "LeftLowerArm": ((-0.42, 0, 1.18), (-0.47, 0, 0.94), "LeftUpperArm"),
        "LeftHand": ((-0.47, 0, 0.94), (-0.48, -0.07, 0.82), "LeftLowerArm"),
        "LeftIndexProximal": ((-0.48, -0.07, 0.91), (-0.48, -0.18, 0.91), "LeftHand"),
        "RightShoulder": ((0, 0, 1.47), (0.30, 0, 1.47), "Chest"),
        "RightUpperArm": ((0.30, 0, 1.47), (0.42, 0, 1.18), "RightShoulder"),
        "RightLowerArm": ((0.42, 0, 1.18), (0.47, 0, 0.94), "RightUpperArm"),
        "RightHand": ((0.47, 0, 0.94), (0.48, -0.07, 0.82), "RightLowerArm"),
        "RightIndexProximal": ((0.48, -0.07, 0.91), (0.48, -0.18, 0.91), "RightHand"),
        "LeftUpperLeg": ((-0.13, 0, 0.94), (-0.14, 0, 0.53), "Hips"),
        "LeftLowerLeg": ((-0.14, 0, 0.53), (-0.14, 0, 0.13), "LeftUpperLeg"),
        "LeftFoot": ((-0.14, 0, 0.13), (-0.14, -0.22, 0.08), "LeftLowerLeg"),
        "RightUpperLeg": ((0.13, 0, 0.94), (0.14, 0, 0.53), "Hips"),
        "RightLowerLeg": ((0.14, 0, 0.53), (0.14, 0, 0.13), "RightUpperLeg"),
        "RightFoot": ((0.14, 0, 0.13), (0.14, -0.22, 0.08), "RightLowerLeg"),
    }

    for name, (head, tail, parent_name) in bones.items():
        bone = armature.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        if parent_name:
            bone.parent = armature.edit_bones[parent_name]
            bone.use_connect = (Vector(head) - bone.parent.tail).length < 0.001
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig, bones


def build(output):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    skin = material("Human_Skin", (0.58, 0.34, 0.23))
    shirt = material("Human_Shirt", (0.16, 0.30, 0.36))
    trousers = material("Human_Trousers", (0.12, 0.16, 0.21))
    shoes = material("Human_Shoes", (0.055, 0.06, 0.07))
    hair = material("Human_Hair", (0.12, 0.075, 0.055))
    eyes = material("Human_Eyes", (0.035, 0.03, 0.025), 0.42)

    parts = []
    add = lambda *args, **kwargs: parts.append(add_ellipsoid(*args, **kwargs))
    add("ShirtTorso", (0, 0, 1.31), (0.29, 0.17, 0.32), "Chest", shirt)
    add("Pelvis", (0, 0, 1.00), (0.24, 0.15, 0.18), "Hips", trousers)
    add("NeckMesh", (0, 0, 1.61), (0.085, 0.085, 0.12), "Neck", skin)
    add("HeadMesh", (0, 0, 1.82), (0.18, 0.16, 0.23), "Head", skin)
    add("HairCap", (0, 0.005, 1.99), (0.18, 0.16, 0.09), "Head", hair)
    add("Nose", (0, -0.153, 1.82), (0.035, 0.045, 0.055), "Head", skin)
    add("LeftEye", (-0.065, -0.147, 1.855), (0.018, 0.015, 0.016), "Head", eyes)
    add("RightEye", (0.065, -0.147, 1.855), (0.018, 0.015, 0.016), "Head", eyes)

    for side, xsign in (("Left", -1), ("Right", 1)):
        shoulder = (xsign * 0.30, 0, 1.47)
        elbow = (xsign * 0.42, 0, 1.18)
        wrist = (xsign * 0.47, 0, 0.94)
        hand = (xsign * 0.48, -0.055, 0.88)
        parts.append(capsule(side + "Sleeve", shoulder, elbow, 0.085, side + "UpperArm", shirt))
        parts.append(capsule(side + "Forearm", elbow, wrist, 0.060, side + "LowerArm", skin))
        add(side + "Palm", hand, (0.065, 0.045, 0.085), side + "Hand", skin)
        finger_start = (xsign * 0.48, -0.075, 0.91)
        finger_end = (xsign * 0.48, -0.185, 0.91)
        parts.append(capsule(side + "IndexFinger", finger_start, finger_end, 0.018, side + "IndexProximal", skin))

        hip = (xsign * 0.13, 0, 0.94)
        knee = (xsign * 0.14, 0, 0.53)
        ankle = (xsign * 0.14, 0, 0.13)
        toe = (xsign * 0.14, -0.18, 0.08)
        parts.append(capsule(side + "Thigh", hip, knee, 0.115, side + "UpperLeg", trousers))
        parts.append(capsule(side + "Shin", knee, ankle, 0.082, side + "LowerLeg", trousers))
        parts.append(capsule(side + "Shoe", ankle, toe, 0.075, side + "Foot", shoes, depth=0.105))

    rig, _ = create_armature()
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    mesh = bpy.context.object
    mesh.name = "HumanVisualMesh"
    modifier = mesh.modifiers.new("HumanSkinning", "ARMATURE")
    modifier.object = rig
    modifier.use_vertex_groups = True
    mesh.parent = rig

    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    os.makedirs(os.path.dirname(output), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=output,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        apply_unit_scale=True,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="AUTO",
    )
    print("Generated original rigged human:", output)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    args = parser.parse_args(argv)
    build(os.path.abspath(args.output))


if __name__ == "__main__":
    main()
