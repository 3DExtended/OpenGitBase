namespace OpenGitBase.Cli.Configuration;

public interface IConfigStore
{
    OgbConfigFile Load();

    void Save(OgbConfigFile config);

    void Clear();
}
