using SharpGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VisionNet.DataType;

namespace VisionNet.Controls
{
    public class CxMeshAdvancedItem : ICxObjRenderItem
    {
        public event Action OnDisposed;
        public CxMesh Mesh { get; private set; }

        // OpenGL��Դ
        private uint vaoId = 0;
        private uint[] vboIds = new uint[2]; // ���㡢UV����
        private uint elementBufferId = 0; // ����������
        private uint intensityTextureId = 0; // ��������
        private int textureWidth = 1; // �����
        private int textureHeight = 1; // �����
        private uint shaderProgram = 0;

        private bool resourcesInitialized = false;
        private bool pointCloudUpdated = false;
        public bool IsDisposed { get; private set; } = false;
        public float ZMin { get; set; }
        public float ZMax { get; set; }
        public Box3D? BoundingBox { get; private set; }


        // ��Ⱦģʽ
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

        // ��ɫģʽ
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


        public CxMeshAdvancedItem(CxMesh mesh, SurfaceMode surfaceMode = SurfaceMode.PointCloud,
            SurfaceColorMode surfaceColorMode = SurfaceColorMode.Color)
        {
            this.Mesh = mesh;
            this.SurfaceMode = surfaceMode;
            this.SurfaceColorMode = surfaceColorMode;

            BoundingBox = GetBoundingBox();
            ZMax = (float)(BoundingBox?.Center.Z + BoundingBox?.Size.Depth / 2);
            ZMin = (float)(BoundingBox?.Center.Z - BoundingBox?.Size.Depth / 2);
            pointCloudUpdated = true;
        }

