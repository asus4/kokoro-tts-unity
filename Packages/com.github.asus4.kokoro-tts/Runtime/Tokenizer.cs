using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;

namespace Kokoro
{
    /// <summary>
    /// https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/blob/main/tokenizer.json
    /// </summary>
    public sealed class Tokenizer
    {
        static readonly Dictionary<char, long> vocab = new()
        {
            {'$', 0},
            {';', 1},
            {':', 2},
            {',', 3},
            {'.', 4},
            {'!', 5},
            {'?', 6},
            {'\u2014', 9},
            {'\u2026', 10},
            {'\"', 11},
            {'(', 12},
            {')', 13},
            {'\u201c', 14},
            {'\u201d', 15},
            {' ', 16},
            {'\u0303', 17},
            {'\u02a3', 18},
            {'\u02a5', 19},
            {'\u02a6', 20},
            {'\u02a8', 21},
            {'\u1d5d', 22},
            {'\uab67', 23},
            {'A', 24},
            {'I', 25},
            {'O', 31},
            {'Q', 33},
            {'S', 35},
            {'T', 36},
            {'W', 39},
            {'Y', 41},
            {'\u1d4a', 42},
            {'a', 43},
            {'b', 44},
            {'c', 45},
            {'d', 46},
            {'e', 47},
            {'f', 48},
            {'h', 50},
            {'i', 51},
            {'j', 52},
            {'k', 53},
            {'l', 54},
            {'m', 55},
            {'n', 56},
            {'o', 57},
            {'p', 58},
            {'q', 59},
            {'r', 60},
            {'s', 61},
            {'t', 62},
            {'u', 63},
            {'v', 64},
            {'w', 65},
            {'x', 66},
            {'y', 67},
            {'z', 68},
            {'\u0251', 69},
            {'\u0250', 70},
            {'\u0252', 71},
            {'\u00e6', 72},
            {'\u03b2', 75},
            {'\u0254', 76},
            {'\u0255', 77},
            {'\u00e7', 78},
            {'\u0256', 80},
            {'\u00f0', 81},
            {'\u02a4', 82},
            {'\u0259', 83},
            {'\u025a', 85},
            {'\u025b', 86},
            {'\u025c', 87},
            {'\u025f', 90},
            {'\u0261', 92},
            {'\u0265', 99},
            {'\u0268', 101},
            {'\u026a', 102},
            {'\u029d', 103},
            {'\u026f', 110},
            {'\u0270', 111},
            {'\u014b', 112},
            {'\u0273', 113},
            {'\u0272', 114},
            {'\u0274', 115},
            {'\u00f8', 116},
            {'\u0278', 118},
            {'\u03b8', 119},
            {'\u0153', 120},
            {'\u0279', 123},
            {'\u027e', 125},
            {'\u027b', 126},
            {'\u0281', 128},
            {'\u027d', 129},
            {'\u0282', 130},
            {'\u0283', 131},
            {'\u0288', 132},
            {'\u02a7', 133},
            {'\u028a', 135},
            {'\u028b', 136},
            {'\u028c', 138},
            {'\u0263', 139},
            {'\u0264', 140},
            {'\u03c7', 142},
            {'\u028e', 143},
            {'\u0292', 147},
            {'\u0294', 148},
            {'\u02c8', 156},
            {'\u02cc', 157},
            {'\u02d0', 158},
            {'\u02b0', 162},
            {'\u02b2', 164},
            {'\u2193', 169},
            {'\u2192', 171},
            {'\u2197', 172},
            {'\u2198', 173},
            {'\u1d7b', 177},
        };

        static readonly ArrayPool<long> Pool = ArrayPool<long>.Shared;
        static long[] buffer = Pool.Rent(1024);

        public static ReadOnlyMemory<long> Encode(ReadOnlySpan<char> phonemes)
        {
            if (phonemes.IsEmpty)
            {
                return ReadOnlyMemory<long>.Empty;
            }

            // Re-allocate buffer if needed
            if (phonemes.Length > buffer.Length)
            {
                Pool.Return(buffer);
                buffer = Pool.Rent(phonemes.Length);
            }

            int index = 0;
            foreach (char c in phonemes)
            {
                if (vocab.TryGetValue(c, out long id))
                {
                    buffer[index++] = id;
                }
                else
                {
                    Debug.LogWarning($"Unknown character: {c}");
                }
            }
            return new ReadOnlyMemory<long>(buffer, 0, index);
        }
    }
}
