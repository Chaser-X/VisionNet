# SurfaceToMesh：CxSurface → CxMesh 三角网格转换

## Context

将结构化高度图（`CxSurface`）转换为可渲染/可处理的三角网格（`CxMesh`）。
- `SurfaceType.Surface`：每格存一个 Z 值，相邻四格组成一个 quad → 2 个三角形
- `SurfaceType.PointCloud`：每格存有序 XYZ 三元组，空间分布由 data 决定而非规则间距，但网格拓扑结构与 Surface 类型相同（行列有序，可三角化）
- 无效格子（`short.MinValue`）跳过含其顶点的 quad，形成带空洞的网格
- 生成 UV 坐标（归一化行列坐标），Intensity 不生成

---

## 新增代码位置

| 位置 | 说明 |
|------|------|
| `src/VisionNet/VisionOperator.cs` | 新增静态方法 `SurfaceToMesh`，与 `TransformSurface` 同层 |

**无需新文件**（纯 CPU 操作，不涉及 GPU / kernel）

---

## 算法设计

### 两阶段处理

```
Phase 1 — 顶点生成 + 索引映射
  输入 : CxSurface (width × height 格子)
  输出 : vertices[], uvs[], indexMap[width×height]（-1 = 无效）

Phase 2 — 四边形三角化
  输入 : indexMap
  输出 : indices[]（三角形列表，CCW 绕序）
```

### 顶点坐标计算

**SurfaceType.Surface**（cell `(row, col)`，线性索引 `i = row*width + col`）：

```
if data[i] == short.MinValue → invalid (indexMap[i] = -1)
else:
  X = XOffset + col * XScale
  Y = YOffset + row * YScale
  Z = ZOffset + data[i] * ZScale
```

**SurfaceType.PointCloud**（cell `(row, col)`，线性索引 `i = row*width + col`，data 步长 3）：

```
if data[i*3] == short.MinValue → invalid
else:
  X = XOffset + data[i*3    ] * XScale
  Y = YOffset + data[i*3 + 1] * YScale
  Z = ZOffset + data[i*3 + 2] * ZScale
```

### UV 坐标

```
u = col / (float)(width  - 1)     // width  == 1 时 u = 0
v = row / (float)(height - 1)     // height == 1 时 v = 0
```

### 四边形三角化（Phase 2）

对每个 quad `(row, col)` → `(row+1, col+1)`：

```
v00 = indexMap[row     * width + col    ]
v01 = indexMap[row     * width + col + 1]
v10 = indexMap[(row+1) * width + col    ]
v11 = indexMap[(row+1) * width + col + 1]

若任一 == -1 → 跳过（含无效顶点的 quad 不生成三角形）

Triangle 1 : (v00, v10, v11)   ← CCW，法线朝 +Z
Triangle 2 : (v00, v11, v01)
```

### 索引映射构建（Phase 1 实现细节）

```
Step 1a: 并行遍历所有 cell，写入 valid[] bool 数组 (Parallel.For)
Step 1b: 串行前缀和，计算每个有效格子的连续顶点索引 + 总有效数
Step 1c: 并行填充 vertices[]、uvs[] 数组（索引已知，无竞争）
```

**预分配上界**（避免动态扩容）：
- `vertices`: `validCount`（精确，前缀和后确定）
- `indices`: `(width-1) * (height-1) * 6`（上界，实际可能更少）

---

## 方法签名

```csharp
/// <summary>
/// Converts a <see cref="CxSurface"/> to a triangle mesh.
/// Cells marked as invalid (<see cref="short.MinValue"/>) are excluded;
/// quads containing any invalid corner are skipped, leaving holes in the mesh.
/// </summary>
/// <param name="surface">Source surface. Supports both Surface and PointCloud layouts.</param>
/// <param name="generateUVs">
/// When <c>true</c>, populates <see cref="CxMesh.UVs"/> with normalised (u = col/(W-1),
/// v = row/(H-1)) coordinates and sets <see cref="CxMesh.TextureWidth"/>/<see cref="CxMesh.TextureHeight"/>
/// to the surface grid dimensions.
/// </param>
/// <returns>
/// A <see cref="CxMesh"/> with vertices and triangle indices,
/// or <c>null</c> if the surface contains no valid cells.
/// </returns>
public static CxMesh SurfaceToMesh(CxSurface surface, bool generateUVs = false)
```

---

## 实现伪代码

