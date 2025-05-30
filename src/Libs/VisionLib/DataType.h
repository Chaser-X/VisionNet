#pragma once

#ifndef DATATYPE_H
#define DATATYPE_H

#include <vector>

namespace VisionLib
{
    namespace DataType
    {
        struct Point3D
        {
            float X;
            float Y;
            float Z;
        };

        struct Point3DI
        {
            float X;
            float Y;
            float Z;
            float Intensity;
        };
    }
}

#endif