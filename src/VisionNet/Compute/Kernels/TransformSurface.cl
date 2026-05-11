__kernel void TransformSurface(
    __global const short* data,
    __global float*       dstPoints,
    int   width,
    int   length,
    float xOffset, float yOffset, float zOffset,
    float xScale,  float yScale,  float zScale,
    __global const float* matrix)
{
    int gid = get_global_id(0);
    int x   = gid % width;
    int y   = gid / width;
    if (x >= width || y >= length) return;
    int idx = y * width + x;
    float px = x * xScale + xOffset;
    float py = y * yScale + yOffset;
    float pz = zOffset + data[idx] * zScale;
    float4 p = (float4)(px, py, pz, 1.0f);
    float4 r;
    r.x = matrix[0]*p.x + matrix[1]*p.y + matrix[2]*p.z  + matrix[3]*p.w;
    r.y = matrix[4]*p.x + matrix[5]*p.y + matrix[6]*p.z  + matrix[7]*p.w;
    r.z = matrix[8]*p.x + matrix[9]*p.y + matrix[10]*p.z + matrix[11]*p.w;
    r.w = matrix[12]*p.x+ matrix[13]*p.y+ matrix[14]*p.z + matrix[15]*p.w;
    dstPoints[gid * 4]     = r.x;
    dstPoints[gid * 4 + 1] = r.y;
    dstPoints[gid * 4 + 2] = data[idx] == -32768 ? NAN : r.z;
    dstPoints[gid * 4 + 3] = r.w;
}
