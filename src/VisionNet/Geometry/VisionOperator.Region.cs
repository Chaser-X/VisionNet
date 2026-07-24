using System;
using System.Collections.Generic;
using VisionNet.DataType;

namespace VisionNet
{
    public static partial class VisionOperator
    {
        // ── Region: Conversion ───────────────────────────────────────────────────

        /// <summary>Converts a polygon boundary (closed) to an RLE region via scanline fill.</summary>
        public static void PolygonToRegion2D(CxPolygon2D polygon, int w, int h, out CxRegion2D region)
        {
            var pts = polygon.Points;
            if (pts == null || pts.Length < 3 || !polygon.IsClosed)
            {
                region = new CxRegion2D(w, h, null);
                return;
            }

            var runs = new List<CxRun>();
            int yMin = int.MaxValue, yMax = int.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                int y = (int)Math.Round(pts[i].Y);
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
            }
            if (yMin < 0) yMin = 0;
            if (yMax >= h) yMax = h - 1;

            // Reuse a single list across all scanlines to avoid per-row heap allocations.
            var xHits = new List<float>(8);
            for (int y = yMin; y <= yMax; y++)
            {
                xHits.Clear();
                for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                {
                    float yi = pts[i].Y, yj = pts[j].Y;
                    if ((yi > y) != (yj > y))
                    {
                        float xi = pts[i].X, xj = pts[j].X;
                        float x = (xj - xi) * (y - yi) / (yj - yi) + xi;
                        xHits.Add(x);
                    }
                }
                if (xHits.Count < 2) continue;
                xHits.Sort();
                for (int k = 0; k < xHits.Count - 1; k += 2)
                {
                    int s = (int)Math.Round(xHits[k]);
                    int e = (int)Math.Round(xHits[k + 1]);
                    if (s < 0) s = 0;
                    if (e > w) e = w;
                    if (s < e)
                        runs.Add(new CxRun(y, s, e));
                }
            }

            region = new CxRegion2D(w, h, runs.ToArray());
        }

        /// <summary>
        /// Extracts the outer boundary polygon from an RLE region.
        /// Uses direct run-based edge extraction — O(N log N) in run count,
        /// with no bitmap allocation. Replaces the former mask + Moore-Neighbor approach.
        /// </summary>
        public static void BoundaryToPolygon2D(CxRegion2D region, out CxPolygon2D polygon)
        {
            if (region.IsEmpty) { polygon = new CxPolygon2D(null, true); return; }

            var runs = region.Runs;
            int n = runs.Length;

            // adjH[corner] = the horizontal neighbour of that corner.
            // adjV[corner] = the vertical   neighbour of that corner.
            // Corner key = ((long)x << 32) | (uint)y
            var adjH = new Dictionary<long, long>(n * 4);
            var adjV = new Dictionary<long, long>(n * 4);

            // ── Step 1: Horizontal edges ──────────────────────────────────────────
            // At each integer y, XOR of coverage between row y-1 and row y gives H-edges.
            int aboveS = 0, aboveE = 0;   // run-index range for row y-1
            int belowS = 0, belowE = 0;   // run-index range for row y

            // Initialise belowS/E to span the first row.
            while (belowE < n && runs[belowE].Row == runs[0].Row) belowE++;

            int lastRow = runs[n - 1].Row;
            for (int y = runs[0].Row; y <= lastRow + 1; y++)
            {
                EmitXorHEdges(runs, aboveS, aboveE, belowS, belowE, y, adjH);
                aboveS = belowS;
                aboveE = belowE;
                belowS = belowE;
                int next = y + 1;
                while (belowE < n && runs[belowE].Row == next) belowE++;
            }

            // ── Step 2: Vertical edges ────────────────────────────────────────────
            // Each run contributes a left edge at ColStart and a right edge at ColEnd.
            // Collinear consecutive V-edges (same x, adjacent rows without an H-corner)
            // are merged on the fly to keep the corner count small.
            for (int i = 0; i < n; i++)
            {
                int r = runs[i].Row, cs = runs[i].ColStart, ce = runs[i].ColEnd;
                MergeVEdge(cs, r, r + 1, adjH, adjV);
                MergeVEdge(ce, r, r + 1, adjH, adjV);
            }

            // ── Step 3: Trace closed polygon(s) ──────────────────────────────────
            if (adjH.Count == 0) { polygon = new CxPolygon2D(null, true); return; }

            var visited = new HashSet<long>(adjH.Count * 2);
            List<CxPoint2D> best = null;

            foreach (var startKey in adjH.Keys)
            {
                if (visited.Contains(startKey)) continue;

                var pts = new List<CxPoint2D>();
                long cur = startKey;
                bool moveH = true;   // alternate between H and V at each step

                while (!visited.Contains(cur))
                {
                    visited.Add(cur);
                    pts.Add(new CxPoint2D((int)(cur >> 32), (int)(uint)cur));

                    long next;
                    bool ok = moveH ? adjH.TryGetValue(cur, out next)
                                    : adjV.TryGetValue(cur, out next);
                    if (!ok) break;   // degenerate corner — stop this trace
                    moveH = !moveH;
                    cur = next;
                }

                if (best == null || pts.Count > best.Count)
                    best = pts;
            }

            polygon = (best != null && best.Count >= 3)
                ? new CxPolygon2D(best.ToArray(), true)
                : new CxPolygon2D(null, true);
        }

