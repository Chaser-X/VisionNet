using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Public API surface: surface data, geometric overlays, and view management.</summary>
    public partial class CxDisplay
    {
        // ── Internal surface-item management ────────────────────────────────────

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
        /// Appends <paramref name="newItem"/> without clearing existing surface items.
        /// Adjusts the camera to fit the combined bounding box of all items.
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
        /// Called when any surface item's CPU data is invalidated.
        /// Marks all handles as needing a GPU rebuild on the next render frame.
        /// </summary>
        private void OnItemRenderDataChanged()
        {
            lock (_resourceLock)
            {
                foreach (var item in _surfaceItems)
                    if (_resourcePool.TryGetValue(item, out var handle))
                        handle.NeedsUpdate = true;
            }
            Invalidate();
        }

        // ── Surface: Set* (replace semantics) ───────────────────────────────────

        /// <summary>
        /// Replaces the current view with a single point cloud.
        /// Surfaces larger than 100 M points are automatically down-sampled to ≤ 10 M.
        /// </summary>
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

        /// <summary>Replaces the current view with a single mesh.</summary>
        public void SetMesh(CxMesh mesh)
            => ReplaceSurfaceItem(new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode));

        /// <summary>
        /// Replaces the current view with a surface rendered via the high-performance shader path
        /// (VAO + GLSL, max 2 000 000 points).
        /// </summary>
        public void SetSurfaceAdvancedItem(CxSurface surface)
            => ReplaceSurfaceItem(new CxSurfaceAdvancedItem(surface, SurfaceMode, SurfaceColorMode, 2_000_000));

        /// <summary>Replaces the current view with a mesh rendered via the high-performance shader path.</summary>
        public void SetMeshAdvancedItem(CxMesh mesh)
            => ReplaceSurfaceItem(new CxMeshAdvancedItem(mesh, SurfaceMode, SurfaceColorMode));

        // ── Surface: Add* (append semantics) ────────────────────────────────────

        /// <summary>
        /// Appends a point cloud without clearing existing surface items.
        /// Surfaces larger than 100 M points are automatically down-sampled to ≤ 10 M.
        /// </summary>
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

        /// <summary>Appends a mesh without clearing existing surface items.</summary>
        public void AddMesh(CxMesh mesh)
            => AppendSurfaceItem(new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode));

        /// <summary>Appends a surface via the high-performance shader path without clearing existing items.</summary>
        public void AddSurfaceAdvancedItem(CxSurface surface)
            => AppendSurfaceItem(new CxSurfaceAdvancedItem(surface, SurfaceMode, SurfaceColorMode, 2_000_000));

        /// <summary>Appends a mesh via the high-performance shader path without clearing existing items.</summary>
        public void AddMeshAdvancedItem(CxMesh mesh)
            => AppendSurfaceItem(new CxMeshAdvancedItem(mesh, SurfaceMode, SurfaceColorMode));

        /// <summary>Appends an externally constructed render item without clearing existing items.</summary>
        public void AddSurfaceItem(ICxObjRenderItem item)
            => AppendSurfaceItem(item);

        /// <summary>Removes all surface items and queues their GL resources for deferred release.</summary>
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

        // ── Geometric overlays (always append) ──────────────────────────────────

        /// <summary>Appends a set of 3D line segments to the overlay layer.</summary>
        public void SetSegment(Segment3D[] segment, Color color, float size = 1.0f)
        { _renderItems.Add(new CxSegment3DItem(segment, color, size)); Invalidate(); }

        /// <summary>Appends a set of 3D points to the overlay layer.</summary>
        public void SetPoint(CxPoint3D[] point, Color color, float size = 1.0f,
            PointShape shape = PointShape.Point)
        { _renderItems.Add(new CxPoint3DItem(point, color, size, shape)); Invalidate(); }

        /// <summary>Appends a set of 3D polygons to the overlay layer.</summary>
        public void SetPolygon(Polygon3D[] polygon, Color color, float size = 1.0f)
        { _renderItems.Add(new CxPolygon3DItem(polygon, color, size)); Invalidate(); }

        /// <summary>Appends a set of 3D planes to the overlay layer.</summary>
        public void SetPlane(Plane3D[] plane, Color color, float size = 100.0f)
        { _renderItems.Add(new CxPlane3DItem(plane, color, size)); Invalidate(); }

        /// <summary>Appends a set of axis-aligned bounding boxes to the overlay layer.</summary>
        public void SetBox(Box3D[] box, Color color, float size = 1.0f)
        { _renderItems.Add(new CxBox3DItem(box, color, size)); Invalidate(); }

        /// <summary>Appends world-anchored text labels to the overlay layer.</summary>
        public void SetTextInfo(TextInfo[] textInfo, Color color)
        { _renderItems.Add(new CxTextInfoItem(textInfo, color, 1)); Invalidate(); }

        /// <summary>Appends screen-space 2D text overlays.</summary>
        public void SetText2D(Text2D[] text2Ds, Color color)
        { _renderItems.Add(new CxText2DItem(text2Ds, color, 1)); Invalidate(); }

        /// <summary>
        /// Appends a 3D coordinate-frame axes indicator to the overlay layer.
        /// If <paramref name="coordination"/> is <c>null</c>, the world-origin axes are used.
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

        // ── View management ──────────────────────────────────────────────────────

        /// <summary>
        /// Resets the display: disposes all overlay geometry and reinitialises HUD items.
        /// If <paramref name="resetAll"/> is <c>true</c>, also removes all surface items.
        /// </summary>
        public void ResetView(bool resetAll = true)
        {
            _renderItems.ForEach(item => item.Dispose());
            _renderItems.Clear();

            _coordinationItem = new CxCoordinateSystemItem();
            _coordTagItem     = new CxCoordinationTagItem();
            _colorBarItem     = new CxColorBarItem();

            if (resetAll)
                ClearSurfaceItems();

            Invalidate();
        }

        /// <summary>Sets the camera rotation pivot to the given world-space position.</summary>
        public void SetViewCenter(CxPoint3D center)
        {
            _camera.FocusOnPoint(new Vector3(center.X, center.Y, center.Z));
            Invalidate();
        }

        /// <summary>Sets the camera up-direction vector.</summary>
        public void SetViewUpDirection(CxVector3D upDirection)
        {
            _camera.SetDefaultUpView(new Vector3(upDirection.X, upDirection.Y, upDirection.Z));
        }
    }
}
