﻿using System;
using System.Linq.Expressions;

namespace ParserGenerator {
    public struct CountPrefixedRepeatParser<T> : IParser<T[]> {
        public bool IsBlittable { get { return false; } }
        public int? OptionalConstantSerializedLength { get { return null; } }
        public Expression TryParseInline(Expression array, Expression offset, Expression count) {
            return null;
        }

        private readonly IParser<int> _counter; 
        private readonly IArrayParser<T> _repeatParser;

        public CountPrefixedRepeatParser(IParser<int> counter, IArrayParser<T> subParser) {
            this._counter = counter;
            this._repeatParser = subParser;
        }

        public ParsedValue<T[]> Parse(ArraySegment<byte> data) {
            var count = _counter.Parse(data);
            var array = _repeatParser.Parse(data.Skip(count.Consumed), count.Value);
            return new ParsedValue<T[]>(array.Value, count.Consumed + array.Consumed);
        }
    }
}
