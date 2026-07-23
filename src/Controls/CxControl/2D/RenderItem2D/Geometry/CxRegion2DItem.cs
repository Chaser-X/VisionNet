using System;
using System.Collections.Generic;
using System.Drawing;
using ScottPlot;
using ScottPlot.Plottables;
using VisionNet.DataType;
using Color = System.Drawing.Color;

namespace VisionNet.Controls
{
    public class CxRegion2DItem : Abstract2DRenderItem
    {
        private readonly List<IPlottable> _plottables = new List<IPlottable>();
        private readonly List<IPlottable> _handlePlottables = new List<IPlottable>();

        private int _activeIndex = -1;
        private CxPolygon2D[] _cachedPolys;
        private Polygon[] _polygonRefs;

        public CxRegion2D[] Regions { get; private set; }

        public CxRegion2DItem(CxRegion2D[] regions, Color color, float size = 1f)
        {
            Regions = regions ?? Array.Empty<CxRegion2D>();
            Color = color;
            Size = size;
            _cachedPolys = new CxPolygon2D[Regions.Length];
            for (int i = 0; i < Regions.Length; i++)
            {
                VisionOperator.BoundaryToPolygon2D(Regions[i], out _cachedPolys[i]);
                VisionOperator.SimplifyPolygon2D(_cachedPolys[i], 0.5f, out _cachedPolys[i]);
            }
        }

        public override void AddToPlot(Plot plot)
        {
            _plot = plot;
            BuildPlottables();
        }

        public override void RemoveFromPlot(Plot plot)
        {
            foreach (var p in _plottables) plot.PlottableList.Remove(p);
            RemoveHandlePlottables(plot);
            _plottables.Clear();
            _plot = null;
        }

        public override void UpdatePlottable()
        {
            if (_plot == null) return;
            foreach (var p in _plottables) _plot.PlottableList.Remove(p);
            RemoveHandlePlottables(_plot);
            _plottables.Clear();
            BuildPlottables();
        }

        private void BuildPlottables()
        {
            _polygonRefs = new Polygon[Regions.Length];
            for (int fi = 0; fi < Regions.Length; fi++)
            {
                var region = Regions[fi];
                var fColor = fi == _activeIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);

                if (!region.IsEmpty)
                {
                    var poly = _cachedPolys[fi];
                    if (poly.Points != null && poly.Points.Length >= 3)
                    {
                        var coords = new Coordinates[poly.Points.Length];
                        for (int j = 0; j < poly.Points.Length; j++)
                            coords[j] = new Coordinates(poly.Points[j].X, poly.Points[j].Y);
                        var p = _plot.Add.Polygon(coords);
                        p.LineStyle.Width = Size;
                        p.LineStyle.Color = fColor;
                        p.FillStyle.Color = fColor;
                        p.FillStyle.IsVisible = true;
                        p.MarkerStyle.IsVisible = false;
                        _plottables.Add(p);
                        _polygonRefs[fi] = p;
                    }
                }
            }

            if (_activeIndex >= 0)
            {
                // Selected state: color change only (handled by fColor in the loop above).
                // No expensive BoundaryToPolygon or per-vertex markers.
            }
        }

        public override bool HitTest(CxPoint2D plotPos)
        {
            //for (int fi = 0; fi < Regions.Length; fi++)
            //{
            //    var region = Regions[fi];
            //    if (region.IsEmpty) continue;
            //    int px = (int)Math.Round(plotPos.X);
            //    int py = (int)Math.Round(plotPos.Y);
            //    if (region.Contains(px, py))
            //    { _activeIndex = fi; return true; }
            //}
            return false;
        }

        public override void OnMouseDown(CxPoint2D plotPos)
        {
            if (_activeIndex < 0) { UpdatePlottable(); return; }
            UpdateColors();
        }

        public override void OnMouseUp()
        {
            // Re-extract accurate boundary after drag ends (BoundaryToPolygon is expensive,
            // so it is deferred from Translate() and run only once on mouse-up).
            if (_activeIndex >= 0 && !Regions[_activeIndex].IsEmpty)
            {
                VisionOperator.BoundaryToPolygon2D(Regions[_activeIndex], out _cachedPolys[_activeIndex]);
                VisionOperator.SimplifyPolygon2D(_cachedPolys[_activeIndex], 0.5f, out _cachedPolys[_activeIndex]);
                UpdatePlottable();
            }
        }

        public override void OnDeselected()
        {
            _activeIndex = -1;
            UpdateColors();
        }

        public override void Translate(float dx, float dy)
        {
            if (_activeIndex < 0) return;
            var region = Regions[_activeIndex];
            if (region.IsEmpty) return;

            int idx = (int)Math.Round(dx);
            int idy = (int)Math.Round(dy);
            if (idx == 0 && idy == 0) return;

            // Update RLE runs to keep data consistent for HitTest and OnMouseUp re-extraction.
            var runs = new CxRun[region.Runs.Length];
            for (int i = 0; i < runs.Length; i++)
            {
                int row = region.Runs[i].Row + idy;
                int cs  = region.Runs[i].ColStart + idx;
                int ce  = region.Runs[i].ColEnd   + idx;
                if (row >= 0 && row < region.Height && cs >= 0 && ce <= region.Width && cs < ce)
                    runs[i] = new CxRun(row, cs, ce);
            }
            Regions[_activeIndex] = new CxRegion2D(region.Width, region.Height, runs);

            // Fast path: offset the pre-simplified cached polygon directly.
            // Avoids BoundaryToPolygon (mask alloc + Moore-Neighbor tracing) on every mouse-move frame.
            // BoundaryToPolygon is called once in OnMouseUp() when drag ends.
            var oldPoly = _cachedPolys[_activeIndex];
            if (oldPoly.Points != null)
            {
                var pts = new CxPoint2D[oldPoly.Points.Length];
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = new CxPoint2D(oldPoly.Points[i].X + idx, oldPoly.Points[i].Y + idy);
                _cachedPolys[_activeIndex] = new CxPolygon2D(pts, oldPoly.IsClosed);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _plot != null)
            {
                foreach (var p in _plottables) _plot.PlottableList.Remove(p);
                RemoveHandlePlottables(_plot);
            }
        }

        private void RemoveHandlePlottables(Plot plot)
        {
            foreach (var h in _handlePlottables) plot.PlottableList.Remove(h);
            _handlePlottables.Clear();
        }

        private void UpdateColors()
        {
            if (_polygonRefs == null) return;
            for (int fi = 0; fi < Regions.Length; fi++)
            {
                if (_polygonRefs[fi] == null) continue;
                var fColor = fi == _activeIndex ? ToSPColor(SelectedColor) : ToSPColor(Color);
                _polygonRefs[fi].LineStyle.Color = fColor;
                _polygonRefs[fi].FillStyle.Color = fColor;
            }
        }
    }
}
