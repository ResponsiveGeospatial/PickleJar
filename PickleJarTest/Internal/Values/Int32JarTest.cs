﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Strilanc.PickleJar.Internal;
using Strilanc.PickleJar.Internal.Values;

[TestClass]
public class Int32JarTest {
    [TestMethod]
    public void TestBigEndian() {
        var jar = new Int32Jar(Endianess.BigEndian);
        jar.AssertPicklesNoMoreNoLess(0, 0, 0, 0, 0);
        jar.AssertPicklesNoMoreNoLess(1, 0, 0, 0, 1);
        jar.AssertPicklesNoMoreNoLess(-1, 0xFF, 0xFF, 0xFF, 0xFF);
        jar.AssertPicklesNoMoreNoLess(0x12345678, 0x12, 0x34, 0x56, 0x78);
        jar.AssertPicklesNoMoreNoLess(Int32.MaxValue, 0x7F, 0xFF, 0xFF, 0xFF);
        jar.AssertPicklesNoMoreNoLess(Int32.MinValue, 0x80, 0, 0, 0);

        jar.AssertCantParse();
        jar.AssertCantParse(1, 2, 3);
    }
    [TestMethod]
    public void TestLittleEndian() {
        var jar = new Int32Jar(Endianess.LittleEndian);
        jar.AssertPicklesNoMoreNoLess(0, 0, 0, 0, 0);
        jar.AssertPicklesNoMoreNoLess(1, 1, 0, 0, 0);
        jar.AssertPicklesNoMoreNoLess(-1, 0xFF, 0xFF, 0xFF, 0xFF);
        jar.AssertPicklesNoMoreNoLess(0x12345678, 0x78, 0x56, 0x34, 0x12);
        jar.AssertPicklesNoMoreNoLess(Int32.MaxValue, 0xFF, 0xFF, 0xFF, 0x7F);
        jar.AssertPicklesNoMoreNoLess(Int32.MinValue, 0, 0, 0, 0x80);

        jar.AssertCantParse();
        jar.AssertCantParse(1, 2, 3);
    }
}
