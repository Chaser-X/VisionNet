using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet
{
    public static class VisionOperator
    {
        public static Point3D GetPoint3DArrayCenter(List<Point3D> point3Ds)
        {
            Point3D center = new Point3D();
            var ret = Export.GetCenter(point3Ds.ToArray(), point3Ds.Count, ref center);
            if (ret == -1)
                center = new Point3D(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            return center;
        }
    }
}
