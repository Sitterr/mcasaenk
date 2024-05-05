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

namespace Mcasaenk.UI {
    public class EButton : Button {
        Style fullimgbtn;
        public EButton() {
            fullimgbtn = this.TryFindResource("fullbtnimg") as Style;
        }
        static EButton() {
            BorderColorProperty = DependencyProperty.Register("BorderColor", typeof(Brush), typeof(EButton));
            IsActiveProperty = DependencyProperty.Register("IsActive", typeof(bool), typeof(EButton));
        }
        public static DependencyProperty BorderColorProperty;
        public Brush BorderColor {
            get { return (Brush)base.GetValue(BorderColorProperty); }
            set { base.SetValue(BorderColorProperty, value); }
        }
        public static DependencyProperty IsActiveProperty;
        public bool IsActive {
            get { return (bool)base.GetValue(IsActiveProperty); }
            set { base.SetValue(IsActiveProperty, value); }
        }

        public Border border;
        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            this.border = (Border)this.GetTemplateChild("b");
        }



        void handleFullimgbtn() {
            if(this.Content is Image img) {
                if(img.Style == fullimgbtn) {
                    if(IsPressed) img.Effect = new BrightnessContrastEffect() { Brightness = -0.02, Contrast = 0.05 };
                    else if(IsMouseOver) img.Effect = new BrightnessContrastEffect() { Brightness = 0.06, Contrast = 0.05 };
                    else img.Effect = new BrightnessContrastEffect() { Brightness = 0, Contrast = 0 };
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
