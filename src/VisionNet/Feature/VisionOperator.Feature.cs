using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Feature extraction and primitive recognition operations (normals, descriptors, keypoints).
    /// </summary>
    public static partial class VisionOperator
    {
        // ── 预留扩展 ──────────────────────────────────────────────────────────
        // EstimateNormals     法线估计
        // ComputeFPFH         FPFH 特征描述子
        // ComputeVFH          VFH 全局描述子
        // DetectKeypoints     关键点检测
        // FindCircle3D        三维圆检测（多方法时从 Analysis 迁移至此）
    }
}
