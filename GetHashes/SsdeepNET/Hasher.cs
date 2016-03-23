using System.Diagnostics;
using System;
using System.Text;
using System.IO;

namespace SsdeepNET
{
    public sealed class Hasher
    {
        /// <summary>
        /// Computes the fuzzy hash of the first len bytes of the buffer.
        /// </summary>
        public static string HashBuffer(byte[] buf, int len, FuzzyHashMode flags = FuzzyHashMode.None)
        {
            var ctx = new Hasher();
            ctx.Update(buf, len);
            return ctx.Digest(flags);
        }

        private uint _bhstart = 0;
        private uint _bhend = 1;
        private BlockhashContext[] _bh;
        private uint _totalSize = 0;
        private Roll _roll = new Roll();

        private Hasher()
        {
            _bh = new BlockhashContext[FuzzyConstants.NumBlockhashes];
            for (int i = 0; i < _bh.Length; i++)
                _bh[i] = new BlockhashContext();
            _bh[0].Reset(true);
        }

        private static int MemcpyEliminateSequences(byte[] dst, int pos, byte[] src, int n)
        {
            var i = 0;

            while (i < 3 && i < n)
                dst[pos++] = src[i++];

            while (i < n)
            {
                var current = src[i++];
                if (current == dst[pos - 1] && current == dst[pos - 2] && current == dst[pos - 3])
                    n--;
                else
                    dst[pos++] = current;
            }

            return n;
        }

        /// <summary>
        /// Obtain the fuzzy hash from the state.
        /// This operation does not change the state at all. It reports the hash for the
        /// concatenation of the data previously fed using fuzzy_update.
        /// </summary>
        private string Digest(FuzzyHashMode flags)
        {
            var result = new byte[FuzzyConstants.MaxResultLength];
            var pos = 0;

            uint bi = _bhstart;
            uint h = _roll.Sum();
            int i; // Exclude terminating '\0'.

            /* Initial blocksize guess. */
            while ((uint)(((uint)FuzzyConstants.MinBlocksize) << (int)(bi)) * FuzzyConstants.SpamSumLength < _totalSize)
            {
                ++bi;
                if (bi >= FuzzyConstants.NumBlockhashes)
                {
                    throw new OverflowException("EOVERFLOW");
                }
            }
            /* Adapt blocksize guess to actual digest length. */
            while (bi >= _bhend)
            {
                --bi;
            }
            while (bi > _bhstart && _bh[bi].DLen < FuzzyConstants.SpamSumLength / 2)
            {
                --bi;
            }

            var actualBlockSize = (((uint)FuzzyConstants.MinBlocksize) << (int)(bi));
            var blockSizeChars = actualBlockSize.ToString().ToCharArray();
            i = blockSizeChars.Length;
            for (int j = 0; j < i; j++)
                result[j + pos] = (byte)blockSizeChars[j];
            result[i++] = (byte)':';

            pos += i;
            i = (int)_bh[bi].DLen;

            if (flags.HasFlag(FuzzyHashMode.EliminateSequences))
                i = MemcpyEliminateSequences(result, pos, _bh[bi].Digest, i);
            else
                Array.Copy(_bh[bi].Digest, 0, result, pos, i);

            pos += i;
            if (h != 0)
            {
                var base64Val = FuzzyConstants.Base64[_bh[bi].H % 64];
                result[pos] = base64Val;
                if (!flags.HasFlag(FuzzyHashMode.EliminateSequences) || i < 3 || base64Val != result[pos - 1] || base64Val != result[pos - 2] || base64Val != result[pos - 3])
                    ++pos;
            }
            else if (_bh[bi].Digest[i] != '\0')
            {
                var digestVal = _bh[bi].Digest[i];
                result[pos] = digestVal;
                if (!flags.HasFlag(FuzzyHashMode.EliminateSequences) || i < 3 || digestVal != result[pos - 1] || digestVal != result[pos - 2] || digestVal != result[pos - 3])
                    ++pos;
            }
            result[pos++] = (byte)':';

            if (bi < _bhend - 1)
            {
                ++bi;
                i = (int)_bh[bi].DLen;
                if (!flags.HasFlag(FuzzyHashMode.DoNotTruncate) && i > FuzzyConstants.SpamSumLength / 2 - 1)
                {
                    i = FuzzyConstants.SpamSumLength / 2 - 1;
                }

                if (flags.HasFlag(FuzzyHashMode.EliminateSequences))
                    i = MemcpyEliminateSequences(result, pos, _bh[bi].Digest, i);
                else
                    Array.Copy(_bh[bi].Digest, 0, result, pos, i);

                pos += i;
                if (h != 0)
                {
                    h = flags.HasFlag(FuzzyHashMode.DoNotTruncate) ? _bh[bi].H : _bh[bi].HalfH;
                    var base64Val = FuzzyConstants.Base64[h % 64];
                    result[pos] = base64Val;
                    if (!flags.HasFlag(FuzzyHashMode.EliminateSequences) || i < 3 || base64Val != result[pos - 1] || base64Val != result[pos - 2] || base64Val != result[pos - 3])
                        ++pos;
                }
                else
                {
                    i = flags.HasFlag(FuzzyHashMode.DoNotTruncate) ? _bh[bi].Digest[_bh[bi].DLen] : _bh[bi].HalfDigest;
                    if (i != '\0')
                    {
                        result[pos] = (byte)i;
                        if (!flags.HasFlag(FuzzyHashMode.EliminateSequences) || i < 3 || i != result[pos - 1] || i != result[pos - 2] || i != result[pos - 3])
                            ++pos;
                    }
                }
            }
            else if (h != 0)
            {
                result[pos++] = FuzzyConstants.Base64[_bh[bi].H % 64];
                /* No need to bother with FuzzyHashMode.EliminateSequences, because this
                 * digest has length 1. */
            }

            return Encoding.ASCII.GetString(result, 0, pos);
        }

