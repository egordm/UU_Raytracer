﻿using OpenTK;

namespace FrockRaytracer.Structs
{
    public class CheckerboardTexture : Texture
    {
        public float DownScaleX, DownScaleY;

        public CheckerboardTexture(float downScale)
        {
            DownScaleX = DownScaleY = downScale;
        }

        public CheckerboardTexture(float downScaleX, float downScaleY)
        {
            DownScaleX = downScaleX;
            DownScaleY = downScaleY;
        }

        public Vector3 GetColor(Vector2 coords)
        {
            return (((int) (coords.X * DownScaleX) + (int) (coords.Y * DownScaleY)) & 1) == 0 ? Vector3.One : Vector3.Zero;
        }
    }
}