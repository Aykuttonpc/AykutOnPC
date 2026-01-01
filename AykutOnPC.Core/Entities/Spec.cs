namespace AykutOnPC.Core.Entities;

public class Spec(string name, string category, int proficiency)
{
    public int Id { get; set; }
    public string Name { get; set; } = name;
    public string Category { get; set; } = category; // Language, Framework, Tool
    public int Proficiency { get; set; } = proficiency; // 0-100
    public string? IconClass { get; set; } // FontAwesome or similar
}
