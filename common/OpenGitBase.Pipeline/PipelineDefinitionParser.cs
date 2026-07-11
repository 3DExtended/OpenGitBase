using YamlDotNet.RepresentationModel;

namespace OpenGitBase.Pipeline;

public static class PipelineDefinitionParser
{
    public static ParsePipelineResult ParsePipelineDefinition(string yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText))
        {
            return new ParsePipelineResult
            {
                Errors =
                [
                    new PipelineValidationError
                    {
                        Path = "$",
                        Message = "Pipeline definition is empty.",
                    },
                ],
            };
        }

        var errors = new List<PipelineValidationError>();
        YamlMappingNode root;
        try
        {
            using var reader = new StringReader(yamlText);
            var yaml = new YamlStream();
            yaml.Load(reader);
            root = (YamlMappingNode)yaml.Documents[0].RootNode;
        }
        catch (Exception ex)
        {
            return new ParsePipelineResult
            {
                Errors =
                [
                    new PipelineValidationError
                    {
                        Path = "$",
                        Message = $"Invalid YAML: {ex.Message}",
                    },
                ],
            };
        }

        var reservedKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "stages",
            "image",
            "dependencies",
            "variables",
        };

        var explicitStages = ReadStages(root, errors);
        var defaults = ReadDefaults(root, errors);
        var jobs = new List<ResolvedJob>();
        var firstSeenStages = new List<string>();

        foreach (var child in root.Children)
        {
            var key = child.Key.ToString();
            if (reservedKeys.Contains(key))
            {
                continue;
            }

            if (child.Value is not YamlMappingNode jobNode)
            {
                errors.Add(new PipelineValidationError
                {
                    Path = $"$.{key}",
                    Message = "Job definition must be a mapping.",
                });
                continue;
            }

            var resolved = ResolveJob(key, jobNode, defaults, errors);
            if (resolved is null)
            {
                continue;
            }

            jobs.Add(resolved);
            if (!firstSeenStages.Contains(resolved.Stage, StringComparer.Ordinal))
            {
                firstSeenStages.Add(resolved.Stage);
            }
        }

        if (jobs.Count == 0)
        {
            errors.Add(new PipelineValidationError
            {
                Path = "$",
                Message = "At least one job must be defined.",
            });
        }

        var stages = explicitStages.Count > 0 ? explicitStages : firstSeenStages;
        foreach (var job in jobs)
        {
            if (!stages.Contains(job.Stage, StringComparer.Ordinal))
            {
                errors.Add(new PipelineValidationError
                {
                    Path = $"$.{job.Name}.stage",
                    Message = $"Stage '{job.Stage}' is not declared in stages list.",
                });
            }
        }

        if (errors.Count > 0)
        {
            return new ParsePipelineResult { Errors = errors };
        }

        return new ParsePipelineResult
        {
            Definition = new PipelineDefinition
            {
                DefaultImage = defaults.Image,
                DefaultDependencies = defaults.Dependencies,
                DefaultVariables = defaults.Variables,
                Stages = stages,
                Jobs = jobs,
            },
        };
    }

    private static IReadOnlyList<string> ReadStages(
        YamlMappingNode root,
        List<PipelineValidationError> errors
    )
    {
        if (!TryGetNode(root, "stages", out var stagesNode))
        {
            return Array.Empty<string>();
        }

        if (stagesNode is not YamlSequenceNode sequence)
        {
            errors.Add(new PipelineValidationError
            {
                Path = "$.stages",
                Message = "Stages must be a sequence.",
            });
            return Array.Empty<string>();
        }

        var stages = new List<string>();
        foreach (var stageNode in sequence.Children)
        {
            var stage = stageNode.ToString().Trim();
            if (string.IsNullOrWhiteSpace(stage))
            {
                errors.Add(new PipelineValidationError
                {
                    Path = "$.stages",
                    Message = "Stage names must be non-empty.",
                });
                continue;
            }

            stages.Add(stage);
        }

        return stages;
    }

    private static PipelineDefaults ReadDefaults(
        YamlMappingNode root,
        List<PipelineValidationError> errors
    )
    {
        var image = TryGetNode(root, "image", out var imageNode) ? imageNode.ToString() : string.Empty;
        var vars = ReadVariables(root, "$.variables", "variables", errors);
        var deps = ReadDependencies(root, "$.dependencies", "dependencies", errors);
        return new PipelineDefaults(image, vars, deps);
    }

    private static ResolvedJob? ResolveJob(
        string jobName,
        YamlMappingNode jobNode,
        PipelineDefaults defaults,
        List<PipelineValidationError> errors
    )
    {
        var only = ReadOnlyPatterns(jobNode, jobName, errors);
        var runsOn = GetString(jobNode, "runs-on");
        if (string.IsNullOrWhiteSpace(runsOn))
        {
            errors.Add(new PipelineValidationError
            {
                Path = $"$.{jobName}.runs-on",
                Message = "runs-on is required for every job.",
            });
            return null;
        }

        var stage = GetString(jobNode, "stage");
        if (string.IsNullOrWhiteSpace(stage))
        {
            stage = "default";
        }

        var script = GetString(jobNode, "script");
        if (string.IsNullOrWhiteSpace(script))
        {
            errors.Add(new PipelineValidationError
            {
                Path = $"$.{jobName}.script",
                Message = "script is required for every job.",
            });
            return null;
        }

        var user = GetString(jobNode, "user");
        if (string.IsNullOrWhiteSpace(user))
        {
            user = "ogb";
        }

        if (!string.Equals(user, "ogb", StringComparison.Ordinal) && !string.Equals(user, "root", StringComparison.Ordinal))
        {
            errors.Add(new PipelineValidationError
            {
                Path = $"$.{jobName}.user",
                Message = "user must be either 'ogb' or 'root'.",
            });
            return null;
        }

        var image = GetString(jobNode, "image");
        if (string.IsNullOrWhiteSpace(image))
        {
            image = defaults.Image;
        }

        if (string.IsNullOrWhiteSpace(image))
        {
            errors.Add(new PipelineValidationError
            {
                Path = $"$.{jobName}.image",
                Message = "image is required either as default or job override.",
            });
            return null;
        }

        var variables = new Dictionary<string, string>(defaults.Variables, StringComparer.Ordinal);
        foreach (var pair in ReadVariables(jobNode, $"$.{jobName}.variables", "variables", errors))
        {
            variables[pair.Key] = pair.Value;
        }

        var dependencies = ReadDependencies(jobNode, $"$.{jobName}.dependencies", "dependencies", errors);
        if (dependencies.Count == 0)
        {
            dependencies = defaults.Dependencies;
        }

        return new ResolvedJob
        {
            Name = jobName,
            Stage = stage,
            RunsOn = runsOn,
            Image = image,
            Variables = variables,
            Dependencies = dependencies,
            Only = only,
            Script = script,
            ScriptUser = user,
            InstallScriptUser = "root",
        };
    }

    private static IReadOnlyList<string> ReadOnlyPatterns(
        YamlMappingNode jobNode,
        string jobName,
        List<PipelineValidationError> errors
    )
    {
        if (!TryGetNode(jobNode, "only", out var onlyNode))
        {
            return Array.Empty<string>();
        }

        if (onlyNode is not YamlSequenceNode seq)
        {
            errors.Add(new PipelineValidationError
            {
                Path = $"$.{jobName}.only",
                Message = "only must be a sequence of glob patterns.",
            });
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var entry in seq.Children)
        {
            var pattern = entry.ToString().Trim();
            if (!OnlyGlobMatcher.IsSupportedPattern(pattern))
            {
                errors.Add(new PipelineValidationError
                {
                    Path = $"$.{jobName}.only",
                    Message = $"Pattern '{pattern}' is invalid. v1 supports * but not **.",
                });
                continue;
            }

            values.Add(pattern);
        }

        return values;
    }

    private static IReadOnlyDictionary<string, string> ReadVariables(
        YamlMappingNode node,
        string path,
        string key,
        List<PipelineValidationError> errors
    )
    {
        if (!TryGetNode(node, key, out var variablesNode))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (variablesNode is not YamlMappingNode mapping)
        {
            errors.Add(new PipelineValidationError
            {
                Path = path,
                Message = "variables must be a key/value mapping.",
            });
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in mapping.Children)
        {
            result[child.Key.ToString()] = child.Value.ToString();
        }

        return result;
    }

    private static IReadOnlyList<DependencyRecipe> ReadDependencies(
        YamlMappingNode node,
        string path,
        string key,
        List<PipelineValidationError> errors
    )
    {
        if (!TryGetNode(node, key, out var dependenciesNode))
        {
            return Array.Empty<DependencyRecipe>();
        }

        if (dependenciesNode is not YamlSequenceNode sequence)
        {
            errors.Add(new PipelineValidationError
            {
                Path = path,
                Message = "dependencies must be a sequence.",
            });
            return Array.Empty<DependencyRecipe>();
        }

        var result = new List<DependencyRecipe>();
        foreach (var item in sequence.Children)
        {
            if (item is not YamlMappingNode map || map.Children.Count != 1)
            {
                errors.Add(new PipelineValidationError
                {
                    Path = path,
                    Message = "Each dependency entry must contain a single recipe name.",
                });
                continue;
            }

            var pair = map.Children.First();
            var name = pair.Key.ToString();
            var version = string.Empty;
            var installScript = string.Empty;

            if (pair.Value is YamlSequenceNode details)
            {
                foreach (var detail in details.Children.OfType<YamlMappingNode>())
                {
                    foreach (var detailPair in detail.Children)
                    {
                        var detailKey = detailPair.Key.ToString();
                        if (detailKey == "version")
                        {
                            version = detailPair.Value.ToString();
                        }
                        else if (detailKey == "installscript")
                        {
                            installScript = detailPair.Value.ToString();
                        }
                    }
                }
            }

            result.Add(
                new DependencyRecipe
                {
                    Name = name,
                    Version = string.IsNullOrWhiteSpace(version) ? null : version,
                    InstallScript = string.IsNullOrWhiteSpace(installScript) ? null : installScript,
                }
            );
        }

        return result;
    }

    private static bool TryGetNode(YamlMappingNode root, string key, out YamlNode node)
    {
        foreach (var child in root.Children)
        {
            if (string.Equals(child.Key.ToString(), key, StringComparison.Ordinal))
            {
                node = child.Value;
                return true;
            }
        }

        node = default!;
        return false;
    }

    private static string GetString(YamlMappingNode root, string key) =>
        TryGetNode(root, key, out var node) ? node.ToString() : string.Empty;

    private sealed record PipelineDefaults(
        string Image,
        IReadOnlyDictionary<string, string> Variables,
        IReadOnlyList<DependencyRecipe> Dependencies
    );
}
