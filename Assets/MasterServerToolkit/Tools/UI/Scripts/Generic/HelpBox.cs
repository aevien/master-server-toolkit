using System;

namespace MasterServerToolkit.MasterServer
{
    [Serializable]
    public class HelpBox
    {
        public string Text { get; set; }
        public float Height { get; set; }
        public HelpBoxType Type { get; set; }

        public HelpBox(string text, float height, HelpBoxType type = HelpBoxType.Info)
        {
            Text = text;
            Height = height;
            Type = type;
        }

        public HelpBox(string text, HelpBoxType type = HelpBoxType.Info)
        {
            Text = text;
            Height = 40;
            Type = type;
        }

        public HelpBox()
        {
            Height = 40;
            Text = "";
            Type = HelpBoxType.Info;
        }
    }
}


