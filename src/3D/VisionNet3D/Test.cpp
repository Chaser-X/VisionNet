//#include "pch.h"
#include "DataType.h"


extern "C" _declspec(dllexport) int GetCenter(Point3D* Pts, int size, Point3D & outCenter)
{
	if (size == 0)
		return -1;
	float tx = 0, ty = 0, tz = 0;
	for (size_t i = 0; i < size; i++)
	{
		tx += Pts[i].X;
		ty += Pts[i].Y;
		tz += Pts[i].Z;
	}

	outCenter.X = tx / size;
	outCenter.Y = ty / size;
	outCenter.Z = tz / size;

	return 0;
}