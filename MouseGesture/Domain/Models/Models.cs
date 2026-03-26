using System.Collections.Generic;
using System.ComponentModel;

namespace WindowsFormsApp1
{
    public class GestureMap
    {
        [DisplayName("名前")]
        public string Name { get; set; }
        
        [DisplayName("ジェスチャー")]
        public string Gesture { get; set; }
        
        [DisplayName("キー割り当て")]
        public string KeyBinding { get; set; }
    }
    
    public class AppProfile
    {
        public string AppPath { get; set; } // Null or Empty for Global
        public string AppName { get; set; }
        public bool Disabled { get; set; }
        public List<GestureMap> Gestures { get; set; } = new List<GestureMap>();

        [System.Xml.Serialization.XmlIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public Dictionary<string, GestureMap> GestureDict { get; set; } = new Dictionary<string, GestureMap>();

        public void RebuildDictionary()
        {
            GestureDict.Clear();
            if (Gestures != null)
            {
                foreach (var g in Gestures)
                {
                    if (!string.IsNullOrEmpty(g.Gesture))
                    {
                        GestureDict[g.Gesture] = g;
                    }
                }
            }
        }
    }

    public class GroupProfile
    {
        public string GroupName { get; set; }
        public List<string> TargetAppPaths { get; set; } = new List<string>();
        public List<GestureMap> Gestures { get; set; } = new List<GestureMap>();

        [System.Xml.Serialization.XmlIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public Dictionary<string, GestureMap> GestureDict { get; set; } = new Dictionary<string, GestureMap>();

        public void RebuildDictionary()
        {
            GestureDict.Clear();
            if (Gestures != null)
            {
                foreach (var g in Gestures)
                {
                    if (!string.IsNullOrEmpty(g.Gesture))
                    {
                        GestureDict[g.Gesture] = g;
                    }
                }
            }
        }
    }

    public class GestureConfig
    {
        public bool AutoStart { get; set; } = false;
        public List<GestureMap> GlobalGestures { get; set; } = new List<GestureMap>();
        public List<AppProfile> AppProfiles { get; set; } = new List<AppProfile>();
        public List<GroupProfile> GroupProfiles { get; set; } = new List<GroupProfile>();
    }
}
