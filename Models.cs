namespace LerouxCompiler;

public class AppSettings
{
    public string            StudiomdlPath { get; set; } = "";
    public string            HlmvPath      { get; set; } = "";
    public string            GamePath      { get; set; } = "";
    public List<ModelEntry>  Models        { get; set; } = new();
}

public class ModelEntry
{
    public string Name   { get; set; } = "";
    public string QcPath { get; set; } = "";
}
