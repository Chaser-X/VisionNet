﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionNet;
using VisionNet.DataType;
namespace Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            List<Point3D> pts = new List<Point3D>();
            pts.Add(new Point3D());
            pts.Add(new Point3D(1,1,2));
            var  p = VisionOperator.GetPoint3DArrayCenter(pts);
            Console.ReadKey();
        }
    }
}
