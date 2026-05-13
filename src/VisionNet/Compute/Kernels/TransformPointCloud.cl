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

    float tx = matrix[0]*x + matrix[4]*y + matrix[8]*z  + matrix[12];
    float ty = matrix[1]*x + matrix[5]*y + matrix[9]*z  + matrix[13];
    float tz = matrix[2]*x + matrix[6]*y + matrix[10]*z + matrix[14];
    float tw = matrix[3]*x + matrix[7]*y + matrix[11]*z + matrix[15];

    if (fabs(tw) > 1e-9f) { tx /= tw; ty /= tw; tz /= tw; }

    transformed[gid * 3]     = tx;
    transformed[gid * 3 + 1] = ty;
    transformed[gid * 3 + 2] = tz;
}
