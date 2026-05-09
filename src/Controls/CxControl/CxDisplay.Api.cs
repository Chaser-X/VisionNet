using System;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public partial class CxDisplay
    {
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

            camera.FitView(newItem.BoundingBox);
            Invalidate();
        }

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

            camera.FitView(combined);
            Invalidate();
        }

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

        public void SetPointCloud(CxSurface inpointCloud)
        {
            var surface = inpointCloud;
            if (inpointCloud.Width * inpointCloud.Length > 100_000_000)
            {
                var points = inpointCloud.ToPoints();
                float ratio  = points.Length / 10_000_000f;
                surface = VisionOperator.UniformSuface(points, inpointCloud.Intensity,
                    (int)(inpointCloud.Width / ratio), (int)(inpointCloud.Length / ratio),
                    inpointCloud.XScale * ratio, inpointCloud.YScale * ratio,
                    inpointCloud.ZScale, inpointCloud.XOffset, inpointCloud.YOffset, inpointCloud.ZOffset);
            }
            ReplaceSurfaceItem(new CxSurfaceItem(surface, SurfaceMode, SurfaceColorMode));
        }

        public void SetMesh(CxMesh mesh)
        {
            ReplaceSurfaceItem(new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode));
        }

        public void SetSurfaceAdvancedItem(CxSurface surface)
        {
            ReplaceSurfaceItem(new CxSurfaceAdvancedItem(surface, SurfaceMode, SurfaceColorMode, 2_000_0000));
        }

        public void SetMeshAdvancedItem(CxMesh mesh)
        {
            ReplaceSurfaceItem(new CxMeshAdvancedItem(mesh, SurfaceMode, SurfaceColorMode));
        }

        public void SetSegment(Segment3D[] segment, Color color, float size = 1.0f)
        {
            renderItem.Add(new CxSegment3DItem(segment, color, size));
            Invalidate();
        }

        public void SetPoint(CxPoint3D[] point, Color color, float size = 1.0f, PointShape shape = PointShape.Point)
        {
            renderItem.Add(new CxPoint3DItem(point, color, size, shape));
            Invalidate();
        }

        public void SetPolygon(Polygon3D[] polygon, Color color, float size = 1.0f)
        {
            renderItem.Add(new CxPolygon3DItem(polygon, color, size));
            Invalidate();
        }

        public void SetPlane(Plane3D[] plane, Color color, float size = 100.0f)
        {
            renderItem.Add(new CxPlane3DItem(plane, color, size));
            Invalidate();
        }

        public void SetBox(Box3D[] box, Color color, float size = 1.0f)
        {
            renderItem.Add(new CxBox3DItem(box, color, size));
            Invalidate();
        }

        public void SetTextInfo(TextInfo[] textInfo, Color color)
        {
            renderItem.Add(new CxTextInfoItem(textInfo, color, 1));
            Invalidate();
        }

        public void SetText2D(Text2D[] text2Ds, Color color)
        {
            renderItem.Add(new CxText2DItem(text2Ds, color, 1));
            Invalidate();
        }

        public void SetCoordinate3DSystem(CxCoordination3D? coordination = null, float axisLength = 5)
        {
            if (!coordination.HasValue)
                coordination = new CxCoordination3D
                {
                    Origin = new CxPoint3D(0, 0, 0),
                    XAxis = new CxVector3D(1, 0, 0),
                    YAxis = new CxVector3D(0, 1, 0),
                    ZAxis = new CxVector3D(0, 0, 1),
                };
            renderItem.Add(new CxCoordinateSystemItem(
                axisLength, axisLength / 50, axisLength / 10, axisLength / 25, coordination));
            Invalidate();
        }

        public void AddPointCloud(CxSurface inpointCloud)
        {
            var surface = inpointCloud;
            if (inpointCloud.Width * inpointCloud.Length > 100_000_000)
            {
                var points = inpointCloud.ToPoints();
                float ratio = points.Length / 10_000_000f;
                surface = VisionOperator.UniformSuface(points, inpointCloud.Intensity,
                    (int)(inpointCloud.Width / ratio), (int)(inpointCloud.Length / ratio),
                    inpointCloud.XScale * ratio, inpointCloud.YScale * ratio,
                    inpointCloud.ZScale, inpointCloud.XOffset, inpointCloud.YOffset, inpointCloud.ZOffset);
            }
            AppendSurfaceItem(new CxSurfaceItem(surface, SurfaceMode, SurfaceColorMode));
        }

        public void AddMesh(CxMesh mesh)
            => AppendSurfaceItem(new CxMeshItem(mesh, SurfaceMode, SurfaceColorMode));

        public void AddSurfaceAdvancedItem(CxSurface surface)
            => AppendSurfaceItem(new CxSurfaceAdvancedItem(surface, SurfaceMode, SurfaceColorMode, 2_000_000));

        public void AddMeshAdvancedItem(CxMesh mesh)
            => AppendSurfaceItem(new CxMeshAdvancedItem(mesh, SurfaceMode, SurfaceColorMode));

        public void AddSurfaceItem(ICxObjRenderItem item)
            => AppendSurfaceItem(item);

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
