﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.ComponentModel.Design;
using System.Reflection;
using System.Xml.Linq;
using static ConsoleAppFramework.Emitter;

namespace ConsoleAppFramework;

[Generator(LanguageNames.CSharp)]
public partial class ConsoleAppGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(EmitConsoleAppTemplateSource);

        // ConsoleApp.Run
        var runSource = context.SyntaxProvider.CreateSyntaxProvider((node, ct) =>
        {
            if (node.IsKind(SyntaxKind.InvocationExpression))
            {
                var invocationExpression = (node as InvocationExpressionSyntax);
                if (invocationExpression == null) return false;

                var expr = invocationExpression.Expression as MemberAccessExpressionSyntax;
                if ((expr?.Expression as IdentifierNameSyntax)?.Identifier.Text == "ConsoleApp")
                {
                    var methodName = expr?.Name.Identifier.Text;
                    if (methodName is "Run" or "RunAsync")
                    {
                        return true;
                    }
                }

                return false;
            }

            return false;
        }, (context, ct) => ((InvocationExpressionSyntax)context.Node, context.SemanticModel));

        context.RegisterSourceOutput(runSource, EmitConsoleAppRun);

        // ConsoleAppBuilder
        var builderSource = context.SyntaxProvider
            .CreateSyntaxProvider((node, ct) =>
            {
                if (node.IsKind(SyntaxKind.InvocationExpression))
                {
                    var invocationExpression = (node as InvocationExpressionSyntax);
                    if (invocationExpression == null) return false;

                    var expr = invocationExpression.Expression as MemberAccessExpressionSyntax;
                    var methodName = expr?.Name.Identifier.Text;
                    if (methodName is "Add" or "UseFilter" or "Run" or "RunAsync")
                    {
                        return true;
                    }

                    return false;
                }

                return false;
            }, (context, ct) => (
                (InvocationExpressionSyntax)context.Node,
                ((context.Node as InvocationExpressionSyntax)!.Expression as MemberAccessExpressionSyntax)!.Name.Identifier.Text,
                context.SemanticModel))
            .Where(x =>
            {
                var model = x.SemanticModel.GetTypeInfo((x.Item1.Expression as MemberAccessExpressionSyntax)!.Expression);
                return model.Type?.Name == "ConsoleAppBuilder";
            });

        context.RegisterSourceOutput(builderSource.Collect(), EmitConsoleAppBuilder);
    }

    public const string ConsoleAppBaseCode = """
// <auto-generated/>
#nullable enable
namespace ConsoleAppFramework;

using System;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel.DataAnnotations;

internal interface IArgumentParser<T>
{
    static abstract bool TryParse(ReadOnlySpan<char> s, out T result);
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
internal sealed class FromServicesAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
internal sealed class ArgumentAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class CommandAttribute : Attribute
{
    public string Command { get; }

    public CommandAttribute(string command)
    {
        this.Command = command;
    }
}

internal record class ConsoleAppContext(string CommandName, string[] Arguments, object? State);

internal abstract class ConsoleAppFilter(ConsoleAppFilter next)
{
    protected readonly ConsoleAppFilter Next = next;

    public abstract Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class ConsoleAppFilterAttribute<T> : Attribute
    where T : ConsoleAppFilter
{
}

internal static partial class ConsoleApp
{
    public static IServiceProvider? ServiceProvider { get; set; }
    public static TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    static Action<string>? logAction;
    public static Action<string> Log
    {
        get => logAction ??= Console.WriteLine;
        set => logAction = value;
    }

    static Action<string>? logErrorAction;
    public static Action<string> LogError
    {
        get => logErrorAction ??= (static msg => Log(msg));
        set => logErrorAction = value;
    }

    public static void Run(string[] args)
    {
    }

    public static Task RunAsync(string[] args)
    {
        return Task.CompletedTask;
    }

    public static ConsoleAppBuilder Create() => new ConsoleAppBuilder();

    static void ThrowArgumentParseFailed(string argumentName, string value)
    {
        throw new ArgumentException($"Argument '{argumentName}' parse failed. value: {value}");
    }

    static void ThrowRequiredArgumentNotParsed(string name)
    {
        throw new ArgumentException($"Require argument '{name}' does not parsed.");
    }

    static void ThrowArgumentNameNotFound(string argumentName)
    {
        throw new ArgumentException($"Argument '{argumentName}' does not found in command prameters.");
    }

    static bool TryParseParamsArray<T>(ReadOnlySpan<string> args, ref T[] result, ref int i)
       where T : IParsable<T>
    {
        result = new T[args.Length - i];
        var resultIndex = 0;
        for (; i < args.Length; i++)
        {
            if (!T.TryParse(args[i], null, out result[resultIndex++]!)) return false;
        }
        return true;
    }

    static bool TrySplitParse<T>(ReadOnlySpan<char> s, out T[] result)
       where T : ISpanParsable<T>
    {
        if (s.StartsWith("["))
        {
            try
            {
                result = System.Text.Json.JsonSerializer.Deserialize<T[]>(s)!;
                return true;
            }
            catch
            {
                result = default!;
                return false;
            }
        }

        var count = s.Count(',') + 1;
        result = new T[count];

        var source = s;
        var destination = result.AsSpan();
        Span<Range> ranges = stackalloc Range[Math.Min(count, 128)];

        while (true)
        {
            var splitCount = source.Split(ranges, ',');
            var parseTo = splitCount;
            if (splitCount == 128 && source[ranges[^1]].Contains(','))
            {
                parseTo = splitCount - 1;
            }

            for (int i = 0; i < parseTo; i++)
            {
                if (!T.TryParse(source[ranges[i]], null, out destination[i]!))
                {
                    return false;
                }
            }
            destination = destination.Slice(parseTo);

            if (destination.Length != 0)
            {
                source = source[ranges[^1]];
                continue;
            }
            else
            {
                break;
            }
        }

        return true;
    }

    static void ValidateParameter(object? value, ParameterInfo parameter, ValidationContext validationContext, ref StringBuilder? errorMessages)
    {
        validationContext.DisplayName = parameter.Name ?? "";
        validationContext.Items.Clear();

        foreach (var validator in parameter.GetCustomAttributes<ValidationAttribute>(false))
        {
            var result = validator.GetValidationResult(value, validationContext);
            if (result != null)
            {
                if (errorMessages == null)
                {
                    errorMessages = new StringBuilder();
                }
                errorMessages.AppendLine(result.ErrorMessage);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool TryShowHelpOrVersion(ReadOnlySpan<string> args, int parameterCount, int helpId)
    {
        if (args.Length == 0)
        {
            if (parameterCount == 0) return false;
            
            ShowHelp(helpId);
            return true;
        }

        if (args.Length == 1)
        {
            switch (args[0])
            {
                case "--version":
                    ShowVersion();
                    return true;
                case "-h":
                case "--help":
                    ShowHelp(helpId);
                    return true;
                default:
                    break;
            }
        }

        return false;
    }

    static void ShowVersion()
    {
        var asm = Assembly.GetEntryAssembly();
        var version = "1.0.0";
        var infoVersion = asm!.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (infoVersion != null)
        {
            version = infoVersion.InformationalVersion;
        }
        else
        {
            var asmVersion = asm!.GetCustomAttribute<AssemblyVersionAttribute>();
            if (asmVersion != null)
            {
                version = asmVersion.Version;
            }
        }
        Log(version);
    }

    static partial void ShowHelp(int helpId);

    static async Task RunWithFilterAsync(string commandName, string[] args, ConsoleAppFilter invoker)
    {
        using var posixSignalHandler = PosixSignalHandler.Register(Timeout);
        try
        {
            await Task.Run(() => invoker.InvokeAsync(new ConsoleAppContext(commandName, args, null), posixSignalHandler.Token)).WaitAsync(posixSignalHandler.TimeoutToken);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                Environment.ExitCode = 130;
                return;
            }

            Environment.ExitCode = 1;
            if (ex is ValidationException)
            {
                LogError(ex.Message);
            }
            else
            {
                LogError(ex.ToString());
            }
        }
    }

    sealed class PosixSignalHandler : IDisposable
    {
        public CancellationToken Token => cancellationTokenSource.Token;
        public CancellationToken TimeoutToken => timeoutCancellationTokenSource.Token;

        CancellationTokenSource cancellationTokenSource;
        CancellationTokenSource timeoutCancellationTokenSource;
        TimeSpan timeout;

        PosixSignalRegistration? sigInt;
        PosixSignalRegistration? sigQuit;
        PosixSignalRegistration? sigTerm;

        PosixSignalHandler(TimeSpan timeout)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.timeoutCancellationTokenSource = new CancellationTokenSource();
            this.timeout = timeout;
        }

        public static PosixSignalHandler Register(TimeSpan timeout)
        {
            var handler = new PosixSignalHandler(timeout);

            Action<PosixSignalContext> handleSignal = handler.HandlePosixSignal;

            handler.sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, handleSignal);
            handler.sigQuit = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, handleSignal);
            handler.sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, handleSignal);

            return handler;
        }

        void HandlePosixSignal(PosixSignalContext context)
        {
            context.Cancel = true;
            cancellationTokenSource.Cancel();
            timeoutCancellationTokenSource.CancelAfter(timeout);
        }

        public void Dispose()
        {
            sigInt?.Dispose();
            sigQuit?.Dispose();
            sigTerm?.Dispose();
            timeoutCancellationTokenSource.Dispose();
        }
    }

    internal partial struct ConsoleAppBuilder
    {
        public ConsoleAppBuilder()
        {
        }

        public void Add(string commandName, Delegate command)
        {
            AddCore(commandName, command);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public void Add<T>() { }

        [System.Diagnostics.Conditional("DEBUG")]
        public void Add<T>(string commandPath) { }

        [System.Diagnostics.Conditional("DEBUG")]
        public void UseFilter<T>() where T : ConsoleAppFilter { }

        public void Run(string[] args)
        {
            RunCore(args);
        }

        public Task RunAsync(string[] args)
        {
            Task? task = null;
            RunAsyncCore(args, ref task!);
            return task ?? Task.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        partial void AddCore(string commandName, Delegate command);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        partial void RunCore(string[] args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        partial void RunAsyncCore(string[] args, ref Task result);

        static partial void ShowHelp(int helpId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryShowHelpOrVersion(ReadOnlySpan<string> args, int parameterCount, int helpId)
        {
            if (args.Length == 0)
            {
                if (parameterCount == 0) return false;
            
                ShowHelp(helpId);
                return true;
            }

            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "--version":
                        ShowVersion();
                        return true;
                    case "-h":
                    case "--help":
                        ShowHelp(helpId);
                        return true;
                    default:
                        break;
                }
            }

            return false;
        }
    }
}
""";

    static void EmitConsoleAppTemplateSource(IncrementalGeneratorPostInitializationContext context)
    {
        context.AddSource("ConsoleApp.cs", ConsoleAppBaseCode);
    }

    const string GeneratedCodeHeader = """
// <auto-generated/>
#nullable enable
#pragma warning disable CS0108 // hides inherited member
#pragma warning disable CS0162 // Unreachable code
#pragma warning disable CS0164 // This label has not been referenced
#pragma warning disable CS0219 // Variable assigned but never used
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8601 // Possible null reference assignment
#pragma warning disable CS8602
#pragma warning disable CS8604 // Possible null reference argument for parameter
#pragma warning disable CS8619
#pragma warning disable CS8620
#pragma warning disable CS8631 // The type cannot be used as type parameter in the generic type or method
#pragma warning disable CS8765 // Nullability of type of parameter
#pragma warning disable CS9074 // The 'scoped' modifier of parameter doesn't match overridden or implemented member
#pragma warning disable CA1050 // Declare types in namespaces.
#pragma warning disable CS1998
        
namespace ConsoleAppFramework;
        
using System;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel.DataAnnotations;

""";

    static void EmitConsoleAppRun(SourceProductionContext sourceProductionContext, (InvocationExpressionSyntax, SemanticModel) generatorSyntaxContext)
    {
        var node = generatorSyntaxContext.Item1;
        var model = generatorSyntaxContext.Item2;

        var wellKnownTypes = new WellKnownTypes(model.Compilation);

        var parser = new Parser(sourceProductionContext, node, model, wellKnownTypes, DelegateBuildType.MakeDelegateWhenHasDefaultValue, []);
        var command = parser.ParseAndValidate();
        if (command == null)
        {
            return;
        }
        if (command.HasFilter)
        {
            sourceProductionContext.ReportDiagnostic(DiagnosticDescriptors.CommandHasFilter, node.GetLocation());
            return;
        }

        var isRunAsync = ((node.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.Text == "RunAsync");

        var sb = new SourceBuilder(0);
        sb.AppendLine(GeneratedCodeHeader);
        using (sb.BeginBlock("internal static partial class ConsoleApp"))
        {
            var emitter = new Emitter(wellKnownTypes);
            var withId = new Emitter.CommandWithId(null, command, -1);
            emitter.EmitRun(sb, withId, isRunAsync);
        }
        sourceProductionContext.AddSource("ConsoleApp.Run.g.cs", sb.ToString());

        var help = new SourceBuilder(0);
        help.AppendLine(GeneratedCodeHeader);
        using (help.BeginBlock("internal static partial class ConsoleApp"))
        {
            var emitter = new Emitter(wellKnownTypes);
            emitter.EmitHelp(help, command);
        }
        sourceProductionContext.AddSource("ConsoleApp.Run.Help.g.cs", help.ToString());
    }

    static void EmitConsoleAppBuilder(SourceProductionContext sourceProductionContext, ImmutableArray<(InvocationExpressionSyntax Node, string Name, SemanticModel Model)> generatorSyntaxContexts)
    {
        if (generatorSyntaxContexts.Length == 0) return;

        var model = generatorSyntaxContexts[0].Model;

        var wellKnownTypes = new WellKnownTypes(model.Compilation);

        // validation, invoke in loop is not allowed.
        foreach (var item in generatorSyntaxContexts)
        {
            if (item.Name is "Run" or "RunAsync") continue;
            foreach (var n in item.Node.Ancestors())
            {
                if (n.Kind() is SyntaxKind.WhileStatement or SyntaxKind.DoStatement or SyntaxKind.ForStatement or SyntaxKind.ForEachStatement)
                {
                    sourceProductionContext.ReportDiagnostic(DiagnosticDescriptors.AddInLoopIsNotAllowed, item.Node.GetLocation());
                    return;
                }
            }
        }

        var methodGroup = generatorSyntaxContexts.ToLookup(x =>
        {
            if (x.Name == "Add" && ((x.Node.Expression as MemberAccessExpressionSyntax)?.Name.IsKind(SyntaxKind.GenericName) ?? false))
            {
                return "Add<T>";
            }

            return x.Name;
        });

        var globalFilters = methodGroup["UseFilter"]
            .OrderBy(x => x.Node.GetLocation().SourceSpan) // sort by line number
            .Select(x =>
            {
                var genericName = (x.Node.Expression as MemberAccessExpressionSyntax)?.Name as GenericNameSyntax;
                var genericType = genericName!.TypeArgumentList.Arguments[0];
                var type = model.GetTypeInfo(genericType).Type;
                if (type == null) return null!;

                var filter = FilterInfo.Create(type);

                if (filter == null)
                {
                    sourceProductionContext.ReportDiagnostic(DiagnosticDescriptors.FilterMultipleConsturtor, genericType.GetLocation());
                    return null!;
                }

                return filter!;
            })
            .ToArray();

        // don't emit if exists failure(already reported error)
        if (globalFilters.Any(x => x == null))
        {
            return;
        }

        var names = new HashSet<string>();
        var commands1 = methodGroup["Add"]
            .Select(x =>
            {
                var parser = new Parser(sourceProductionContext, x.Node, x.Model, wellKnownTypes, DelegateBuildType.OnlyActionFunc, globalFilters);
                var command = parser.ParseAndValidateForBuilderDelegateRegistration();

                // validation command name duplicate
                if (command != null && !names.Add(command.CommandFullName))
                {
                    sourceProductionContext.ReportDiagnostic(DiagnosticDescriptors.DuplicateCommandName, x.Node.ArgumentList.Arguments[0].GetLocation(), command!.CommandFullName);
                    return null;
                }

                return command;
            })
            .ToArray(); // evaluate first.

        var commands2 = methodGroup["Add<T>"]
            .SelectMany(x =>
            {
                var parser = new Parser(sourceProductionContext, x.Node, x.Model, wellKnownTypes, DelegateBuildType.None, globalFilters);
                var commands = parser.ParseAndValidateForBuilderClassRegistration();

                // validation command name duplicate?
                foreach (var command in commands)
                {
                    if (command != null && !names.Add(command.CommandFullName))
                    {
                        sourceProductionContext.ReportDiagnostic(DiagnosticDescriptors.DuplicateCommandName, x.Node.GetLocation(), command!.CommandFullName);
                        return [null];
                    }
                }

                return commands;
            });

        var commands = commands1.Concat(commands2).ToArray();

        // don't emit if exists failure(already reported error)
        if (commands.Any(x => x == null))
        {
            return;
        }

        if (commands.Length == 0) return;

        var hasRun = methodGroup["Run"].Any();
        var hasRunAsync = methodGroup["RunAsync"].Any();

        if (!hasRun && !hasRunAsync) return;

        var sb = new SourceBuilder(0);
        sb.AppendLine(GeneratedCodeHeader);

        // with id number
        var commandIds = commands
            .Select((x, i) =>
            {
                return new CommandWithId(
                    FieldType: x!.BuildDelegateSignature(out _), // for builder, always generate Action/Func so ok to ignore out var.
                    Command: x!,
                    Id: i
                );
            })
            .ToArray();

        using (sb.BeginBlock("internal static partial class ConsoleApp"))
        {
            var emitter = new Emitter(wellKnownTypes);
            emitter.EmitBuilder(sb, commandIds, hasRun, hasRunAsync);
        }
        sourceProductionContext.AddSource("ConsoleApp.Builder.g.cs", sb.ToString());

        // Build Help

        var help = new SourceBuilder(0);
        help.AppendLine(GeneratedCodeHeader);
        using (help.BeginBlock("internal static partial class ConsoleApp"))
        using (help.BeginBlock("internal partial struct ConsoleAppBuilder"))
        {
            var emitter = new Emitter(wellKnownTypes);
            emitter.EmitHelp(help, commandIds!);
        }
        sourceProductionContext.AddSource("ConsoleApp.Builder.Help.g.cs", help.ToString());
    }
}