        /// <summary>
        /// Merges a new vertical edge (x, y1)↔(x, y2) into adjV, collapsing collinear
        /// interior points to keep the corner count minimal.
        /// </summary>
        private static void MergeVEdge(int x, int y1, int y2,
            Dictionary<long, long> adjH, Dictionary<long, long> adjV)
        {
            long k1 = Mk(x, y1), k2 = Mk(x, y2);

            if (adjV.TryGetValue(k1, out long kAbove))
            {
                // k1 already connected vertically to kAbove.
                // If k1 has no horizontal neighbour it is collinear → merge the two edges.
                if (!adjH.ContainsKey(k1))
                {
                    adjV[kAbove] = k2;
                    adjV[k2]     = kAbove;
                    adjV.Remove(k1);
                }
                // else k1 is a real corner; a second V-edge would be degenerate — skip.
            }
            else
            {
                adjV[k1] = k2;
                adjV[k2] = k1;
            }
        }

        /// <summary>
        /// Emits horizontal boundary edges at row y by computing the XOR
        /// of run coverage between rows y-1 (above) and y (below).
        /// XOR intervals are those x-ranges covered by exactly one of the two rows.
        /// </summary>
        private static void EmitXorHEdges(CxRun[] runs,
            int aS, int aE, int bS, int bE, int y,
            Dictionary<long, long> adjH)
        {
            int total = (aE - aS + bE - bS) * 2;
            if (total == 0) return;

            // Collect start/end events from both row's runs and sort by x.
            var evts = new List<(int x, int da, int db)>(total);
            for (int i = aS; i < aE; i++)
            {
                evts.Add((runs[i].ColStart,  1, 0));
                evts.Add((runs[i].ColEnd,   -1, 0));
            }
            for (int i = bS; i < bE; i++)
            {
                evts.Add((runs[i].ColStart, 0,  1));
                evts.Add((runs[i].ColEnd,   0, -1));
            }
            evts.Sort((a, b) => a.x.CompareTo(b.x));

            // Sweep: detect XOR transitions.
            int cA = 0, cB = 0, xSt = -1;
            int j = 0;
            while (j < evts.Count)
            {
                int x = evts[j].x;
                bool before = (cA > 0) != (cB > 0);
                while (j < evts.Count && evts[j].x == x)
                {
                    cA += evts[j].da;
                    cB += evts[j].db;
                    j++;
                }
                bool after = (cA > 0) != (cB > 0);
                if (!before && after) { xSt = x; }
                else if (before && !after)
                {
                    long k1 = Mk(xSt, y), k2 = Mk(x, y);
                    adjH[k1] = k2;
                    adjH[k2] = k1;
                }
            }
        }

        private static long Mk(int x, int y) => ((long)x << 32) | (uint)y;

        /// <summary>Scales all runs to fit a new width/height. Adjacent runs on the same row are merged.</summary>
        public static void ResizeRegion2D(CxRegion2D src, int newW, int newH, out CxRegion2D dst)
        {
            if (src.IsEmpty || newW <= 0 || newH <= 0)
            {
                dst = new CxRegion2D(newW > 0 ? newW : src.Width, newH > 0 ? newH : src.Height, null);
                return;
            }
            float sx = (float)newW / src.Width;
            float sy = (float)newH / src.Height;

            // Scale runs directly into a list (eliminates the intermediate temp[] array).
            var scaled = new List<CxRun>(src.Runs.Length);
            for (int i = 0; i < src.Runs.Length; i++)
            {
                int row = (int)(src.Runs[i].Row * sy);
                int cs  = (int)(src.Runs[i].ColStart * sx);
                int ce  = (int)(src.Runs[i].ColEnd * sx);
                cs = Math.Max(0, cs);
                ce = Math.Min(newW, ce);
                if (cs < ce && row >= 0 && row < newH)
                    scaled.Add(new CxRun(row, cs, ce));
            }

            // Sort is still needed: when sy < 1, multiple source rows map to the same
            // target row and their column ranges might interleave.
            scaled.Sort((a, b) =>
            {
                int d = a.Row.CompareTo(b.Row);
                return d != 0 ? d : a.ColStart.CompareTo(b.ColStart);
            });

            var merged = new List<CxRun>(scaled.Count);
            for (int i = 0; i < scaled.Count; i++)
            {
                var cur = scaled[i];
                if (merged.Count > 0)
                {
                    var last = merged[merged.Count - 1];
                    if (last.Row == cur.Row && last.ColEnd >= cur.ColStart)
                    {
                        if (cur.ColEnd > last.ColEnd)
                            merged[merged.Count - 1] = new CxRun(last.Row, last.ColStart, cur.ColEnd);
                        continue;
                    }
                }
                merged.Add(cur);
            }

            dst = new CxRegion2D(newW, newH, merged.ToArray());
        }

