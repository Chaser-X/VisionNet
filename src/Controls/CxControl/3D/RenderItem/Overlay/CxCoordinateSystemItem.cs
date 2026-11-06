using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>Renders world and screen space coordinate axes with cached GLSL geometry.</summary>
    public class CxCoordinateSystemItem : AbstractRenderItem
    {
        private readonly float _axisLength;
        private readonly float _axisRadius;
        private readonly float _coneHeight;
        private readonly float _coneRadius;
        private readonly CxCoordination3D _coordination;
        private readonly float[] _xAxisMesh;
        private readonly float[] _yAxisMesh;
        private readonly float[] _zAxisMesh;
        private readonly float[] _screenPositions = new float[12];
        private readonly float[] _screenColors = new float[24];

        public CxCoordinateSystemItem(float axisLength = 5f, float axisRadius = .1f,
            float coneHeight = .5f, float coneRadius = .2f, CxCoordination3D? coordination = null)
        {
            _axisLength = axisLength;
            _axisRadius = axisRadius;
            _coneHeight = coneHeight;
            _coneRadius = coneRadius;
            _coordination = coordination ?? new CxCoordination3D(new CxPoint3D(0, 0, 0),
                new CxVector3D(1, 0, 0), new CxVector3D(0, 1, 0), new CxVector3D(0, 0, 1));
            _xAxisMesh = BuildAxisMesh(_coordination.Origin, _coordination.XAxis.Normalize(),
                _axisLength, _axisRadius, _coneHeight, _coneRadius);
            _yAxisMesh = BuildAxisMesh(_coordination.Origin, _coordination.YAxis.Normalize(),
                _axisLength, _axisRadius, _coneHeight, _coneRadius);
            _zAxisMesh = BuildAxisMesh(_coordination.Origin, _coordination.ZAxis.Normalize(),
                _axisLength, _axisRadius, _coneHeight, _coneRadius);
        }

        public override bool SupportsGroupTransform => true;

        public override void Draw(CxGlContext gl)
        {
            PushModelMatrix(gl);
            DrawAxis(gl, _xAxisMesh, _coordination.XAxis, Color.Red, "X");
            DrawAxis(gl, _yAxisMesh, _coordination.YAxis, Color.Lime, "Y");
            DrawAxis(gl, _zAxisMesh, _coordination.ZAxis, Color.Blue, "Z");
            PopModelMatrix(gl);
        }

        public void DrawScreenPositionedAxes(CxGlContext gl)
        {
            var renderer = ModernGLRenderer.For(gl);
            renderer.EnsureSceneMatrices(gl);
            float[] mv = renderer.SceneView;
            const float ox = 50f, oy = 50f, length = 42f;
            // SceneView is row-major. Transforming each world basis vector gives
            // screen X from row 0 and screen Y from row 1 (the first two entries
            // of each matrix column), rather than reading contiguous row entries.
            float xX = mv[0], xY = mv[4];
            float yX = mv[1], yY = mv[5];
            float zX = mv[2], zY = mv[6];
            float xZ = mv[8], yZ = mv[9], zZ = mv[10];
            // Normalize the complete projected 3D basis vectors.  Normalizing only
            // X/Y makes an axis pointing toward the camera look the same length as
            // an edge-on axis, and the ZScale camera option would otherwise distort
            // the screen indicator.  Keeping the Z component in the normalization
            // preserves the expected foreshortening in screen space.
            NormalizeDirection(ref xX, ref xY, ref xZ);
            NormalizeDirection(ref yX, ref yY, ref yZ);
            NormalizeDirection(ref zX, ref zY, ref zZ);
            var positions = _screenPositions;
            positions[0] = ox; positions[1] = oy; positions[2] = ox + xX * length; positions[3] = oy + xY * length;
            positions[4] = ox; positions[5] = oy; positions[6] = ox + yX * length; positions[7] = oy + yY * length;
            positions[8] = ox; positions[9] = oy; positions[10] = ox + zX * length; positions[11] = oy + zY * length;
            var colors = _screenColors;
            SetLineColor(colors, 0, Color.Red);
            SetLineColor(colors, 1, Color.Red);
            SetLineColor(colors, 2, Color.Lime);
            SetLineColor(colors, 3, Color.Lime);
            SetLineColor(colors, 4, Color.Blue);
            SetLineColor(colors, 5, Color.Blue);
            renderer.DrawScreenGeometryColored(gl, PrimitiveType.Lines, positions, colors, 2f);
            renderer.DrawText(gl, (int)(ox + xX * length), (int)(oy + xY * length), Color.Red, 14, "X");
            renderer.DrawText(gl, (int)(ox + yX * length), (int)(oy + yY * length), Color.Lime, 14, "Y");
            renderer.DrawText(gl, (int)(ox + zX * length), (int)(oy + zY * length), Color.Blue, 14, "Z");
        }

        private static void SetLineColor(float[] colors, int vertex, Color color)
        {
            int offset = vertex * 4;
            colors[offset] = color.R / 255f;
            colors[offset + 1] = color.G / 255f;
            colors[offset + 2] = color.B / 255f;
            colors[offset + 3] = color.A / 255f;
        }

        private static void NormalizeDirection(ref float x, ref float y, ref float z)
        {
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            if (length > 1e-6f)
            {
                x /= length;
                y /= length;
                z /= length;
            }
        }

        private void DrawAxis(CxGlContext gl, float[] mesh, CxVector3D direction, Color color, string label)
        {
            var renderer = ModernGLRenderer.For(gl);
            renderer.Draw(gl, PrimitiveType.Triangles,
                mesh, color);
            var tip = Add(_coordination.Origin, Scale(direction, _axisLength));
            float sx, sy, sz;
            if (renderer.Project(gl, tip.X, tip.Y, tip.Z, out sx, out sy, out sz))
                renderer.DrawText(gl, (int)sx, (int)sy, color, 16, label);
        }

        private static float[] BuildAxisMesh(CxPoint3D origin, CxVector3D direction,
            float length, float radius, float coneHeight, float coneRadius)
        {
            const int slices = 20;
            var w = direction.Normalize();
            var reference = Math.Abs(w.Z) < .9f ? new CxVector3D(0, 0, 1) : new CxVector3D(0, 1, 0);
            var u = reference.Cross(w).Normalize();
            var v = w.Cross(u).Normalize();
            float shaftEnd = Math.Max(0, length - coneHeight);
            var data = new List<float>(slices * 18);
            for (int i = 0; i < slices; i++)
            {
                double a0 = i * 2 * Math.PI / slices;
                double a1 = (i + 1) * 2 * Math.PI / slices;
                var b0 = Add(origin, Add(Scale(u, (float)Math.Cos(a0) * radius), Scale(v, (float)Math.Sin(a0) * radius)));
                var b1 = Add(origin, Add(Scale(u, (float)Math.Cos(a1) * radius), Scale(v, (float)Math.Sin(a1) * radius)));
                var t0 = Add(b0, Scale(w, shaftEnd));
                var t1 = Add(b1, Scale(w, shaftEnd));
                var apex = Add(origin, Scale(w, length));
                AddTriangle(data, b0, t0, t1); AddTriangle(data, b0, t1, b1);
                var c0 = Add(origin, Add(Scale(u, (float)Math.Cos(a0) * coneRadius), Scale(v, (float)Math.Sin(a0) * coneRadius)));
                var c1 = Add(origin, Add(Scale(u, (float)Math.Cos(a1) * coneRadius), Scale(v, (float)Math.Sin(a1) * coneRadius)));
                c0 = Add(c0, Scale(w, shaftEnd)); c1 = Add(c1, Scale(w, shaftEnd));
                AddTriangle(data, c0, apex, c1);
            }
            return data.ToArray();
        }

        private static CxPoint3D Add(CxPoint3D a, CxVector3D b) => new CxPoint3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        private static CxVector3D Add(CxVector3D a, CxVector3D b) => new CxVector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        private static CxVector3D Scale(CxVector3D a, float s) => new CxVector3D(a.X * s, a.Y * s, a.Z * s);
        private static void AddTriangle(List<float> data, CxPoint3D a, CxPoint3D b, CxPoint3D c)
        {
            data.Add(a.X); data.Add(a.Y); data.Add(a.Z);
            data.Add(b.X); data.Add(b.Y); data.Add(b.Z);
            data.Add(c.X); data.Add(c.Y); data.Add(c.Z);
        }
    }
}
