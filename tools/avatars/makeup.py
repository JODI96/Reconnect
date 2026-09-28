"""
Face paint for the character creator: where lips, eyeshadow, blush and eyeliner go on the skin texture, and the iris of the
eye texture – as masks the game colours in (any skin, any colour).

The regions come from the face morphs themselves: the vertices the lip-volume targets move are the lips, the upper lid is
what "eye open" moves, the cheeks what "cheek volume" moves; the lash line is the skin next to the eyelashes. They are
painted into the body's UV space (the skins share it), softened, and saved as one RGBA image:
R lips, G eyeshadow, B blush, A eyeliner.
"""
import bpy
import numpy as np
from mathutils.kdtree import KDTree

SIZE = 1024


def _weights(deltas, keys, count):
    total = np.zeros(count)
    for key in keys:
        if key in deltas:
            total += np.linalg.norm(deltas[key], axis=1)
    if total.max() <= 0:
        return total
    # The strongest part of the move is the region; its fringe fades out.
    return np.clip(total / (total.max() * 0.45), 0.0, 1.0)


def _near(positions, other_positions, reach):
    tree = KDTree(len(other_positions))
    for index, co in enumerate(other_positions):
        tree.insert(co, index)
    tree.balance()
    result = np.zeros(len(positions))
    for index, co in enumerate(positions):
        _, _, distance = tree.find(co)
        result[index] = max(0.0, 1.0 - distance / reach)
    return result


def _raster(obj, values, size):
    """Paints per-vertex values (N × channels) into the UV square (first UV map), interpolated across triangles."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    uv_layer = mesh.uv_layers[0].data
    channels = values.shape[1]
    image = np.zeros((size, size, channels), dtype=np.float32)
    for triangle in mesh.loop_triangles:
        corners = [uv_layer[loop].uv for loop in triangle.loops]
        vertex_values = values[list(triangle.vertices)]
        if vertex_values.max() <= 0.001:
            continue
        points = np.array([(uv.x * size, uv.y * size) for uv in corners])
        x0, y0 = np.floor(points.min(axis=0)).astype(int)
        x1, y1 = np.ceil(points.max(axis=0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, size - 1), min(y1, size - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        (ax, ay), (bx, by), (cx, cy) = points
        area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay)
        if abs(area) < 1e-9:
            continue
        w0 = ((bx - xs) * (cy - ys) - (cx - xs) * (by - ys)) / area
        w1 = ((cx - xs) * (ay - ys) - (ax - xs) * (cy - ys)) / area
        w2 = 1.0 - w0 - w1
        inside = (w0 >= -0.01) & (w1 >= -0.01) & (w2 >= -0.01)
        if not inside.any():
            continue
        painted = (w0[..., None] * vertex_values[0] + w1[..., None] * vertex_values[1] + w2[..., None] * vertex_values[2])
        region = image[y0:y1 + 1, x0:x1 + 1]
        region[inside] = np.maximum(region[inside], painted[inside])
    return image


def _blur(image, passes):
    for _ in range(passes):
        padded = np.pad(image, ((1, 1), (1, 1), (0, 0)), mode="edge")
        image = (padded[:-2, 1:-1] + padded[2:, 1:-1] + padded[1:-1, :-2] + padded[1:-1, 2:] + 2 * padded[1:-1, 1:-1]) / 6.0
    return image


def _save(pixels, path):
    height, width = pixels.shape[:2]
    image = bpy.data.images.new("mask", width, height, alpha=True)
    image.pixels.foreach_set(pixels.astype(np.float32).ravel())
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def makeup_mask(body_obj, body_positions, body_deltas, lash_positions, path):
    count = len(body_positions)
    lips = _weights(body_deltas, ["upper-lip+", "lower-lip+"], count)
    shadow = _weights(body_deltas, ["eye-open+", "eye-lid+"], count)
    blush = _weights(body_deltas, ["cheeks+"], count)
    liner = _near(body_positions, lash_positions, 0.004) if len(lash_positions) else np.zeros(count)
    values = np.stack([lips, shadow, blush, liner], axis=1)
    image = _raster(body_obj, values, SIZE)
    image = _blur(image, 3)
    image[..., 3] = np.clip(image[..., 3] * 1.5, 0.0, 1.0)
    _save(image, path)
    print(f"MAKEUP {path}: lips {lips.sum():.0f}, shadow {shadow.sum():.0f}, blush {blush.sum():.0f}, liner {liner.sum():.0f}")


def iris_texture(source, path, size=512):
    """The eye texture with the iris as alpha (the game tints it any colour, keeping its fibres); the pupil stays black."""
    image = bpy.data.images.load(source, check_existing=False)
    if image.size[0] > size:
        image.scale(size, size)
    width, height = image.size
    pixels = np.empty(width * height * 4, np.float32)
    image.pixels.foreach_get(pixels)
    pixels = pixels.reshape(height, width, 4)
    rgb = pixels[..., :3]
    high, low = rgb.max(axis=2), rgb.min(axis=2)
    saturation = np.where(high > 1e-4, (high - low) / np.maximum(high, 1e-4), 0.0)
    mask = np.clip((saturation - 0.18) / 0.17, 0.0, 1.0) * np.clip((high - 0.08) / 0.1, 0.0, 1.0)
    mask = _blur(mask[..., None], 2)[..., 0]
    pixels[..., 3] = mask
    _save(pixels, path)
    bpy.data.images.remove(image)
