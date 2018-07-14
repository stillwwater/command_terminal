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

        public string[] Complete(string partial_word) {
            partial_word = partial_word.ToLower();
            buffer.Clear();
            string known;

            for (int i = 0; i < known_words.Count; i++) {
                known = known_words[i];

                if (known.StartsWith(partial_word)) {
                    buffer.Add(known);
                }
            }

            return buffer.ToArray();
        }
    }
}
