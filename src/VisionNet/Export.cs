using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet
{
    internal static class Export
    {
        [DllImport(@"VisionNet3D", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetCenter(Point3D[] Pts, int size,ref Point3D outCenter);
    }
}