        public void Draw(OpenGL gl)
        {
            if (IsDisposed)
            {
                CleanupResources(gl);
                return;
            }

            if (Mesh == null || Mesh.Vertexs.Length == 0) return;

            // �����Ҫ���»��ʼ����Դ
            if (!resourcesInitialized || pointCloudUpdated)
            {
                // ����Ѿ���ʼ���������������Դ
                if (resourcesInitialized)
                {
                    // ����VAO��VBO��Դ
                    CleanupVAOVBO(gl);

                    // �ͷ���������
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

                // ��ʼ����ɫ��
                InitializeShaders(gl);

                // ��ʼ��VAO��VBO
                InitializeBuffers(gl);

                // ������������
                CreateIntensityTexture(gl);

                resourcesInitialized = true;
                pointCloudUpdated = false;
            }

            // ʹ����ɫ������
            gl.UseProgram(shaderProgram);
            // ��VAO
            gl.BindVertexArray(vaoId);
            var stat = gl.GetError(); // �������״̬
            Debug.WriteLine($"OpenGL Error:0 {stat}-{gl.GetErrorDescription(stat)}");
            if (stat != 0)
            {
                // �����������������Դ�����³�ʼ��
                CleanupVAOVBO(gl);
                // ��ʼ��VAO��VBO
                InitializeBuffers(gl);
                gl.BindVertexArray(vaoId);
                stat = gl.GetError(); // �������״̬
                Debug.WriteLine($"OpenGL Error:1 {stat}-{gl.GetErrorDescription(stat)}");
            }
            // ����ͶӰ����ͼ����
            float[] projectionMatrix = new float[16]; // �������Ѿ�������ͶӰ����
            gl.GetFloat(OpenGL.GL_PROJECTION_MATRIX, projectionMatrix); // ��ȡ��ǰ��ͶӰ����
            float[] viewMatrix = new float[16];       // �������Ѿ���������ͼ����
            gl.GetFloat(OpenGL.GL_MODELVIEW_MATRIX, viewMatrix); // ��ȡ��ǰ��ģ����ͼ����
                                                                 // ��ȡuniformλ��
            int viewLoc = gl.GetUniformLocation(shaderProgram, "view");
            int projectionLoc = gl.GetUniformLocation(shaderProgram, "projection");
            // ����uniformֵ
            gl.UniformMatrix4(viewLoc, 1, false, viewMatrix);
            gl.UniformMatrix4(projectionLoc, 1, false, projectionMatrix);

            // ������ɫ��ͳһ����
            int zMinLocation = gl.GetUniformLocation(shaderProgram, "zMin");
            gl.Uniform1(zMinLocation, ZMin);
            int zMaxLocation = gl.GetUniformLocation(shaderProgram, "zMax");
            gl.Uniform1(zMaxLocation, ZMax);
            int colorModeLocation = gl.GetUniformLocation(shaderProgram, "colorMode");
            gl.Uniform1(colorModeLocation, (int)SurfaceColorMode);

            int intensityTextureLocation = gl.GetUniformLocation(shaderProgram, "intensityTexture");
            gl.ActiveTexture(OpenGL.GL_TEXTURE0);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, intensityTextureId);
            gl.Uniform1(intensityTextureLocation, 0); // ������Ԫ0

            // ������Ⱦģʽ����
            if (SurfaceMode == SurfaceMode.PointCloud)
            {
                gl.DrawArrays(OpenGL.GL_POINTS, 0, Mesh.Vertexs.Length);
            }
            else if (SurfaceMode == SurfaceMode.Mesh)
            {
                gl.DrawElements(OpenGL.GL_TRIANGLES, Mesh.Indices.Length, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
            }

            // �������
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            // ���VAO
            gl.BindVertexArray(0);
            // ͣ����ɫ������
            gl.UseProgram(0);
        }
        /// <summary>
        /// ��ʼ����ɫ��
        /// </summary>
        private void InitializeShaders(OpenGL gl)
        {
            // ������ɫ��Դ�� - ʹ����ͨ�ַ������Ӷ��������ַ���
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

            // Ƭ����ɫ��Դ��
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

            // ���붥����ɫ��
            uint vertexShader = gl.CreateShader(OpenGL.GL_VERTEX_SHADER);
            gl.ShaderSource(vertexShader, vertexShaderSource);
            gl.CompileShader(vertexShader);

            // ���������
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

            // ����Ƭ����ɫ��
            uint fragmentShader = gl.CreateShader(OpenGL.GL_FRAGMENT_SHADER);
            gl.ShaderSource(fragmentShader, fragmentShaderSource);
            gl.CompileShader(fragmentShader);

            // ���������
            gl.GetShader(fragmentShader, OpenGL.GL_COMPILE_STATUS, status);
            if (status[0] == OpenGL.GL_FALSE)
            {
                int[] logLength = new int[1];
                gl.GetShader(fragmentShader, OpenGL.GL_INFO_LOG_LENGTH, logLength);
                StringBuilder log = new StringBuilder(logLength[0]);
                gl.GetShaderInfoLog(fragmentShader, logLength[0], IntPtr.Zero, log);
                Debug.WriteLine("Fragment shader compilation error: " + log.ToString());
            }

            // ������ɫ������
            shaderProgram = gl.CreateProgram();
            gl.AttachShader(shaderProgram, vertexShader);
            gl.AttachShader(shaderProgram, fragmentShader);
            gl.LinkProgram(shaderProgram);

            // ������Ӵ���
            gl.GetProgram(shaderProgram, OpenGL.GL_LINK_STATUS, status);
            if (status[0] == OpenGL.GL_FALSE)
            {
                int[] logLength = new int[1];
                gl.GetProgram(shaderProgram, OpenGL.GL_INFO_LOG_LENGTH, logLength);
                StringBuilder log = new StringBuilder(logLength[0]);
                gl.GetProgramInfoLog(shaderProgram, logLength[0], IntPtr.Zero, log);
                Debug.WriteLine("Shader program linking error: " + log.ToString());
            }

            // ɾ����ɫ�������Ѿ����ӵ������У�
            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);
        }
        /// <summary>
        /// ��ʼ��VAO��VBO
        /// </summary>
        private void InitializeBuffers(OpenGL gl)
        {
            // ���ɶ��㡢UV�������������
            //��ȡMesh.Vertexs��IntPtr
            var sizePoint3d = Marshal.SizeOf(typeof(CxPoint3D));
            GCHandle handle = GCHandle.Alloc(Mesh.Vertexs, GCHandleType.Pinned);
            var verticesPtr = handle.AddrOfPinnedObject();
            //��ȡMesh.UVs��IntPtr
            var sizePoint2d = Marshal.SizeOf(typeof(CxPoint2D));
            GCHandle uvHandle = GCHandle.Alloc(Mesh.UVs, GCHandleType.Pinned);
            var uvCoordsPtr = uvHandle.AddrOfPinnedObject();
            // ��ʼ����������
            GCHandle indicesHandle = GCHandle.Alloc(Mesh.Indices, GCHandleType.Pinned);
            var indicesPtr = indicesHandle.AddrOfPinnedObject();
            try
            {
                // ����VAO
                uint[] vaoIds = new uint[1];
                gl.GenVertexArrays(1, vaoIds);
                vaoId = vaoIds[0];
                gl.BindVertexArray(vaoId);
                // ����VBO
                gl.GenBuffers(2, vboIds);
                // �󶨶�������
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[0]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, Mesh.Vertexs.Length * sizePoint3d, verticesPtr, OpenGL.GL_STATIC_DRAW);
                gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 3 * sizeof(float), IntPtr.Zero);
                gl.EnableVertexAttribArray(0);
                // ��UV����
                gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, vboIds[1]);
                gl.BufferData(OpenGL.GL_ARRAY_BUFFER, Mesh.UVs.Length * sizePoint2d, uvCoordsPtr, OpenGL.GL_STATIC_DRAW);
                gl.VertexAttribPointer(1, 2, OpenGL.GL_FLOAT, false, 2 * sizeof(float), IntPtr.Zero);
                gl.EnableVertexAttribArray(1);

