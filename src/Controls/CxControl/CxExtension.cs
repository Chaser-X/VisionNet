using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisionNet.Controls
{
    public class CxExtension
    {
        /// <summary>
        /// 根据给定范围的Zmin,zmax,对每个点Z返回和Z相关的颜色
        /// </summary>
        public static (float r, float g, float b) GetColorByHeight(double z, double zMin, double zMax)
        {
            float range = (float)(zMax - zMin);
            if (z > zMax) z = zMax;
            if (z < zMin) z = zMin;
            float normalizedZ = (float)(z - zMin) / range;

            float r, g, b;
            if (normalizedZ < 1.0f / 7.0f)
            {
                // 深蓝
                r = 0.0f;
                g = 0.0f;
                b = 0.5f + normalizedZ * 3.5f;
            }
            else if (normalizedZ < 2.0f / 7.0f)
            {
                // 天空蓝
                r = 0.0f;
                g = (normalizedZ - 1.0f / 7.0f) * 7.0f;
                b = 1.0f;
            }
            else if (normalizedZ < 3.0f / 7.0f)
            {
                // 绿
                r = 0.0f;
                g = 1.0f;
                b = 1.0f - (normalizedZ - 2.0f / 7.0f) * 7.0f;
            }
            else if (normalizedZ < 4.0f / 7.0f)
            {
                // 黄
                r = (normalizedZ - 3.0f / 7.0f) * 7.0f;
                g = 1.0f;
                b = 0.0f;
            }
            else if (normalizedZ < 5.0f / 7.0f)
            {
                // 红
                r = 1.0f;
                g = 1.0f - (normalizedZ - 4.0f / 7.0f) * 7.0f;
                b = 0.0f;
            }
            else if (normalizedZ < 6.0f / 7.0f)
            {
                // 粉
                r = 1.0f;
                g = 0.0f;
                b = (normalizedZ - 5.0f / 7.0f) * 7.0f;
            }
            else
            {
                // 白
                r = 1.0f;
                g = (normalizedZ - 6.0f / 7.0f) * 7.0f;
                b = 1.0f;
            }
            return (r, g, b);
        }

    }
}
