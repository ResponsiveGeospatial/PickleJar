﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Strilanc.PickleJar;
using Strilanc.PickleJar.Internal;

[TestClass]
public class ApiTest {
    private static readonly IEnumerable<object> ApiJarGetters = typeof (Jar)
        .GetProperties(BindingFlags.Static | BindingFlags.Public)
        .Select(e => e.GetValue(null))
        .ToArray();
    private static readonly IEnumerable<MethodInfo> ApiJarMakers = typeof(Jar)
        .GetMethods(BindingFlags.Static | BindingFlags.Public)
        // just assume type parameters should be Int32
        .Select(e => !e.IsGenericMethodDefinition ? e : e.MakeGenericMethod(e.GetGenericArguments().Select(_ => typeof (int)).ToArray()))
        .ToArray();

    private static dynamic[] ChooseTestValues(Type type) {
        if (type == typeof(int)) return new dynamic[] { -100, -1, 0, 1, 2, 100 };
        if (type == typeof(sbyte)) return ChooseTestValues(typeof(int)).Select(e => (dynamic)(sbyte)(int)e).ToArray();
        if (type == typeof(short)) return ChooseTestValues(typeof(int)).Select(e => (dynamic)(short)(int)e).ToArray();
        if (type == typeof(long)) return ChooseTestValues(typeof(int)).Select(e => (dynamic)(long)(int)e).ToArray();
        if (type == typeof(float)) return ChooseTestValues(typeof(int)).Select(e => (dynamic)(float)(int)e).ToArray();
        if (type == typeof(double)) return ChooseTestValues(typeof(int)).Select(e => (dynamic)(double)(int)e).ToArray();
        if (type == typeof(byte)) return ChooseTestValues(typeof(int)).Where(e => (int)e > 0).Select(e => (dynamic)(byte)(int)e).ToArray();
        if (type == typeof(ushort)) return ChooseTestValues(typeof(int)).Where(e => (int)e > 0).Select(e => (dynamic)(ushort)(int)e).ToArray();
        if (type == typeof(uint)) return ChooseTestValues(typeof(int)).Where(e => (int)e > 0).Select(e => (dynamic)(uint)(int)e).ToArray();
        if (type == typeof(ulong)) return ChooseTestValues(typeof(int)).Where(e => (int)e > 0).Select(e => (dynamic)(ulong)(int)e).ToArray();

        if (type == typeof(IJar<int>)) return new dynamic[] { Jar.Int32LittleEndian, Jar.Int32BigEndian };
        if (type == typeof(IReadOnlyList<int>)) return new dynamic[] { new int[0], new[] { -1 }, new[] { 2, 3, 5, 7 } };
        if (type == typeof(Func<int, bool>)) return new dynamic[] { new Func<int, bool>(e1 => e1 % 2 == 0) };
        
        // note: this function must currently be its own inverse, else Select's packer projection won't undo its parser projection
        if (type == typeof(Func<int, int>)) return new dynamic[] { new Func<int, int>(e1 => e1 * -1) };
        if (type == typeof(IJar<IReadOnlyList<int>>)) return new dynamic[] { Jar.Int32LittleEndian.RepeatNTimes(2) };

        var matchingJar = ApiJarGetters.Where(type.IsInstanceOfType).Select(e => (dynamic)e).ToArray();
        if (matchingJar.Length > 0) return matchingJar;
        throw new Exception(type.ToString());
    }
    private static IEnumerable<object> JarsExposedByPublicApi() {
        var derivedJars = (from jarMaker in ApiJarMakers
                           from args in jarMaker.GetParameters()
                                                .Select(e => ChooseTestValues(e.ParameterType))
                                                .ToArray()
                                                .AllChoiceCombinationsVolatile()
                           let e = TestingUtilities.InvokeWithDefaultOnFail(() => jarMaker.Invoke(null, args))
                           where e != null
                           select e
                          ).ToArray();
        (derivedJars.Length > 0).AssertTrue();

        return ApiJarGetters.Concat(derivedJars);
    }
    [TestMethod]
    public void TestApiHasValidJars() {
        foreach (dynamic jar in JarsExposedByPublicApi()) {
            var itemType = 
                ((Type)jar.GetType())
                .GetInterfaces()
                .Single(e => e.IsGenericType && e.GetGenericTypeDefinition() == typeof(IJar<>))
                .GetGenericArguments()
                .Single();
            var data = Enumerable.Range(0, 8 * 3 * 5 * 7).Select(e => (byte)e).ToArray();
            var full = new ArraySegment<byte>(data);

            // parsing appears to work correctly?
            var segments = new[] {
                full,
                full.Skip(data.Length/2),
                full.Skip(data.Length),
                new ArraySegment<byte>(data, 0, 0),
                new ArraySegment<byte>(new byte[0])
            };
            var vs = segments.Select(e => AssertParsesCorrectlyIfParses(jar, e)).ToArray();

            // round trips work?
            var packed = new List<byte[]>();
            foreach (var item in ChooseTestValues(itemType)) {
                byte[] itemData;
                try {
                    itemData = jar.Pack(item);
                    packed.Add(itemData);
                } catch (Exception) {
                    continue;
                }
                var p = jar.Parse(new ArraySegment<byte>(itemData));
                Assert.AreEqual(itemData.Length, p.Consumed);
                TestingUtilities.AssertSimilar(item, p.Value);
            }

            // metadata is good?
            var metadata = jar as IJarMetadataInternal;
            if (metadata != null) {
                // length matches, if specified?
                var len = metadata.OptionalConstantSerializedLength;
                if (len.HasValue) {
                    if (len.Value > 0) {
                        TestingUtilities.AssertThrows(() => jar.Parse(new ArraySegment<byte>(data, 0, len.Value - 1)));
                    }
                    foreach (var b in vs.Where(b => !ReferenceEquals(b, null))) {
                        Assert.AreEqual(b.Consumed, len.Value);
                    }
                    foreach (var b in packed) {
                        Assert.AreEqual(b.Length, len.Value);
                    }
                }

                // inlined expression has same result?
                for (var i = 0; i < segments.Length; i++) {
                    if (ReferenceEquals(null, vs[i])) continue;
                    var inlined = metadata.TryMakeInlinedParserComponents(
                        Expression.Constant(segments[i].Array),
                        Expression.Constant(segments[i].Offset),
                        Expression.Constant(segments[i].Count));
                    if (inlined == null) continue;

                    var compiledValue = Expression.Lambda(
                        Expression.Block(
                            inlined.ResultStorage,
                            inlined.PerformParse,
                            inlined.AfterParseValueGetter)).Compile().DynamicInvoke();
                    TestingUtilities.AssertSimilar(compiledValue, vs[i].Value);

                    var compiledConsumed = Expression.Lambda(
                        Expression.Block(
                            inlined.ResultStorage,
                            inlined.PerformParse,
                            inlined.AfterParseConsumedGetter)).Compile().DynamicInvoke();
                    TestingUtilities.AssertSimilar(compiledConsumed, vs[i].Consumed);
                }
            }
        }
    }

    private static ParsedValue<T>? AssertParsesCorrectlyIfParses<T>(IJar<T> parser, ArraySegment<byte> data) {
        ParsedValue<T> v;
        try {
            v = parser.Parse(data);
        }
        catch (Exception) {
            return null;
        }
        (v.Consumed <= data.Count).AssertTrue();

        // only consumed data matters
        var v2 = parser.Parse(new ArraySegment<byte>(data.Array, data.Offset, v.Consumed));
        v.Consumed.AssertEquals(v2.Consumed);
        v.Value.AssertSimilar(v2.Value);

        return v;
    }
}