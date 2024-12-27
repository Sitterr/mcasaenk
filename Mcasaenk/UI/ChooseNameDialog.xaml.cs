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
        public delegate bool IsValid(string str);

        private bool saved = false;

        public ChooseNameDialog(string entityname, IsValid isStringValid, string[] takenstrings, string buttontext, string starttext = "", int windowWidth = 200) {
            InitializeComponent();
            this.Title = $"Choose {entityname}";
            this.Width = windowWidth;
            btn_save.Content = buttontext;

            txt_name.TextChanged += (o, e) => {
                if(txt_name.Text == "") {
                    lbl_warn.Text = "";
                    btn_save.IsEnabled = false;
                } else if(takenstrings.Contains(txt_name.Text)) {
                    lbl_warn.Text = $"{entityname} with this name already exist";
                    lbl_warn.Foreground = this.TryFindResource("RED_B") as Brush;
                    btn_save.IsEnabled = false;
                } else if(isStringValid(txt_name.Text) == false) {
                    lbl_warn.Text = $"{entityname} name is not valid";
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

            txt_name.Text = starttext;
            txt_name.Select(txt_name.Text.Length, 0);
            txt_name.Focus();
        }

        public bool Result(out string res) {
            res = txt_name.Text;
            return saved;
        }






        public static ChooseNameDialog AddBLockDialog(string[] takennames) {
            var minecraftdefnames = takennames.Where(x => x.StartsWith("minecraft:")).Select(x => x.Substring(10)).ToArray();
            return new ChooseNameDialog("block", (block) => block.isminecraftname(), takennames.Concat(minecraftdefnames).ToArray(), "Add", "minecraft:", 300);
        }


        public static ChooseNameDialog SaveFileDialog(string folder, string[] allowedextensions) {
            if(Path.Exists(folder) == false) Directory.CreateDirectory(folder);

            var notallowed = Global.FromFolder(folder, true, true).Select(f => Global.ReadName(f)).ToArray();

            Regex regex = new Regex("^[\\w\\-]+" + "(" + string.Join('|', allowedextensions) + ")$");

            return new ChooseNameDialog("file", (file) => regex.IsMatch(file), notallowed, "Save");
        }
    }
}
