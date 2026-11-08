using Godot;

namespace WorldEdit;

public static class Stub
{
    public static event System.Action<string> Fired;

    public static void Fire(string id)
    {
        GD.Print($"[STUB] {id}");
        Fired?.Invoke(id);
    }
}
