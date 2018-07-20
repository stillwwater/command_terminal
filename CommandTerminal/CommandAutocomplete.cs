using System.Collections.Generic;

namespace CommandTerminal
{
    public class CommandAutocomplete
    {
        List<string> known_words = new List<string>();
        List<string> buffer = new List<string>();

        public void Register(string word) {
            known_words.Add(word.ToLower());
        }

        public string[] Complete(ref string text) {
            string partial_word = EatLastWord(ref text).ToLower();
            string known;
            buffer.Clear();

            for (int i = 0; i < known_words.Count; i++) {
                known = known_words[i];

                if (known.StartsWith(partial_word)) {
                    buffer.Add(known);
                }
            }

            return buffer.ToArray();
        }

        string EatLastWord(ref string text) {
            int last_space = text.LastIndexOf(' ');
            string result = text.Substring(last_space + 1);

            text = text.Substring(0, last_space + 1); // Remaining (keep space)
            return result;
        }
    }
}
