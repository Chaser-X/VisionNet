using SharpGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxSurfaceAdvancedItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public CxSurface Surface { get; private set; }

        // OpenGL资源
        private uint vaoId = 0;
        private uint[] vboIds = new uint[2]; // 顶点、UV坐标
        private uint intensityTextureId = 0; // 亮度纹理
        private int textureWidth = 1; // 纹理宽
        private int textureHeight = 1; // 纹理高
        private uint elementBufferId = 0; // 索引缓冲区
        private uint shaderProgram = 0;

        private bool resourcesInitialized = false;
        private bool pointCloudUpdated = false;
        private List<uint> meshIndexs = new List<uint>();
        public bool IsDisposed { get; private set; } = false;
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        public Box3D? BoundingBox { get; private set; }

        // 最大顶点数量限制
        public int MaxPointCount { get; set; } = int.MaxValue;

        // 采样因子 - 根据MaxPointCount动态计算
        private int samplingFactorX = 1;
        private int samplingFactorY = 1;

        // 渲染模式
        private SurfaceMode surfaceMode;
        public SurfaceMode SurfaceMode
        {
            get { return surfaceMode; }
            set
            {
                //pointCloudUpdated = value != surfaceMode;
                surfaceMode = value;
            }
        }

        // 颜色模式
        private SurfaceColorMode surfaceColorMode;
        public SurfaceColorMode SurfaceColorMode
        {
            get { return surfaceColorMode; }
            set
            {
                //pointCloudUpdated = value != surfaceColorMode;
                surfaceColorMode = value;
            }
        }

        // 顶点数量
        private int vertexCount = 0;
        private int indexCount = 0;

        public CxSurfaceAdvancedItem(CxSurface surface, SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color, int maxPointCount = int.MaxValue)
        {
            this.Surface = surface;
            this.SurfaceMode = surfaceMode;
            this.SurfaceColorMode = surfaceColorMode;
            this.MaxPointCount = maxPointCount;

            // 计算采样因子
            CalculateSamplingFactors();

            BoundingBox = GetBoundingBox();
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
            pointCloudUpdated = true;
        }

        /// <summary>
        /// 计算采样因子
        /// </summary>
        private void CalculateSamplingFactors()
        {
            if (Surface == null || Surface.Width <= 0 || Surface.Length <= 0)
                return;

            int totalPoints = Surface.Width * Surface.Length;

            // 如果总点数小于最大点数限制，不需要采样
            if (totalPoints <= MaxPointCount)
            {
                samplingFactorX = 1;
                samplingFactorY = 1;
                return;
            }

            // 计算需要的采样率
            double samplingRate = Math.Sqrt((double)MaxPointCount / totalPoints);

            // 计算X和Y方向的采样因子
            samplingFactorX = Math.Max(1, (int)(1.0 / samplingRate));
            samplingFactorY = Math.Max(1, (int)(1.0 / samplingRate));

            // 确保采样后的点数不会超过最大限制
            while ((Surface.Width / samplingFactorX) * (Surface.Length / samplingFactorY) > MaxPointCount)
            {
                if (samplingFactorX <= samplingFactorY)
                    samplingFactorX++;
                else
                    samplingFactorY++;
            }

            // 确保计算的采样尺寸是正确的
            int sampledWidth = (Surface.Width + samplingFactorX - 1) / samplingFactorX;  // 向上取整
            int sampledLength = (Surface.Length + samplingFactorY - 1) / samplingFactorY;  // 向上取整

            // 再次验证总点数
            if (sampledWidth * sampledLength > MaxPointCount)
            {
                // 如果仍然超出，增加采样因子
                double ratio = Math.Sqrt((double)(sampledWidth * sampledLength) / MaxPointCount);
                samplingFactorX = Math.Max(1, (int)(samplingFactorX * ratio));
                samplingFactorY = Math.Max(1, (int)(samplingFactorY * ratio));
            }
        }
        public void Draw(OpenGL gl)
        {
            if (IsDisposed)
            {
                CleanupResources(gl);
                return;
            }

            if (Surface == null || Surface.Data.Length == 0) return;

            // 如果需要更新或初始化资源
            if (!resourcesInitialized || pointCloudUpdated)
            {
                // 如果已经初始化过，先清理旧资源
                if (resourcesInitialized)
                {
                    // 清理VAO和VBO资源
                    CleanupVAOVBO(gl);

                    // 释放亮度纹理
                    if (intensityTextureId != 0)
                    {
                        gl.DeleteTextures(1, new uint[] { intensityTextureId });
                        intensityTextureId = 0;
                    }

                    if (shaderProgram != 0)
                    {
                        gl.DeleteProgram(shaderProgram);
                        shaderProgram = 0;
                    }
                }

                // 初始化着色器
                InitializeShaders(gl);

                // 初始化VAO和VBO
                InitializeBuffers(gl);

                // 创建亮度纹理
                CreateIntensityTexture(gl);

                resourcesInitialized = true;
                pointCloudUpdated = false;
            }

            // 使用着色器程序
            gl.UseProgram(shaderProgram);
            // 绑定VAO
            gl.BindVertexArray(vaoId);
            var stat = gl.GetError(); // 清除错误状态
            Debug.WriteLine($"OpenGL Error:0 {stat}-{gl.GetErrorDescription(stat)}");
            if (stat != 0)
            {
                // 如果发生错误，清理资源并重新初始化
                CleanupVAOVBO(gl);
                // 初始化VAO和VBO
                InitializeBuffers(gl);
                gl.BindVertexArray(vaoId);
                stat = gl.GetError(); // 清除错误状态
                Debug.WriteLine($"OpenGL Error:1 {stat}-{gl.GetErrorDescription(stat)}");
            }
            // 设置投影和视图矩阵
            float[] projectionMatrix = new float[16]; // 假设您已经计算了投影矩阵
            gl.GetFloat(OpenGL.GL_PROJECTION_MATRIX, projectionMatrix); // 获取当前的投影矩阵
            float[] viewMatrix = new float[16];       // 假设您已经计算了视图矩阵
            gl.GetFloat(OpenGL.GL_MODELVIEW_MATRIX, viewMatrix); // 获取当前的模型视图矩阵
                                                                 // 获取uniform位置
            int viewLoc = gl.GetUniformLocation(shaderProgram, "view");
            int projectionLoc = gl.GetUniformLocation(shaderProgram, "projection");
            // 设置uniform值
            gl.UniformMatrix4(viewLoc, 1, false, viewMatrix);
            gl.UniformMatrix4(projectionLoc, 1, false, projectionMatrix);

            // 设置着色器统一变量
            int zMinLocation = gl.GetUniformLocation(shaderProgram, "zMin");
            gl.Uniform1(zMinLocation, ZMin);
            int zMaxLocation = gl.GetUniformLocation(shaderProgram, "zMax");
            gl.Uniform1(zMaxLocation, ZMax);
            int colorModeLocation = gl.GetUniformLocation(shaderProgram, "colorMode");
            gl.Uniform1(colorModeLocation, (int)SurfaceColorMode);

            int intensityTextureLocation = gl.GetUniformLocation(shaderProgram, "intensityTexture");
            gl.ActiveTexture(OpenGL.GL_TEXTURE0);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, intensityTextureId);
            gl.Uniform1(intensityTextureLocation, 0); // 绑定纹理单元0

            // 根据渲染模式绘制
            if (SurfaceMode == SurfaceMode.PointCloud)
            {
                gl.DrawArrays(OpenGL.GL_POINTS, 0, vertexCount);
            }
            else if (SurfaceMode == SurfaceMode.Mesh)
            {
                gl.DrawElements(OpenGL.GL_TRIANGLES, indexCount, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
            }

            // 解绑纹理
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            // 解绑VAO
            gl.BindVertexArray(0);
            // 停用着色器程序
            gl.UseProgram(0);
        }
        /// <summary>
        /// 初始化着色器
        /// </summary>
        private void InitializeShaders(OpenGL gl)
        {
            // 顶点着色器源码 - 使用普通字符串连接而非逐字字符串
            string vertexShaderSource =
                @"#version 330 core
                layout (location = 0) in vec3 aPos;
                layout (location = 1) in vec2 aTexCoord;

                uniform mat4 view;
                uniform mat4 projection;

                out float height;
                out vec2 TexCoord;

                void main()
                { 
                    gl_Position = projection * view * vec4(aPos, 1.0);
                    height = aPos.z;
                    TexCoord = aTexCoord;
                }";

            // 片段着色器源码
            string fragmentShaderSource =
                @"#version 330 core
                in float height;
                in vec2 TexCoord;
                out vec4 FragColor;

                uniform float zMin;
                uniform float zMax;
                uniform int colorMode; 
                uniform sampler2D intensityTexture;

                vec3 getColorByHeight(float height)
                {
                    float normalized = (height - zMin) / (zMax - zMin);
                    normalized = clamp(normalized, 0.0, 1.0);
                    if (normalized < 0.2) {
                        return mix(vec3(0, 0, 1), vec3(0, 1, 1), normalized * 5.0);
                    } else if (normalized < 0.4) {
                        return mix(vec3(0, 1, 1), vec3(0, 1, 0), (normalized - 0.2) * 5.0);
                    } else if (normalized < 0.6) {
                        return mix(vec3(0, 1, 0), vec3(1, 1, 0), (normalized - 0.4) * 5.0);
                    } else if (normalized < 0.8) {
                        return mix(vec3(1, 1, 0), vec3(1, 0, 0), (normalized - 0.6) * 5.0);
                    } else {
                        return mix(vec3(1, 0, 0), vec3(1, 0, 1), (normalized - 0.8) * 5.0);
                    }
                }
                
                void main()
                {
                    if (isinf(height)) {
                        discard;
                    }
                    float intensity = texture(intensityTexture, TexCoord).r;
                    if (colorMode == 0) { 
                        vec3 color = getColorByHeight(height);
                        FragColor = vec4(color, 1.0);
                    } else if(colorMode == 1){
                        FragColor = vec4(vec3(intensity), 1.0);
                    } else {
                        vec3 color = getColorByHeight(height);
                        FragColor = vec4(mix(vec3(intensity),color,0.5),1.0);
                    }
                }";

            // 编译顶点着色器
            uint vertexShader = gl.CreateShader(OpenGL.GL_VERTEX_SHADER);
            gl.ShaderSource(vertexShader, vertexShaderSource);
            gl.CompileShader(vertexShader);

            // 检查编译错误
            int[] status = new int[1];
            gl.GetShader(vertexShader, OpenGL.GL_COMPILE_STATUS, status);
            if (status[0] == OpenGL.GL_FALSE)
            {
                int[] logLength = new int[1];
                gl.GetShader(vertexShader, OpenGL.GL_INFO_LOG_LENGTH, logLength);
                StringBuilder log = new StringBuilder(logLength[0]);
                gl.GetShaderInfoLog(vertexShader, logLength[0], IntPtr.Zero, log);
                Debug.WriteLine("Vertex shader compilation error: " + log.ToString());
            }

            // 编译片段着色器
            uint fragmentShader = gl.CreateShader(OpenGL.GL_FRAGMENT_SHADER);
            gl.ShaderSource(fragmentShader, fragmentShaderSource);
            gl.CompileShader(fragmentShader);

            // 检查编译错误
            gl.GetShader(fragmentShader, OpenGL.GL_COMPILE_STATUS, status);
            if (status[0] == OpenGL.GL_FALSE)
            {
                int[] logLength = new int[1];
                gl.GetShader(fragmentShader, OpenGL.GL_INFO_LOG_LENGTH, logLength);
                StringBuilder log = new StringBuilder(logLength[0]);
                gl.GetShaderInfoLog(fragmentShader, logLength[0], IntPtr.Zero, log);
                Debug.WriteLine("Fragment shader compilation error: " + log.ToString());
            }

            // 创建着色器程序
            shaderProgram = gl.CreateProgram();
            gl.AttachShader(shaderProgram, vertexShader);
            gl.AttachShader(shaderProgram, fragmentShader);
            gl.LinkProgram(shaderProgram);

            // 检查链接错误
            gl.GetProgram(shaderProgram, OpenGL.GL_LINK_STATUS, status);
            if (status[0] == OpenGL.GL_FALSE)
            {
                int[] logLength = new int[1];
                gl.GetProgram(shaderProgram, OpenGL.GL_INFO_LOG_LENGTH, logLength);
                StringBuilder log = new StringBuilder(logLength[0]);
                gl.GetProgramInfoLog(shaderProgram, logLength[0], IntPtr.Zero, log);
                Debug.WriteLine("Shader program linking error: " + log.ToString());
            }

            // 删除着色器对象（已经链接到程序中）
            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);
        }
        /// <summary>
        /// 初始化VAO和VBO
        /// </summary>
        private void InitializeBuffers(OpenGL gl)
        {
            // 重新计算采样因子
            CalculateSamplingFactors();

            // 计算采样后的网格尺寸
            int sampledWidth = (Surface.Width + samplingFactorX - 1) / samplingFactorX;  // 向上取整
            int sampledLength = (Surface.Length + samplingFactorY - 1) / samplingFactorY;  // 向上取整
            int totalVertices = sampledWidth * sampledLength;
            vertexCount = totalVertices;

            // 生成顶点、UV坐标和亮度数据
            float[] vertices = new float[totalVertices * 3];
            float[] uvCoords = new float[totalVertices * 2];
            //float[] intensities = new float[totalVertices];

            int vertexIndex = 0;

            for (int y = 0; y < Surface.Length && vertexIndex < totalVertices; y += samplingFactorY)
            {
                for (int x = 0; x < Surface.Width && vertexIndex < totalVertices; x += samplingFactorX)
                {
                    int surfaceIndex = y * Surface.Width + x;

                    // 获取顶点坐标
                    float xPos, yPos, zPos;

                    if (Surface.Type == SurfaceType.Surface)
                    {
                        xPos = Surface.XOffset + x * Surface.XScale;
                        yPos = Surface.YOffset + y * Surface.YScale;
                        zPos = Surface.Data[surfaceIndex] == -32768 ?
                            float.NegativeInfinity :
                            Surface.ZOffset + Surface.Data[surfaceIndex] * Surface.ZScale;
                    }
                    else // PointCloud
                    {
                        xPos = Surface.Data[surfaceIndex * 3] == -32768 ?
                            float.NegativeInfinity :
                            Surface.XOffset + Surface.Data[surfaceIndex * 3] * Surface.XScale;

                        yPos = Surface.Data[surfaceIndex * 3 + 1] == -32768 ?
                            float.NegativeInfinity :
                            Surface.YOffset + Surface.Data[surfaceIndex * 3 + 1] * Surface.YScale;

                        zPos = Surface.Data[surfaceIndex * 3 + 2] == -32768 ?
                            float.NegativeInfinity :
                            Surface.ZOffset + Surface.Data[surfaceIndex * 3 + 2] * Surface.ZScale;
                    }

                    // 设置顶点坐标
                    vertices[vertexIndex * 3] = xPos;
                    vertices[vertexIndex * 3 + 1] = yPos;
                    vertices[vertexIndex * 3 + 2] = zPos;

                    // 设置UV坐标 - 使用原始坐标系的比例
                    uvCoords[vertexIndex * 2] = (float)x / (Surface.Width - 1);
                    uvCoords[vertexIndex * 2 + 1] = (float)y / (Surface.Length - 1);

                    // 设置亮度值
                    //float intensity = 1.0f;
                    //if (Surface.Intensity != null && Surface.Intensity.Length > surfaceIndex)
                    //{
                    //    intensity = (float)Surface.Intensity[surfaceIndex] / 255.0f;
                    //}
                    //intensities[vertexIndex] = intensity;

                    vertexIndex++;
                }
            }

            // 如果是网格模式，生成索引
            uint[] indices = null;
            //if (SurfaceMode == SurfaceMode.Mesh)
            //{
            meshIndexs = GenerateMeshIndexFromSampledPointCloud(sampledWidth, sampledLength);
            indices = meshIndexs.ToArray();
            indexCount = indices.Length;
            //}

            // 创建VAO
            uint[] vaoIds = new uint[1];
            gl.GenVertexArrays(1, vaoIds);
            vaoId = vaoIds[0];
            gl.BindVertexArray(vaoId);

            // 创建VBO
            gl.GenBuffers(2, vboIds);

            // 绑定顶点数据
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices, OpenGL.GL_STATIC_DRAW);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
            gl.EnableVertexAttribArray(0);

            // 绑定UV坐标
            gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, uvCoords, OpenGL.GL_STATIC_DRAW);
            gl.VertexAttribPointer(1, 2, OpenGL.GL_FLOAT, false, 2 * sizeof(float), IntPtr.Zero);
            gl.EnableVertexAttribArray(1);

            // 绑定亮度数据
            //gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[2]);
            //gl.BufferData(OpenGL.GL_ARRAY_BUFFER, intensities, OpenGL.GL_STATIC_DRAW);
            //gl.VertexAttribPointer(2, 1, OpenGL.GL_FLOAT, false, sizeof(float), IntPtr.Zero);
            //gl.EnableVertexAttribArray(2);

            // 如果是网格模式，绑定索引
            //if (SurfaceMode == SurfaceMode.Mesh && indices != null)
            //{
            uint[] elementBufferIds = new uint[1];
            gl.GenBuffers(1, elementBufferIds);
            elementBufferId = elementBufferIds[0];

            gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, elementBufferId);
            int sizeInBytes = indices.Length * sizeof(uint);
            IntPtr ptr = Marshal.AllocHGlobal(sizeInBytes);
            try
            {
                // 用Buffer.BlockCopy拷贝uint[]到byte[]
                byte[] indexBytes = new byte[sizeInBytes];
                Buffer.BlockCopy(indices, 0, indexBytes, 0, sizeInBytes);
                Marshal.Copy(indexBytes, 0, ptr, sizeInBytes);
                gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, sizeInBytes, ptr, OpenGL.GL_STATIC_DRAW);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            //}

            // 解绑VAO
            gl.BindVertexArray(0);
        }

        /// <summary>
        /// 初始化纹理
        /// </summary>
        /// <param name="gl"></param>
        private void CreateIntensityTexture(OpenGL gl)
        {
            // 查询最大纹理尺寸
            int[] maxSize = new int[1];
            gl.GetInteger(OpenGL.GL_MAX_TEXTURE_SIZE, maxSize);
            var maxTextureSize = maxSize[0];

            // 确定纹理尺寸，考虑硬件限制
            textureWidth = Math.Min(Surface.Width, maxTextureSize);
            textureHeight = Math.Min(Surface.Length, maxTextureSize);

            // 计算缩放因子
            var textureScaleX = (float)textureWidth / Surface.Width;
            var textureScaleY = (float)textureHeight / Surface.Length;

            // 创建亮度数据
            byte[] intensityData = new byte[textureWidth * textureHeight * 4];

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    // 计算对应的原始点云坐标
                    int srcX = (int)(x / textureScaleX);
                    int srcY = (int)(y / textureScaleY);

                    // 确保坐标在有效范围内
                    srcX = Math.Min(srcX, Surface.Width - 1);
                    srcY = Math.Min(srcY, Surface.Length - 1);

                    int srcIndex = srcY * Surface.Width + srcX;
                    int destIndex = y * textureWidth + x;

                    // 获取亮度值
                    byte intensity = 255; // 默认值
                    if (Surface.Intensity != null && Surface.Intensity.Length > srcIndex)
                    {
                        intensity = Surface.Intensity[srcIndex];
                    }

                    intensityData[destIndex * 4] = intensity;
                    intensityData[destIndex * 4 + 1] = intensity;
                    intensityData[destIndex * 4 + 2] = intensity;
                    intensityData[destIndex * 4 + 3] = intensity;
                }
            }

            // 创建纹理
            uint[] ids = new uint[1];
            gl.GenTextures(1, ids);
            intensityTextureId = ids[0];

            // 绑定纹理
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, intensityTextureId);

            // 设置纹理参数
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);

            // 上传纹理数据
            IntPtr ptr = Marshal.AllocHGlobal(intensityData.Length);
            try
            {
                Marshal.Copy(intensityData, 0, ptr, intensityData.Length);
                gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, textureWidth, textureHeight,
                        0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
                // 解绑纹理
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            }
        }

        /// <summary>
        /// 清理OpenGL资源
        /// </summary>
        private void CleanupResources(OpenGL gl)
        {
            IsDisposed = false; // 临时重置标记以执行清理

            // 清理VAO和VBO资源
            CleanupVAOVBO(gl);

            // 释放亮度纹理
            if (intensityTextureId != 0)
            {
                gl.DeleteTextures(1, new uint[] { intensityTextureId });
                intensityTextureId = 0;
            }

            if (shaderProgram != 0)
            {
                gl.DeleteProgram(shaderProgram);
                shaderProgram = 0;
            }

            resourcesInitialized = false;

            // 释放Surface资源
            if (Surface != null)
                Surface.Dispose();
            Surface = null;

            OnDisposed?.Invoke();
            IsDisposed = false;
            Debug.WriteLine("SurfaceAdvancedItem disposed");
        }
        /// <summary>
        /// 清理VAO和VBO资源
        /// </summary>
        /// <param name="gl"></param>
        private void CleanupVAOVBO(OpenGL gl)
        {
            if (vaoId != 0)
            {
                gl.DeleteVertexArrays(1, new uint[] { vaoId });
                vaoId = 0;
            }
            if (vboIds[0] != 0)
            {
                gl.DeleteBuffers(2, vboIds);
                vboIds[0] = 0;
                vboIds[1] = 0;
            }
            // 释放索引缓冲区
            if (elementBufferId != 0)
            {
                gl.DeleteBuffers(1, new uint[] { elementBufferId });
                elementBufferId = 0;
            }
        }

        /// <summary>
        /// 为采样后的点云生成网格索引
        /// </summary>
        private List<uint> GenerateMeshIndexFromSampledPointCloud(int sampledWidth, int sampledLength)
        {
            if (sampledWidth <= 1 || sampledLength <= 1)
                return new List<uint>();

            int totalTriangles = (sampledWidth - 1) * (sampledLength - 1) * 2;
            int totalIndices = totalTriangles * 3;

            List<uint> meshIndices = new List<uint>(totalIndices);
            object lockObj = new object();

            // 使用并行循环
            Parallel.For(0, sampledLength - 1, y =>
            {
                uint rowStart = (uint)y * (uint)sampledWidth;
                uint nextRowStart = (uint)(y + 1) * (uint)sampledWidth;

                List<uint> localIndices = new List<uint>();

                for (uint x = 0; x < sampledWidth - 1; x++)
                {
                    uint topLeft = rowStart + x;
                    uint topRight = topLeft + 1;
                    uint bottomLeft = nextRowStart + x;
                    uint bottomRight = bottomLeft + 1;

                    // 添加第一个三角形
                    localIndices.Add(topLeft);
                    localIndices.Add(bottomLeft);
                    localIndices.Add(topRight);

                    // 添加第二个三角形
                    localIndices.Add(topRight);
                    localIndices.Add(bottomLeft);
                    localIndices.Add(bottomRight);
                }

                // 合并结果
                lock (lockObj)
                {
                    meshIndices.AddRange(localIndices);
                }
            });

            return meshIndices;
        }

        /// <summary>
        /// 获取点云的边界
        /// </summary>
        private Box3D? GetBoundingBox()
        {
            if (Surface == null || Surface.Data.Length == 0) return null;

            // 计算点云的边界
            var data = Surface.ToPoints();

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var point in data)
            {
                if (float.IsInfinity(point.X) || float.IsInfinity(point.Y) || float.IsInfinity(point.Z))
                    continue;

                if (point.X < minX) minX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Z < minZ) minZ = point.Z;
                if (point.X > maxX) maxX = point.X;
                if (point.Y > maxY) maxY = point.Y;
                if (point.Z > maxZ) maxZ = point.Z;
            }

            // 计算中心点和尺寸
            var center = new CxPoint3D(
                (minX + maxX) / 2,
                (minY + maxY) / 2,
                (minZ + maxZ) / 2
            );

            var size = new CxSize3D(
                maxX - minX,
                maxY - minY,
                maxZ - minZ
            );

            return new Box3D(center, size);
        }
        public void Dispose()
        {
            IsDisposed = true; // 设置释放标记
        }
    }
}