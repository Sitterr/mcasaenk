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
                foreach(var fileorfolder in Global.FromFolder(Path.Combine(Global.App.APPFOLDER, "colormaps"), true, true)) {
                    __allColormaps.Add(Global.ReadName(fileorfolder));
                }

                __allColormaps = __allColormaps.OrderByDescending(c => c switch { 
                    "default" => 4,
                    "java map" => 3,
                    "bedrock map" => 2,
                    "betaplus" => 1,
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
