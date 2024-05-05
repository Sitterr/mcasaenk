using Mcasaenk.Shade3d;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using static Mcasaenk.Rendering.GenerateTilePool;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mcasaenk.Rendering {
    public static class TileDraw {

        public static long drawTime = 0, drawCount = 1;

        public static ArrayPool<uint> very_temp = ArrayPool<uint>.Create(512 * 512, 8);
        public static ArrayPool<(int r, int g, int b, int br)> very_temp_blur = ArrayPool<(int r, int g, int b, int br)>.Create((512 + 512 + 512) * (512 + 512 + 512), 8);

        public static void FillPixels(Span<uint> pixels, IGenData genData, IGenData[,] neighbours) {
            var st = Stopwatch.StartNew();

            for(int i = 0; i < 512 * 512; i++) {
                var biome = (Settings.LANDBIOMES) ? genData.biomeIds(i) : default;
                pixels[i] = ColorMapping.Current.GetColor(genData.blockIds(i), biome);
            }

            uint[] tintcolors = very_temp.Rent(512 * 512);

            if(Settings.LANDBIOMES && Settings.LAND_BLEND > 1) { // biome blend
                foreach(var tintgroup in ColorMapping.Current.GetTintGroups()) {
                    if(tintgroup.Contains(ColorMapping.BLOCK_WATER)) continue;
                    GausBlur.BoxBlur((Settings.LAND_BLEND - 1) / 2, tintcolors, tintgroup, neighbours);
                    for(int i = 0; i < 512 * 512; i++) {
                        if(tintgroup.Contains(genData.terrainblock(i))) {
                            pixels[i] = tintcolors[i];
                        }
                    }
                    tintcolors.AsSpan().Fill(default);
                }
            }
            if(Settings.WATERBIOMES && Settings.WATER_BLEND > 1) { // water blend
                tintcolors.AsSpan().Fill(default);
                GausBlur.BoxBlur((Settings.WATER_BLEND - 1) / 2, tintcolors, new DummySingleBlockSet(ColorMapping.BLOCK_WATER), neighbours);
            } else {
                for(int i = 0; i < 512 * 512; i++) {
                    if(genData.terrainblock(i) == ColorMapping.BLOCK_WATER) {
                        ushort waterbiome = Settings.WATERBIOMES ? genData.biomeIds(i) : default;
                        tintcolors[i] = ColorMapping.Current.GetColor(ColorMapping.BLOCK_WATER, waterbiome);
                    }
                }
            }

            { // water
                double l = Settings.CONTRAST;
                double watercontrast = -50 * Math.Pow(l, 8) + -10 * l;

                double k = Settings.WATEROPACITY;
                double wateropacity = -30 * Math.Pow(k, 8) + -30 * k;

                int i = 0;
                for(int z = 0; z < 512; z++) {
                    for(int x = 0; x < 512; x++, i++) {
                        if(genData.terrainblock(i) == ColorMapping.BLOCK_WATER) {
                            int waterDepth = genData.heights(i) - genData.terrainHeights(i);

                            if(Settings.WATERDEPTH) {
                                pixels[i] = Global.Blend(tintcolors[i], pixels[i], I(waterDepth, Settings.WATEROPACITY, 1.5 * wateropacity));

                                double multintensity = 1 - I(waterDepth, 0, watercontrast);
                                pixels[i] = Global.MultShade(pixels[i], multintensity, multintensity, multintensity);
                            } else {
                                pixels[i] = tintcolors[i];
                            }
                        }
                    }
                }
            }


            if(Settings.STATIC_SHADE) {
                double q = 8 * (2 * Settings.CONTRAST);
                if(Settings.SHADE3D) q = q / 4;

                staticshade(pixels, genData, ShadeConstants.GLB.cosA, ShadeConstants.GLB.sinA, q);
            }


            if(Settings.SHADE3D) {
                int i = 0;
                for(int z = 0; z < 512; z++) {
                    for(int x = 0; x < 512; x++, i++) {
                        if(genData.isShade(i)) {
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

            very_temp.Return(tintcolors, true);

            st.Stop();
            drawTime += st.ElapsedMilliseconds;
            drawCount++;
        }

        private static double I(int x, double m = 0.3, double b = -2) {
            return m + (1 - Math.Pow(10.0, b * ((double)x / (319 + 64)))) * (1 - m);
        } 

        private static void staticshade(Span<uint> pixelBuffer, IGenData gdata, double cosA, double sinA, double q) {
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
                            zShade = (gdata.heights(index + 512)) - (gdata.heights(index));
                        } else if(z == 512 - 1) {
                            zShade = (gdata.heights(index)) - (gdata.heights(index - 512));
                        } else {
                            zShade = ((gdata.heights(index + 512)) - (gdata.heights(index - 512))) * 2;
                        }

                        if(x == 0) {
                            xShade = (gdata.heights(index + 1)) - (gdata.heights(index));
                        } else if(x == 512 - 1) {
                            xShade = (gdata.heights(index)) - (gdata.heights(index - 1));
                        } else {
                            xShade = ((gdata.heights(index + 1)) - (gdata.heights(index - 1))) * 2;
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

    static class GausBlur {
        static void XVBlur(int R, double q, Span<uint> pixels, ISet<ushort> blocks, IGenData[,] neighbours) {
            //var xdata = new (int r, int g, int b, int br)[(512 + 2 * R) * (512 + 2 * R)]; //!!!
            var xdata = TileDraw.very_temp_blur.Rent((512 + 2 * R) * (512 + 2 * R));

            int STRIDE = 512 + 2 * R;

            (int r, int g, int b, int br) blf(int x, int z, bool usexdata) {
                (int q, int rem) rx = Math.DivRem(512 + x, 512), rz = Math.DivRem(512 + z, 512);
                int ri = rz.rem * 512 + rx.rem;
                //if(usexdata && rx.q == 1 && rz.q == 1) return xdata[ri];
                if(usexdata) { 
                    return xdata[(z + R) * STRIDE + (x + R)];
                }
                var gen = neighbours[rx.q, rz.q];
                if(gen == null) return (0, 0, 0, 0);
                var bl = gen.terrainblock(ri);

                (int r, int g, int b, int br) answ = (0, 0, 0, 0);

                if(blocks.Contains(bl)) {
                    var vals = Global.FromARGBInt(ColorMapping.Current.GetColor(bl, gen.biomeIds(ri)));
                    if(vals.a == 0) return answ;
                    answ = (vals.r, vals.g, vals.b, 1);
                }
                return answ;
            }

            var genData = neighbours[1, 1];
            Span<(int r, int g, int b, int br)> cx = stackalloc (int r, int g, int b, int br)[512 + R + R];

            for(int z = -R; z < 512 + R; z++) {
                cx.Fill(default);
                int accr = 0, accg = 0, accb = 0;
                int br = 0;

                for(int r = -R; r <= R; r++) {
                    var answ = blf(r, z, false);
                    accr += answ.r * answ.br;
                    accg += answ.g * answ.br;
                    accb += answ.b * answ.br;
                    br += answ.br;
                    cx[R + r] = answ;
                }
                //watercolors[z * 512] = Global.ToARGBInt((byte)(accr / br), (byte)(accg / br), (byte)(accb / br));
                if(br > 0) xdata[(R + z) * STRIDE + (R + 0)] = (accr/br, accg/br, accb/br, br);

                for(int x = 1; x < 512; x++) {
                    { // old
                        accr -= cx[R + x - 1 - R].r * cx[R + x - 1 - R].br;
                        accg -= cx[R + x - 1 - R].g * cx[R + x - 1 - R].br;
                        accb -= cx[R + x - 1 - R].b * cx[R + x - 1 - R].br;
                        br -= cx[R + x - 1 - R].br;
                    }
                    { // new
                        var answ = blf(x + R, z, false);
                        accr += answ.r * answ.br;
                        accg += answ.g * answ.br;
                        accb += answ.b * answ.br;
                        br += answ.br;
                        cx[R + x + R] = answ;
                    }

                    if(br > 0) xdata[(R + z) * STRIDE + (R + x)] = (accr/br, accg/br, accb/br, br);
                }
            }


            for(int x = 0; x < 512; x++) {
                cx.Fill(default);
                int accr = 0, accg = 0, accb = 0;
                int br = 0;


                for(int r = -R; r <= R; r++) {
                    var answ = blf(x, r, true);
                    accr += answ.r * answ.br;
                    accg += answ.g * answ.br;
                    accb += answ.b * answ.br;
                    br += answ.br;
                    cx[R + r] = answ;
                }

                if(br > 0) pixels[0 * 512 + x] = Global.AddShade(pixels[0 * 512 + x], (byte)(accr / br * q), (byte)(accg / br * q), (byte)(accb / br * q));

                for(int z = 1; z < 512; z++) {
                    { // old
                        accr -= cx[R + z - 1 - R].r * cx[R + z - 1 - R].br;
                        accg -= cx[R + z - 1 - R].g * cx[R + z - 1 - R].br;
                        accb -= cx[R + z - 1 - R].b * cx[R + z - 1 - R].br;
                        br -= cx[R + z - 1 - R].br;
                    }
                    { // new
                        var answ = blf(x, z + R, true);
                        accr += answ.r * answ.br;
                        accg += answ.g * answ.br;
                        accb += answ.b * answ.br;
                        br += answ.br;
                        cx[R + z + R] = answ;
                    }

                    if(br > 0) pixels[z * 512 + x] = Global.AddShade(pixels[z * 512 + x], (byte)(accr / br * q), (byte)(accg / br * q), (byte)(accb / br * q));
                }
            }

            TileDraw.very_temp_blur.Return(xdata, true);
        }

        public static void BoxBlur(int R, Span<uint> pixels, ISet<ushort> blocks, IGenData[,] neighbours) {
            int boxcount = 1;
            Span<int> boxes = stackalloc int[boxcount];
            boxesForGauss(R, boxes);
            for(int i = 0; i < boxes.Length; i++) {
                XVBlur((boxes[i] - 1) / 2, 1d / boxcount, pixels, blocks, neighbours);
            }
        }


        static void boxesForGauss(double sigma, Span<int> boxes) {
            int n = boxes.Length;
            var wIdeal = Math.Sqrt((12 * sigma * sigma / n) + 1);  // Ideal averaging filter width 
            int wl = (int)Math.Floor(wIdeal); if(wl % 2 == 0) wl--;
            int wu = wl + 2;

            var mIdeal = (12 * sigma * sigma - n * wl * wl - 4 * n * wl - 3 * n) / (-4 * wl - 4);
            int m = (int)Math.Round(mIdeal);
            // var sigmaActual = Math.sqrt( (m*wl*wl + (n-m)*wu*wu - n)/12 );

            for(int i = 0; i < n; i++) boxes[i] = i < m ? wl : wu;
        }
    }



    class DummySingleBlockSet : ISet<ushort> {
        public int Count => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        private readonly ushort value;
        public DummySingleBlockSet(ushort value) {
            this.value = value;
        }

        public bool Add(ushort item) {
            throw new NotImplementedException();
        }

        public void Clear() {
            throw new NotImplementedException();
        }

        public bool Contains(ushort item) {
            return item == value;
        }

        public void CopyTo(ushort[] array, int arrayIndex) {
            throw new NotImplementedException();
        }

        public void ExceptWith(IEnumerable<ushort> other) {
            throw new NotImplementedException();
        }

        public IEnumerator<ushort> GetEnumerator() {
            throw new NotImplementedException();
        }

        public void IntersectWith(IEnumerable<ushort> other) {
            throw new NotImplementedException();
        }

        public bool IsProperSubsetOf(IEnumerable<ushort> other) {
            throw new NotImplementedException();
        }

        public bool IsProperSupersetOf(IEnumerable<ushort> other) {
            throw new NotImplementedException();
        }

        public bool IsSubsetOf(IEnumerable<ushort> other) {
            throw new NotImplementedException();
        }

        public bool IsSupersetOf(IEnumerable<ushort> other) {
            throw new NotImplementedException();
        }

        public bool Overlaps(IEnumerable<ushort> other) {
            throw new NotImplementedException();
        }

        public bool Remove(ushort item) {
            throw new NotImplementedException();
        }

        public bool SetEquals(IEnumerable<ushort> other) {
            throw new NotImplementedException();
        }

        public void SymmetricExceptWith(IEnumerable<ushort> other) {
            throw new NotImplementedException();
        }

        public void UnionWith(IEnumerable<ushort> other) {
            throw new NotImplementedException();
        }

        void ICollection<ushort>.Add(ushort item) {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }
    }
}
