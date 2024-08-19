using Mcasaenk.Colormaping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mcasaenk.UI
{
    public class ViewModel : INotifyPropertyChanged {
        private ObservableCollection<string> _allColormaps;
        public ObservableCollection<string> AllColormaps {
            get { return _allColormaps; }
            set {
                var __allColormaps = new List<string>();
                foreach(var dir in Directory.GetDirectories(Path.Combine(Global.App.APPFOLDER, "colormaps"))) {
                    if(Colormap.IsColormap(dir)) {
                        __allColormaps.Add(new DirectoryInfo(dir).Name);
                    }
                }
                foreach(var file in Directory.GetFiles(Path.Combine(Global.App.APPFOLDER, "colormaps"))) {
                    if(Path.GetExtension(file) != ".zip") continue;

                    if(Colormap.IsColormap(file)) {
                        __allColormaps.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }
                __allColormaps = __allColormaps.OrderByDescending(c => c switch { 
                    "default" => 4,
                    "java map" => 3,
                    "bedrock map" => 2,
                    _ => 0,
                }).ToList();

                _allColormaps = new ObservableCollection<string>(__allColormaps);

                OnPropertyChanged(nameof(AllColormaps));
            }
        }





        public ViewModel() {
            // folders
            {
                AllColormaps = null;
            }
        }

        // Implement INotifyPropertyChanged interface
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
