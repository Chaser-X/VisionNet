using System;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public partial class CxDisplay
    {
        /// <summary>
        /// Computes the axis-aligned bounding box that encloses all current surface items.
        /// Caller must hold <see cref="_resourceLock"/>.
        /// </summary>
        private Box3D? GetCombinedBoundingBox()
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var item in _surfaceItems)
            {
                var bb = item.BoundingBox;
                if (bb == null) continue;
                float x0 = bb.Value.Center.X - bb.Value.Size.Width  / 2f;
                float x1 = bb.Value.Center.X + bb.Value.Size.Width  / 2f;
                float y0 = bb.Value.Center.Y - bb.Value.Size.Height / 2f;
                float y1 = bb.Value.Center.Y + bb.Value.Size.Height / 2f;
                float z0 = bb.Value.Center.Z - bb.Value.Size.Depth  / 2f;
                float z1 = bb.Value.Center.Z + bb.Value.Size.Depth  / 2f;
                if (x0 < minX) minX = x0; if (x1 > maxX) maxX = x1;
                if (y0 < minY) minY = y0; if (y1 > maxY) maxY = y1;
                if (z0 < minZ) minZ = z0; if (z1 > maxZ) maxZ = z1;
            }

            if (minX == float.MaxValue) return null;
            return new Box3D(
                new CxPoint3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2),
                new CxSize3D(maxX - minX, maxY - minY, maxZ - minZ));
        }

        /// <summary>
        /// Clears all existing surface items and replaces them with <paramref name="newItem"/>.
        /// Old GL resources are queued for deferred release.
        /// </summary>
        private void ReplaceSurfaceItem(ICxObjRenderItem newItem)
        {
            lock (_resourceLock)
            {
                foreach (var old in _surfaceItems)
                {
                    if (_resourcePool.TryGetValue(old, out var h))
                    {
                        _pendingRelease.Enqueue(h);
                        _resourcePool.Remove(old);
                    }
                    old.OnRenderDataChanged -= OnItemRenderDataChanged;
                    old.Dispose();
                }
                _surfaceItems.Clear();

                _surfaceItems.Add(newItem);
                _resourcePool[newItem] = new GLResourceHandle { IsValid = false, NeedsUpdate = true };
                newItem.OnRenderDataChanged += OnItemRenderDataChanged;
            }

            _camera.FitView(newItem.BoundingBox);
            Invalidate();
        }

        /// <summary>
        /// Appends <paramref name="newItem"/> to the list of surface items without clearing existing ones.
        /// Adjusts the camera to fit the combined bounding box.
        /// </summary>
        private void AppendSurfaceItem(ICxObjRenderItem newItem)
        {
            Box3D? combined;
            lock (_resourceLock)
            {
                _surfaceItems.Add(newItem);
                _resourcePool[newItem] = new GLResourceHandle { IsValid = false, NeedsUpdate = true };
                newItem.OnRenderDataChanged += OnItemRenderDataChanged;
                combined = GetCombinedBoundingBox();
            }

            _camera.FitView(combined);
            Invalidate();
        }

        /// <summary>
        /// Called when any surface item's render data is invalidated.
        /// Marks all resource handles as needing a GPU update.
        /// </summary>
        private void OnItemRenderDataChanged()
        {
            lock (_resourceLock)
            {
                foreach (var item in _surfaceItems)
                {
                    if (_resourcePool.TryGetValue(item, out var handle))
                        handle.NeedsUpdate = true;
                }
            }
            Invalidate();
        }

        // ── Public Set* API (replace semantics) ─────────────────────────────────

        /// <summary>
        /// Sets the display to show a single point cloud, replacing any previous surface content.
        /// Surfaces larger than 100 M points are automatically down-sampled.
        /// </summary>
        /// <param name="pointCloud">Source surface data.</param>
        public void SetPointCloud(CxSurface pointCloud)
        {
            var surface = pointCloud;
            if (pointCloud.Width * pointCloud.Length > 100_000_000)
            {
                var points = pointCloud.ToPoints();
                float ratio = points.Length / 10_000_000f;
                surface = VisionOperator.UniformSurface(points, pointCloud.Intensity,
                    (int)(pointCloud.Width  / ratio), (int)(pointCloud.Length / ratio),
                    pointCloud.XScale * ratio, pointCloud.YScale * ratio,
                    pointCloud.ZScale, pointCloud.XOffset, pointCloud.YOffset, pointCloud.ZOffset);
            }
            ReplaceSurfaceItem(new CxSurfaceItem(surface, SurfaceMode, SurfaceColorMode));
        }

        /// <summary>Sets the display to show a single mesh, replacing any previous surface content.</summary>
        /// <param name="mesh">Source mesh data.</param>
        public void SetMesh(CxMesh mesh)
        {
            ReplaceSurfaceItem(new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode));
        }

        /// <summary>
        /// Sets the display to show a surface using the high-performance shader path,
        /// replacing any previous surface content. Maximum 2 000 000 points.
        /// </summary>
        /// <param name="surface">Source surface data.</param>
        public void SetSurfaceAdvancedItem(CxSurface surface)
        {
            ReplaceSurfaceItem(new CxSurfaceAdvancedItem(surface, SurfaceMode, SurfaceColorMode, 2_000_000));
        }

        /// <summary>
        /// Sets the display to show a mesh using the high-performance shader path,
        /// replacing any previous surface content.
        /// </summary>
        /// <param name="mesh">Source mesh data.</param>
        public void SetMeshAdvancedItem(CxMesh mesh)
        {
            ReplaceSurfaceItem(new CxMeshAdvancedItem(mesh, SurfaceMode, SurfaceColorMode));
        }

        // ── Geometric overlay helpers ────────────────────────────────────────────

        /// <summary>Adds a set of 3D line segments to the overlay layer.</summary>
        public void SetSegment(Segment3D[] segment, Color color, float size = 1.0f)
        {
            _renderItems.Add(new CxSegment3DItem(segment, color, size));
            Invalidate();
        }

        /// <summary>Adds a set of 3D points to the overlay layer.</summary>
        public void SetPoint(CxPoint3D[] point, Color color, float size = 1.0f,
            PointShape shape = PointShape.Point)
        {
            _renderItems.Add(new CxPoint3DItem(point, color, size, shape));
            Invalidate();
        }

        /// <summary>Adds a set of 3D polygons to the overlay layer.</summary>
        public void SetPolygon(Polygon3D[] polygon, Color color, float size = 1.0f)
        {
            _renderItems.Add(new CxPolygon3DItem(polygon, color, size));
            Invalidate();
        }

        /// <summary>Adds a set of 3D planes to the overlay layer.</summary>
        public void SetPlane(Plane3D[] plane, Color color, float size = 100.0f)
        {
            _renderItems.Add(new CxPlane3DItem(plane, color, size));
            Invalidate();
        }

        /// <summary>Adds a set of 3D bounding boxes to the overlay layer.</summary>
        public void SetBox(Box3D[] box, Color color, float size = 1.0f)
        {
            _renderItems.Add(new CxBox3DItem(box, color, size));
            Invalidate();
        }

        /// <summary>Adds 3D text labels at the given world-space positions.</summary>
        public void SetTextInfo(TextInfo[] textInfo, Color color)
        {
            _renderItems.Add(new CxTextInfoItem(textInfo, color, 1));
            Invalidate();
        }

        /// <summary>Adds screen-space 2D text overlays.</summary>
        public void SetText2D(Text2D[] text2Ds, Color color)
        {
            _renderItems.Add(new CxText2DItem(text2Ds, color, 1));
            Invalidate();
        }

        /// <summary>
        /// Adds a 3D coordinate system axes indicator to the overlay layer.
        /// If <paramref name="coordination"/> is <c>null</c>, the world origin axes are used.
        /// </summary>
        public void SetCoordinate3DSystem(CxCoordination3D? coordination = null, float axisLength = 5)
        {
            if (!coordination.HasValue)
                coordination = new CxCoordination3D
                {
                    Origin = new CxPoint3D(0, 0, 0),
                    XAxis  = new CxVector3D(1, 0, 0),
                    YAxis  = new CxVector3D(0, 1, 0),
                    ZAxis  = new CxVector3D(0, 0, 1),
                };
            _renderItems.Add(new CxCoordinateSystemItem(
                axisLength, axisLength / 50, axisLength / 10, axisLength / 25, coordination));
            Invalidate();
        }

        // ── Public Add* API (append semantics) ──────────────────────────────────

        /// <summary>
        /// Appends a point cloud to the current surface view without clearing existing items.
        /// Surfaces larger than 100 M points are automatically down-sampled.
        /// </summary>
        /// <param name="pointCloud">Source surface data.</param>
        public void AddPointCloud(CxSurface pointCloud)
        {
            var surface = pointCloud;
            if (pointCloud.Width * pointCloud.Length > 100_000_000)
            {
                var points = pointCloud.ToPoints();
                float ratio = points.Length / 10_000_000f;
                surface = VisionOperator.UniformSurface(points, pointCloud.Intensity,
                    (int)(pointCloud.Width  / ratio), (int)(pointCloud.Length / ratio),
                    pointCloud.XScale * ratio, pointCloud.YScale * ratio,
                    pointCloud.ZScale, pointCloud.XOffset, pointCloud.YOffset, pointCloud.ZOffset);
            }
            AppendSurfaceItem(new CxSurfaceItem(surface, SurfaceMode, SurfaceColorMode));
        }

        /// <summary>Appends a mesh to the current surface view without clearing existing items.</summary>
        public void AddMesh(CxMesh mesh)
            => AppendSurfaceItem(new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode));

        /// <summary>
        /// Appends a surface using the high-performance shader path without clearing existing items.
        /// </summary>
        public void AddSurfaceAdvancedItem(CxSurface surface)
            => AppendSurfaceItem(new CxSurfaceAdvancedItem(surface, SurfaceMode, SurfaceColorMode, 2_000_000));

        /// <summary>
        /// Appends a mesh using the high-performance shader path without clearing existing items.
        /// </summary>
        public void AddMeshAdvancedItem(CxMesh mesh)
            => AppendSurfaceItem(new CxMeshAdvancedItem(mesh, SurfaceMode, SurfaceColorMode));

        /// <summary>
        /// Appends an externally constructed render item to the surface view without clearing existing items.
        /// </summary>
        public void AddSurfaceItem(ICxObjRenderItem item)
            => AppendSurfaceItem(item);

        /// <summary>
        /// Removes all surface items (point clouds / meshes) and queues their GL resources for release.
        /// </summary>
        public void ClearSurfaceItems()
        {
            lock (_resourceLock)
            {
                foreach (var old in _surfaceItems)
                {
                    if (_resourcePool.TryGetValue(old, out var h))
                    {
                        _pendingRelease.Enqueue(h);
                        _resourcePool.Remove(old);
                    }
                    old.OnRenderDataChanged -= OnItemRenderDataChanged;
                    old.Dispose();
                }
                _surfaceItems.Clear();
            }
            Invalidate();
        }
    }
}
