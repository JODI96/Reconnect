using System.Collections.Generic;
using Reconnect.Contracts.Rooms;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// What the build editor draws into the room: the 50 cm build grid (walking tiles a bit stronger), zones that stay
    /// free (entrance, in front of the lift) in red, and the item in hand as a see-through ghost – green where it may
    /// be put down, red where not – with its footprint on the floor.
    /// </summary>
    public sealed class BuildPreview
    {
        private const int PixelsPerCell = 8;

        private readonly RoomView _room;
        private readonly Material _grid;
        private readonly Material _ok;
        private readonly Material _bad;
        private GameObject _gridObject;
        private GameObject _ghost;
        private RoomItemDto _ghostItem;
        private int _ghostCarried;
        private bool _ghostValid;

        public BuildPreview(RoomView room, Material transparent)
        {
            _room = room;
            _grid = Transparent(transparent, Color.white);
            _grid.SetFloat("_Smoothness", 0f);
            _ok = Transparent(transparent, new Color(0.3f, 1f, 0.55f, 0.5f));
            _bad = Transparent(transparent, new Color(1f, 0.3f, 0.3f, 0.5f));
        }

        public void Show(RoomLayoutContext context)
        {
            Hide();
            var cells = context.Cells;
            var texture = new Texture2D(cells.Width * PixelsPerCell, cells.Depth * PixelsPerCell, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Build Grid",
            };
            var pixels = new Color32[texture.width * texture.height];
            // Bright cyan reads on light and dark floors alike; metre lines are bold, 25 cm lines fine.
            var line = new Color32(90, 220, 255, 90);
            var halfLine = new Color32(90, 220, 255, 150);
            var tileLine = new Color32(90, 220, 255, 235);
            var reserved = new Color32(255, 70, 70, 70);
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var cellX = x / PixelsPerCell;
                    var cellZ = y / PixelsPerCell;
                    var edgeX = x % PixelsPerCell == 0 || x == texture.width - 1;
                    var edgeZ = y % PixelsPerCell == 0 || y == texture.height - 1;
                    // Metre lines two pixels wide.
                    var boldX = (x + 1) % (PixelsPerCell * BuildGrid.CellsPerTile) == 0;
                    var boldZ = (y + 1) % (PixelsPerCell * BuildGrid.CellsPerTile) == 0;
                    var tileEdge = (edgeX && cellX % BuildGrid.CellsPerTile == 0) || (edgeZ && cellZ % BuildGrid.CellsPerTile == 0);
                    var halfEdge = (edgeX && cellX % (BuildGrid.CellsPerTile / 2) == 0) || (edgeZ && cellZ % (BuildGrid.CellsPerTile / 2) == 0);
                    Color32 colour = default;
                    foreach (var zone in context.Reserved)
                    {
                        if (zone.Contains(cellX, cellZ))
                        {
                            colour = reserved;
                        }
                    }
                    if (edgeX || edgeZ)
                    {
                        colour = tileEdge ? tileLine : halfEdge ? halfLine : line;
                    }
                    else if (boldX || boldZ)
                    {
                        colour = tileLine;
                    }
                    pixels[y * texture.width + x] = colour;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _grid.mainTexture = texture;

            _gridObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _gridObject.name = "Build Grid";
            Object.Destroy(_gridObject.GetComponent<Collider>());
            _gridObject.GetComponent<Renderer>().sharedMaterial = _grid;
            _gridObject.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _gridObject.transform.SetParent(_room.Content, false);
            _gridObject.transform.localPosition = new Vector3(_room.Width / 2f, 0.012f, _room.Depth / 2f);
            _gridObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _gridObject.transform.localScale = new Vector3(_room.Width, _room.Depth, 1f);
        }

        /// <summary>Redraws the ghost when the item in hand changed.</summary>
        public void Update(BuildEditor editor)
        {
            var valid = editor.SelectionIsValid;
            if (Equals(editor.Selection, _ghostItem) && editor.Carried.Count == _ghostCarried && valid == _ghostValid && (_ghost != null) == editor.HasSelection)
            {
                return;
            }
            ClearGhost();
            _ghostItem = editor.Selection;
            _ghostCarried = editor.Carried.Count;
            _ghostValid = valid;
            if (!editor.HasSelection)
            {
                return;
            }

            _ghost = new GameObject("Build Ghost");
            _ghost.transform.SetParent(_room.Content, false);
            var material = valid ? _ok : _bad;
            var items = new List<RoomItemDto> { editor.Selection };
            items.AddRange(editor.Carried);
            var context = editor.Context(editor.Items);
            float? tableTop = null;
            foreach (var item in items)
            {
                var definition = ItemDefinitions.Find(item.ItemId);
                var bounds = _room.BuildGhost(_ghost.transform, item, definition, context, item == editor.Selection ? null : tableTop);
                if (item == editor.Selection && definition.HasSurface)
                {
                    tableTop = _room.transform.InverseTransformPoint(bounds.max).y;
                }
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "Footprint";
                Object.Destroy(plate.GetComponent<Collider>());
                plate.transform.SetParent(_ghost.transform, false);
                if (definition.Kind == ItemKind.Decor)
                {
                    // Small things: their real outline, just above the table top.
                    var area = RoomLayout.DecorArea(item, definition);
                    var y = _room.transform.InverseTransformPoint(bounds.min).y + 0.01f;
                    plate.transform.localPosition = new Vector3(area.CentreX, y, area.CentreZ);
                    plate.transform.localScale = new Vector3(area.MaxX - area.MinX, 0.01f, area.MaxZ - area.MinZ);
                }
                else
                {
                    var cells = RoomLayout.Footprint(item, definition);
                    var (x, z) = RoomLayout.Centre(cells);
                    plate.transform.localPosition = new Vector3(x, 0.02f, z);
                    plate.transform.localScale = new Vector3(cells.Width * BuildGrid.CellSize - 0.02f, 0.02f, cells.Depth * BuildGrid.CellSize - 0.02f);
                }
            }
            foreach (var renderer in _ghost.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var light in _ghost.GetComponentsInChildren<Light>())
            {
                light.enabled = false;
            }
        }

        public void Hide()
        {
            ClearGhost();
            if (_gridObject != null)
            {
                Object.Destroy(_gridObject);
                _gridObject = null;
            }
        }

        private void ClearGhost()
        {
            if (_ghost != null)
            {
                Object.Destroy(_ghost);
                _ghost = null;
            }
            _ghostItem = null;
            _ghostCarried = 0;
        }

        private static Material Transparent(Material source, Color colour)
        {
            var material = new Material(source);
            material.SetColor("_BaseColor", colour);
            return material;
        }
    }
}
