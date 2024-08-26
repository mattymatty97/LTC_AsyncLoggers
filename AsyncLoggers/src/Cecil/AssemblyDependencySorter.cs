using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace AsyncLoggers.Cecil;

public static class AssemblyDependencySorter
{
    public static IEnumerable<string> SortAssembliesByDependency(IEnumerable<string> assemblies)
    {
        var graph = new Dictionary<string, List<string>>();
        var assemblyPaths = new Dictionary<string, string>();  // Maps assembly full names to their paths

        // Build the graph based on the assembly full names and references
        foreach (var path in assemblies)
        {
            using (var assemblyDefinition = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadSymbols = false, ReadingMode = ReadingMode.Deferred }))
            {
                var assemblyFullName = assemblyDefinition.FullName;
                assemblyPaths[assemblyFullName] = path;
                graph[assemblyFullName] = new List<string>();

                foreach (var reference in assemblyDefinition.MainModule.AssemblyReferences)
                {
                    var referenceFullName = reference.FullName;
                    graph[assemblyFullName].Add(referenceFullName);
                }
            }
        }

        // Detect and break cycles
        var nonCircularAssemblies = BreakCycles(graph);

        // Perform topological sorting
        return TopologicalSort(nonCircularAssemblies).Select(an => assemblyPaths[an]);
    }

    private static Dictionary<string, List<string>> BreakCycles(Dictionary<string, List<string>> graph)
    {
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        var cyclicNodes = new HashSet<string>();

        foreach (var node in graph.Keys)
        {
            if (!visited.Contains(node))
            {
                DetectCycles(node, graph, visited, visiting, cyclicNodes);
            }
        }

        // Remove the minimal number of nodes to break cycles
        foreach (var node in cyclicNodes)
        {
            AsyncLoggers.Log.LogWarning($"Removing circular dependency: {node}");
            graph.Remove(node);
        }

        return graph;
    }

    private static bool DetectCycles(string node, Dictionary<string, List<string>> graph, HashSet<string> visited, HashSet<string> visiting, HashSet<string> cyclicNodes)
    {
        if (visiting.Contains(node))
        {
            cyclicNodes.Add(node);
            return true;  // Circular dependency detected
        }

        if (!visited.Contains(node))
        {
            visiting.Add(node);
            foreach (var neighbor in graph[node])
            {
                if (graph.ContainsKey(neighbor) && DetectCycles(neighbor, graph, visited, visiting, cyclicNodes))
                {
                    cyclicNodes.Add(node);
                }
            }
            visiting.Remove(node);
            visited.Add(node);
        }

        return false;
    }

    private static List<string> TopologicalSort(Dictionary<string, List<string>> graph)
    {
        var sorted = new List<string>();
        var visited = new HashSet<string>();

        foreach (var node in graph.Keys)
        {
            if (!visited.Contains(node))
            {
                TopologicalSortVisit(node, graph, visited, sorted);
            }
        }

        sorted.Reverse();
        return sorted;
    }

    private static void TopologicalSortVisit(string node, Dictionary<string, List<string>> graph, HashSet<string> visited, List<string> sorted)
    {
        if (!visited.Contains(node))
        {
            visited.Add(node);

            foreach (var neighbor in graph[node])
            {
                if (graph.ContainsKey(neighbor) && !visited.Contains(neighbor))
                {
                    TopologicalSortVisit(neighbor, graph, visited, sorted);
                }
            }

            sorted.Add(node);
        }
    }
}