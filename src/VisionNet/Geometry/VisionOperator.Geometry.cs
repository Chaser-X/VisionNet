using VisionNet.DataType;

namespace VisionNet
{
    /// <summary>
    /// Pure geometric construction and computation operations (planes, intersections, distances).
    /// 数学精确构造，无数据拟合/误差。区别于 Analysis/（基于点云数据的统计/拟合）。
    /// </summary>
    public static partial class VisionOperator
    {
        // ── 预留扩展 ──────────────────────────────────────────────────────────
        // CreatePlane(CxPoint3D p1, p2, p3)            从三点构造平面
        // CreatePlane(CxPoint3D point, CxSegment3D line) 从点和直线构造平面
        // CreatePlane(CxPoint3D point, CxVector3D normal)  从点和法线构造平面
        // IntersectPlanePlane(CxPlane3D, CxPlane3D)        两平面求交线
        // IntersectPlaneSegment(CxPlane3D, CxSegment3D)    平面与线段求交点
        // ProjectPointToPlane(CxPoint3D, CxPlane3D)      点投影到平面
        // DistancePointToPlane / DistancePointToLine   点面距、点线距
        // TransformPoint3D / CalculateBoundingBox      (当前在 Analysis/，可迁入此处)
    }
}
