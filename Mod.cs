namespace Loremaster;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(Loremaster), new PatchClass(this));
}
