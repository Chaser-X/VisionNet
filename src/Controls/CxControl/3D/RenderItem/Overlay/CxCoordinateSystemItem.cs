using SharpGL;
using SharpGL.SceneGraph;
using SharpGL.SceneGraph.Primitives;
using SharpGL.SceneGraph.Quadrics;
using System;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    /// <summary>
    /// Renders a 3D coordinate system with labelled X (red), Y (green), and Z (blue) axes.
    /// Each axis is drawn as a cylinder capped with a cone arrow-head.
    /// <para>
    /// In addition to the world-space <see cref="Draw"/> call, <see cref="DrawScreenPositionedAxes"/>
    /// renders a small orientation indicator fixed to the lower-left corner of the viewport
    /// that rotates with the camera but is not affected by translation or zoom.
    /// </para>
    /// </summary>
    public class CxCoordinateSystemItem : AbstractRenderItem
    {
        private readonly float _axisLength;
        private readonly float _axisRadius;
        private readonly float _coneHeight;
        private readonly float _coneRadius;
        private readonly CxCoordination3D _coordination;

        /// <summary>
        /// Initializes the coordinate system item.
        /// </summary>
        /// <param name="axisLength">Total length of each axis (cylinder + cone).</param>
        /// <param name="axisRadius">Radius of the cylindrical shaft.</param>
        /// <param name="coneHeight">Height of the arrow-head cone.</param>
        /// <param name="coneRadius">Base radius of the arrow-head cone.</param>
        /// <param name="coordination">
        /// Coordinate frame to render. Defaults to the world origin with unit axes when <c>null</c>.
        /// </param>
        public CxCoordinateSystemItem(
            float axisLength = 5.0f, float axisRadius = 0.1f,
            float coneHeight = 0.5f, float coneRadius  = 0.2f,
            CxCoordination3D? coordination = null)
        {
            _axisLength  = axisLength;
            _axisRadius  = axisRadius;
            _coneHeight  = coneHeight;
            _coneRadius  = coneRadius;
            _coordination = coordination ?? new CxCoordination3D(
                new CxPoint3D(0, 0, 0),
                new CxVector3D(1, 0, 0),
                new CxVector3D(0, 1, 0),
                new CxVector3D(0, 0, 1));
        }

        /// <summary>Draws the three labelled axes at the configured world-space origin.</summary>
        public override void Draw(OpenGL gl)
        {
            DrawAxisWithLabel(gl, _coordination.Origin, _coordination.XAxis, "X", 1f, 0f, 0f);
            DrawAxisWithLabel(gl, _coordination.Origin, _coordination.YAxis, "Y", 0f, 1f, 0f);
            DrawAxisWithLabel(gl, _coordination.Origin, _coordination.ZAxis, "Z", 0f, 0f, 1f);
        }

        /// <summary>
        /// Draws a small orientation indicator in the lower-left corner of the viewport.
        /// The indicator reflects the current camera rotation but ignores translation and zoom.
        /// </summary>
        public void DrawScreenPositionedAxes(OpenGL gl)
        {
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);

            // Extract the rotation-only part of the modelview matrix.
            float[] mvMatrix = new float[16];
            gl.GetFloat(OpenGL.GL_MODELVIEW_MATRIX, mvMatrix);
            mvMatrix[12] = 0f; mvMatrix[13] = 0f; mvMatrix[14] = 0f;
            NormalizeColumn(mvMatrix, 0);
            NormalizeColumn(mvMatrix, 1);
            NormalizeColumn(mvMatrix, 2);

            // Switch to screen-space orthographic projection.
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Ortho(0, gl.RenderContextProvider.Width, 0, gl.RenderContextProvider.Height, -100, 100);

            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PushMatrix();
            gl.LoadIdentity();

            // Place the indicator 50 px from the lower-left corner and scale it up.
            gl.Translate(50f, 50f, 0f);
            gl.Scale(10f, 10f, 10f);
            gl.MultMatrix(mvMatrix);

            Draw(gl);

            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.PopMatrix();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.PopMatrix();

            gl.PopAttrib();
            gl.Enable(OpenGL.GL_DEPTH_TEST);
        }

        /// <summary>
        /// Draws a single axis as a cylinder (shaft) followed by a cone (arrow-head) along <paramref name="direction"/>.
        /// </summary>
        private void DrawAxisWithLabel(OpenGL gl, CxPoint3D origin, CxVector3D direction,
            string label, float r, float g, float b)
        {
            var dir        = direction.Normalize();
            var defaultDir = new CxVector3D(0, 0, 1);   // OpenGL default cylinder direction.
            float dot      = defaultDir.Dot(dir);

            gl.PushMatrix();
            gl.Translate(origin.X, origin.Y, origin.Z);

            if (Math.Abs(dot - 1f) >= 1e-3f)
            {
                if (Math.Abs(dot + 1f) < 1e-3f)
                {
                    gl.Rotate(180, 1, 0, 0);   // Anti-parallel: rotate 180° around X.
                }
                else
                {
                    var   axis  = defaultDir.Cross(dir);
                    float angle = (float)(Math.Acos(dot) * 180.0 / Math.PI);
                    gl.Rotate(angle, axis.X, axis.Y, axis.Z);
                }
            }

            DrawAxis(gl, _axisLength, _axisRadius, _coneHeight, _coneRadius, r, g, b);
            DrawLabel(gl, label, 0, 0, _axisLength, r, g, b);

            gl.PopMatrix();
        }

        /// <summary>Draws a cylinder shaft followed by a cone arrow-head along the local Z axis.</summary>
        private void DrawAxis(OpenGL gl, float length, float radius,
            float coneHeight, float coneRadius, float r, float g, float b)
        {
            gl.Color(r, g, b);

            var cylinder = new Cylinder();
            cylinder.BaseRadius = radius;
            cylinder.TopRadius  = radius;
            cylinder.Height     = length - coneHeight;
            cylinder.CreateInContext(gl);
            cylinder.Render(gl, SharpGL.SceneGraph.Core.RenderMode.Render);

            gl.Translate(0f, 0f, length - coneHeight);
            DrawCone(gl, coneHeight, coneRadius);
            gl.Translate(0f, 0f, -(length - coneHeight));
        }

        /// <summary>Draws a cone with its apex at <c>(0, 0, height)</c> and its base at the origin.</summary>
        private void DrawCone(OpenGL gl, float height, float radius)
        {
            const int slices = 20;

            gl.Begin(OpenGL.GL_TRIANGLE_FAN);
            gl.Vertex(0f, 0f, height);   // Apex.
            for (int i = 0; i <= slices; i++)
            {
                double angle = 2 * Math.PI * i / slices;
                gl.Vertex(radius * Math.Cos(angle), radius * Math.Sin(angle), 0f);
            }
            gl.End();

            gl.Begin(OpenGL.GL_TRIANGLE_FAN);
            gl.Vertex(0f, 0f, 0f);   // Base centre.
            for (int i = 0; i <= slices; i++)
            {
                double angle = 2 * Math.PI * i / slices;
                gl.Vertex(radius * Math.Cos(angle), radius * Math.Sin(angle), 0f);
            }
            gl.End();
        }

        /// <summary>
        /// Draws a 2D text label at the screen position corresponding to the given local 3D point.
        /// The label is skipped if the point projects outside the viewport or behind the camera.
        /// </summary>
        private void DrawLabel(OpenGL gl, string label, float x, float y, float z,
            float r, float g, float b)
        {
            var screenCoord = gl.Project(new Vertex(x, y, z));

            if (screenCoord.X < 0 || screenCoord.X > gl.RenderContextProvider.Width ||
                screenCoord.Y < 0 || screenCoord.Y > gl.RenderContextProvider.Height)
                return;
            if (screenCoord.Z < 0 || screenCoord.Z > 1)
                return;

            gl.DrawText((int)screenCoord.X, (int)screenCoord.Y, r, g, b, "Arial", 20, label);
        }

        /// <summary>Normalises the specified column of a 4×4 column-major matrix in place.</summary>
        private void NormalizeColumn(float[] matrix, int col)
        {
            float x = matrix[col], y = matrix[4 + col], z = matrix[8 + col];
            float len = (float)Math.Sqrt(x * x + y * y + z * z);
            if (len > 0f)
            {
                matrix[col]     /= len;
                matrix[4 + col] /= len;
                matrix[8 + col] /= len;
            }
        }
    }
}