                // ����EBO
                uint[] elementBufferIds = new uint[1];
                gl.GenBuffers(1, elementBufferIds);
                elementBufferId = elementBufferIds[0];

                gl.BindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, elementBufferId);
                //int sizeInBytes = ;
                //IntPtr ptr = Marshal.AllocHGlobal(sizeInBytes);
                // ��Buffer.BlockCopy����uint[]��byte[]
                //byte[] indexBytes = new byte[sizeInBytes];
                //Buffer.BlockCopy(indices, 0, indexBytes, 0, sizeInBytes);
                //Marshal.Copy(indexBytes, 0, ptr, sizeInBytes);
                gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, Mesh.Indices.Length * sizeof(uint), indicesPtr, OpenGL.GL_STATIC_DRAW);
            }
            finally
            {
                indicesHandle.Free();
                handle.Free();
                uvHandle.Free();
                // ���VAO
                gl.BindVertexArray(0);
            }
        }

        /// <summary>
        /// ��ʼ������
        /// </summary>
        /// <param name="gl"></param>
        private void CreateIntensityTexture(OpenGL gl)
        {
            // ��ѯ�������ߴ�
            int[] maxSize = new int[1];
            gl.GetInteger(OpenGL.GL_MAX_TEXTURE_SIZE, maxSize);
            var maxTextureSize = maxSize[0];

            // ȷ������ߴ磬����Ӳ������
            textureWidth = Math.Min(Mesh.TextureWidth, maxTextureSize);
            textureHeight = Math.Min(Mesh.TextureHeight, maxTextureSize);

            // ������������
            var textureScaleX = (float)textureWidth / Mesh.TextureWidth;
            var textureScaleY = (float)textureHeight / Mesh.TextureHeight;

            // ������������
            byte[] intensityData = new byte[textureWidth * textureHeight * 4];

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    // �����Ӧ��ԭʼ��������
                    int srcX = (int)(x / textureScaleX);
                    int srcY = (int)(y / textureScaleY);

                    // ȷ����������Ч��Χ��
                    srcX = Math.Min(srcX, Mesh.TextureWidth - 1);
                    srcY = Math.Min(srcY, Mesh.TextureHeight - 1);

                    int srcIndex = srcY * Mesh.TextureWidth + srcX;
                    int destIndex = y * textureWidth + x;

                    // ��ȡ����ֵ
                    byte intensity = 255; // Ĭ��ֵ
                    if (Mesh.Intensity != null && Mesh.Intensity.Length > srcIndex)
                    {
                        intensity = Mesh.Intensity[srcIndex];
                    }

                    intensityData[destIndex * 4] = intensity;
                    intensityData[destIndex * 4 + 1] = intensity;
                    intensityData[destIndex * 4 + 2] = intensity;
                    intensityData[destIndex * 4 + 3] = intensity;
                }
            }

            // ��������
            uint[] ids = new uint[1];
            gl.GenTextures(1, ids);
            intensityTextureId = ids[0];
            // ������
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, intensityTextureId);

            // �����������
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_LINEAR);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);

            // �ϴ���������
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
                // �������
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            }
        }

        /// <summary>
        /// ����OpenGL��Դ
        /// </summary>
        private void CleanupResources(OpenGL gl)
        {
            IsDisposed = false; // ��ʱ���ñ����ִ������

            // ����VAO��VBO��Դ
            CleanupVAOVBO(gl);

            // �ͷ���������
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

            Mesh = null;

            OnDisposed?.Invoke();
            IsDisposed = false;
            Debug.WriteLine("SurfaceAdvancedItem disposed");
        }
        /// <summary>
        /// ����VAO��VBO��Դ
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
            // �ͷ�����������
            if (elementBufferId != 0)
            {
                gl.DeleteBuffers(1, new uint[] { elementBufferId });
                elementBufferId = 0;
            }
        }

        /// <summary>
        /// ��ȡ���Ƶı߽�
        /// </summary>
        private Box3D? GetBoundingBox()
        {
            if (Mesh == null || Mesh.Vertexs.Length == 0) return null;

            // ������Ƶı߽�
            var data = Mesh.Vertexs;

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

            // �������ĵ�ͳߴ�
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
            IsDisposed = true; // �����ͷű��
        }
    }
}