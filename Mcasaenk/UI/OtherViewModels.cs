using Mcasaenk.Rendering;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.UI {
    public class ViewModel : INotifyPropertyChanged {
        private ObservableCollection<string> _allColormaps;
        public ObservableCollection<string> AllColormaps {
            get { return _allColormaps; }
            set {
                _allColormaps = value;
                OnPropertyChanged(nameof(AllColormaps));
            }
        }

        public ViewModel() {
            // folders
            {
                AllColormaps = new ObservableCollection<string>();
                foreach(var dir in Directory.GetDirectories(Path.Combine(Global.App.APPFOLDER, "colormaps"))) {
                    if(Colormap.IsColormap(dir)) { 
                        AllColormaps.Add(new DirectoryInfo(dir).Name);
                    }
                }
            }
        }

        // Implement INotifyPropertyChanged interface
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
