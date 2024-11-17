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
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace Mcasaenk.Rendering {
    public static class TileDraw {

        public static long drawTime = 0, drawCount = 1;
        public unsafe static void FillPixels(uint* pixels, Colormap colormap, GenData genData, GenData[,] neighbours) {
            var st = Stopwatch.StartNew();
            //uint[] tintcolors = very_temp.Rent(512 * 512);

            //int maxdepth = Global.App.Settings.MAXABSHEIGHT;
            //double transparency = 50 * Global.Settings.WATER_TRANSPARENCY + Math.Pow(Global.App.Settings.WATER_TRANSPARENCY, 10) * (maxdepth - 50);
            double transparency2 = Math.Pow(Global.App.Settings.WATER_TRANSPARENCY, 0.1);
            double qshade = Global.App.Settings.SHADE3D ? 8 * Global.App.Settings.CONTRAST : 16 * Global.App.Settings.CONTRAST;

            double maxshadow = Global.App.Settings.CONTRAST * 150;

            double jmapqshade = Global.Settings.Jmap_REVEALED_WATER;
            double jmapnormal = 1, jmapdark = 180 / 220d, jmapdarker = 135 / 220d, jmaplight = 255 / 220d;
            jmapdark += (1 - jmapdark) * (0.5 - Global.Settings.CONTRAST) * 2;
            jmapdarker += (1 - jmapdarker) * (0.5 - Global.Settings.CONTRAST) * 2;
            jmaplight -= (jmaplight - 1) * (0.5 - Global.Settings.CONTRAST) * 2;



            byte[][] relvis8 = null;
            short[] meanheights = null;
            if(Global.Settings.TRANSPARENTLAYERS > 1 || Global.Settings.OCEAN_DEPTH_BLENDING > 1 || Global.Settings.SHADETYPE == ShadeType.jmap) {
                relvis8 = new byte[genData.columns.Length][];
                meanheights = ArrayPool<short>.Shared.Rent((512 + 2) * (512 + 2));
                for(int w = 0; w < genData.columns.Length; w++) {
                    relvis8[w] = ArrayPool<byte>.Shared.Rent((512 + 2) * (512 + 2));
                }

                genData.SetTemporal_WaterRelief();

                var terrblur = new GausBlur<TerrHeightBlur, short, TerrHeightBlurData>((Global.Settings.OCEAN_DEPTH_BLENDING - 1) / 2, 1, TerrHeightBlur.pool, new TerrHeightBlurData() { mindiff = 15 }, neighbours);
                terrblur.BoxBlur();

                Span<byte> relshadevis8 = stackalloc byte[genData.columns.Length];
                for(int x = -1; x < 513; x++) {
                    terrblur.OnXStart();
                    for(int z = -1; z < 513; z++) {
                        int bi = (z + 1) * 514 + (x + 1);
                        var res = terrblur.GetAndMoveOn_detail();
                        short terrdepth = res.value;

                        if(res.sector != null) {

                            // relvis
                            {
                                byte ostatuk = 255;
                                for(int w = 0; w < genData.columns.Length - 1; w++) {
                                    if(res.sector.columns[w].ContainsInfo(res.ri) == false) continue;

                                    byte a = (byte)(ostatuk * (1 - Math.Pow(1 - (colormap.Grouping.GetGroup(res.sector.columns[w].GroupId(res.ri)).filter.ABSORBTION / 15d), genData.columns[w].Depth(res.ri))));
                                    relvis8[w][bi] = a;
                                    ostatuk -= a;
                                }
                                {
                                    if(res.sector.columns[res.sector.columns.Length - 1].ContainsInfo(res.ri)) {
                                        byte a = (byte)(ostatuk * (1 - Math.Pow(1 - (colormap.Grouping.GetGroup(res.sector.columns[res.sector.columns.Length - 1].GroupId(res.ri)).filter.ABSORBTION / 15d), Math.Max((int)terrdepth, 1))));
                                        relvis8[res.sector.columns.Length - 1][bi] = a;
                                        ostatuk -= a;
                                    }
                                }
                                for(int w = 0; w < genData.columns.Length; w++) {
                                    relvis8[w][bi] += (byte)((relvis8[w][bi] / (float)(255 - ostatuk)) * ostatuk);
                                }
                            }


                            // relshadevis                  
                            if(Global.Settings.NOSHADE_STATIC_SHADE == false) {
                                relshadevis8.Clear();
                                byte ostatuk = 255;
                                for(int w = 0; w < genData.columns.Length - 1; w++) {
                                    if(res.sector.columns[w].ContainsInfo(res.ri) == false) continue;
                                    if(colormap.Grouping.GetGroup(genData.columns[w].GroupId(res.ri)).shade == false) continue;

                                    byte a = (byte)(ostatuk * (1 - Math.Pow(1 - (colormap.Grouping.GetGroup(res.sector.columns[w].GroupId(res.ri)).filter.ABSORBTION / 15d), genData.columns[w].Depth(res.ri))));
                                    relshadevis8[w] = a;
                                    ostatuk -= a;
                                }
                                {
                                    if(res.sector.columns[res.sector.columns.Length - 1].ContainsInfo(res.ri)) {
                                        if(colormap.Grouping.GetGroup(genData.columns[res.sector.columns.Length - 1].GroupId(res.ri)).shade) {
                                            byte a = (byte)(ostatuk * (1 - Math.Pow(1 - (colormap.Grouping.GetGroup(res.sector.columns[res.sector.columns.Length - 1].GroupId(res.ri)).filter.ABSORBTION / 15d), Math.Max((int)terrdepth, 1))));
                                            relshadevis8[res.sector.columns.Length - 1] = a;
                                            ostatuk -= a;
                                        }
                                    }
                                }
                                for(int w = 0; w < genData.columns.Length; w++) {
                                    relshadevis8[w] += (byte)((relshadevis8[w] / (float)(255 - ostatuk)) * ostatuk);
                                }
                            } else {
                                for(int w = 0; w < genData.columns.Length; w++) relshadevis8[w] = relvis8[w][bi];
                            }

                            // meanheights
                            {
                                for(int w = 0; w < genData.columns.Length - 1; w++) {
                                    meanheights[bi] += (short)((relshadevis8[w] / 255f) * res.sector.columns[w].TerrHeight(res.ri));
                                }
                                {
                                    var col = res.sector.columns[res.sector.columns.Length - 1];
                                    meanheights[bi] += (short)((relshadevis8[res.sector.columns.Length - 1] / 255f) * GenDataColumn.TerrHeight(col.IsDepth(res.ri), col.Height(res.ri), Math.Max(terrdepth, (short)1)));
                                }
                            }


                        } else {
                            meanheights[bi] = -1;
                        }

                        if(genData.depthColumn.depths != null && x >= 0 && x < 512 && z >= 0 && z < 512) {
                            genData.depthColumn.depths[z * 512 + x] = terrdepth;
                        } else {
                            meanheights[bi] = -1;
                        }
                    }
                    terrblur.OnXEnd();
                }

                terrblur.Dispose();
            }


            foreach(var tint in colormap.TintManager.GetBlendingTints().Concat([colormap.TintManager.NullTint])) {
                var blendmode = tint.GetBlendMode();

                Blur tintblur = null;
                if(tint is DynamicTint dtint) {
                    if(blendmode == Tint.Blending.biomeonly) {
                        tintblur = new GausBlur<ColorBlur, uint, ColorBlurData>((dtint.Blend - 1) / 2, 0, ColorBlur.pool, new ColorBlurData() { tint = tint, colormap = colormap }, neighbours);
                    } else if(blendmode == Tint.Blending.full) {
                        tintblur = new GausBlur<PrecBlur, PrecBlurData, PrecBlurData>((dtint.Blend - 1) / 2, 0, PrecBlur.pool, new PrecBlurData() { tint = tint, dynbiomes = new DynBiome(PrecBlur.MB), colormap = colormap }, neighbours);
                    }
                }
                tintblur?.BoxBlur();

                for(int x = 0; x < 512; x++) {
                    tintblur?.OnXStart();
                    for(int z = 0; z < 512; z++) {
                        int i = z * 512 + x, bi = (z + 1) * 514 + (x + 1);

                        object blurdata = tintblur?.GetAndMoveOn();

                        for(int w = 0; w < genData.columns.Length; w++) {
                            var colw = genData.columns[w];
                            if(!colw.ContainsInfo(i)) continue;
                            var groupw = colormap.Grouping.GetGroup(colw.GroupId(i));
                            if(tint != groupw.tint && tint != colormap.TintManager.NullTint) continue;
                            bool depth = colw.IsDepth(i);
                            uint color = 0xFF000000;

                            double relvis = relvis8 != null ? relvis8[w][bi] / 255d : 1d;

                            // base tinted color
                            {
                                if(tintblur is GausBlur<ColorBlur, uint, ColorBlurData> colorblur) {
                                    color = Global.ColorMult(colw.Color(i), (uint)blurdata);
                                } else if(tintblur is GausBlur<PrecBlur, PrecBlurData, PrecBlurData> precblur) {
                                    var data = (PrecBlurData)blurdata;

                                    double br = 0, r = 0, g = 0, b = 0;
                                    for(int e = 0; e < data.dynbiomes.max(); e++) {
                                        float lcolor = 0;
                                        for(int h = genData.columns[w].Height(i); h > genData.columns[w].Height(i) - genData.columns[w].Depth(i); h--) {
                                            float q = groupw.filter.ABSORBTION / 15f * (1 - lcolor);
                                            var c = tint.TintColorFor(data.dynbiomes.back(e), (short)h).ToColor();
                                            r += c.R * data.biomeresults[e] * q;
                                            g += c.G * data.biomeresults[e] * q;
                                            b += c.B * data.biomeresults[e] * q;
                                            br += data.biomeresults[e] * q;
                                            lcolor += q;
                                            if(lcolor >= 1) break;
                                        }
                                    }
                                    color = Global.ColorMult(genData.columns[w].Color(i), WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)).ToUInt());
                                } else if(groupw.tint.GetBlendMode() == Tint.Blending.none) {
                                    color = groupw.tint.GetTintedColor(colw.Color(i), Global.Settings.DEFBIOME, Colormap.DEFHEIGHT);
                                } else if(groupw.tint.GetBlendMode() == Tint.Blending.heightonly) {
                                    float lcolor = 0.001f;
                                    for(int h = colw.Height(i); h > colw.Height(i) - colw.Depth(i); h--) {
                                        float q = groupw.filter.ABSORBTION / 15f * (1 - lcolor);
                                        color = Global.Blend(groupw.tint.GetTintedColor(colw.Color(i), colw.BiomeId(i), (short)h), color, q / lcolor);
                                        lcolor += q;
                                        if(lcolor >= 1) break;
                                    }
                                } else continue;
                            }


                            double fd = 1;
                            // water?
                            {
                                if(depth) {

                                    if(Global.App.Settings.SHADETYPE == ShadeType.OG) {
                                        uint terrainColor = colw.ActColor(i);
                                        int waterDepth = (int)colw.Depth(i);
                                        fd = Math.Pow(2, -4 * (1 - transparency2) * (waterDepth + 3));
                                        color = Global.Blend(terrainColor, color, fd);

                                        double multintensity = Math.Pow(fd, 0.75) * Global.App.Settings.CONTRAST + 1 * (1 - Global.App.Settings.CONTRAST);
                                        color = Global.MultShade(color, multintensity);
                                    } else if(Global.App.Settings.SHADETYPE == ShadeType.jmap) {
                                        if(Global.Settings.Jmap_WATER_MODE == JsmapWaterMode.vanilla) {
                                            int hd = colw.Depth(i);

                                            if(hd > 9 * jmapqshade) {
                                                color = Global.MultShade(color, jmapdark);
                                            } else if(hd <= 2 * jmapqshade) {
                                                color = Global.MultShade(color, jmaplight);
                                            } else if(hd <= 4 * jmapqshade) {
                                                if(x % 2 == z % 2) color = Global.MultShade(color, jmaplight);
                                                else color = Global.MultShade(color, jmapnormal);
                                            } else if(hd <= 6 * jmapqshade) {
                                                color = Global.MultShade(color, jmapnormal);
                                            } else if(hd <= 9 * jmapqshade) {
                                                if(x % 2 == z % 2) color = Global.MultShade(color, jmapnormal);
                                                else color = Global.MultShade(color, jmapdark);
                                            }

                                        } else if(Global.Settings.Jmap_WATER_MODE == JsmapWaterMode.translucient) {
                                            uint terrainColor = colw.ActColor(i);
                                            short heightAtComp = meanheights[bi + Global.Settings.Jmap_MAP_DIRECTION switch {
                                                Direction.North => -514,
                                                Direction.South => +514,
                                                Direction.West => -1,
                                                Direction.East => 1,
                                            }];
                                            short terrainheight = (short)(colw.Height(i) - colw.Depth(i));

                                            if(terrainheight < heightAtComp) terrainColor = Global.MultShade(terrainColor, jmapdark);
                                            else if(terrainheight > heightAtComp) terrainColor = Global.MultShade(terrainColor, jmaplight);

                                            color = Global.Blend(terrainColor, color, Global.Settings.WATER_TRANSPARENCY);
                                        }
                                    }

                                }
                            }

                            // shades
                            {
                                if(Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.STATIC_SHADE && (Global.Settings.NOSHADE_STATIC_SHADE || groupw.shade)) { // traebva da e praedi shade

                                    short hi, hx, hnx, hz, hnz;
                                    if(meanheights != null) {
                                        hi = meanheights[bi];
                                        hx = meanheights[bi + 1] != -1 ? meanheights[bi + 1] : hi;
                                        hnx = meanheights[bi - 1] != -1 ? meanheights[bi - 1] : hi;
                                        hz = meanheights[bi + 514] != -1 ? meanheights[bi + 514] : hi;
                                        hnz = meanheights[bi - 514] != -1 ? meanheights[bi - 514] : hi;
                                    } else {
                                        hi = colw.TerrHeight(i);
                                        hx = x < 511 ? colw.TerrHeight(i + 1) : hi;
                                        hnx = x >= 1 ? colw.TerrHeight(i - 1) : hi;
                                        hz = z < 511 ? colw.TerrHeight(i + 512) : hi;
                                        hnz = z >= 1 ? colw.TerrHeight(i - 512) : hi;
                                    }
                                    int xdiff = 0;
                                    xdiff += hx;
                                    xdiff -= hnx;
                                    int zdiff = 0;
                                    zdiff += hz;
                                    zdiff -= hnz;

                                    double shade =  Math.Clamp(-(ShadeConstants.GLB.cosA * xdiff + -ShadeConstants.GLB.sinA * zdiff), -5, 5);
                                    int fq = (int)(shade * qshade * fd);

                                    color = Global.AddShade(color, fq, fq, fq);
                                } else if(Global.App.Settings.SHADETYPE == ShadeType.jmap) {
                                    if(depth == false) {
                                        short heightAtComp = meanheights[bi + Global.Settings.Jmap_MAP_DIRECTION switch {
                                            Direction.North => -514,
                                            Direction.South => +514,
                                            Direction.West => -1,
                                            Direction.East => 1,
                                        }];

                                        if(colw.TerrHeight(i) < heightAtComp) color = Global.MultShade(color, jmapdark);
                                        else if(colw.TerrHeight(i) > heightAtComp) color = Global.MultShade(color, jmaplight);
                                    }

                                }
                            }

                            // shadows & light
                            {
                                double sh = 0;

                                sh += Global.App.Settings.SUN_LIGHT;
                                if(Global.App.Settings.SHADETYPE == ShadeType.OG && colw.Shade(i) > 0) {

                                    var c = color.ToColor();

                                    sh = Math.Clamp(
                                        sh * Math.Max(
                                                (1 - (Global.App.Settings.CONTRAST * (colw.Shade(i) / 15d) * (Global.App.Settings.WATER_SMART_SHADE ? fd : 1))),
                                                ((c.R + c.G + c.B) / 3 - maxshadow) / 255
                                            ), 0, 15);

                                    //double multcontr = 1 - (Global.App.Settings.CONTRAST * fd[i]);
                                    //int addcontr = (int)(-Settings.CONTRAST * 100);


                                    //if(c.a < 255) pixels[i] = 0;
                                    //else pixels[i] = Global.ToARGBInt((byte)Math.Max(c.r * multcontr, c.r - max), (byte)Math.Max(c.g * multcontr, c.g - max), (byte)Math.Max(c.b * multcontr, c.b - max));
                                }
                                // option 1:
                                sh = Math.Clamp(sh + Math.Clamp((colw.BlockLight(i) - 15) / fd + Global.Settings.BLOCK_LIGHT, 0, 15), 0, 15);
                                // option 2:
                                //sh = Math.Clamp(sh + Math.Clamp(colw.BlockLight(i) * fd, 0, Global.Settings.BLOCK_LIGHT), 0, 15);

                                sh = (sh / 15);
                                color = Global.MultShade(color, sh, sh, sh);
                            }


                            pixels[i] += Global.MultShade(color, relvis);
                        }
                    }
                    tintblur?.OnXEnd();
                }

                tintblur?.Dispose();
            }



            if(Global.Settings.USEMAPPALETTE) {
                for(int i = 0; i < 512 * 512; i++) {
                    pixels[i] = JavaMapColors.Nearest(JavaMapColors.derivatives, pixels[i], Global.App.OpenedSave.levelDatInfo.version_id).uintcolor;
                }
            }

            if(meanheights != null) ArrayPool<short>.Shared.Return(meanheights, true);
            if(relvis8 != null) {
                for(int w = 0; w < genData.columns.Length; w++) {
                    ArrayPool<byte>.Shared.Return(relvis8[w], true);
                }
            }
            st.Stop();
            drawTime += st.ElapsedMilliseconds;
            drawCount++;
        }

    }







    interface BlurConstruct<T, U, V> where T : BlurConstruct<T, U, V> {
        void IncreaseNewBlock(GenData gen, V data, int ri);
        void SetNewBlock(GenData gen, V data, int ri);
        void CopyFrom(T blur);
        void Subtract(T blur);
        void Plus(T blur);
        U Generate(GenData gen, V data, int i);
    }

    class TerrHeightBlurData {
        public int mindiff;

        public double q(int diff) {
            if(diff < 8) return 0;
            if(diff > 18) return 1;
            return (diff - 8) / 10d;
        }
    }
    struct TerrHeightBlur : BlurConstruct<TerrHeightBlur, short, TerrHeightBlurData> {
        public static ArrayPool<TerrHeightBlur> pool = ArrayPool<TerrHeightBlur>.Create((512 * 3) * (512 * 3), 8);

        int h;
        int br;

        public TerrHeightBlur() {
            h = 0;
            br = 0;
        }

        public void CopyFrom(TerrHeightBlur blur) {
            h = blur.h; br = blur.br;
        }
        public void Plus(TerrHeightBlur blur) {
            h += blur.h;
            br += blur.br;
        }
        public void Subtract(TerrHeightBlur blur) {
            h -= blur.h;
            br -= blur.br;
        }

        public void IncreaseNewBlock(GenData gen, TerrHeightBlurData data, int ri) {
            if(gen == null) return;
            if(gen.depthColumn.IsDepth(ri)) {
                h += gen.depthColumn.Depth(ri);
                br++;
            }
        }
        public void SetNewBlock(GenData gen, TerrHeightBlurData data, int ri) {
            if(gen == null) return;
            if(gen.depthColumn.IsDepth(ri)) {
                h = gen.depthColumn.Depth(ri);
                br = 1;
            }
        }

        public short Generate(GenData gen, TerrHeightBlurData data, int i) {
            if(gen == null) return -1;
            if(gen.depthColumn.IsDepth(i) == false) return gen.depthColumn.depths[i];
            double q = data.q(gen.depthColumn.Depth(i));
            //return (short)(h / br);
            return (short)(h / br * q + gen.depthColumn.Depth(i) * (1 - q));
        }
    }
    
    class ColorBlurData {
        public Tint tint;
        public Colormap colormap;
    }
    struct ColorBlur : BlurConstruct<ColorBlur, uint, ColorBlurData> {
        public static ArrayPool<ColorBlur> pool = ArrayPool<ColorBlur>.Create((512 * 3) * (512 * 3), 8);

        public int r, g, b;
        public int br;

        public ColorBlur() {
            Reset();
        }
        void Reset() {
            br = 0; r = 0; g = 0; b = 0;
        }

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

        public void IncreaseNewBlock(GenData gen, ColorBlurData data, int ri) {
            for(int w = 0; w < gen.columns.Length; w++) {
                if(gen.columns[w].ContainsInfo(ri) == false) continue;
                if(data.colormap.Grouping.GetGroup(gen.columns[w].GroupId(ri)).tint == data.tint) {
                    var f = data.tint.TintColorFor(gen.columns[w].BiomeId(ri), gen.columns[w].Height(ri)).ToColor();
                    r += f.R;
                    g += f.G;
                    b += f.B;
                    br++;
                }
            }
        }
        public void SetNewBlock(GenData gen, ColorBlurData data, int ri) {
            Reset();
            for(int w = 0; w < gen.columns.Length; w++) {
                if(gen.columns[w].ContainsInfo(ri) == false) continue;
                if(data.colormap.Grouping.GetGroup(gen.columns[w].GroupId(ri)).tint == data.tint) {
                    var f = data.tint.TintColorFor(gen.columns[w].BiomeId(ri), gen.columns[w].Height(ri)).ToColor();
                    r += f.R;
                    g += f.G;
                    b += f.B;
                    br++;
                }
            }
        }

        public uint Generate(GenData gen, ColorBlurData data, int i) {
            if(br == 0) {
                return 0xFFFF0000;
            }
            return WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)).ToUInt();
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
    class PrecBlurData {
        public DynBiome dynbiomes;
        public Tint tint;
        public Colormap colormap;

        public int[] biomeresults = new int[PrecBlur.MB];
    }
    unsafe struct PrecBlur : BlurConstruct<PrecBlur, PrecBlurData, PrecBlurData> {
        public static ArrayPool<PrecBlur> pool = ArrayPool<PrecBlur>.Create((512 * 3) * (512 * 3), 8);
        public const int MB = 15;


        public fixed int biome[MB];

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


        public void IncreaseNewBlock(GenData gen, PrecBlurData data, int ri) {
            for(int w = 0; w < gen.columns.Length; w++) {
                if(gen.columns[w].ContainsInfo(ri) == false) continue;
                var group = data.colormap.Grouping.GetGroup(gen.columns[w].GroupId(ri));
                if(group.tint == data.tint) {
                    int i = data.dynbiomes.get(gen.columns[w].BiomeId(ri));
                    biome[i] += group.filter.ABSORBTION;
                }
            }
        }
        public void SetNewBlock(GenData gen, PrecBlurData data, int ri) {
            for(int w = 0; w < gen.columns.Length; w++) {
                if(gen.columns[w].ContainsInfo(ri) == false) continue;
                var group = data.colormap.Grouping.GetGroup(gen.columns[w].GroupId(ri));
                if(group.tint == data.tint) {
                    int i = data.dynbiomes.get(gen.columns[w].BiomeId(ri));
                    biome[i] = group.filter.ABSORBTION;
                }
            }
        }



        public PrecBlurData Generate(GenData gen, PrecBlurData data, int ri) {
            for(int i = 0; i < MB; i++) {
                data.biomeresults[i] = biome[i];
            }
            return data;
        }
    }

    


    interface Blur : IDisposable {
        void BoxBlur();
        void OnXStart();
        void OnXEnd();
        object GetAndMoveOn();
    }

    class GausBlur<T, U, V> : Blur where T : struct, BlurConstruct<T, U, V> {
        private readonly int R, resR, STRIDE;
        private readonly ArrayPool<T> pool;
        private readonly V data;
        private readonly GenData[,] neighbours;
        private T[] xdata2;
        public GausBlur(int R, int resR, ArrayPool<T> pool, V data, GenData[,] neighbours) {
            this.R = R; this.resR = resR;
            this.pool = pool;
            this.data = data;
            this.neighbours = neighbours;
            this.STRIDE = 512 + 2 * R;

            if(R > 0) {
                this.xdata2 = pool.Rent((512 + 2 * R) * (512 + 2 * R));
                this.cx2 = pool.Rent(512 + R + R);              
            }

            ax = -resR; az = -resR;
        }
        public void Dispose() {
            if(R > 0) {
                pool.Return(xdata2, true);
                pool.Return(cx2, true);
            }
        }


        public void BoxBlur() {
            if(R == 0) return;

            var genData = neighbours[1, 1];

            Parallel.For(-R, 512 + R, new ParallelOptions() { MaxDegreeOfParallelism = 1 }, z => {
                Span<T> cx2 = MemoryMarshal.Cast<byte, T>(stackalloc byte[Unsafe.SizeOf<T>() * (512 + R + R)]);
                T acc = new T();

                for(int r = -R; r < R - resR; r++) {
                    (int q, int rem) rx = Math.DivRem(512 + r, 512), rz = Math.DivRem(512 + z, 512);
                    int ri = rz.rem * 512 + rx.rem;
                    var gen = neighbours[rx.q, rz.q];

                    if(gen != null) {
                        acc.IncreaseNewBlock(gen, data, ri);
                        cx2[R + r].SetNewBlock(gen, data, ri);
                    }

                }

                for(int x = -resR; x < 512 + resR; x++) {
                    // old
                    if(R + x - 1 - R >= 0) acc.Subtract(cx2[R + x - 1 - R]);

                    // new
                    if(R + x + R < cx2.Length) {
                        (int q, int rem) rx = Math.DivRem(512 + x + R, 512), rz = Math.DivRem(512 + z, 512);
                        int ri = rz.rem * 512 + rx.rem;
                        var gen = neighbours[rx.q, rz.q];

                        if(gen != null) {
                            acc.IncreaseNewBlock(gen, data, ri);
                            cx2[R + x + R].SetNewBlock(gen, data, ri);
                        }
                    }

                    xdata2[(R + z) * STRIDE + (R + x)].CopyFrom(acc);
                }
            });

        }


        private T[] cx2;
        private T acc;
        private int ax, az;

        public void OnXStart() {
            acc = new T();

            for(int r = -R; r < R - resR; r++) {
                cx2[R + r].CopyFrom(xdata2[(r + R) * STRIDE + (ax + R)]);
                acc.Plus(cx2[R + r]);
            }
            az = -resR;
        }

        public void OnXEnd() {
            ax++;
        }

        public (U value, GenData sector, int ri) GetAndMoveOn_detail() {
            if(R > 0) {
                // old
                if(az - 1 >= 0) acc.Subtract(cx2[az - 1]);

                // new
                if(R + az + R < cx2.Length) {
                    cx2[R + az + R].CopyFrom(xdata2[(az + R + R) * STRIDE + (ax + R)]);
                    acc.Plus(cx2[R + az + R]);
                }
            }

            var xx = Math.DivRem(ax + 512, 512);
            var zz = Math.DivRem(az++ + 512, 512);
            int ri = zz.Remainder * 512 + xx.Remainder;
            if(R == 0) acc.SetNewBlock(neighbours[xx.Quotient, zz.Quotient], data, ri);

            return (acc.Generate(neighbours[xx.Quotient, zz.Quotient], data, ri), neighbours[xx.Quotient, zz.Quotient], ri);
        }

        public U GetAndMoveOn() {
            if(R > 0) {
                // old
                if(az - 1 >= 0) acc.Subtract(cx2[az - 1]);

                // new
                if(R + az + R < cx2.Length) {
                    cx2[R + az + R].CopyFrom(xdata2[(az + R + R) * STRIDE + (ax + R)]);
                    acc.Plus(cx2[R + az + R]);
                }
            }

            var xx = Math.DivRem(ax + 512, 512);
            var zz = Math.DivRem(az++ + 512, 512);
            int ri = zz.Remainder * 512 + xx.Remainder;
            if(R == 0) acc.SetNewBlock(neighbours[xx.Quotient, zz.Quotient], data, ri);

            return acc.Generate(neighbours[xx.Quotient, zz.Quotient], data, ri);
        }

        object Blur.GetAndMoveOn() => this.GetAndMoveOn();
    }

}