        private void TryForkBlockhash()
        {
            BlockhashContext obh;
            BlockhashContext nbh;
            if (_bhend >= FuzzyConstants.NumBlockhashes)
                return;
            obh = _bh[_bhend - 1];
            nbh = _bh[_bhend];
            nbh.H = obh.H;
            nbh.HalfH = obh.HalfH;
            nbh.Digest[0] = 0;
            nbh.HalfDigest = 0;
            nbh.DLen = 0;
            ++_bhend;
        }

        private void TryReduceBlockhash()
        {
            if (_bhend - _bhstart < 2)
                /* Need at least two working hashes. */
                return;
            if ((uint)(((uint)FuzzyConstants.MinBlocksize) << (int)(_bhstart)) * FuzzyConstants.SpamSumLength >= _totalSize)
                /* Initial blocksize estimate would select this or a smaller
                 * blocksize. */
                return;
            if (_bh[_bhstart + 1].DLen < FuzzyConstants.SpamSumLength / 2)
                /* Estimate adjustment would select this blocksize. */
                return;
            /* At this point we are clearly no longer interested in the
             * start_blocksize. Get rid of it. */
            ++_bhstart;
        }

        private void EngineStep(byte c)
        {
            uint h;
            uint i;
            /* At each character we update the rolling hash and the normal hashes.
             * When the rolling hash hits a reset value then we emit a normal hash
             * as a element of the signature and reset the normal hash. */
            _roll.Hash(c);
            h = _roll.Sum();

            for (i = _bhstart; i < _bhend; ++i)
                _bh[i].Hash(c);

            for (i = _bhstart; i < _bhend; ++i)
            {
                /* With growing blocksize almost no runs fail the next test. */
                if (h % (((uint)FuzzyConstants.MinBlocksize) << (int)(i)) != (((uint)FuzzyConstants.MinBlocksize) << (int)(i)) - 1)
                    /* Once this condition is false for one bs, it is
                     * automatically false for all further bs. I.e. if
                     * h === -1 (mod 2*bs) then h === -1 (mod bs). */
                    break;
                /* We have hit a reset point. We now emit hashes which are
                 * based on all characters in the piece of the message between
                 * the last reset point and this one */
                if (0 == _bh[i].DLen)
                {
                    /* Can only happen 30 times. */
                    /* First step for this blocksize. Clone next. */
                    this.TryForkBlockhash();
                }
                _bh[i].Digest[_bh[i].DLen] = FuzzyConstants.Base64[_bh[i].H % 64];
                _bh[i].HalfDigest = FuzzyConstants.Base64[_bh[i].HalfH % 64];
                if (_bh[i].DLen < FuzzyConstants.SpamSumLength - 1)
                {
                    /* We can have a problem with the tail overflowing. The
                     * easiest way to cope with this is to only reset the
                     * normal hash if we have room for more characters in
                     * our signature. This has the effect of combining the
                     * last few pieces of the message into a single piece
                     * */
                    _bh[i].Reset();
                }
                else
                {
                    this.TryReduceBlockhash();
                }
            }
        }

        /// <summary>
        /// Feed the data contained in the given buffer to the state.
        /// </summary>
        private void Update(byte[] buffer, int len)
        {
            _totalSize += (uint)buffer.Length;

            for (int i = 0; i < len; i++)
                this.EngineStep(buffer[i]);
        }
    }
}