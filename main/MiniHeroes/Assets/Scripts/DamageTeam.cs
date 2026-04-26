using UnityEngine;

public enum DamageTeam
{
    Neutral,
    Player,
    Enemy
}

public interface IDamageable
{
    DamageTeam Team { get; }
    bool IsDead { get; }
    void ReceiveDamage(int amount, GameObject source, DamageTeam sourceTeam);
}
