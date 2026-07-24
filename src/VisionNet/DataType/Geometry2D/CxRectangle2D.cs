using System;

namespace VisionNet.DataType
{
    public struct CxRectangle2D
    {
        public CxPoint2D Center;
        public CxSize2D Size;
        public float Angle;

        public CxRectangle2D(CxPoint2D center, CxSize2D size, float angle = 0f)
        {
            Center = center;
            Size = size;
            Angle = angle;
        }

        public CxRectangle2D(float centerX, float centerY, float width, float height, float angle = 0f)
        {
            Center = new CxPoint2D(centerX, centerY);
            Size = new CxSize2D(width, height);
            Angle = angle;
        }

        public void GetCorners(out CxPoint2D TopLeft , out CxPoint2D TopRight, out CxPoint2D BottomLeft, out CxPoint2D BottomRight)
        {
            float halfWidth = Size.Width / 2f;
            float halfHeight = Size.Height / 2f;
            // Calculate the corners relative to the center
            CxPoint2D[] corners = new CxPoint2D[4];
            corners[0] = new CxPoint2D(-halfWidth, -halfHeight); // TopLeft
            corners[1] = new CxPoint2D(halfWidth, -halfHeight);  // TopRight
            corners[2] = new CxPoint2D(-halfWidth, halfHeight);  // BottomLeft
            corners[3] = new CxPoint2D(halfWidth, halfHeight);   // BottomRight
            // Rotate and translate the corners
            float cosAngle = (float)Math.Cos(Angle);
            float sinAngle = (float)Math.Sin(Angle);
            for (int i = 0; i < corners.Length; i++)
            {
                float xRotated = corners[i].X * cosAngle - corners[i].Y * sinAngle;
                float yRotated = corners[i].X * sinAngle + corners[i].Y * cosAngle;
                corners[i].X = xRotated + Center.X;
                corners[i].Y = yRotated + Center.Y;
            }
            TopLeft = corners[0];
            TopRight = corners[1];
            BottomLeft = corners[2];
            BottomRight = corners[3];
        }
    }
}
