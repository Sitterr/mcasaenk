using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
