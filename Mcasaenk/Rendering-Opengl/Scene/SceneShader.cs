using Mcasaenk.Colormaping;
using Mcasaenk.Rendering;
using Mcasaenk.Resources;
using Mcasaenk.Opengl_rendering.Kawase;
using Mcasaenk.UI.Canvas;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Mcasaenk.Opengl_rendering.Scene {
    public class SceneShader : Shader {
        public int fbo;
        private readonly int VAO = 0;
        public SceneShader(int VAO) : base(ResourceMapping.tile_vert, ResourceMapping.scene_frag) {
            this.VAO = VAO;

            fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        }
        public override void Dispose() {
            base.Dispose();
            GL.DeleteFramebuffer(fbo);
        }

        public void Use(WorldPosition screen, GenDataTileMap tilemap, KawaseTexture tex, WorldPosition map_screenshot, int[] blendtints, int kawaseR, int outputtexture) {
            int w = (int)Math.Ceiling(screen.Width * screen.InSimZoom), h = (int)Math.Ceiling(screen.Height * screen.InSimZoom);
            
            GL.Viewport(0, 0, w, h);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, outputtexture, 0);

            GL.ClearColor(Color4.Transparent); GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(Handle);

            // vertex uniforms
            {
                GL.Uniform1(GL.GetUniformLocation(Handle, "tv_zoom"), (float)screen.InSimZoom);
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_resolution"), w, h);
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_cam"), (int)Math.Floor(screen.Start.X), (int)Math.Floor(screen.Start.Y));
                GL.Uniform2(GL.GetUniformLocation(Handle, "tv_regSize"), 512, 512);
            }

            GL.BindVertexArray(VAO);

            if(Global.App.Colormap != null && tilemap != null) {
                // fragment uniforms
                {
                    // blured data
                    {
                        GL.ActiveTexture(TextureUnit.Texture8);
                        GL.BindTexture(TextureTarget.Texture2D, tex.meanheight_oceandepth);
                        GL.Uniform1(GL.GetUniformLocation(Handle, "blur_meanheight_oceandepth"), 8);

                        GL.ActiveTexture(TextureUnit.Texture9);
                        GL.BindTexture(TextureTarget.Texture2DArray, tex.tints);
                        GL.Uniform1(GL.GetUniformLocation(Handle, "blur_tintcolors"), 9);

                        GL.Uniform1(GL.GetUniformLocation(Handle, "blur_tintcount"), blendtints.Length);
                        GL.Uniform1(GL.GetUniformLocation(Handle, "blur_blendtints"), blendtints.Length, blendtints);
                        GL.Uniform1(GL.GetUniformLocation(Handle, "blur_R"), kawaseR);
                    }

                    Global.App.Colormap.TintManager.GetTexture().Use((int)TextureUnit.Texture11);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "tintpalette"), 11);

                    Global.App.Colormap.BlocksManager.GetTexture().Use((int)TextureUnit.Texture10);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "palette"), 10);
                }

                // settings
                {
                    GL.Uniform1(GL.GetUniformLocation(Handle, "CONTRAST"), (float)Global.Settings.Contrast);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "SUN_LIGHT"), Global.Settings.SUN_LIGHT / 15f);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "BLOCK_LIGHT"), Global.Settings.BLOCK_LIGHT / 15f);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "WATER_TRANSPARENCY"), (float)Global.Settings.WATER_TRANSPARENCY);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "WATER_SMART_SHADE"), Global.Settings.WATER_SMART_SHADE ? 1 : 0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "SHADE3D"), Global.Settings.SHADE3D ? 1 : 0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "STATIC_SHADE"), Global.Settings.STATIC_SHADE ? 1 : 0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "ADEG"), (float)Global.Settings.ADEG);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "SHADETYPE"), (int)Global.Settings.SHADETYPE);

                    GL.Uniform1(GL.GetUniformLocation(Handle, "Jmap_REVEALED_WATER"), (float)Global.Settings.Jmap_REVEALED_WATER);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "Jmap_WATER_MODE"), (int)Global.Settings.Jmap_WATER_MODE);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "Jmap_MAP_DIRECTION"), (int)Global.Settings.Jmap_MAP_DIRECTION);
                }


                if(map_screenshot != default) {
                    GL.Uniform1(GL.GetUniformLocation(Handle, "MAPAPPROXIMATIONALGO"), (int)Global.Settings.MAPAPPROXIMATIONALGO);

                    GL.Uniform4(GL.GetUniformLocation(Handle, "map_screenshot"), (float)map_screenshot.Start.X, (float)map_screenshot.Start.Y, (float)map_screenshot.Width, (float)map_screenshot.Height);

                    if(CheckMapColor()) {
                        GL.Uniform1(GL.GetUniformLocation(Handle, "map_screenshot_mapcolors"), mapcolors.Length, mapcolors);
                    }
                } else {
                    GL.Uniform4(GL.GetUniformLocation(Handle, "map_screenshot"), 0f, 0f, 0f, 0f);
                }

                // fragment uniforms
                foreach (var reg in tilemap.GetVisibleTilesPositions(screen)) {
                    var tile = tilemap?.GetTile(reg);
                    if(tile == null) continue;

                    tile.GetTexture().Use((int)TextureUnit.Texture0);
                    GL.Uniform1(GL.GetUniformLocation(Handle, "region0"), 0);

                    //tilemap.GetTile(reg + new Point2i(1, 0))?.GetTexture().Use((int)TextureUnit.Texture1); GL.Uniform1(GL.GetUniformLocation(Handle, "regionr"), 1);
                    //tilemap.GetTile(reg - new Point2i(1, 0))?.GetTexture().Use((int)TextureUnit.Texture2); GL.Uniform1(GL.GetUniformLocation(Handle, "regionl"), 2);
                    //tilemap.GetTile(reg + new Point2i(0, 1))?.GetTexture().Use((int)TextureUnit.Texture3); GL.Uniform1(GL.GetUniformLocation(Handle, "regiond"), 3);
                    //tilemap.GetTile(reg - new Point2i(0, 1))?.GetTexture().Use((int)TextureUnit.Texture4); GL.Uniform1(GL.GetUniformLocation(Handle, "regiont"), 4);

                    GL.Uniform2(GL.GetUniformLocation(Handle, "tv_glR"), reg.X, reg.Z);
                    GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
                }
            }

        }

        uint[] mapcolors = new uint[64];
        private int version = -1;
        bool CheckMapColor() {
            if(version != Global.App.OpenedSave.levelDatInfo.version_id) {
                version = Global.App.OpenedSave.levelDatInfo.version_id;

                int jp = 0;
                for(int i = 0; i < JavaMapColors.colors.Length; i++) {
                    if(JavaMapColors.colors[i].version > version) continue;

                    mapcolors[jp++] = JavaMapColors.colors[i].V255.color.ToUInt();
                }
                for(; jp < mapcolors.Length; jp++) {
                    mapcolors[jp] = 0x00000000;
                }

                return true;
            }

            return false;
        }
    }
}
