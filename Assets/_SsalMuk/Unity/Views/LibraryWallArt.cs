using System;
using UnityEngine;

namespace SsalMuk.Unity
{
    // All books and wooden shelf frames join the chunk mesh, with no per-book objects or physics.
    internal static class LibraryWallArt
    {
        private static readonly Color32[] Covers = {
            new Color32(137, 57, 51, 255), new Color32(59, 94, 100, 255),
            new Color32(108, 119, 69, 255), new Color32(157, 117, 60, 255),
            new Color32(93, 65, 104, 255), new Color32(69, 97, 72, 255)
        };
        internal static void Draw(long chunkX, Action<float, float, float, float, Color, float> quad)
        {
            foreach (int bottom in new[] { 0, 29 }) for (int cabinet = 0; cabinet < 8; cabinet++)
            {
                float x = cabinet * 4;
                Paint(x, bottom, 4, 3, new Color32(34, 26, 24, 255));
                Paint(x + .12f, bottom + .12f, 3.76f, 2.76f, new Color32(49, 33, 25, 255));
                for (int shelf = 0; shelf < 3; shelf++)
                {
                    float y = bottom + .16f + shelf * .9f;
                    for (int book = 0; book < 10; book++)
                    {
                        int pattern = (int)((chunkX % 97 + 97) * 13 + cabinet * 11 + shelf * 7 + book * 17 + bottom);
                        float h = .48f + (pattern % 4) * .065f, bx = x + .26f + book * .35f;
                        Paint(bx, y + .12f, .29f, h, Covers[pattern % Covers.Length]);
                        Paint(bx + .035f, y + .17f, .025f, h - .1f, new Color32(192, 161, 105, 255));
                        if (book % 3 == 0) Paint(bx + .04f, y + .27f, .21f, .045f, new Color32(192, 161, 105, 255));
                    }
                    Paint(x + .1f, y, 3.8f, .12f, new Color32(112, 77, 44, 255));
                    Paint(x + .1f, y + .09f, 3.8f, .035f, new Color32(158, 113, 65, 255));
                }
                Paint(x, bottom, .18f, 3, new Color32(93, 61, 37, 255));
                Paint(x + 3.82f, bottom, .18f, 3, new Color32(93, 61, 37, 255));
                Paint(x, bottom + 2.87f, 4, .13f, new Color32(147, 104, 60, 255));
                Paint(x, bottom, 4, .1f, new Color32(147, 104, 60, 255));
            }
            void Paint(float x, float y, float width, float height, Color color) => quad(x, y, width, height, color, .65f);
        }
    }
}
