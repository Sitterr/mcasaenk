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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Xml.Linq;
using static Mcasaenk.Global;

namespace Mcasaenk.Rendering {
    public static unsafe class TileDraw {

        public static long drawTime = 0, drawCount = 1;
        public static void FillPixels(uint* pixels, Colormap colormap, GenData genData, GenData[,] neighbours) {
            var st = Stopwatch.StartNew();

            PrepConstants consts = new PrepConstants() {
                transparency2 = Math.Pow(Global.App.Settings.WATER_TRANSPARENCY, 0.1),
                qshade = Global.App.Settings.SHADE3D ? 8 * Global.App.Settings.CONTRAST : 16 * Global.App.Settings.CONTRAST,
                maxshadow = Global.App.Settings.CONTRAST * 150,
                jmap = new JmapShade()
            };

            byte[][] relvis8 = null;
            short[] meanheights = null;
            if(genData.columns.Length > 1 || Global.Settings.OCEAN_DEPTH_BLENDING > 1 || Global.Settings.SHADETYPE == ShadeType.jmap) {
                relvis8 = new byte[genData.columns.Length][];
                meanheights = ArrayPool<short>.Shared.Rent((512 + 2) * (512 + 2));
                Array.Clear(meanheights);
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

                                    byte a = (byte)(ostatuk * (1 - Math.Pow(1 - (res.sector.columns[w].Filter(res.ri).ABSORBTION / 15d), genData.columns[w].Depth(res.ri))));
                                    relvis8[w][bi] = a;
                                    ostatuk -= a;
                                }
                                {
                                    if(res.sector.columns[res.sector.columns.Length - 1].ContainsInfo(res.ri)) {
                                        byte a = (byte)(ostatuk * (1 - Math.Pow(1 - (res.sector.columns[res.sector.columns.Length - 1].Filter(res.ri).ABSORBTION / 15d), Math.Max((int)terrdepth, 1))));
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
                                    if(res.sector.columns[w].NeedShade(res.ri) == false) continue;

                                    byte a = (byte)(ostatuk * (1 - Math.Pow(1 - (res.sector.columns[w].Filter(res.ri).ABSORBTION / 15d), genData.columns[w].Depth(res.ri))));
                                    relshadevis8[w] = a;
                                    ostatuk -= a;
                                }
                                {
                                    if(res.sector.columns[res.sector.columns.Length - 1].ContainsInfo(res.ri)) {
                                        if(genData.columns[res.sector.columns.Length - 1].NeedShade(res.ri)) {
                                            byte a = (byte)(ostatuk * (1 - Math.Pow(1 - (res.sector.columns[res.sector.columns.Length - 1].Filter(res.ri).ABSORBTION / 15d), Math.Max((int)terrdepth, 1))));
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
                            //meanheights[bi] = -1;
                        }
                    }
                    terrblur.OnXEnd();
                }

                terrblur.Dispose();
            }


            foreach(var tint in colormap.TintManager.GetBlendingTints()) {
                var blendmode = tint.GetBlendMode();
                if(colormap.Grouping.HaveInRecord(tint) == false) continue;

                if(tint is DynamicTint dtint) {
                    if(blendmode == Tint.Blending.biomeonly) {
                        TraverseAndPaintForBlur(genData, pixels, consts, relvis8, meanheights, new ColorBlurDraw((dtint.Blend - 1) / 2, 0, new ColorBlurData() { tint = tint, colormap = colormap }, neighbours), tint);
                    } else if(blendmode == Tint.Blending.full) {
                        TraverseAndPaintForBlur(genData, pixels, consts, relvis8, meanheights, new PrecBlurDraw((dtint.Blend - 1) / 2, 0, new PrecBlurData() { tint = tint, dynbiomes = new DynBiome(PrecBlur.MB), colormap = colormap }, neighbours), tint);
                    }
                }
            }
            TraverseAndPaintForBlur(genData, pixels, consts, relvis8, meanheights, new NullBlurDraw(), null); // all other tints that dont blur



            if(Global.Settings.USEMAPPALETTE) {
                for(int i = 0; i < 512 * 512; i++) {
                    pixels[i] = JavaMapColors.Nearest(WPFColor.FromUInt(pixels[i]), Global.App.OpenedSave.levelDatInfo.version_id).color.ToUInt();
                }
            }

            if(meanheights != null) ArrayPool<short>.Shared.Return(meanheights);
            if(relvis8 != null) {
                for(int w = 0; w < genData.columns.Length; w++) {
                    ArrayPool<byte>.Shared.Return(relvis8[w], true);
                }
            }
            st.Stop();
            drawTime += st.ElapsedMilliseconds;
            drawCount++;
        }


