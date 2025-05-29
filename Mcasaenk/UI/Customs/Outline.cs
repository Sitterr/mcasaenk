using System.ComponentModel;
using System.Windows.Controls;

namespace Mcasaenk.UI {
    class Outline : Border {
        public Outline() {
            DependencyPropertyDescriptor borderThicknessDescriptor = DependencyPropertyDescriptor.FromProperty(BorderThicknessProperty, typeof(Border));
            borderThicknessDescriptor.AddValueChanged(this, BorderThicknessChanged);
        }

        private void BorderThicknessChanged(object sender, EventArgs e) {
            var t = this.BorderThickness;
            this.Padding = new System.Windows.Thickness(t.Right, t.Bottom, t.Left, t.Top);
        }
    }
}
