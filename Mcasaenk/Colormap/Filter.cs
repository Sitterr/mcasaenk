using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Mcasaenk.Colormaping {
    public class Filter : GroupElement<Filter> {
        public bool caneditsettings { get; private set; }
        public bool visible { get; private set; }

        public Filter(GroupManager<Filter> groupManager, string name, bool visible = true, bool caneditsettings = true) : base(groupManager, name) {
            this.groupManager = groupManager;
            this.visible = visible;
            this.caneditsettings = caneditsettings;

            ABSORBTION = 15;
        }


        public override void InternalSetFromBack() {
            if(ABSORBTION != Absorbtion) ABSORBTION = Absorbtion;
        }
        public override void InternalReset() {
            Absorbtion = ABSORBTION;
        }
        public override bool InternalChangedBack() =>
                   ABSORBTION != Absorbtion;


        private int absorbtion, absorbtion_back;
        [JsonIgnore]
        public int Absorbtion {
            get => absorbtion_back;
            set {
                if(absorbtion_back == value) return;

                absorbtion_back = value;
                OnAutoChange(nameof(Absorbtion));
                if(Global.App.OpenedSave == null || SettingsHub == null) {
                    absorbtion = value;
                    OnAutoChange(nameof(ABSORBTION));
                }
            }
        }
        public int ABSORBTION { get => absorbtion; set { absorbtion = value; Absorbtion = value; OnHardChange(nameof(ABSORBTION)); } }

    }
}
