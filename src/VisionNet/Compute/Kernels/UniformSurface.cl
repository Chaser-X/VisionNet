__kernel void UniformSurfaceSample(
    __global const float* points,
    __global const uchar* intensity,
    int   pointCount,
    int   width,
    int   height,
    float xScale,
    float yScale,
    float zScale,
    float xOffset,
    float yOffset,
    float zOffset,
    __global int* heightMap,
    __global int* intensityMap,
    __global int* pointCountMap,
    int   inMode,
    int   stride)
{
    int gid = get_global_id(0);
    float px = points[gid * stride + 0];
    float py = points[gid * stride + 1];
    float pz = points[gid * stride + 2];
    if (isnan(pz) || isinf(pz)) return;
    int xIdx = (int)((px - xOffset) / xScale);
    int yIdx = (int)((py - yOffset) / yScale);
    if (xIdx < 0 || xIdx >= width || yIdx < 0 || yIdx >= height) return;
    int scaledZ = (int)((pz - zOffset) / zScale);
    int idx     = yIdx * width + xIdx;
    int inten   = intensity ? (int)intensity[gid] : 0;
    switch (inMode)
    {
        case 0:
        {
            int old = atomic_max(&heightMap[idx], scaledZ);
            if (scaledZ >= old) intensityMap[idx] = inten;
            break;
        }
        case 1:
        {
            int old = atomic_min(&heightMap[idx], scaledZ);
            if (scaledZ < old) intensityMap[idx] = inten;
            break;
        }
        default:
            atomic_add(&heightMap[idx], scaledZ);
            atomic_add(&intensityMap[idx], inten);
            break;
    }
    atomic_inc(&pointCountMap[idx]);
}
