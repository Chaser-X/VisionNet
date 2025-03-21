#include "DataType.h"
#include "CxUniformGridSampler.h"
#include <vector>

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

extern "C" _declspec(dllexport) void UniformGridSample(Point3D* points,uint8_t* intensitys, 
    int size, float xScale, float yScale, float xMin, float xMax, float yMin, float yMax,
    float* heightMap, uint8_t* intensityMap , int* mapSize)
{
    if (size == 0)
        return;

    // ���� CxUniformGridSampler ʵ��
    CxUniformGridSampler sampler(xScale, yScale, xMin, xMax, yMin, yMax);

    // �������������ת��Ϊ std::vector<Point3DI>
    std::vector<Point3D> pointCloud(points, points + size);
	std::vector<uint8_t> intensity;
    if(intensitys != NULL)
        intensity.assign(intensitys, intensitys + size);
    // �����������
    sampler.processPointCloud(pointCloud, intensity);

    // ��ȡ�߶�ͼ������ͼ���
    const std::vector<float>& heightResult = sampler.getHeightMap();
    const std::vector<uint8_t>& intensityResult = sampler.getIntensityMap();

    // ��������Ƶ��������
    *mapSize = static_cast<int>(heightResult.size());
    for (int i = 0; i < *mapSize; i++)
    {
        heightMap[i] = heightResult[i];
        if(!intensityResult.empty())
            intensityMap[i] = intensityResult[i];
    }
}