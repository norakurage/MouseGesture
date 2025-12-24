using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public static class KeyUtils
    {
        public static string GetReadableKeyString(Keys keyData)
        {
            List<string> parts = new List<string>();
            if ((keyData & Keys.Control) == Keys.Control) parts.Add("Ctrl");
            if ((keyData & Keys.Alt) == Keys.Alt) parts.Add("Alt");
            if ((keyData & Keys.Shift) == Keys.Shift) parts.Add("Shift");

            Keys keyCode = keyData & Keys.KeyCode;
            if (keyCode != Keys.ControlKey && keyCode != Keys.ShiftKey && keyCode != Keys.Menu && keyCode != Keys.None)
            {
                if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
                {
                    parts.Add(keyCode.ToString().Replace("D", ""));
                }
                else
                {
                    parts.Add(keyCode.ToString());
                }
            }

            return string.Join("+", parts);
        }

        public static string ConvertReadableToSendKeys(string readable)
        {
            if (string.IsNullOrEmpty(readable)) return "";

            if (!readable.Contains("Ctrl") && !readable.Contains("Alt") && !readable.Contains("Shift") &&
                (readable.Contains("^") || readable.Contains("%") || readable.Contains("{")))
            {
                return readable;
            }

            string result = "";

            var parts = readable.Split('+').ToList();
            bool ctrl = parts.Remove("Ctrl");
            bool alt = parts.Remove("Alt");
            bool shift = parts.Remove("Shift");

            if (ctrl) result += "^";
            if (alt) result += "%";
            if (shift) result += "+";

            if (parts.Count > 0)
            {
                string key = parts.Last();

                if (key.Length == 1)
                {
                    result += key.ToLower();
                }
                else
                {
                    switch (key.ToUpper())
                    {
                        case "ENTER": result += "{ENTER}"; break;
                        case "RETURN": result += "{ENTER}"; break;
                        case "TAB": result += "{TAB}"; break;
                        case "ESCAPE": result += "{ESC}"; break;
                        case "ESC": result += "{ESC}"; break;
                        case "BACK": result += "{BACKSPACE}"; break;
                        case "BACKSPACE": result += "{BACKSPACE}"; break;
                        case "DELETE": result += "{DELETE}"; break;
                        default: result += "{" + key.ToUpper() + "}"; break;
                    }
                }
            }

            return result;
        }
    }
}
