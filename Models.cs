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
        public List<GestureMap> Gestures { get; set; } = new List<GestureMap>();
    }
}
