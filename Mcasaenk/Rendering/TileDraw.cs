using Accessibility;
using CommunityToolkit.HighPerformance;
using Mcasaenk.Colormaping;
using Mcasaenk.Shade3d;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Mcasaenk.Rendering {
    public static class TileDraw {

        public static long drawTime = 0, drawCount = 1;
        public unsafe static void FillPixels(uint* pixels, Colormap colormap, IGenData genData, IGenData[,] neighbours) {
            var st = Stopwatch.StartNew();
            //uint[] tintcolors = very_temp.Rent(512 * 512);

            foreach(var tint in colormap.GetTints()) {
                if(tint.Settings() == null) continue;

                int radius = (tint.Settings().Blend - 1) / 2;
                switch(tint.GetBlendMode()) {
                    case Tint.Blending.none:
                    case Tint.Blending.heightonly:
                        break;
                    case Tint.Blending.biomeonly:
                        //GausBlur.BoxBlur<PrecBlur>(radius, pixels, tint, colormap, PrecBlur.pool, neighbours);
                        //PrecGausBlur.BoxBlur(radius, pixels, tint, colormap, neighbours);
                        ColorGausBlur.BoxBlur(radius, pixels, tint, colormap, neighbours);
                        //GausBlur.BoxBlur<ColorBlur>(radius, pixels, tint, colormap, ColorBlur.pool, neighbours);
                        break;
                    case Tint.Blending.full:
                        //GausBlur.BoxBlur<PrecBlur>(radius, pixels, tint, colormap, PrecBlur.pool, neighbours);
                        PrecGausBlur.BoxBlur(radius, pixels, tint, colormap, neighbours);
                        //GausBlur.BoxBlur<ColorBlur>(radius, pixels, tint, colormap, ColorBlur.pool, neighbours);
                        //ColorGausBlur.BoxBlur(radius, pixels, tint, colormap, neighbours);
                        break;
                }
            }

            for(int i = 0; i < 512 * 512; i++) {
                var blockid = genData.block(i);
                var block = colormap.Value(blockid);

                if(block.tint.GetBlendMode() == Tint.Blending.none) {
                    pixels[i] = block.GetColor(Global.Settings.DEFBIOME, Colormap.DEFHEIGHT);
                } else if(block.tint.GetBlendMode() == Tint.Blending.heightonly) {
                    pixels[i] = block.GetColor(genData.biomeIds(i), genData.heights(i));
                }
            }

            double[] fd = ArrayPool<double>.Shared.Rent(512 * 512);
            Array.Fill<double>(fd, 1);
            if(Global.App.Settings.SHADETYPE == ShadeType.OG) {

                int maxdepth = Global.App.Settings.MAXABSHEIGHT;
                double transparency = 50 * Global.Settings.WATER_TRANSPARENCY + Math.Pow(Global.App.Settings.WATER_TRANSPARENCY, 10) * (maxdepth - 50);
                double transparency2 = Math.Pow(Global.App.Settings.WATER_TRANSPARENCY, 0.1);
                
                double contrast = 0.70 * Math.Pow(Global.App.Settings.CONTRAST, 10) + 0.17 * Math.Pow(Global.App.Settings.CONTRAST, 2) + 0.08 * Global.App.Settings.CONTRAST;

                for(int i = 0; i < 512 * 512; i++) {
                    var block = genData.block(i);

                    if(Global.App.Settings.WATERDEPTH) {
                        if(block == colormap.depth) {
                            uint terrainColor = colormap.Value(genData.terrainBlock(i)).GetColor(genData.biomeIds(i), genData.terrainHeights(i));
                            int waterDepth = genData.heights(i) - genData.terrainHeights(i);

                            double ratio = Math.Pow(2, -4 * (1 - transparency2) * (waterDepth + 3));
                            if(Global.App.Settings.WATER_SMART_SHADE) fd[i] = ratio;
                            pixels[i] = Global.Blend(terrainColor, pixels[i], ratio);

                            double multintensity = Math.Pow(10.0, -5 * contrast * Math.Min(waterDepth / transparency, 1));
                            pixels[i] = Global.MultShade(pixels[i], multintensity, multintensity, multintensity);
                        }
                    }
                }
            }

            if(Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.STATIC_SHADE) {
                double q = 12 * Global.App.Settings.CONTRAST;
                if(Global.App.Settings.SHADE3D) q = q / 2;

                embossshade(new Span<uint>(pixels, 512 * 512), genData, fd, ShadeConstants.GLB.cosA, ShadeConstants.GLB.sinA, q);
            } else if(Global.App.Settings.SHADETYPE == ShadeType.jmap) {
                mapshade(new Span<uint>(pixels, 512 * 512), colormap, genData, neighbours);
            }


            for(int i = 0; i < 512 * 512; i++) {
                double sh = 0;

                sh += Global.App.Settings.SUN_LIGHT;
                if(genData.isShade(i)) {
                    double max = (Global.App.Settings.CONTRAST * 150);
                    var c = pixels[i].ToColor();

                    sh = Math.Clamp(sh * Math.Max((1 - (Global.App.Settings.CONTRAST * fd[i])), ((c.R + c.G + c.B) / 3 - max) / 256), 0, 15);

                    //double multcontr = 1 - (Global.App.Settings.CONTRAST * fd[i]);
                    //int addcontr = (int)(-Settings.CONTRAST * 100);


                    //if(c.a < 255) pixels[i] = 0;
                    //else pixels[i] = Global.ToARGBInt((byte)Math.Max(c.r * multcontr, c.r - max), (byte)Math.Max(c.g * multcontr, c.g - max), (byte)Math.Max(c.b * multcontr, c.b - max));
                }
                // option 1:
                sh = Math.Clamp(sh + Global.Settings.BLOCK_LIGHT / 15d * genData.blockLights(i) * Global.Settings.WaterTransparency * 2, 0, 15);
                //sh = Math.Clamp(sh + Math.Clamp(genData.blockLights(i) - 15 + Global.Settings.BLOCK_LIGHT * fd[i], 0, 15), 0, 15);
                // option 2:
                //sh = Math.Clamp(sh + Math.Clamp(genData.blockLights(i) * fd[i], 0, Global.Settings.BLOCK_LIGHT), 0, 15);

                sh = sh / 15;
                pixels[i] = Global.MultShade(pixels[i], sh, sh, sh);
            }


            if(Global.Settings.USEMAPPALETTE) {
                for(int i = 0; i < 512 * 512; i++) {
                    pixels[i] = JavaMapColors.Nearest(JavaMapColors.derivatives, pixels[i], Global.App.OpenedSave.levelDatInfo.version_id).uintcolor;
                }
            }


            ArrayPool<double>.Shared.Return(fd);
            st.Stop();
            drawTime += st.ElapsedMilliseconds;
            drawCount++;
        }

        private static double I(int x, double m = 0.3, double b = -2) {
            return m + (1 - Math.Pow(10.0, b * ((double)x / (319 + 64)))) * (1 - m); //!!!
        }

        private static void embossshade(Span<uint> pixelBuffer, IGenData gdata, Span<double> fd, double cosA, double sinA, double q) {
            int index = 0;
            for(int z = 0; z < 512; z++) {
                for(int x = 0; x < 512; x++, index++) {
                    float xShade, zShade;

                    if(pixelBuffer[index] == 0) {
                        continue;
                    }

                    {
                        if(z == 0) {
                            zShade = (gdata.terrainHeights(index + 512)) - (gdata.terrainHeights(index));
                        } else if(z == 512 - 1) {
                            zShade = (gdata.terrainHeights(index)) - (gdata.terrainHeights(index - 512));
                        } else {
                            zShade = ((gdata.terrainHeights(index + 512)) - (gdata.terrainHeights(index - 512))) * 2;
                        }

                        if(x == 0) {
                            xShade = (gdata.terrainHeights(index + 1)) - (gdata.terrainHeights(index));
                        } else if(x == 512 - 1) {
                            xShade = (gdata.terrainHeights(index)) - (gdata.terrainHeights(index - 1));
                        } else {
                            xShade = ((gdata.terrainHeights(index + 1)) - (gdata.terrainHeights(index - 1))) * 2;
                        }

                        double shade = -(cosA * xShade + -sinA * zShade);
                        if(shade < -8) {
                            shade = -8;
                        }
                        if(shade > 8) {
                            shade = 8;
                        }

                        int fq = (int)(shade * q * fd[index]);

                        pixelBuffer[index] = Global.AddShade(pixelBuffer[index], fq, fq, fq);
                    }
                }
            }
        }
        private static void mapshade(Span<uint> pixelBuffer, Colormap colormap, IGenData gdata, IGenData[,] neighbours) {
            double normal = 1, dark = 180 / 220d, darker = 135 / 220d, light = 255 / 220d;
            dark += (1 - dark) * (0.5 - Global.Settings.CONTRAST) * 2;
            darker += (1 - darker) * (0.5 - Global.Settings.CONTRAST) * 2;
            light -= (light - 1) * (0.5 - Global.Settings.CONTRAST) * 2;

            Point2i p = Global.Settings.Jmap_MAP_DIRECTION switch {
                Direction.North => new Point2i(0, -1),
                Direction.South => new Point2i(0, 1),
                Direction.East => new Point2i(1, 0),
                Direction.West => new Point2i(-1, 0),
            };

            Func<IGenData, int, short> h = Global.Settings.Jmap_WATER_MODE == JsmapWaterMode.vanilla ? (gendata, i) => gendata.heights(i)
            : (gendata, i) => gendata.terrainHeights(i);

            for(int z = 0; z < 512; z++) {
                for(int x = 0; x < 512; x++) {
                    int i = z * 512 + x;

                    int heightAtComp = h(gdata, i);
                    if(z + p.Z < 0 || z + p.Z >= 512 || x + p.X < 0 || x + p.X >= 512) {
                        if(neighbours[p.X + 1, p.Z + 1] != null) {
                            heightAtComp = h(neighbours[p.X + 1, p.Z + 1], Global.Settings.Jmap_MAP_DIRECTION switch {
                                Direction.North => 511 * 512 + x,
                                Direction.South => 0 * 512 + x,
                                Direction.East => z * 512 + 0,
                                Direction.West => z * 512 + 511,
                            });
                        }
                    } else {
                        heightAtComp = h(gdata, i + p.Z * 512 + p.X);
                    }


                    if(gdata.depth(i)) {
                        if(Global.Settings.Jmap_WATER_MODE == JsmapWaterMode.vanilla) {
                            int hd = gdata.heights(i) - gdata.terrainHeights(i);
                            double q = Global.Settings.Jmap_REVEALED_WATER;

                            if(hd > 9 * q) {
                                pixelBuffer[i] = Global.MultShade(pixelBuffer[i], dark);
                            } else if(hd <= 2 * q) {
                                pixelBuffer[i] = Global.MultShade(pixelBuffer[i], light);
                            } else if(hd <= 4 * q) {
                                if(x % 2 == z % 2) pixelBuffer[i] = Global.MultShade(pixelBuffer[i], light);
                                else pixelBuffer[i] = Global.MultShade(pixelBuffer[i], normal);
                            } else if(hd <= 6 * q) {
                                pixelBuffer[i] = Global.MultShade(pixelBuffer[i], normal);
                            } else if(hd <= 9 * q) {
                                if(x % 2 == z % 2) pixelBuffer[i] = Global.MultShade(pixelBuffer[i], normal);
                                else pixelBuffer[i] = Global.MultShade(pixelBuffer[i], dark);
                            }
                        } else if(Global.Settings.Jmap_WATER_MODE == JsmapWaterMode.translucient) {
                            uint terrainColor = colormap.Value(gdata.terrainBlock(i)).GetColor(gdata.biomeIds(i), gdata.terrainHeights(i));

                            if(h(gdata, i) < heightAtComp) terrainColor = Global.MultShade(terrainColor, dark);
                            else if(h(gdata, i) > heightAtComp) terrainColor = Global.MultShade(terrainColor, light);

                            pixelBuffer[i] = Global.Blend(terrainColor, pixelBuffer[i], Global.Settings.WATER_TRANSPARENCY);
                        }
                    } else {
                        if(h(gdata, i) < heightAtComp) pixelBuffer[i] = Global.MultShade(pixelBuffer[i], dark);
                        else if(h(gdata, i) > heightAtComp) pixelBuffer[i] = Global.MultShade(pixelBuffer[i], light);
                    }


                }
            }
        }

    }

    interface Blur<T> where T : Blur<T> {
        void IncreaseNewBlock(BlockValue gb, IGenData gen, int ri, DynBiome dynbiomes);
        void SetNewBlock(BlockValue gb, IGenData gen, int ri, DynBiome dynbiomes);
        void CopyFrom(T blur);
        void Subtract(T blur);
        void Plus(T blur);
        uint GenerateColor(BlockValue gb, IGenData gen, int i, DynBiome dynbiomes);
    }

    struct ColorBlur : Blur<ColorBlur> {
        public static ArrayPool<ColorBlur> pool = ArrayPool<ColorBlur>.Create((512 * 3) * (512 * 3), 8);

        public int r, g, b, br;

        public void CopyFrom(ColorBlur blur) {
            r = blur.r; g = blur.g; b = blur.b; br = blur.br;
        }
        public void Plus(ColorBlur blur) {
            r += blur.r;
            g += blur.g;
            b += blur.b;
            br += blur.br;
        }
        public void Subtract(ColorBlur blur) {
            r -= blur.r;
            g -= blur.g;
            b -= blur.b;
            br -= blur.br;
        }

        public void IncreaseNewBlock(BlockValue gb, IGenData gen, int ri, DynBiome dynbiomes) {
            var f = gb.tint.TintColorFor(gen.biomeIds(ri), gen.heights(ri)).ToColor();
            r += f.R;
            g += f.G;
            b += f.B;
            br++;
        }
        public void SetNewBlock(BlockValue gb, IGenData gen, int ri, DynBiome dynbiomes) {
            var f = gb.tint.TintColorFor(gen.biomeIds(ri), gen.heights(ri)).ToColor();
            r += f.R;
            g += f.G;
            b += f.B;
            br = 1;
        }

        public uint GenerateColor(BlockValue gb, IGenData gen, int i, DynBiome dynbiomes) {
            if(br == 0) return 0xFFFF0000;
            return Global.ColorMult(gb.color, WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)).ToUInt());
        }
    }
    unsafe struct PrecBlur : Blur<PrecBlur> {
        public static ArrayPool<PrecBlur> pool = ArrayPool<PrecBlur>.Create((512 * 3) * (512 * 3), 8);
        public const int MB = 15;

        public fixed ushort biome[MB];

        public void CopyFrom(PrecBlur blur) {
            if(blur.biome == null) return;
            for(int i = 0; i < MB; i++) biome[i] = blur.biome[i];
        }
        public void Plus(PrecBlur blur) {
            if(blur.biome == null) return;
            for(int i = 0; i < MB; i++) biome[i] += blur.biome[i];
        }
        public void Subtract(PrecBlur blur) {
            if(blur.biome == null) return;
            for(int i = 0; i < MB; i++) biome[i] -= blur.biome[i];
        }


        public void IncreaseNewBlock(BlockValue gb, IGenData gen, int ri, DynBiome dynbiomes) {
            int i = dynbiomes.get(gen.biomeIds(ri));
            biome[i]++;
        }
        public void SetNewBlock(BlockValue gb, IGenData gen, int ri, DynBiome dynbiomes) {
            int i = dynbiomes.get(gen.biomeIds(ri));
            biome[i] = 1;
        }



        public uint GenerateColor(BlockValue gb, IGenData gen, int ri, DynBiome dynbiomes) {
            int r = 0, g = 0, b = 0, br = 0;
            for(int i = 0; i < dynbiomes.max(); i++) {
                var c = gb.tint.TintColorFor(dynbiomes.back(i), gen.heights(ri)).ToColor();
                r += c.R * biome[i];
                g += c.G * biome[i];
                b += c.B * biome[i];
                br += biome[i];
            }
            if(br == 0) return 0xFFFF0000;
            return Global.ColorMult(gb.color, WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)).ToUInt());
        }
    }

    class DynBiome {
        ConcurrentDictionary<ushort, int> dynamicbiome = new ConcurrentDictionary<ushort, int>();
        ConcurrentDictionary<int, ushort> dynamicbiomeback = new ConcurrentDictionary<int, ushort>();

        readonly int MB;
        public DynBiome(int MB) {
            this.MB = MB;
        }

        volatile int c = 0;
        public int get(ushort biome) {
            return dynamicbiome.GetOrAdd(biome, key => {
                int value = Interlocked.Increment(ref c) - 1;
                if(value >= MB) {
                    Interlocked.Decrement(ref c);
                    return MB - 1;
                }
                dynamicbiomeback.TryAdd(value, key);
                return value;
            });
        }

        public int max() => c;

        public ushort back(int i) => dynamicbiomeback[i];
    }

    unsafe static class GausBlur {
        public static void BoxBlur<T>(int R, uint* pixels, GridTint tint, Colormap colormap, ArrayPool<T> pool, IGenData[,] neighbours) where T : struct, Blur<T> {
            DynBiome dynbiomes = null;
            if(typeof(T) == typeof(PrecBlur)) {
                dynbiomes = new DynBiome(PrecBlur.MB);
            }

            var xdata2 = pool.Rent((512 + 2 * R) * (512 + 2 * R));

            int STRIDE = 512 + 2 * R;

            var genData = neighbours[1, 1];

            Parallel.For(-R, 512 + R, new ParallelOptions() { MaxDegreeOfParallelism = 1 }, z => {
                Span<T> cx2 = MemoryMarshal.Cast<byte, T>(stackalloc byte[Unsafe.SizeOf<T>() * (512 + R + R)]);
                T acc = new T();

                for(int r = -R; r <= R; r++) {
                    (int q, int rem) rx = Math.DivRem(512 + r, 512), rz = Math.DivRem(512 + z, 512);
                    int ri = rz.rem * 512 + rx.rem;
                    var gen = neighbours[rx.q, rz.q];

                    if(gen != null) {
                        if(colormap.Value(gen.block(ri)) is BlockValue gb) {
                            if(gb.tint == tint) {
                                acc.IncreaseNewBlock(gb, gen, ri, dynbiomes);
                                cx2[R + r].SetNewBlock(gb, gen, ri, dynbiomes);
                            }
                        }
                    }
                }

                xdata2[(R + z) * STRIDE + R].CopyFrom(acc);

                for(int x = 1; x < 512; x++) {
                    // old
                    acc.Subtract(cx2[R + x - 1 - R]);

                    // new
                    (int q, int rem) rx = Math.DivRem(512 + x + R, 512), rz = Math.DivRem(512 + z, 512);
                    int ri = rz.rem * 512 + rx.rem;
                    var gen = neighbours[rx.q, rz.q];

                    if(gen != null) {
                        if(colormap.Value(gen.block(ri)) is BlockValue gb) {
                            if(gb.tint == tint) {
                                acc.IncreaseNewBlock(gb, gen, ri, dynbiomes);
                                cx2[R + x + R].SetNewBlock(gb, gen, ri, dynbiomes);
                            }
                        }
                    }

                    xdata2[(R + z) * STRIDE + (R + x)].CopyFrom(acc);
                }
            });


            Parallel.For(0, 512, new ParallelOptions() { MaxDegreeOfParallelism = 1 }, x => {
                Span<T> cx2 = MemoryMarshal.Cast<byte, T>(stackalloc byte[Unsafe.SizeOf<T>() * (512 + R + R)]);
                T acc = new T();

                for(int r = -R; r <= R; r++) {
                    cx2[R + r].CopyFrom(xdata2[(r + R) * STRIDE + (x + R)]);
                    acc.Plus(cx2[R + r]);
                }

                if(colormap.Value(genData.block(0 * 512 + x)) is BlockValue bl) {
                    if(bl.tint == tint) {
                        pixels[0 * 512 + x] = acc.GenerateColor(bl, genData, 0 * 512 + x, dynbiomes);
                    }
                }

                for(int z = 1; z < 512; z++) {
                    // old
                    acc.Subtract(cx2[z - 1]);

                    // new
                    cx2[R + z + R].CopyFrom(xdata2[(z + R + R) * STRIDE + (x + R)]);
                    acc.Plus(cx2[R + z + R]);

                    if(colormap.Value(genData.block(z * 512 + x)) is BlockValue bll) {
                        if(bll.tint == tint) {
                            pixels[z * 512 + x] = acc.GenerateColor(bll, genData, z * 512 + x, dynbiomes);
                        }
                    }
                }
            });

            pool.Return(xdata2, false);
        }
    }












    unsafe static class PrecGausBlur {
        public static ArrayPool<ushort> pool;
        public const int MB = 15;

        public static void BoxBlur(int R, uint* pixels, Tint tint, Colormap colormap, IGenData[,] neighbours) {
            var dynbiomes = new DynBiome(MB);

            var xdata2 = pool.Rent((512 + 2 * R) * (512 + 2 * R) * MB);

            int STRIDE = 512 + 2 * R;

            var genData = neighbours[1, 1];


            //for(int z = -R; z < 512 + R; z++) {
                Parallel.For(-R, 512 + R, z => {
                    Span<ushort> cx2 = stackalloc ushort[(512 + R + R) * MB];
                    Span<ushort> acc_biome = stackalloc ushort[MB];

                    for(int r = -R; r <= R; r++) {
                    (int q, int rem) rx = Math.DivRem(512 + r, 512), rz = Math.DivRem(512 + z, 512);
                    int ri = rz.rem * 512 + rx.rem;
                    var gen = neighbours[rx.q, rz.q];

                    if(gen != null) {
                        if(colormap.Value(gen.block(ri)) is BlockValue gb) {
                            if(gb.tint == tint) {
                                int i = dynbiomes.get(gen.biomeIds(ri));
                                acc_biome[i] += 1;
                                cx2[(R + r) * MB + i] = 1;
                            }
                        }
                    }
                }

                int _f = ((R + z) * STRIDE + R) * MB;
                for(int i = 0; i < MB; i++) xdata2[_f + i] = acc_biome[i];

                for(int x = 1; x < 512; x++) {
                    // old
                    int ___f = (R + x - 1 - R) * MB;
                    for(int i = 0; i < MB; i++) acc_biome[i] -= cx2[___f + i];

                    // new
                    (int q, int rem) rx = Math.DivRem(512 + x + R, 512), rz = Math.DivRem(512 + z, 512);
                    int ri = rz.rem * 512 + rx.rem;
                    var gen = neighbours[rx.q, rz.q];

                    if(gen != null) {
                        if(colormap.Value(gen.block(ri)) is BlockValue gb) {
                            if(gb.tint == tint) {
                                int i = dynbiomes.get(gen.biomeIds(ri));
                                acc_biome[i] += 1;
                                cx2[(R + x + R) * MB + i] = 1;
                            }
                        }
                    }

                    int __f = ((R + z) * STRIDE + (R + x)) * MB;
                    for(int i = 0; i < MB; i++) xdata2[__f + i] = acc_biome[i];
                }
            }
            );

            //for(int x = 0; x < 512; x++) {
                Parallel.For(0, 512, x => {
                    Span<ushort> cx2 = stackalloc ushort[(512 + R + R) * MB];
                    Span<ushort> acc_biome = stackalloc ushort[MB];

                    for(int r = -R; r <= R; r++) {
                    int sd = ((r + R) * STRIDE + (x + R)) * MB;
                    int _f = (R + r) * MB;
                    for(int i = 0; i < MB; i++) {
                        cx2[_f + i] = xdata2[sd + i];
                        acc_biome[i] += cx2[_f + i];
                    }
                }

                var block = colormap.Value(genData.block(0 * 512 + x));
                if(block.tint == tint) {
                    int r = 0, g = 0, b = 0, br = 0;
                    for(int i = 0; i < dynbiomes.max(); i++) {
                        var c = tint.TintColorFor(dynbiomes.back(i), genData.heights(0 * 512 + x)).ToColor();
                        r += c.R * acc_biome[i];
                        g += c.G * acc_biome[i];
                        b += c.B * acc_biome[i];
                        br += acc_biome[i];
                    }

                    pixels[0 * 512 + x] = Global.ColorMult(block.color, WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)).ToUInt());
                }

                for(int z = 1; z < 512; z++) {
                    // old
                    int _f = (z - 1) * MB;
                    for(int i = 0; i < MB; i++) acc_biome[i] -= cx2[_f + i];

                    // new
                    int sd = ((z + R + R) * STRIDE + (x + R)) * MB;
                    int _fNew = (R + z + R) * MB;
                    for(int i = 0; i < MB; i++) {
                        cx2[_fNew + i] = xdata2[sd + i];
                        acc_biome[i] += cx2[_fNew + i];
                    }

                    block = colormap.Value(genData.block(z * 512 + x));
                    if(block.tint == tint) {
                        int r = 0, g = 0, b = 0, br = 0;
                        for(int i = 0; i < dynbiomes.max(); i++) {
                            var c = tint.TintColorFor(dynbiomes.back(i), genData.heights(z * 512 + x)).ToColor();
                            r += c.R * acc_biome[i];
                            g += c.G * acc_biome[i];
                            b += c.B * acc_biome[i];
                            br += acc_biome[i];
                        }

                        pixels[z * 512 + x] = Global.ColorMult(block.color, WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)).ToUInt());
                    }
                }
            }
            );

            pool.Return(xdata2, true);
        }
    }
    unsafe static class ColorGausBlur {
        public struct C {
            public int r, g, b, br;
        }

        public static ArrayPool<C> pool;

        public static void BoxBlur(int R, uint* pixels, Tint tint, Colormap colormap, IGenData[,] neighbours) {
            var xdata2 = pool.Rent((512 + 2 * R) * (512 + 2 * R));

            int STRIDE = 512 + 2 * R;
            var genData = neighbours[1, 1];

            //for(int z = -R; z < 512 + R; z++) { 
            Parallel.For(-R, 512 + R, z => {
                Span<C> cx2 = stackalloc C[(512 + R + R)];
                C acc = new C();

                for(int r = -R; r <= R; r++) {
                    (int q, int rem) rx = Math.DivRem(512 + r, 512), rz = Math.DivRem(512 + z, 512);
                    int ri = rz.rem * 512 + rx.rem;
                    var gen = neighbours[rx.q, rz.q];

                    if(gen != null) {
                        if(colormap.Value(gen.block(ri)).tint == tint) {
                            var f = tint.TintColorFor(gen.biomeIds(ri), gen.heights(ri)).ToColor();
                            acc.r += f.R;
                            acc.g += f.G;
                            acc.b += f.B;
                            acc.br++;
                            cx2[R + r].r = f.R;
                            cx2[R + r].g = f.G;
                            cx2[R + r].b = f.B;
                            cx2[R + r].br = 1;
                        }
                    }
                }

                int _f = (R + z) * STRIDE + R;
                xdata2[_f].r = acc.r;
                xdata2[_f].g = acc.g;
                xdata2[_f].b = acc.b;
                xdata2[_f].br = acc.br;

                for(int x = 1; x < 512; x++) {
                    // old
                    int ___f = R + x - 1 - R;
                    acc.r -= cx2[___f].r;
                    acc.g -= cx2[___f].g;
                    acc.b -= cx2[___f].b;
                    acc.br -= cx2[___f].br;

                    // new
                    (int q, int rem) rx = Math.DivRem(512 + x + R, 512), rz = Math.DivRem(512 + z, 512);
                    int ri = rz.rem * 512 + rx.rem;
                    var gen = neighbours[rx.q, rz.q];

                    if(gen != null) {
                        if(colormap.Value(gen.block(ri)).tint == tint) {
                            var f = tint.TintColorFor(gen.biomeIds(ri), gen.heights(ri)).ToColor();
                            acc.r += f.R;
                            acc.g += f.G;
                            acc.b += f.B;
                            acc.br++;
                            cx2[R + x + R].r = f.R;
                            cx2[R + x + R].g = f.G;
                            cx2[R + x + R].b = f.B;
                            cx2[R + x + R].br = 1;
                        }
                    }

                    int __f = (R + z) * STRIDE + (R + x);
                    xdata2[__f].r = acc.r;
                    xdata2[__f].g = acc.g;
                    xdata2[__f].b = acc.b;
                    xdata2[__f].br = acc.br;
                }
            }
            );

            //for(int x = 0; x < 512; x++) {
            Parallel.For(0, 512, x => {
                Span<C> cx2 = stackalloc C[(512 + R + R)];
                C acc = new C();

                for(int r = -R; r <= R; r++) {
                    int sd = (r + R) * STRIDE + (x + R);
                    int _f = (R + r);

                    cx2[_f].r = xdata2[sd].r;
                    cx2[_f].g = xdata2[sd].g;
                    cx2[_f].b = xdata2[sd].b;
                    cx2[_f].br = xdata2[sd].br;

                    acc.r += cx2[_f].r;
                    acc.g += cx2[_f].g;
                    acc.b += cx2[_f].b;
                    acc.br += cx2[_f].br;
                }

                var block = colormap.Value(genData.block(0 * 512 + x));
                if(block.tint == tint) {
                    pixels[0 * 512 + x] = Global.ColorMult(block.color, WPFColor.FromRgb((byte)(acc.r / acc.br), (byte)(acc.g / acc.br), (byte)(acc.b / acc.br)).ToUInt());
                }

                for(int z = 1; z < 512; z++) {
                    // old
                    int _f = (z - 1);
                    acc.r -= cx2[_f].r;
                    acc.g -= cx2[_f].g;
                    acc.b -= cx2[_f].b;
                    acc.br -= cx2[_f].br;

                    // new
                    int sd = (z + R + R) * STRIDE + (x + R);
                    int _fNew = (R + z + R);
                    cx2[_fNew].r = xdata2[sd].r;
                    cx2[_fNew].g = xdata2[sd].g;
                    cx2[_fNew].b = xdata2[sd].b;
                    cx2[_fNew].br = xdata2[sd].br;

                    acc.r += cx2[_fNew].r;
                    acc.g += cx2[_fNew].g;
                    acc.b += cx2[_fNew].b;
                    acc.br += cx2[_fNew].br;

                    block = colormap.Value(genData.block(z * 512 + x));
                    if(block.tint == tint) {
                        pixels[z * 512 + x] = Global.ColorMult(block.color, WPFColor.FromRgb((byte)(acc.r / acc.br), (byte)(acc.g / acc.br), (byte)(acc.b / acc.br)).ToUInt());
                    }
                }
            }
            );

            pool.Return(xdata2, true);
        }
    }

}
