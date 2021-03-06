﻿using System;
using System.Linq.Expressions;

namespace Strilanc.PickleJar.Internal.Values {
    internal struct UInt32Jar : IJarMetadataInternal, IJar<UInt32> {
        private const int SerializedLength = 32 / 8;

        private readonly bool _isSystemEndian;
        public bool IsBlittable { get { return _isSystemEndian; } }
        public int? OptionalConstantSerializedLength { get { return SerializedLength; } }
        public bool CanBeFollowed { get { return true; } }

        public UInt32Jar(Endianess endianess) {
            if (endianess != Endianess.BigEndian && endianess != Endianess.LittleEndian)
                throw new ArgumentException("Unrecognized endianess", "endianess");
            var isLittleEndian = endianess == Endianess.LittleEndian;
            _isSystemEndian = isLittleEndian == BitConverter.IsLittleEndian;
        }

        public ParsedValue<UInt32> Parse(ArraySegment<byte> data) {
            if (data.Count < SerializedLength) throw new DataFragmentException();
            var value = BitConverter.ToUInt32(data.Array, data.Offset);
            if (!_isSystemEndian) value = value.ReverseBytes();
            return value.AsParsed(SerializedLength);
        }
        public byte[] Pack(UInt32 value) {
            var v = _isSystemEndian ? value : value.ReverseBytes();
            return BitConverter.GetBytes(v);
        }

        public InlinedParserComponents TryMakeInlinedParserComponents(Expression array, Expression offset, Expression count) {
            return ParserUtil.MakeInlinedNumberParserComponents<UInt32>(_isSystemEndian, array, offset, count);
        }
        public override string ToString() {
            var end = _isSystemEndian ? ""
                    : BitConverter.IsLittleEndian ? "[BigEndian]"
                    : "[LittleEndian]";
            return "UInt32" + end;
        }
    }
}
