using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for ChooseNameDialog.xaml
    /// </summary>
    public partial class ChooseNameDialog : Window {
        private bool saved = false;
        public ChooseNameDialog(string folder, string[] allowedextensions) {
            InitializeComponent();

            var notallowed = Directory.GetFiles(folder).Select(f => Path.GetFileName(f)).Concat(Directory.GetDirectories(folder).Select(d => new DirectoryInfo(d).Name));

            Regex regex = new Regex("^[\\w\\-]+" + "(" + string.Join('|', allowedextensions) + ")$");

            txt_name.TextChanged += (o, e) => {
                if(txt_name.Text == "") {
                    lbl_warn.Text = "";
                    btn_save.IsEnabled = false;
                } else if(notallowed.Contains(txt_name.Text)) {
                    lbl_warn.Text = "file with this name already exists";
                    lbl_warn.Foreground = this.TryFindResource("RED_B") as Brush;
                    btn_save.IsEnabled = false;
                } else if(regex.IsMatch(txt_name.Text) == false) {
                    lbl_warn.Text = "file name is not valid";
                    lbl_warn.Foreground = this.TryFindResource("RED_B") as Brush;
                    btn_save.IsEnabled = false;
                } else {
                    lbl_warn.Text = "";
                    btn_save.IsEnabled = true;
                }
           
            };

            btn_save.Click += (o, e) => { 
                saved = true;
                this.Close();
            };
        }

        public bool Result(out string res) {
            res = txt_name.Text;
            return saved;
        }
    }
}
