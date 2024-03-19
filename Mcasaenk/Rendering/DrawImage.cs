using Mcasaenk.Shade3d;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mcasaenk.Rendering.GenerateTilePool;

namespace Mcasaenk.Rendering {
    public static class DrawImage {

        public static void FillPixels(Span<uint> pixels, GenData genData) {
            for(int i = 0; i < 512 * 512; i++) {
                pixels[i] = ColorMapping.Current.GetColor(genData.blockIds[i], genData.biomeIds[i]);
            }

            { // water
                double l = Settings.CONTRAST;
                double watercontrast = -50 * Math.Pow(l, 4) + -5 * l;
                int i = 0;
                for(int z = 0; z < 512; z++) {
                    for(int x = 0; x < 512; x++, i++) {
                        if(genData.terrainHeights[i] != genData.heights[i]) {
                            ushort waterbiome = Settings.WATERBIOMES ? genData.biomeIds[i] : default;
                            int waterDepth = genData.heights[i] - genData.terrainHeights[i];

                            if(Settings.WATERDEPTH) {
                                pixels[i] = Global.Blend(ColorMapping.Current.GetColor(ColorMapping.BLOCK_WATER, waterbiome), pixels[i], I(waterDepth, Math.Clamp(-(1/watercontrast), 0.4, 0.7), 4*watercontrast));
                            } else pixels[i] = ColorMapping.Current.GetColor(ColorMapping.BLOCK_WATER, waterbiome);

                            double intensity = I(waterDepth, 0.0, Math.Min(watercontrast, -1));
                            double multintensity = 1 - intensity;
                            pixels[i] = Global.MultShade(pixels[i], multintensity, multintensity, multintensity);
                        }
                    }
                }
            }


            if(Settings.STATIC_SHADE) {
                double q = 8 * (2 * Settings.CONTRAST);
                if(Settings.SHADE3D) q = q / 4;

                staticshade(pixels, genData.heights, ShadeConstants.GLB.cosA, ShadeConstants.GLB.sinA, q);
            }


            if(Settings.SHADE3D) {
                int i = 0;
                for(int z = 0; z < 512; z++) {
                    for(int x = 0; x < 512; x++, i++) {
                        if(genData.isShade[i]) {
                            double multcontr = 1 - Settings.CONTRAST;
                            int addcontr = (int)(-Settings.CONTRAST * 100);
                            pixels[i] = Global.Blend(Global.MultShade(pixels[i], multcontr, multcontr, multcontr), Global.AddShade(pixels[i], addcontr, addcontr, addcontr), 1);
                        }
                    }
                }
            }


            for(int i = 0; i < 512 * 512; i++) {
                double l = Settings.SUN_LIGHT;
                pixels[i] = Global.MultShade(pixels[i], l, l, l);
            }

        }

        private static double I(int x, double m = 0.3, double b = -2) {
            return m + (1 - Math.Pow(10.0, b * ((double)x / (319 + 64)))) * (1 - m);
        } 

        private static void staticshade(Span<uint> pixelBuffer, ManArray<short> heights, double cosA, double sinA, double q) {
            cosA = Math.Round(cosA, 2);
            sinA = Math.Round(sinA, 2);

            int index = 0;
            for(int z = 0; z < 512; z++) {
                for(int x = 0; x < 512; x++, index++) {
                    float xShade, zShade;

                    if(pixelBuffer[index] == 0) {
                        continue;
                    }

                    {
                        if(z == 0) {
                            zShade = (heights[index + 512]) - (heights[index]);
                        } else if(z == 512 - 1) {
                            zShade = (heights[index]) - (heights[index - 512]);
                        } else {
                            zShade = ((heights[index + 512]) - (heights[index - 512])) * 2;
                        }

                        if(x == 0) {
                            xShade = (heights[index + 1]) - (heights[index]);
                        } else if(x == 512 - 1) {
                            xShade = (heights[index]) - (heights[index - 1]);
                        } else {
                            xShade = ((heights[index + 1]) - (heights[index - 1])) * 2;
                        }

                        double shade = -(cosA * xShade + -sinA * zShade);
                        if(shade < -8) {
                            shade = -8;
                        }
                        if(shade > 8) {
                            shade = 8;
                        }

                        pixelBuffer[index] = Global.AddShade((uint)pixelBuffer[index], (int)(shade * q), (int)(shade * q), (int)(shade * q));
                    }


                }
            }
        }
    }
}
