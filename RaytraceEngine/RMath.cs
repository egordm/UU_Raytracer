﻿using System;
using System.Threading;
using OpenTK;

namespace RaytraceEngine
{
    public static class RMath
    {
        //source: my friend google
        public static float PI = 3.14159265359f;
        public static float roll0_sq = 0.81f;
        public static float R2D = 57.2957795f;
        
        // Sorry forgot the source
        public static class ThreadLocalRandom
        {
            private static readonly Random globalRandom = new Random();
            private static readonly object globalLock = new object();

            private static readonly ThreadLocal<Random> threadRandom = new ThreadLocal<Random>(NewRandom);

            public static Random NewRandom()
            {
                lock (globalLock)
                {
                    return new Random(globalRandom.Next());
                }
            }

            public static Random Instance => threadRandom.Value;

            public static int Next()
            {
                return Instance.Next();
            }
            
            public static int Next(int i, int j)
            {
                return Instance.Next(i, j);
            }
            
            public static double NextDouble()
            {
                return Instance.NextDouble();
            }
        }

        //source: the allmighty wikipedia
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            return (1 - t) * a + t * b;
        }
        public static float Lerp(float a, float b, float t)
        {
            return (1 - t) * a + t * b;
        }

        //source: https://www.scratchapixel.com/lessons/3d-basic-rendering/introduction-to-shading/reflection-refraction-fresnel
        public static Vector3 Reflect(Vector3 incidentVector, Vector3 N) 
        { 
            return incidentVector - 2 * Vector3.Dot(incidentVector, N) * N; 
        }
        //source: wikipedia
        public static Vector3 ToPolar(Vector3 cart)
        {
            float r = cart.Length;
            float o = (float)Math.Acos(cart.Z / r);
            float y = (float)Math.Atan2(cart.Y , cart.X);
            return new Vector3(r, o, y);
        }

        public static Vector3 RandomChange(Vector3 vec, float power)
        {
            Vector3 r = new Vector3((float)ThreadLocalRandom.Instance.NextDouble(),
                                    (float)ThreadLocalRandom.Instance.NextDouble(),
                                    (float)ThreadLocalRandom.Instance.NextDouble());
            return (vec + r * power).Normalized();
        }

        // Source http://asawicki.info/news_1301_reflect_and_refract_functions.html
        public static Vector3 Refract(Vector3 incidentVector, Vector3 N, float eta)
        {
            float N_dot_I = Vector3.Dot(N, incidentVector);
            float k = 1f - eta * eta * (1f - N_dot_I * N_dot_I);
            if (k < 0) return Vector3.Zero;
            return eta * incidentVector - (eta * N_dot_I + (float)Math.Sqrt(k)) * N;
        }

        public static Vector3 RndUnit()
        {
            return new Vector3( (float)ThreadLocalRandom.Instance.NextDouble() - 0.5f,
                                (float)ThreadLocalRandom.Instance.NextDouble() - 0.5f,
                                (float)ThreadLocalRandom.Instance.NextDouble() - 0.5f)
                                .Normalized();
        }
        //source of sampling on sphere, stratified by myself.
        //https://www.gamasutra.com/view/news/169816/Indepth_Generating_uniformly_distributed_points_on_a_sphere.php
        public static Vector3 RndUnitStratified(int steps, int stepZ, int stepT)
        {
            float z = (float)((ThreadLocalRandom.Instance.NextDouble() / steps) + (stepZ * (1f / steps))) * 2f - 1f;
            float t = (float)((ThreadLocalRandom.Instance.NextDouble() / steps) + (stepT * (1f / steps)) * Math.PI) * 2f;
            float r = (float)Math.Sqrt(1 - z*z);
            float x = r * (float)Math.Cos(t);
            float y = r * (float)Math.Sin(t);
            return new Vector3(x, y, z);
        }

        public static int ToIntColour(Vector3 c)
        {
            int i = (int)(Math.Min(255, c.X * 255)) << 16;
            i += (int)(Math.Min(255, c.Y * 255)) << 8;
            i += (int)(Math.Min(255, c.Z * 255));
            return i;
        }

        public static Vector3 ToFloatColour(int c)
        {
            float b = (byte)(c & 0x000000FF);
            float g = (byte)((c & 0x0000FF00) >> 8);
            float t = (byte)((c & 0x00FF0000) >> 16);
            return new Vector3(t / 255f, g / 255f, b / 255f);
        }

        public static float Clamp(float min, float max, float val)
        {
            if (val < min) val = min;
            if (val > max) val = max;
            return val;
        }

        public static string ToStr(Vector3 v)
        {
            return "" + v.X + " , " + v.Y + " , " + v.Z;
        }
        
        public static void Swap<T> (ref T lhs, ref T rhs) {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }
    }
}