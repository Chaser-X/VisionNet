__kernel void TransformVertices(
    __global const float* vertices,
    __global const float* matrix,
    int                   vertexCount,
    __global       float* transformed)
{
    int gid = get_global_id(0);
    if (gid >= vertexCount) return;

    float x = vertices[gid * 3];
    float y = vertices[gid * 3 + 1];
    float z = vertices[gid * 3 + 2];

    float tx = matrix[0]*x + matrix[1]*y + matrix[2]*z  + matrix[3];
    float ty = matrix[4]*x + matrix[5]*y + matrix[6]*z  + matrix[7];
    float tz = matrix[8]*x + matrix[9]*y + matrix[10]*z + matrix[11];
    float tw = matrix[12]*x+ matrix[13]*y+ matrix[14]*z + matrix[15];

    if (fabs(tw) > 1e-9f) { tx /= tw; ty /= tw; tz /= tw; }

    transformed[gid * 3]     = tx;
    transformed[gid * 3 + 1] = ty;
    transformed[gid * 3 + 2] = tz;
}

float edge(float2 a, float2 b, float2 c)
{
    return (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
}

__kernel void RasterizeTriangles(
    __global const float* vertices,
    __global const uint*  indices,
    int   triangleCount,
    int   width,
    int   height,
    float xOffset,
    float yOffset,
    float zOffset,
    float xScale,
    float yScale,
    float zScale,
    __global       int*   heightMap,
    __global       int*   intensityMap,
    __global const uchar* intensityData,
    __global const float* uvs,
    int   textureWidth,
    int   textureHeight,
    int   intensityMode,
    int   mode)
{
    int gid = get_global_id(0);
    if (gid >= triangleCount) return;

    uint i0 = indices[gid * 3];
    uint i1 = indices[gid * 3 + 1];
    uint i2 = indices[gid * 3 + 2];

    float3 v0 = (float3)(vertices[i0 * 3], vertices[i0 * 3 + 1], vertices[i0 * 3 + 2]);
    float3 v1 = (float3)(vertices[i1 * 3], vertices[i1 * 3 + 1], vertices[i1 * 3 + 2]);
    float3 v2 = (float3)(vertices[i2 * 3], vertices[i2 * 3 + 1], vertices[i2 * 3 + 2]);

    float denom2D = edge(v0.xy, v1.xy, v2.xy);
    if (fabs(denom2D) < 1e-9f) return;

    int minX = max(0, (int)floor((min(v0.x, min(v1.x, v2.x)) - xOffset) / xScale));
    int maxX = min(width - 1, (int)ceil((max(v0.x, max(v1.x, v2.x)) - xOffset) / xScale));
    int minY = max(0, (int)floor((min(v0.y, min(v1.y, v2.y)) - yOffset) / yScale));
    int maxY = min(height - 1, (int)ceil((max(v0.y, max(v1.y, v2.y)) - yOffset) / yScale));

    for (int py = minY; py <= maxY; py++)
    {
        for (int px = minX; px <= maxX; px++)
        {
            float2 p = (float2)((px + 0.5f) * xScale + xOffset, (py + 0.5f) * yScale + yOffset);

            float w0 = edge(v1.xy, v2.xy, p);
            float w1 = edge(v2.xy, v0.xy, p);
            float w2 = edge(v0.xy, v1.xy, p);

            if (w0 < 0 || w1 < 0 || w2 < 0) continue;

            w0 /= denom2D;
            w1 /= denom2D;
            w2 /= denom2D;

            float zInterp = w0 * v0.z + w1 * v1.z + w2 * v2.z;
            int   idx     = py * width + px;
            int   scaledZ = (int)((zInterp - zOffset) / zScale);
            int   old;

            if (mode == 0)
                old = atomic_max(&heightMap[idx], scaledZ);
            else
                old = atomic_min(&heightMap[idx], scaledZ);

            bool zWon = (mode == 0) ? (scaledZ >= old) : (scaledZ <= old);

            if (zWon && intensityMode > 0)
            {
                if (intensityMode == 1)
                {
                    float intensity = w0 * (float)intensityData[i0]
                                   + w1 * (float)intensityData[i1]
                                   + w2 * (float)intensityData[i2];
                    atomic_max(&intensityMap[idx], (int)intensity);
                }
                else if (intensityMode == 2 && textureWidth > 0 && textureHeight > 0)
                {
                    float u = clamp(w0 * uvs[i0*2] + w1 * uvs[i1*2] + w2 * uvs[i2*2], 0.0f, 1.0f);
                    float v = clamp(w0 * uvs[i0*2+1] + w1 * uvs[i1*2+1] + w2 * uvs[i2*2+1], 0.0f, 1.0f);

                    float fx = u * (textureWidth - 1);
                    float fy = v * (textureHeight - 1);
                    int x0 = (int)fx, y0 = (int)fy;
                    int x1 = min(x0 + 1, textureWidth - 1);
                    int y1 = min(y0 + 1, textureHeight - 1);
                    float wx = fx - x0, wy = fy - y0;

                    int sampled = (int)(
                        (1-wx)*(1-wy) * (float)intensityData[y0 * textureWidth + x0] +
                        wx    *(1-wy) * (float)intensityData[y0 * textureWidth + x1] +
                        (1-wx)*wy     * (float)intensityData[y1 * textureWidth + x0] +
                        wx    *wy     * (float)intensityData[y1 * textureWidth + x1]);
                    atomic_max(&intensityMap[idx], sampled);
                }
            }
        }
    }
}