        // ── Region: Set Operations ───────────────────────────────────────────────

        /// <summary>Returns the union of two regions (same Width/Height).</summary>
        public static void UnionRegion2D(CxRegion2D a, CxRegion2D b, out CxRegion2D result)
        {
            if (a.Width != b.Width || a.Height != b.Height)
            { result = new CxRegion2D(); return; }

            var list = new List<CxRun>();
            int ai = 0, bi = 0;
            while (ai < a.Runs.Length || bi < b.Runs.Length)
            {
                bool pickA = bi >= b.Runs.Length || (ai < a.Runs.Length &&
                    (a.Runs[ai].Row < b.Runs[bi].Row ||
                     (a.Runs[ai].Row == b.Runs[bi].Row && a.Runs[ai].ColStart <= b.Runs[bi].ColStart)));

                var cur = pickA ? a.Runs[ai++] : b.Runs[bi++];

                if (list.Count > 0)
                {
                    var last = list[list.Count - 1];
                    if (last.Row == cur.Row && last.ColEnd >= cur.ColStart)
                    {
                        if (cur.ColEnd > last.ColEnd)
                            list[list.Count - 1] = new CxRun(last.Row, last.ColStart, cur.ColEnd);
                        continue;
                    }
                }
                list.Add(cur);
            }

            result = new CxRegion2D(a.Width, a.Height, list.ToArray());
        }

        /// <summary>Returns the intersection of two regions (same Width/Height).</summary>
        public static void IntersectRegion2D(CxRegion2D a, CxRegion2D b, out CxRegion2D result)
        {
            if (a.Width != b.Width || a.Height != b.Height)
            { result = new CxRegion2D(); return; }

            var list = new List<CxRun>();
            int ai = 0, bi = 0;
            while (ai < a.Runs.Length && bi < b.Runs.Length)
            {
                var ra = a.Runs[ai];
                var rb = b.Runs[bi];

                if (ra.Row < rb.Row) { ai++; continue; }
                if (rb.Row < ra.Row) { bi++; continue; }

                int s = Math.Max(ra.ColStart, rb.ColStart);
                int e = Math.Min(ra.ColEnd, rb.ColEnd);
                if (s < e)
                {
                    if (list.Count > 0)
                    {
                        var last = list[list.Count - 1];
                        if (last.Row == ra.Row && last.ColEnd >= s)
                            list[list.Count - 1] = new CxRun(ra.Row, last.ColStart, e);
                        else
                            list.Add(new CxRun(ra.Row, s, e));
                    }
                    else
                        list.Add(new CxRun(ra.Row, s, e));
                }

                if (ra.ColEnd < rb.ColEnd) ai++; else bi++;
            }

            result = new CxRegion2D(a.Width, a.Height, list.ToArray());
        }

        /// <summary>Returns the difference a - b (same Width/Height). O(N+M) two-pointer implementation.</summary>
        public static void SubtractRegion2D(CxRegion2D a, CxRegion2D b, out CxRegion2D result)
        {
            if (a.Width != b.Width || a.Height != b.Height)
            { result = new CxRegion2D(); return; }

            var list = new List<CxRun>();
            int bi = 0;

            for (int ai = 0; ai < a.Runs.Length; ai++)
            {
                var ra = a.Runs[ai];
                int curS = ra.ColStart, curE = ra.ColEnd;

                // Advance bi to the first b run that is on or after ra.Row (bi never resets).
                while (bi < b.Runs.Length && b.Runs[bi].Row < ra.Row) bi++;

                // Scan b runs on ra.Row using a local index (bi stays at the row start for
                // the next a run on the same row).
                for (int bj = bi; bj < b.Runs.Length && b.Runs[bj].Row == ra.Row && curS < curE; bj++)
                {
                    var rb = b.Runs[bj];
                    if (rb.ColEnd <= curS) continue;
                    if (rb.ColStart >= curE) break;

                    if (rb.ColStart > curS)
                        list.Add(new CxRun(ra.Row, curS, rb.ColStart));
                    curS = rb.ColEnd;
                }

                if (curS < curE)
                    list.Add(new CxRun(ra.Row, curS, curE));
            }

            result = new CxRegion2D(a.Width, a.Height, list.ToArray());
        }
    }
}
