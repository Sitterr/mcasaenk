using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using WPF.ImageEffects;
using System.Windows.Shapes;

namespace Mcasaenk.UI {
    public class EButton : Button {
        Style fullimgbtn;
        public EButton() {
            fullimgbtn = this.TryFindResource("fullbtnimg") as Style;
            handleFullimgbtn();
            this.InvalidateVisual();
        }
        static EButton() {
            BorderColorProperty = DependencyProperty.Register("BorderColor", typeof(Brush), typeof(EButton));
            IsActiveProperty = DependencyProperty.Register("IsActive", typeof(bool), typeof(EButton));
            Background2Property = DependencyProperty.Register("Background2", typeof(Brush), typeof(EButton));
            BackgroundHProperty = DependencyProperty.Register("BackgroundH", typeof(Brush), typeof(EButton));
            BackgroundPProperty = DependencyProperty.Register("BackgroundP", typeof(Brush), typeof(EButton));
        }
        public static DependencyProperty BorderColorProperty;
        public Brush BorderColor {
            get { return (Brush)base.GetValue(BorderColorProperty); }
            set { base.SetValue(BorderColorProperty, value); }
        }
        public static DependencyProperty Background2Property;
        public Brush Background2 {
            get { return (Brush)base.GetValue(Background2Property); }
            set { base.SetValue(Background2Property, value); if(!IsMouseOver && !IsPressed) { handleFullimgbtn(); this.InvalidateVisual(); } }
        }
        public static DependencyProperty BackgroundHProperty;
        public Brush BackgroundH {
            get { return (Brush)base.GetValue(BackgroundHProperty); }
            set { base.SetValue(BackgroundHProperty, value); if(IsMouseOver) { handleFullimgbtn(); this.InvalidateVisual(); } }
        }
        public static DependencyProperty BackgroundPProperty;
        public Brush BackgroundP {
            get { return (Brush)base.GetValue(BackgroundPProperty); }
            set { base.SetValue(BackgroundPProperty, value); if(IsPressed) { handleFullimgbtn(); this.InvalidateVisual(); } }
        }
        public static DependencyProperty IsActiveProperty;
        public bool IsActive {
            get { return (bool)base.GetValue(IsActiveProperty); }
            set { base.SetValue(IsActiveProperty, value); { handleFullimgbtn(); this.InvalidateVisual(); } }
        }

        public Border border;
        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            this.border = (Border)this.GetTemplateChild("b");

        }

        protected override void OnInitialized(EventArgs e) {
            base.OnInitialized(e);
            handleFullimgbtn();
            this.InvalidateVisual();
        }

        void handleFullimgbtn() {
            if(this.Content is Image img) {
                if(img.Style == fullimgbtn) {
                    if(IsPressed) img.Effect = new BrightnessContrastEffect() { Brightness = -0.02, Contrast = 0.05 };
                    else if(IsMouseOver) img.Effect = new BrightnessContrastEffect() { Brightness = 0.06, Contrast = 0.05 };
                    else img.Effect = new BrightnessContrastEffect() { Brightness = 0, Contrast = 0 };
                }
            } else if(this.Content is Path path) {
                if(path.Fill is SolidColorBrush sfill) {
                    if(IsPressed) path.Fill = BackgroundP;
                    else if(IsMouseOver) path.Fill = BackgroundH;
                    else path.Fill = Background2;
                }
                return;
            }
            
            {
                if(IsPressed) {
                    this.Background = BackgroundP;
                } else if(IsMouseOver) {
                    this.Background = BackgroundH;
                } else if(IsPressed == false && IsMouseOver == false) {
                    this.Background = Background2;
                }
            }
        }
        protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e) {
            base.OnIsPressedChanged(e);
            handleFullimgbtn();
        }

        protected override void OnMouseEnter(MouseEventArgs e) {
            base.OnMouseEnter(e);
            handleFullimgbtn();
        }

        protected override void OnMouseLeave(MouseEventArgs e) {
            base.OnMouseLeave(e);
            handleFullimgbtn();
        }
    }
}
