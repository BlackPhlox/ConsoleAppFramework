﻿using FluentAssertions;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace ConsoleAppFramework.GeneratorTests;

public class Test(ITestOutputHelper output)
{
    VerifyHelper verifier = new VerifyHelper(output, "CAF");

    [Fact]
    public void SyncRun()
    {
        verifier.Execute("ConsoleApp.Run(args, (int x, int y) => { Console.Write((x + y)); });", "--x 10 --y 20", "30");
    }

    [Fact]
    public void SyncRunShouldFailed()
    {
        verifier.Error("ConsoleApp.Run(args, (int x) => { Console.Write((x)); });", "--x").Should().Contain("Argument 'x' failed to parse");
    }

    [Fact]
    public void MissingArgument()
    {
        verifier.Error("ConsoleApp.Run(args, (int x, int y) => { Console.Write((x + y)); });", "--x 10 y 20").Should().Contain("Argument 'y' is not recognized.");

        Environment.ExitCode.Should().Be(1);
        Environment.ExitCode = 0;
    }

    [Fact]
    public void ValidateOne()
    {
        var expected = """
The field x must be between 1 and 10.


""";

        verifier.Execute("""
ConsoleApp.Run(args, ([Range(1, 10)]int x, [Range(100, 200)]int y) => { Console.Write((x + y)); });
""", "--x 100 --y 140", expected);

        Environment.ExitCode.Should().Be(1);
        Environment.ExitCode = 0;
    }

    [Fact]
    public void ValidateTwo()
    {
        var expected = """
The field x must be between 1 and 10.
The field y must be between 100 and 200.


""";

        verifier.Execute("""
ConsoleApp.Run(args, ([Range(1, 10)]int x, [Range(100, 200)]int y) => { Console.Write((x + y)); });
""", "--x 100 --y 240", expected);

        Environment.ExitCode.Should().Be(1);
        Environment.ExitCode = 0;
    }
    [Fact]
    public void Parameters()
    {
        verifier.Execute("""
ConsoleApp.Run(args, (int foo, string bar, Fruit ft, bool flag, Half half, int? itt, Takoyaki.Obj obj) => 
{
    Console.Write(foo); 
    Console.Write(bar); 
    Console.Write(ft); 
    Console.Write(flag); 
    Console.Write(half); 
    Console.Write(itt);
    Console.Write(obj.Foo); 
});

enum Fruit
{
    Orange, Grape, Apple
}

namespace Takoyaki
{
    public class Obj
    {
         public int Foo { get; set; }
    }
}
""", "--foo 10 --bar aiueo --ft Grape --flag --half 1.3 --itt 99 --obj {\"Foo\":1999}", "10aiueoGrapeTrue13991999");
    }

    [Fact]
    public void ValidateClass()
    {
        var expected = """
The field value must be between 0 and 1.


""";

        verifier.Execute("""
var app = ConsoleApp.Create();
app.Add<Test>();
app.Run(args);

public class Test
{
    public void Show(string aaa, [Range(0, 1)] double value) => ConsoleApp.Log($"{value}");
}

""", "show --aaa foo --value 100", expected);

    }

    [Fact]
    public void ValidateAbstractClass()
    {
        var expected = """
The field value must be between 0 and 1.


""";

        verifier.Execute("""
var app = ConsoleApp.Create();
app.Add<Test>();
app.Run(args);

public class TestInfo {
    public string TestName { get; set; }
}

public abstract class TestBase
{
    public abstract TestInfo PrintInfo();
    public virtual void Show(string aaa, [Range(0, 1)] double value) => ConsoleApp.Log($"{PrintInfo().TestName} : {value}");
}

public class Test : TestBase
{
    // This will fail at compile time with the error code CAF003
    // By adding the Ignore Attribute we prevent this compiler error as it then will not be included as a command
    [Ignore]
    public override TestInfo PrintInfo() => new TestInfo() { TestName = "Test1" };
    public override void Show(string aaa, [Range(0, 1)] double value){
        base.Show(aaa, value);
        ConsoleApp.Log($"{PrintInfo().TestName}x2 : {value * 2}");
    }
}

""", "show --aaa foo --value 100", expected);

    }
}