using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SsdeepNET
{
    public sealed class Comparer
    {
        /// <summary>
        /// Given two spamsum strings return a value indicating the degree to which they match
        /// </summary>
        /// <returns>
        /// A value from zero to 100 indicating the match score of the two signatures
        /// </returns>
        public static int Compare(string str1, string str2)
        {
            int block_size1;
            int block_size2;
            int score = 0;
            char[] s1_1;
            char[] s1_2;
            char[] s2_1;
            char[] s2_2;

            if (str1 == null || str2 == null)
                throw new ArgumentNullException();

            // each spamsum is prefixed by its block size
            var colon1Pos = str1.IndexOf(':');
            var colon2Pos = str2.IndexOf(':');
            if (colon1Pos == -1 || colon2Pos == -1 ||
                !Int32.TryParse(str1.Substring(0, colon1Pos), out block_size1) ||
                !Int32.TryParse(str2.Substring(0, colon2Pos), out block_size2) ||
                block_size1 < 1 || block_size2 < 1)
                throw new Exception("Badly formed input");

            // if the blocksizes don't match then we are comparing
            // apples to oranges. This isn't an 'error' per se. We could
            // have two valid signatures, but they can't be compared.
            if (block_size1 != block_size2 && block_size1 != block_size2 * 2 && block_size2 != block_size1 * 2)
                throw new Exception("Given signatures cannot be compared");

            var colon12Pos = str1.IndexOf(':', colon1Pos + 1);
            var colon22Pos = str2.IndexOf(':', colon2Pos + 1);

            if (colon12Pos == -1 || colon22Pos == -1)
                throw new Exception("Badly formed input");

            // Chop the second string at the comma--just before the filename.
            // If the strings don't have a comma (i.e. don't have a filename)
            // that's ok. It's not an error. This function can be called on
            // signatures which don't have filenames attached.
            // We also don't have to advance past the comma however. We don't care
            // about the filename
            var comma1Pos = str1.IndexOf(',', colon12Pos + 1);
            var comma2Pos = str2.IndexOf(',', colon22Pos + 1);

            s1_1 = str1.ToCharArray(colon1Pos + 1, colon12Pos - colon1Pos - 1);
            s2_1 = str2.ToCharArray(colon2Pos + 1, colon22Pos - colon2Pos - 1);

            s1_2 = str1.ToCharArray(colon12Pos + 1, comma1Pos == -1 ? str1.Length - colon12Pos - 1 : comma1Pos - colon12Pos - 1);
            s2_2 = str2.ToCharArray(colon22Pos + 1, comma2Pos == -1 ? str2.Length - colon22Pos - 1 : comma2Pos - colon22Pos - 1);

            if (s1_1.Length == 0 || s2_1.Length == 0 || s1_2.Length == 0 || s2_2.Length == 0)
                throw new Exception("Badly formed input");

            // there is very little information content is sequences of
            // the same character like 'LLLLL'. Eliminate any sequences
            // longer than 3. This is especially important when combined
            // with the has_common_substring() test below.
            // NOTE: This function duplciates str1 and str2
            s1_1 = EliminateSequences(s1_1);
            s2_1 = EliminateSequences(s2_1);
            s1_2 = EliminateSequences(s1_2);
            s2_2 = EliminateSequences(s2_2);

            // Now that we know the strings are both well formed, are they
            // identical? We could save ourselves some work here
            if (block_size1 == block_size2 && s1_1.Length == s2_1.Length)
            {
                bool matched = true;
                for (int i = 0; i < s1_1.Length; i++)
                {
                    if (s1_1[i] != s2_1[i])
                    {
                        matched = false;
                        break;
                    }
                }
                if (matched)
                    return 100;
            }

            // each signature has a string for two block sizes. We now
            // choose how to combine the two block sizes. We checked above
            // that they have at least one block size in common
            if (block_size1 == block_size2)
            {
                int score1;
                int score2;
                score1 = ScoreStrings(s1_1, s2_1, block_size1);
                score2 = ScoreStrings(s1_2, s2_2, block_size1 * 2);
                score = Math.Max(score1, score2);
            }
            else if (block_size1 == block_size2 * 2)
            {
                score = ScoreStrings(s1_1, s2_2, block_size1);
            }
            else
            {
                score = ScoreStrings(s1_2, s2_1, block_size2);
            }

            return score;
        }

        // eliminate sequences of longer than 3 identical characters. These
        // sequences contain very little information so they tend to just bias
        // the result unfairly
        internal static char[] EliminateSequences(char[] str)
        {
            var ret = new char[str.Length];
            uint i;
            uint j;

            var len = str.Length;
            for (i = 0; i < 3 && i < len; i++)
                ret[i] = str[i];
            
            if (len < 3)
                return ret;

            for (i = j = 3; i < len; i++)
            {
                var current = str[i];
                if (current != str[i - 1] || current != str[i - 2] || current != str[i - 3])
                {
                    ret[j++] = str[i];
                }
            }

            var cutted = new char[j];
            Array.Copy(ret, cutted, j);

            return ret;
        }

        //
        // this is the low level string scoring algorithm. It takes two strings
        // and scores them on a scale of 0-100 where 0 is a terrible match and
        // 100 is a great match. The block_size is used to cope with very small
        // messages.
        //
        private static int ScoreStrings(char[] s1, char[] s2, int block_size)
        {
            int score;
            int len1;
            int len2;

            len1 = s1.Length;
            len2 = s2.Length;

            if (len1 > FuzzyConstants.SpamSumLength || len2 > FuzzyConstants.SpamSumLength)
            {
                // not a real spamsum signature?
                return 0;
            }

            // the two strings must have a common substring of length
            // ROLLING_WINDOW to be candidates
            if (!HasCommonSubstring(s1, s2))
                return 0;

            // compute the edit distance between the two strings. The edit distance gives
            // us a pretty good idea of how closely related the two strings are
            score = EditDistance.Compute(s1, s2);

            // scale the edit distance by the lengths of the two
            // strings. This changes the score to be a measure of the
            // proportion of the message that has changed rather than an
            // absolute quantity. It also copes with the variability of
            // the string lengths.
            score = (score * FuzzyConstants.SpamSumLength) / (len1 + len2);

            // at this stage the score occurs roughly on a 0-64 scale,
            // with 0 being a good match and 64 being a complete
            // mismatch

            // rescale to a 0-100 scale (friendlier to humans)
            score = (100 * score) / 64;

            // it is possible to get a score above 100 here, but it is a
            // really terrible match
            if (score >= 100)
            {
                return 0;
            }

            // now re-scale on a 0-100 scale with 0 being a poor match and
            // 100 being a excellent match.
            score = 100 - score;

            // when the blocksize is small we don't want to exaggerate the match size
            var matchSize = block_size / FuzzyConstants.MinBlocksize * Math.Min(len1, len2);
            if (score > matchSize)
                score = matchSize;
            return score;
        }

        //
        // We only accept a match if we have at least one common substring in
        // the signature of length ROLLING_WINDOW. This dramatically drops the
        // false positive rate for low score thresholds while having
        // negligable affect on the rate of spam detection.
        //
        // return 1 if the two strings do have a common substring, 0 otherwise
        //
        private static bool HasCommonSubstring(char[] s1, char[] s2)
        {
            int i;
            int j;
            int num_hashes;
            uint[] hashes = new uint[FuzzyConstants.SpamSumLength];

            // there are many possible algorithms for common substring
            // detection. In this case I am re-using the rolling hash code
            // to act as a filter for possible substring matches

            // first compute the windowed rolling hash at each offset in
            // the first string
            var state = new Roll();

            for (i = 0; i < s1.Length && s1[i] != '\0'; i++)
            {
                state.Hash((byte)s1[i]);
                hashes[i] = state.Sum();
            }
            num_hashes = i;

            state = new Roll();

            // now for each offset in the second string compute the
            // rolling hash and compare it to all of the rolling hashes
            // for the first string. If one matches then we have a
            // candidate substring match. We then confirm that match with
            // a direct string comparison 
            for (i = 0; i < s2.Length && s2[i] != '\0'; i++)
            {
                state.Hash((byte)s2[i]);
                uint h = state.Sum();
                if (i < FuzzyConstants.RollingWindow - 1)
                    continue;
                for (j = FuzzyConstants.RollingWindow - 1; j < num_hashes; j++)
                {
                    if (hashes[j] != 0 && hashes[j] == h)
                    {
                        // we have a potential match - confirm it
                        var s2StartPos = i - FuzzyConstants.RollingWindow + 1;
                        int len = 0;
                        while (len + s2StartPos < s2.Length && s2[len + s2StartPos] != '\0')
                            len++;
                        if (len < FuzzyConstants.RollingWindow)
                            continue;

                        var matched = true;
                        var s1StartPos = j - FuzzyConstants.RollingWindow + 1;
                        for (int pos = 0; pos < FuzzyConstants.RollingWindow; pos++)
                        {
                            var s1char = s1[s1StartPos + pos];
                            var s2char = s2[s2StartPos + pos];
                            if (s1char != s2char)
                            {
                                matched = false;
                                break;
                            }

                            if (s1char == '\0')
                                break;
                        }

                        if (matched)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
