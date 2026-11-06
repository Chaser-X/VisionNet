using System;
using System.Diagnostics;
using VisionNet.DataType;
using Xunit;
using Xunit.Abstractions;

namespace VisionNet.Controls.Tests
{
    /// <summary>
    /// CPU-side baseline for the per-frame work CxDisplay performs on the managed heap. The GL
    /// paths cannot be exercised without a live context, so this measures the three CPU hotspots
    /// that the render pass repeats every frame: colour mapping (colour bar), the surface
    /// bounding-box scan, and the point-to-float position copy.
    ///
    /// This is a <b>baseline recorder</b>, not a regression gate: it asserts only real invariants
    /// of the measured code, never timings. Run with <c>-v n</c> to read the numbers, and record
    /// them in <c>Changlog.md</c> before and after a performance change.
    /// </summary>
    public class CxDisplayCpuBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public CxDisplayCpuBenchmarkTests(ITestOutputHelper output) => _output = output;

        private const int GridSize = 512;      // 512 x 512 = 262_144 surface points
        private const int PointCount = 200_000;

        [Fact]
        public void RecordsBaseline_ForThePerFrameCpuHotspots()
        {
            // ── 1. Colour mapping: the colour bar calls this once per bar row per frame. ──
            var colorResult = Measure("GetColorByHeight (x200k)", 200_000, () =>
            {
                var c = CxExtension.GetColorByHeight(37.5, 0, 100);
                return c.r + c.g + c.b;
            });
            // The mapping must stay in the documented [0, 1] RGB range.
            Assert.InRange(colorResult, 0d, 3d);

            // ── 2. Surface bounding-box scan: the render pass aggregates every item per frame. ──
            var surface = BuildSurface();
            var box = Measure("CalculateBoundingBox(surface 512x512)", 20,
                () => CxExtension.CalculateBoundingBox(surface)?.Size.Width ?? -1f);
            Assert.True(box > 0f, "the benchmark surface must produce a non-degenerate box");

            // ── 3. Position copy: each overlay point item re-materialises float[] every frame. ──
            var points = BuildPoints();
            var buffer = new float[PointCount * 3];
            var lastZ = Measure("Point3D[] -> float[] (200k)", 20, () => FillPositions(points, buffer));
            Assert.Equal((double)points[PointCount - 1].Z, lastZ);

            _output.WriteLine("");
            _output.WriteLine("基线仅记录，不作为回归门槛；改动前后各跑一次并写入 CHANGELOG。");
        }

        /// <summary>
        /// Runs <paramref name="body"/> <paramref name="iterations"/> times after a warm-up pass,
        /// and returns the last result. Prints elapsed milliseconds and Gen0 collections.
        /// </summary>
        private double Measure<T>(string label, int iterations, Func<T> body)
        {
            for (int i = 0; i < Math.Min(iterations, 1000); i++) body();   // JIT + steady state

            GC.Collect();
            GC.WaitForPendingFinalizers();
            int gen0Before = GC.CollectionCount(0);
            var sw = Stopwatch.StartNew();
            T result = default(T);
            for (int i = 0; i < iterations; i++) result = body();
            sw.Stop();
            int gen0 = GC.CollectionCount(0) - gen0Before;

            _output.WriteLine($"{label,-40} {sw.Elapsed.TotalMilliseconds,9:F2} ms  Gen0={gen0}");
            return Convert.ToDouble(result);
        }

        private static CxSurface BuildSurface()
        {
            var data = new short[GridSize * GridSize];
            for (int i = 0; i < data.Length; i++)
                data[i] = (short)(i % 3000);          // no -32768 holes: scan the worst case
            return new CxSurface(GridSize, GridSize, data, null,
                0f, 0f, 0f, 1f, 1f, 0.5f);
        }

        private static CxPoint3D[] BuildPoints()
        {
            var points = new CxPoint3D[PointCount];
            for (int i = 0; i < points.Length; i++)
                points[i] = new CxPoint3D(i * 0.5f, i * 0.25f, i * 0.125f);
            return points;
        }

        private static float FillPositions(CxPoint3D[] points, float[] buffer)
        {
            for (int i = 0; i < points.Length; i++)
            {
                buffer[i * 3] = points[i].X;
                buffer[i * 3 + 1] = points[i].Y;
                buffer[i * 3 + 2] = points[i].Z;
            }
            return buffer[(points.Length - 1) * 3 + 2];
        }
    }
}
