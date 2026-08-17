"""
Blender headless 转换脚本：红狼_玫瑰
用法:
  blender --background "无标题.blend" --python export_fbx.py -- --textures-dir <dir> --output <out.fbx>

步骤:
  1. 打开 .blend
  2. 将每个 Image 的缺失路径重链到本地 textures 目录（按文件名匹配）
  3. 导出 FBX（网格 + 骨骼），贴图以 COPY 模式内嵌到 .fbm 文件夹
"""
import bpy
import os
import sys


def parse_args():
    argv = sys.argv
    if "--" not in argv:
        return None, None
    args = argv[argv.index("--") + 1:]
    textures_dir = None
    output = None
    for i, a in enumerate(args):
        if a == "--textures-dir" and i + 1 < len(args):
            textures_dir = args[i + 1]
        elif a == "--output" and i + 1 < len(args):
            output = args[i + 1]
    return textures_dir, output


textures_dir, output = parse_args()
if not textures_dir or not output:
    print("ERROR: missing --textures-dir or --output")
    sys.exit(1)

print(f"=== 场景加载完成 ===")
print(f"对象数量: {len(bpy.data.objects)}")
print(f"网格数量: {len(bpy.data.meshes)}")
print(f"骨骼数量: {len(bpy.data.armatures)}")
print(f"材质数量: {len(bpy.data.materials)}")
print(f"图片数量: {len(bpy.data.images)}")

armatures = [o for o in bpy.data.objects if o.type == 'ARMATURE']
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print(f"场景中的 ARMATURE: {[a.name for a in armatures]}")
print(f"场景中的 MESH 数量: {len(meshes)}")

# ---- 1. 列出所有图片及其当前路径 ----
print("=== 当前图片引用 ===")
for img in bpy.data.images:
    print(f"  [{img.name}] -> {img.filepath}")

# ---- 2. 重链缺失贴图到本地 textures 目录 ----
if not os.path.isdir(textures_dir):
    print(f"ERROR: textures dir 不存在: {textures_dir}")
    sys.exit(1)

local_textures = {n.lower(): os.path.join(textures_dir, n) for n in os.listdir(textures_dir)}
relinked = 0
for img in bpy.data.images:
    if not img.filepath:
        continue
    base = os.path.basename(img.filepath.replace("\\", "/"))
    key = base.lower()
    if key in local_textures and not os.path.isfile(img.filepath):
        new_path = local_textures[key]
        img.filepath = new_path
        try:
            img.reload()
        except Exception as e:
            print(f"  !! reload 失败 {img.name}: {e}")
        print(f"  重链: {img.name} -> {new_path}")
        relinked += 1
    else:
        print(f"  保留: {img.name} -> {img.filepath}")

print(f"重链图片数: {relinked}")

# 对没有贴图的材质，尝试按材质名匹配（可选增强，先不做）

# ---- 3. 导出 FBX ----
os.makedirs(os.path.dirname(os.path.abspath(output)), exist_ok=True)
export_types = {'MESH'}
if armatures:
    export_types.add('ARMATURE')

bpy.ops.export_scene.fbx(
    filepath=output,
    use_selection=False,
    use_visible=False,
    use_active_collection=False,
    object_types=export_types,
    use_mesh_modifiers=True,
    mesh_smooth_type='OFF',
    use_subsurf=False,
    add_leaf_bones=False,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    bake_space_transform=False,
    use_armature_deform_only=False,
    path_mode='COPY',        # 贴图复制到 <fbx>.fbm 文件夹
    embed_textures=True,     # 贴图内嵌进 FBX（Unity 导入时自动提取，不依赖路径）
    batch_mode='OFF',
    use_batch_own_dir=False,
    bake_anim=False,
    axis_forward='-Z',
    axis_up='Y',
    global_scale=1.0,
)

print(f"=== 导出完成: {output} ===")
if os.path.exists(output):
    print(f"FBX 大小: {os.path.getsize(output) / 1024 / 1024:.1f} MB")
else:
    print("ERROR: FBX 未生成")
    sys.exit(1)
