using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Mcasaenk.UI {
    /// <summary>
    /// Interaction logic for BinaryBlockGroup.xaml
    /// </summary>
    public partial class BinaryBlockGroupWindow : Window {

        public enum Group { Def, This, AlwaysThis, Other }

        private bool saved = false;
        private readonly IEnumerable<(string name, bool important, Group group)> startingstate;
        public BinaryBlockGroupWindow(string name_selected, IEnumerable<(string name, bool important, Group group)> blocks) {
            InitializeComponent();
            grid_selected.Columns[0].Header = $"{name_selected.FirstCharToUpper()} blocks";

            this.Loaded += (_, _) => {
                btn_undo.Margin = new Thickness(btn_undo.Margin.Left + btn_finish.ActualWidth + btn_undo.ActualWidth + 20, btn_undo.Margin.Top, btn_undo.Margin.Right, btn_undo.Margin.Bottom);
            };

            if(blocks.All(f => f.important)) { 
                toggle_showall.Visibility = Visibility.Collapsed;
                lbl_showall.Visibility = Visibility.Collapsed;
            }
            toggle_showall.Checked += (_, _) => FilterLeft();
            toggle_showall.Unchecked += (_, _) => FilterLeft();

            btn_finish.Click += (_, _) => {
                saved = true;
                this.Close();
            };

            btn_undo.Click += (_, _) => {
                SetUp();
            };

            btn_moveright.SetBinding(Button.IsEnabledProperty, new Binding {
                Source = grid_availabe,
                Path = new PropertyPath("SelectedItems.Count"),
                Converter = new GreaterThanConverter(),
                ConverterParameter = 0
            });
            btn_moveleft.SetBinding(Button.IsEnabledProperty, new Binding {
                Source = grid_selected,
                Path = new PropertyPath("SelectedItems.Count"),
                Converter = new GreaterThanConverter(),
                ConverterParameter = 0
            });

            btn_moveright.Click += (_, _) => {
                var tobemoved = grid_availabe.SelectedItems.Cast<BinaryBlockRow>().Where(x => x.CanMove).ToArray();

                grid_availabe.ItemsSource = grid_availabe.ItemsSource.Cast<BinaryBlockRow>().Where(b => !tobemoved.Any(m => m.BlockName == b.BlockName));
                grid_selected.ItemsSource = grid_selected.ItemsSource.Cast<BinaryBlockRow>().Concat(tobemoved);

                grid_availabe.SelectedItems.Clear();
                grid_selected.SortByColumn("BlockName", ListSortDirection.Ascending);
            };

            btn_moveleft.Click += (_, _) => {
                var tobemoved = grid_selected.SelectedItems.Cast<BinaryBlockRow>().Where(x => x.CanMove).ToArray();

                grid_selected.ItemsSource = grid_selected.ItemsSource.Cast<BinaryBlockRow>().Where(b => !tobemoved.Any(m => m.BlockName == b.BlockName));
                grid_availabe.ItemsSource = grid_availabe.ItemsSource.Cast<BinaryBlockRow>().Concat(tobemoved);

                grid_selected.SelectedItems.Clear();
                grid_availabe.SortByColumn("BlockName", ListSortDirection.Ascending);
            };

            txt_searchleft.TextChanged += (_, _) => FilterLeft();

            txt_searchright.TextChanged += (_, _) => FilterRight();

            this.startingstate = blocks;
            SetUp();
        }

        private void FilterLeft() {
            grid_availabe.Items.Filter = ((Predicate<object>)(item => ((BinaryBlockRow)item).BlockName.Contains(txt_searchleft.Text)))
                .And(item => ((BinaryBlockRow)item).important || toggle_showall.IsChecked.Value);
        }

        private void FilterRight() {
            grid_selected.Items.Filter = ((Predicate<object>)(item => ((BinaryBlockRow)item).BlockName.Contains(txt_searchright.Text)));
        }

        public void SetUp() {
            grid_availabe.ItemsSource = startingstate.Where(x => x.group != Group.This).Select(x => new BinaryBlockRow(x.name, x.important, x.group == Group.Def)).ToArray();
            grid_selected.ItemsSource = startingstate.Where(x => x.group == Group.This || x.group == Group.AlwaysThis).Select(x => new BinaryBlockRow(x.name, x.important, x.group == Group.This)).ToArray();
            toggle_showall.IsChecked = false;
            txt_searchleft.Text = "";
            txt_searchright.Text = "";
        }

        public bool Result(out List<(string name, Group group)> blocks) {
            blocks =
            [
                .. grid_availabe.ItemsSource.Cast<BinaryBlockRow>().Select(x => (x.BlockName, x.CanMove ? Group.Def : Group.Other)),
                .. grid_selected.ItemsSource.Cast<BinaryBlockRow>().Select(x => (x.BlockName, Group.This)),
            ];
            return saved;
        }
    }

    public class BinaryBlockRow : INotifyPropertyChanged {
        private bool init = true;
        public readonly bool important;
        public BinaryBlockRow(string blockname, bool important, bool canmove) {
            this.BlockName = blockname;
            this.important = important;
            this.CanMove = canmove;

            init = false;
        }
        public BinaryBlockRow(BinaryBlockRow row) { 
            this.BlockName = row.BlockName;
            this.CanMove = row.CanMove;

            init = false;
        }

        
        private string blockname;
        public string BlockName {
            get => blockname;
            set {
                blockname = value;
                OnPropertyChanged(nameof(BlockName));
            }
        }

        public bool CanMove { get; private set; }




        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) {
            if(init) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