        struct JmapShade {
            public double qshade, normal = 1, dark = 180 / 220d, darker = 135 / 220d, light = 255 / 220d;
            public JmapShade() {
                qshade = Global.Settings.Jmap_REVEALED_WATER;
                dark += (1 - dark) * (0.5 - Global.Settings.CONTRAST) * 2;
                darker += (1 - darker) * (0.5 - Global.Settings.CONTRAST) * 2;
                light -= (light - 1) * (0.5 - Global.Settings.CONTRAST) * 2;
            }
        }
        struct PrepConstants {
            public double transparency2;
            public double qshade;
            public double maxshadow;
            public JmapShade jmap;
        }



        static void TraverseAndPaintForBlur<U>(GenData genData, uint* pixels, PrepConstants consts, byte[][] relvis8, short[] meanheights, BlurDraw<U> tintblur, Tint tint) {
            tintblur.BoxBlur();

            for(int x = 0; x < 512; x++) {
                tintblur.OnXStart();
                for(int z = 0; z < 512; z++) {
                    int i = z * 512 + x, bi = (z + 1) * 514 + (x + 1);

                    U blurdata = tintblur.GetAndMoveOn();
                    for(int w = 0; w < genData.columns.Length; w++) {
                        var colw = genData.columns[w];
                        if(!colw.ContainsInfo(i)) continue;
                        var tintw = colw.Tint(i);
                        if(tint != tintw && tint != null) continue;

                        uint color = tintblur.Draw(colw, i, blurdata, tintw);
                        if(color == 0) continue;




                        double fd = 1;
                        bool depth = colw.IsDepth(i);
                        // water?
                        {
                            if(depth) {

                                if(Global.App.Settings.SHADETYPE == ShadeType.OG) {
                                    uint terrainColor = colw.ActColor(i);
                                    int waterDepth = (int)colw.Depth(i);
                                    if(waterDepth > colw.Height(i)) {
                                        terrainColor = WPFColor.FromColor(color.ToColor(), 0).ToUInt();
                                    }
                                    fd = Math.Pow(2, -4 * (1 - consts.transparency2) * (waterDepth + 3));
                                    color = Global.Blend(terrainColor, color, fd, true);

                                    double multintensity = Math.Pow(fd, 0.75) * Global.App.Settings.CONTRAST + 1 * (1 - Global.App.Settings.CONTRAST);
                                    color = Global.MultShade(color, multintensity);
                                } else if(Global.App.Settings.SHADETYPE == ShadeType.jmap) {
                                    if(Global.Settings.Jmap_WATER_MODE == JsmapWaterMode.vanilla) {
                                        int hd = colw.Depth(i);

                                        if(hd > 9 * consts.jmap.qshade) {
                                            color = Global.MultShade(color, consts.jmap.dark);
                                        } else if(hd <= 2 * consts.jmap.qshade) {
                                            color = Global.MultShade(color, consts.jmap.light);
                                        } else if(hd <= 4 * consts.jmap.qshade) {
                                            if(x % 2 == z % 2) color = Global.MultShade(color, consts.jmap.light);
                                            else color = Global.MultShade(color, consts.jmap.normal);
                                        } else if(hd <= 6 * consts.jmap.qshade) {
                                            color = Global.MultShade(color, consts.jmap.normal);
                                        } else if(hd <= 9 * consts.jmap.qshade) {
                                            if(x % 2 == z % 2) color = Global.MultShade(color, consts.jmap.normal);
                                            else color = Global.MultShade(color, consts.jmap.dark);
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

                                        if(terrainheight < heightAtComp) terrainColor = Global.MultShade(terrainColor, consts.jmap.dark);
                                        else if(terrainheight > heightAtComp) terrainColor = Global.MultShade(terrainColor, consts.jmap.light);
                                        color = Global.Blend(terrainColor, color, Global.Settings.WATER_TRANSPARENCY);
                                    }
                                }

                            }
                        }

                        // shades
                        {
                            if(Global.App.Settings.SHADETYPE == ShadeType.OG && Global.App.Settings.STATIC_SHADE && (Global.Settings.NOSHADE_STATIC_SHADE || colw.NeedShade(i))) { // traebva da e praedi shade

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

                                double shade = Math.Clamp(-(ShadeConstants.GLB.cosA * xdiff + -ShadeConstants.GLB.sinA * zdiff), -5, 5);
                                int fq = (int)(shade * consts.qshade * fd);

                                color = Global.AddShade(color, fq, fq, fq);
                            } else if(Global.App.Settings.SHADETYPE == ShadeType.jmap) {
                                if(depth == false) {
                                    short heightAtComp = meanheights[bi + Global.Settings.Jmap_MAP_DIRECTION switch {
                                        Direction.North => -514,
                                        Direction.South => +514,
                                        Direction.West => -1,
                                        Direction.East => 1,
                                    }];

                                    if(colw.TerrHeight(i) < heightAtComp) color = Global.MultShade(color, consts.jmap.dark);
                                    else if(colw.TerrHeight(i) > heightAtComp) color = Global.MultShade(color, consts.jmap.light);
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
                                            ((c.R + c.G + c.B) / 3 - consts.maxshadow) / 255
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



                        double relvis = relvis8 != null ? relvis8[w][bi] / 255d : colw.relvis != null ? colw.relvis[i] / 255d : 1d;
                        pixels[i] += Global.MultShade(color, relvis);
                    }
                }
                tintblur.OnXEnd();
            }

            tintblur.Dispose();
        }
    }







    interface Blur<U> : IDisposable {
        void BoxBlur();
        void OnXStart();
        void OnXEnd();
        public U GetAndMoveOn();
    }



    interface BlurConstruct<T, U, V> where T : BlurConstruct<T, U, V> {
        void IncreaseNewBlock(GenData gen, V data, int ri);
        void CopyFrom(T blur);
        void Subtract(T blur);
        void Plus(T blur);
        void Reset();
        U Generate(GenData gen, V data, int i);
    }
    class GausBlur<T, U, V> : Blur<U> where T : struct, BlurConstruct<T, U, V> {
        private readonly int R, resR, STRIDE;
        private readonly ArrayPool<T> pool;
        protected readonly V data;
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
            acc = new T();
        }
        public void Dispose() {
            if(R > 0) {
                pool.Return(xdata2, true);
                pool.Return(cx2, true);
            }
        }


        public void BoxBlur() {
            if(R == 0) return;

            acc.Reset();
            //Span<T> cx2 = MemoryMarshal.Cast<byte, T>(stackalloc byte[Unsafe.SizeOf<T>() * (512 + R + R)]); 
            for(int z = -R; z < 512 + R; z++) {
                acc.Reset();

                for(int r = -R; r < R - resR; r++) {
                    (int q, int rem) rx = Math.DivRem(512 + r, 512), rz = Math.DivRem(512 + z, 512);
                    int ri = rz.rem * 512 + rx.rem;
                    var gen = neighbours[rx.q, rz.q];

                    cx2[R + r].Reset();
                    if(gen != null) {
                        acc.IncreaseNewBlock(gen, data, ri);           
                        cx2[R + r].IncreaseNewBlock(gen, data, ri);
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

                        cx2[R + x + R].Reset();
                        if(gen != null) {
                            acc.IncreaseNewBlock(gen, data, ri);                          
                            cx2[R + x + R].IncreaseNewBlock(gen, data, ri);
                        }
                    }

                    xdata2[(R + z) * STRIDE + (R + x)].CopyFrom(acc);
                }
            }

        }


        private T[] cx2;
        private T acc;
        private int ax, az;

        public void OnXStart() {
            acc.Reset();

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
            if(R == 0) {
                acc.Reset();
                acc.IncreaseNewBlock(neighbours[xx.Quotient, zz.Quotient], data, ri);
            }

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
            if(R == 0) {
                acc.Reset();
                acc.IncreaseNewBlock(neighbours[xx.Quotient, zz.Quotient], data, ri);
            }

            return acc.Generate(neighbours[xx.Quotient, zz.Quotient], data, ri);
        }
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
            Reset();
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
        public void Reset() {
            h = 0; br = 0;
        }

        public void IncreaseNewBlock(GenData gen, TerrHeightBlurData data, int ri) {
            if(gen == null) return;
            if(gen.depthColumn.IsDepth(ri)) {
                h += gen.depthColumn.Depth(ri);
                br++;
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



    interface BlurDraw<U> : Blur<U> {
        public uint Draw(GenDataColumn colw, int i, U data, Tint tint);
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
        public void Reset() {
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
                if(gen.columns[w].Tint(ri) == data.tint) {
                    var f = data.tint.TintColorFor(gen.columns[w].BiomeId(ri), gen.columns[w].Height(ri)).ToColor();
                    int mult = gen.columns[w].Filter(ri).ABSORBTION;
                    r += f.R * mult;
                    g += f.G * mult;
                    b += f.B * mult;
                    br += mult;
                }
            }
        }

        public uint Generate(GenData gen, ColorBlurData data, int i) {
            if(br == 0) return 0xFFFF0000;
            return WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)).ToUInt();
        }
    }
    class ColorBlurDraw : GausBlur<ColorBlur, uint, ColorBlurData>, BlurDraw<uint> {
        public ColorBlurDraw(int R, int resR, ColorBlurData data, GenData[,] neighbours) : base(R, resR, ColorBlur.pool, data, neighbours) { }

        uint BlurDraw<uint>.Draw(GenDataColumn colw, int i, uint data, Tint tint) {
            return Global.ColorMult(colw.Color(i), data);
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
        public void Reset() {
            for(int i = 0; i < MB; i++) biome[i] = 0;
        }


        public void IncreaseNewBlock(GenData gen, PrecBlurData data, int ri) {
            for(int w = 0; w < gen.columns.Length; w++) {
                if(gen.columns[w].ContainsInfo(ri) == false) continue;
                if(gen.columns[w].Tint(ri) == data.tint) {
                    int i = data.dynbiomes.get(gen.columns[w].BiomeId(ri));
                    biome[i] += gen.columns[w].Filter(ri).ABSORBTION;
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
    class PrecBlurDraw : GausBlur<PrecBlur, PrecBlurData, PrecBlurData>, BlurDraw<PrecBlurData> {
        public PrecBlurDraw(int R, int resR, PrecBlurData data, GenData[,] neighbours) : base(R, resR, PrecBlur.pool, data, neighbours) { }
        uint BlurDraw<PrecBlurData>.Draw(GenDataColumn colw, int i, PrecBlurData data, Tint tint) {
            double br = 0, r = 0, g = 0, b = 0;
            var filterw = colw.Filter(i);
            for(int e = 0; e < data.dynbiomes.max(); e++) {
                if(data.biomeresults[e] > 0) {
                    float lcolor = 0;
                    for(int h = colw.Height(i); h > colw.Height(i) - colw.Depth(i); h--) {
                        float q = filterw.ABSORBTION / 15f * (1 - lcolor);
                        var c = tint.TintColorFor(data.dynbiomes.back(e), (short)h).ToColor();
                        r += c.R * data.biomeresults[e] * q;
                        g += c.G * data.biomeresults[e] * q;
                        b += c.B * data.biomeresults[e] * q;
                        br += data.biomeresults[e] * q;
                        lcolor += q;
                        if(lcolor >= 1) break;
                    }
                }
            }
            return Global.ColorMult(colw.Color(i), WPFColor.FromRgb((byte)(r / br), (byte)(g / br), (byte)(b / br)).ToUInt());
        }
    }



    class NullBlurDraw : BlurDraw<bool> {
        public void BoxBlur() { }
        public void OnXStart() { }
        public void OnXEnd() { }
        public bool GetAndMoveOn() { return false; }

        public uint Draw(GenDataColumn colw, int i, bool data, Tint tint) {
            if(tint.GetBlendMode() == Tint.Blending.none) {
                return tint.GetTintedColor(colw.Color(i), Global.Settings.DEFBIOME, Colormap.DEFHEIGHT);
            } else if(tint.GetBlendMode() == Tint.Blending.heightonly) {
                uint color = 0;
                float lcolor = 0.001f;
                for(int h = colw.Height(i); h > colw.Height(i) - colw.Depth(i); h--) {
                    float q = colw.Filter(i).ABSORBTION / 15f * (1 - lcolor);
                    color = Global.Blend(tint.GetTintedColor(colw.Color(i), colw.BiomeId(i), (short)h), color, q / lcolor);
                    lcolor += q;
                    if(lcolor >= 1) break;
                }
                return color;
            } else return 0;
        }

        public void Dispose() { }
    }
}
