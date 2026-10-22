namespace Handline
{
    /// <summary>
    /// Maps a pixel in the upright, center-cropped, selfie-mirrored square the
    /// player sees back to a pixel in a WebCamTexture buffer.
    ///
    /// GetPixels32 stores rows bottom-up. Unity's videoRotationAngle is the
    /// clockwise rotation that makes the sensor image upright, and
    /// videoVerticallyMirrored means that upright image is stored upside down.
    /// The selfie mirror is applied in the upright image so screen-left is the
    /// player's left.
    /// </summary>
    public static class FrameOrient
    {
        public static void Map(
            int destX,
            int destY,
            int destSize,
            int srcWidth,
            int srcHeight,
            int rotationClockwise,
            bool verticallyMirrored,
            bool mirrorHorizontal,
            out int bufferX,
            out int bufferY)
        {
            bufferX = 0;
            bufferY = 0;
            if (destSize <= 0 || srcWidth <= 0 || srcHeight <= 0)
            {
                return;
            }

            int rot = ((rotationClockwise % 360) + 360) % 360;
            if (rot != 0 && rot != 90 && rot != 180 && rot != 270)
            {
                rot = 0;
            }

            int uprightW = (rot == 90 || rot == 270) ? srcHeight : srcWidth;
            int uprightH = (rot == 90 || rot == 270) ? srcWidth : srcHeight;
            int crop = uprightW < uprightH ? uprightW : uprightH;
            int cropX = (uprightW - crop) / 2;
            int cropY = (uprightH - crop) / 2;

            float u = (destX + 0.5f) / destSize;
            float v = (destY + 0.5f) / destSize;
            if (mirrorHorizontal)
            {
                u = 1f - u;
            }

            int localX = (int)(u * crop);
            int localY = (int)(v * crop);
            if (localX < 0) localX = 0;
            if (localY < 0) localY = 0;
            if (localX > crop - 1) localX = crop - 1;
            if (localY > crop - 1) localY = crop - 1;

            int ux = cropX + localX;
            int uy = cropY + localY;

            int sx;
            int sy;
            switch (rot)
            {
                case 90:
                    sx = uy;
                    sy = srcHeight - 1 - ux;
                    break;
                case 180:
                    sx = srcWidth - 1 - ux;
                    sy = srcHeight - 1 - uy;
                    break;
                case 270:
                    sx = srcWidth - 1 - uy;
                    sy = ux;
                    break;
                default:
                    sx = ux;
                    sy = uy;
                    break;
            }

            if (verticallyMirrored)
            {
                sy = srcHeight - 1 - sy;
            }

            if (sx < 0) sx = 0;
            if (sy < 0) sy = 0;
            if (sx > srcWidth - 1) sx = srcWidth - 1;
            if (sy > srcHeight - 1) sy = srcHeight - 1;

            bufferX = sx;
            bufferY = srcHeight - 1 - sy;
        }
    }
}
