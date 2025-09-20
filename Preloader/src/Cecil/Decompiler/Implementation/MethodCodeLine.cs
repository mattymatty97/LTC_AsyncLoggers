using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class MethodCodeLine : ICodeLine
{
    protected internal MethodCodeLine(MethodDefinition method, Instruction instruction)
    {
        ICodeLine.CurrentStack.Value.Push(this);

        Method = method;
        EndInstruction = instruction;

        var operand = instruction.Operand;

        if (operand is not MethodReference targetMethod)
            throw new InvalidOperationException(
                $"{Method.FullName}:IL_{EndInstruction.Offset:x4} Is not a Method Call");

        TargetMethod = targetMethod;

        AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
            () =>
                $"{Method.FullName}:{ICodeLine.PrintStack()} - start method {TargetMethod.DeclaringType.FullName}:{TargetMethod.FullName}!");

        var argumentCount = targetMethod.Parameters.Count +
                            (targetMethod.HasThis && instruction.OpCode != OpCodes.Newobj ? 1 : 0);

        for (var i = 0; i < argumentCount; i++)
        {
            var argument = ICodeLine.InternalParseInstruction(method, StartInstruction.Previous);

            if (argument == null)
            {
                if (i < argumentCount - 1)
                    throw new InvalidOperationException(
                        $"{Method.FullName}:IL_{EndInstruction.Offset} Cannot find Argument{argumentCount - 1} for Method");
            }
            else
            {
                var i1 = i;
                AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                    () => $"{Method.FullName}:{ICodeLine.PrintStack()} - Argument{argumentCount - i1} found");

                foreach (var arg in Arguments) arg.SetMissingArgument(argument);

                Arguments.AddFirst(argument);
            }
        }

        var resolvedMethod = targetMethod.Resolve();

        if (resolvedMethod.IsPublic && resolvedMethod.IsSpecialName)
        {
            var name = resolvedMethod.Name;
            if (resolvedMethod.IsStatic)
                Operator = name switch
                {
                    "op_Equality" => "{0} == {1}",
                    "op_Inequality" => "{0} != {1}",
                    "op_LessThan" => "{0} < {1}",
                    "op_LessThanOrEqual" => "{0} <= {1}",
                    "op_GreaterThan" => "{0} > {1}",
                    "op_GreaterThanOrEqual " => "{0} >= {1}",
                    "op_Addition" => "{0} + {1}",
                    "op_Subtraction" => "{0} - {1}",
                    "op_UnaryPlus" => "+{0}",
                    "op_UnaryNegation" => "-{0}",
                    "op_Multiply" => "{0} * {1}",
                    "op_Division" => "{0} / {1}",
                    "op_Modulus" => "{0} % {1}",
                    "op_Increment" => "{0}++",
                    "op_Decrement" => "{0}--",
                    "op_LogicalNot" => "!{0}",
                    "op_OnesComplement" => "~{0}",
                    "op_BitwiseAnd" => "{0} & {1}",
                    "op_BitwiseOr" => "{0} | {1}",
                    "op_ExclusiveOr" => "{0} ^ {1}",
                    "op_LeftShift" => "{0} << {1}",
                    "op_RightShift" => "{0} >> {1}",
                    "get_Item" => "{0}[{1}]",
                    "set_Item" => "{0}[{1}] = {2}",
                    _ => null
                };
            else
                Operator = name switch
                {
                    "get_Item" => "{0}[{1}]",
                    "set_Item" => "{0}[{1}] = {2}",
                    _ => null
                };
        }

        if (FindProperty(resolvedMethod, out var property, out var setter))
        {
            Property = new PropertyData(property, setter);
            HasReturn = !setter;
            AsyncLoggers.VerboseLogWrappingLog(LogLevel.Debug,
                () => $"{Method.FullName}:{ICodeLine.PrintStack()} - is Property {property.Name}");
        }
        else if (instruction.OpCode == OpCodes.Newobj)
        {
            HasReturn = true;
        }
        else
        {
            HasReturn = targetMethod.ReturnType.MetadataType != MetadataType.Void;
        }

        ICodeLine.CurrentStack.Value.Pop();
    }

    public MethodReference TargetMethod { get; }

    public string Operator { get; }

    public PropertyData Property { get; }
    protected LinkedList<ICodeLine> Arguments { get; } = [];

    public ICodeLine Instance => TargetMethod.HasThis ? Arguments.FirstOrDefault() : null;
    public bool HasReturn { get; }
    public MethodDefinition Method { get; }

    public Instruction StartInstruction => Arguments.First?.Value?.StartInstruction ?? EndInstruction;

    public Instruction EndInstruction { get; }

    public IEnumerable<ICodeLine> GetArguments()
    {
        return Arguments.ToArray();
    }

    public bool IsIncomplete =>
        Arguments.Count < TargetMethod.Parameters.Count +
        (TargetMethod.HasThis && EndInstruction.OpCode != OpCodes.Newobj ? 1 : 0) ||
        Arguments.Any(a => a.IsIncomplete);

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsIncomplete)
            return false;

        if (Arguments.Count < TargetMethod.Parameters.Count +
            (TargetMethod.HasThis && EndInstruction.OpCode != OpCodes.Newobj ? 1 : 0))
        {
            Arguments.AddFirst(codeLine);
            return IsIncomplete;
        }

        var @fixed = true;

        foreach (var argument in Arguments) @fixed &= argument?.SetMissingArgument(codeLine) ?? false;

        return @fixed;
    }

    public virtual string ToString(bool isRoot)
    {
        if (Operator != null) return string.Format(Operator, Arguments.ToArray<object>());

        if (Property != null)
        {
            var argCount = Arguments.Count - (Property.Setter ? 1 : 0);

            var name = argCount switch
            {
                0 => $"{Property.Property.DeclaringType.FullName}.{Property.Property.Name}",
                1 => $"{Arguments.First.Value}.{Property.Property.Name}",
                _ => throw new InvalidOperationException(
                    $"{Method.FullName}:IL_{EndInstruction.Offset:x4} Properties cannot have {argCount} arguments!")
            };

            if (Property.Setter)
                return (isRoot ? $"{name} = " : "") + Arguments.Last.Value;
            return name;
        }

        if (EndInstruction.OpCode == OpCodes.Newobj)
            return $"new {TargetMethod.DeclaringType.Name}({{string.Join(\", \", Arguments)}})";

        if (TargetMethod.HasThis)
        {
            var @this = Arguments.First.Value;
            return $"{@this}.{TargetMethod.Name}({string.Join(", ", Arguments.Skip(1))})";
        }

        return $"{TargetMethod.DeclaringType.Name}.{TargetMethod.Name}({string.Join(", ", Arguments)})";
    }

    public override string ToString()
    {
        return ToString(false);
    }


    private static bool FindProperty(MethodDefinition targetMethod, out PropertyReference propertyDefinition,
        out bool setter)
    {
        var targetType = targetMethod!.DeclaringType.Resolve();

        foreach (var property in targetType.Properties)
        {
            if (property.GetMethod != null && targetMethod.FullName == property.GetMethod.FullName)
            {
                propertyDefinition = property;
                setter = false;
                return true;
            }

            if (property.SetMethod != null && targetMethod.FullName == property.SetMethod.FullName)
            {
                propertyDefinition = property;
                setter = true;
                return true;
            }
        }

        propertyDefinition = null;
        setter = false;
        return false;
    }

    public class PropertyData
    {
        protected internal PropertyData(PropertyReference property, bool setter)
        {
            Property = property;
            Setter = setter;
        }

        public PropertyReference Property { get; }

        public bool Setter { get; }
    }
}