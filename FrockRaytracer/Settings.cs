﻿namespace FrockRaytracer
{
    public static class Settings
    {
        // FXAA Settings
        public static bool FXAAShowEdges  = false;
        public static bool FXAAEnableFXAA = true;
        public static float FXAALumaThreashold = 0.5f;
        public static float FXAAMulReduce = 8.0f;
        public static float FXAAMinReduce = 128.0f;
        public static float FXAAMaxSpan = 8.0f;
        
        public static int RaytraceDebugRow = 256;
        public static int RaytraceDebugFrequency = 16;
        public static bool DrawDebugLine = true;
        
        public static int MaxDepth = 4;

        public static bool IsMultithread = true;
    }
}