using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AsyncLoggers.Cecil.Decompiler.Implementation;

public class MethodCodeLine : ICodeLine
{
    public bool HasReturn { get; }
    public MethodDefinition Method { get; }

    public Instruction StartInstruction => Arguments.First?.Value?.StartInstruction ?? EndInstruction;

    public Instruction EndInstruction { get; }
    public MethodReference TargetMethod { get; }

    public PropertyData Property { get; } = null;
    protected LinkedList<ICodeLine> Arguments { get; } = [];

    public IEnumerable<ICodeLine> GetArguments()
    {
        return Arguments.ToArray();
    }

    public bool IsMissingArgument =>
        Arguments.Count < TargetMethod.Parameters.Count +
        (TargetMethod.HasThis && EndInstruction.OpCode != OpCodes.Newobj ? 1 : 0) ||
        Arguments.Any(a => a.IsMissingArgument);

    public bool SetMissingArgument(ICodeLine codeLine)
    {
        if (!IsMissingArgument)
            return false;

        if (Arguments.Count < TargetMethod.Parameters.Count +
            (TargetMethod.HasThis && EndInstruction.OpCode != OpCodes.Newobj ? 1 : 0))
        {
            Arguments.AddFirst(codeLine);
            return IsMissingArgument;
        }

        var @fixed = true;

        foreach (var argument in Arguments)
        {
            @fixed &= argument?.SetMissingArgument(codeLine) ?? false;
        }

        return @fixed;
    }

    public MethodCodeLine(MethodDefinition method, Instruction instruction)
    {
        Method = method;
        EndInstruction = instruction;

        var operand = instruction.Operand;

        if (operand is not MethodReference targetMethod)
            throw new InvalidOperationException($"{Method.FullName}:IL_{EndInstruction.Offset} Is not a Method Call");

        TargetMethod = targetMethod;

        var argumentCount = method.Parameters.Count + (method.HasThis && instruction.OpCode != OpCodes.Newobj ? 1 : 0);

        for (var i = 0; i < argumentCount; i++)
        {
            ICodeLine argument = ICodeLine.InternalParseInstruction(method, StartInstruction.Previous);
            if (argument == null)
                throw new InvalidOperationException($"{Method.FullName}:IL_{EndInstruction.Offset} Cannot find Argument{argumentCount - 1} for Method");
            foreach (var arg in Arguments)
            {
                arg.SetMissingArgument(argument);
            }
            Arguments.AddFirst(argument);
        }

        if (FindProperty(TargetMethod, out var property, out bool setter))
        {
            Property = new PropertyData(property, setter);
            HasReturn = !setter;
        }
        else if (instruction.OpCode == OpCodes.Newobj)
        {
            HasReturn = true;
        }
        else
        {
            HasReturn = targetMethod.ReturnType.MetadataType != MetadataType.Void;
        }
    }

    public override string ToString()
    {
        return ToString(false);
    }

    public virtual string ToString(bool isRoot)
    {
        if (Property != null)
        {
            var argCount = Arguments.Count - (Property.Setter ? 1 : 0);

            var name = argCount switch
            {
                0 => $"{Property.Property.DeclaringType.FullName}.{Property.Property.Name}",
                1 => $"{Arguments.First.Value}.{Property.Property.Name}",
                _ => throw new InvalidOperationException($"{Method.FullName}:IL_{EndInstruction.Offset} Properties cannot have {argCount} arguments!")
            };

            if (Property.Setter)
                return (isRoot ? $"{name} = " : "") + Arguments.Last.Value;
            return name;
        }

        if (EndInstruction.OpCode == OpCodes.Newobj)
        {
            return $"new {TargetMethod.DeclaringType.FullName}({{string.Join(\", \", Arguments)}})";
        }

        if (TargetMethod.HasThis)
        {
            var @this = Arguments.First.Value;
            return $"{@this}.{TargetMethod.Name}({string.Join(", ", Arguments.Skip(1))})";
        }

        return $"{TargetMethod.DeclaringType.FullName}.{TargetMethod.Name}({string.Join(", ", Arguments)})";
    }


    private static bool FindProperty(MethodReference targetMethod, out PropertyReference propertyDefinition,
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
        public PropertyReference Property { get; }

        public bool Setter { get; }

        protected internal PropertyData(PropertyReference property, bool setter)
        {
            Property = property;
            Setter = setter;
        }
    }
}