```csharp
public static CxMesh SurfaceToMesh(CxSurface surface, bool generateUVs = false)
{
    int W = surface.Width, H = surface.Length;
    int total = W * H;
    bool isPointCloud = surface.Type == SurfaceType.PointCloud;

    // ── Phase 1a: 并行标记有效格子 ─────────────────────────────
    bool[] valid = new bool[total];
    Parallel.For(0, total, i =>
    {
        valid[i] = isPointCloud
            ? surface.Data[i * 3] != short.MinValue
            : surface.Data[i] != short.MinValue;
    });

    // ── Phase 1b: 前缀和 → 顶点索引映射 ───────────────────────
    int[] indexMap = new int[total];
    int validCount = 0;
    for (int i = 0; i < total; i++)
        indexMap[i] = valid[i] ? validCount++ : -1;

    if (validCount == 0) return null;

    // ── Phase 1c: 并行填充顶点 + UV ────────────────────────────
    var vertices = new CxPoint3D[validCount];
    var uvs      = generateUVs ? new CxPoint2D[validCount] : null;
    float uDenom = W > 1 ? W - 1f : 1f;
    float vDenom = H > 1 ? H - 1f : 1f;

    Parallel.For(0, total, i =>
    {
        if (!valid[i]) return;
        int vi = indexMap[i];
        int col = i % W, row = i / W;
        if (isPointCloud)
        {
            vertices[vi] = new CxPoint3D(
                surface.XOffset + surface.Data[i*3    ] * surface.XScale,
                surface.YOffset + surface.Data[i*3 + 1] * surface.YScale,
                surface.ZOffset + surface.Data[i*3 + 2] * surface.ZScale);
        }
        else
        {
            vertices[vi] = new CxPoint3D(
                surface.XOffset + col * surface.XScale,
                surface.YOffset + row * surface.YScale,
                surface.ZOffset + surface.Data[i] * surface.ZScale);
        }
        if (generateUVs)
            uvs[vi] = new CxPoint2D(col / uDenom, row / vDenom);
    });

    // ── Phase 2: 四边形三角化（串行写，上界预分配）─────────────
    uint[] indices = new uint[(W - 1) * (H - 1) * 6];
    int triIdx = 0;
    for (int row = 0; row < H - 1; row++)
    for (int col = 0; col < W - 1; col++)
    {
        int v00 = indexMap[ row    * W + col    ];
        int v01 = indexMap[ row    * W + col + 1];
        int v10 = indexMap[(row+1) * W + col    ];
        int v11 = indexMap[(row+1) * W + col + 1];
        if (v00 < 0 || v01 < 0 || v10 < 0 || v11 < 0) continue;
        indices[triIdx++] = (uint)v00;
        indices[triIdx++] = (uint)v10;
        indices[triIdx++] = (uint)v11;
        indices[triIdx++] = (uint)v00;
        indices[triIdx++] = (uint)v11;
        indices[triIdx++] = (uint)v01;
    }

    // 压缩到实际长度
    if (triIdx < indices.Length)
    {
        uint[] compact = new uint[triIdx];
        Array.Copy(indices, compact, triIdx);
        indices = compact;
    }

    var mesh = new CxMesh { Vertices = vertices, Indices = indices };
    if (generateUVs)
    {
        mesh.UVs = uvs;
        mesh.TextureWidth  = W;
        mesh.TextureHeight = H;
    }
    return mesh;
}
```

---

## 关键约束与边界

| 情形 | 处理 |
|------|------|
| `validCount == 0` | 返回 `null` |
| `W == 1` 或 `H == 1`（单行/列） | Phase 2 循环不执行，返回纯顶点 mesh（无三角形） |
| `W == 1` 且 `H == 1` | 单顶点，`indices = new uint[0]` |
| UV 分母为 0（W 或 H == 1） | 分母取 1，UV = 0 |
| PointCloud data 长度校验 | 若 `Data.Length < total * 3` 则抛出 |

---

## 三角化绕序约定

```
v00 ── v01
 │  \    │
v10 ── v11

Triangle 1: (v00, v10, v11) — 逆时针，法线朝 +Z
Triangle 2: (v00, v11, v01)
```

---

## 验证方法

1. **正确性**：2×2 surface（均有效）→ 4 顶点，2 三角形，6 个 index；验证坐标与 offset/scale 计算一致
2. **空洞**：中心格无效的 3×3 surface → 边缘 8 个 quad，部分跳过；验证 index 中无 `-1` 残留
3. **UV**：`generateUVs=true`，角点 UV 应为 `(0,0)`, `(1,0)`, `(0,1)`, `(1,1)`
4. **PointCloud**：构造有序点云 surface，验证顶点坐标与原始 XYZ 一致
5. **往返验证**：`SurfaceToMesh` 后用 `CxProjectMesh` 投影回去，Z 差应在 zScale 量级
