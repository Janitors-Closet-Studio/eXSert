using Utilities.Combat.Attacks;

public static class PlayerAttackContext
{
    public static AttackType? Current { get; private set; }

    public static void Set(AttackType type) => Current = type;
    public static void Clear() => Current = null;
}
